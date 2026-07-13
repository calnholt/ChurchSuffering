#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Content.Enemies;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Authoring.Combat;

/// <summary>
/// Semantic texture keys for battle authoring. The MonoGame host must map these keys to
/// loaded assets; authoring and render extraction never retain Texture2D references.
/// </summary>
public static class CombatPresentationTextureIds
{
    public static readonly TextureAssetId BattleBackdrop = new(48001);
    public static readonly TextureAssetId Player = new(48002);
    public static readonly TextureAssetId Health = new(48003);
    public static readonly TextureAssetId ActionPoints = new(48004);
    public static readonly TextureAssetId Courage = new(48005);
    public static readonly TextureAssetId Temperance = new(48006);
    public static readonly TextureAssetId DrawPile = new(48007);
    public static readonly TextureAssetId Hand = new(48008);
    public static readonly TextureAssetId DiscardPile = new(48009);
    public static readonly TextureAssetId ExhaustPile = new(48010);

    public static TextureAssetId Enemy(EnemyId enemy) => new(48100 + (ushort)enemy);

    public static TextureAssetId Card(CardId card) => new(48200 + (ushort)card);
}

public enum CombatPresentationEntityKind : byte
{
    Backdrop,
    Hud,
    CardZone,
    Deck,
    Card,
    Weapon,
    Text,
}

public readonly record struct CombatPresentationEntity(EntityId Entity, CombatPresentationEntityKind Kind);

/// <summary>
/// Owns the structural changes made by battle authoring. Dispose this handle before the
/// owning CombatSession is ended so no battle-scoped presentation entities survive it.
/// </summary>
public sealed class CombatPresentationAuthoringHandle : IDisposable
{
    private readonly World world;
    private readonly CombatPresentationEntity[] entities;
    private readonly EntityId player;
    private readonly EntityId enemy;
    private readonly bool ownsEquippedWeapon;
    private bool disposed;

    internal CombatPresentationAuthoringHandle(
        World world,
        EntityId battle,
        EntityId player,
        EntityId enemy,
        CombatPresentationEntity[] entities,
        int entityCount,
        EntityId deck,
        bool ownsEquippedWeapon)
    {
        this.world = world;
        Battle = battle;
        this.player = player;
        this.enemy = enemy;
        this.entities = entities.AsSpan(0, entityCount).ToArray();
        Deck = deck;
        this.ownsEquippedWeapon = ownsEquippedWeapon;
    }

    public EntityId Battle { get; }
    public EntityId Deck { get; }
    public EntityId Root => entities.Length == 0 ? EntityId.Null : entities[0].Entity;
    public bool IsDisposed => disposed;
    public ReadOnlySpan<CombatPresentationEntity> Entities => entities;

    public void Dispose()
    {
        if (disposed) return;

        for (var index = entities.Length - 1; index >= 0; index--)
        {
            EntityId entity = entities[index].Entity;
            if (world.IsAlive(entity)) world.Destroy(entity);
        }

        DetachActor(world, enemy, removeEquippedWeapon: false);
        DetachActor(world, player, ownsEquippedWeapon);
        disposed = true;
    }

    internal static void DetachActor(World world, EntityId actor, bool removeEquippedWeapon)
    {
        if (!world.IsAlive(actor)) return;
        if (world.Has<JigglePulseState>(actor)) world.Remove<JigglePulseState>(actor);
        if (world.Has<ActorPresentationState>(actor)) world.Remove<ActorPresentationState>(actor);
        if (world.Has<Sprite>(actor)) world.Remove<Sprite>(actor);
        if (world.Has<Transform>(actor)) world.Remove<Transform>(actor);
        if (removeEquippedWeapon && world.Has<EquippedWeapon>(actor)) world.Remove<EquippedWeapon>(actor);
    }
}

/// <summary>
/// Stable-ID-only test-fight description. It has no dependency on the legacy diagnostics,
/// factories, singletons, or string-key repositories.
/// </summary>
public readonly record struct DataOrientedTestFightFixture(
    CardId Weapon,
    EnemyId Enemy,
    ClimbDifficulty Difficulty,
    ulong Seed)
{
    public int PlayerHealth => Difficulty switch
    {
        ClimbDifficulty.Easy => 25,
        ClimbDifficulty.Normal => 22,
        ClimbDifficulty.Hard => 20,
        _ => throw new ArgumentOutOfRangeException(nameof(Difficulty)),
    };

    public float EnemyHealthMultiplier => Difficulty switch
    {
        ClimbDifficulty.Easy => 0.8f,
        ClimbDifficulty.Normal => 0.9f,
        ClimbDifficulty.Hard => 1f,
        _ => throw new ArgumentOutOfRangeException(nameof(Difficulty)),
    };

    public void Validate()
    {
        if (Weapon is not (CardId.Sword or CardId.Dagger or CardId.Hammer))
            throw new ArgumentOutOfRangeException(nameof(Weapon), "Test fights require sword, dagger, or hammer.");
        _ = PlayerHealth;
        _ = GeneratedCardCatalog.GetDefinition(Weapon);
        _ = GeneratedEnemyCatalog.GetDefinition(Enemy);
    }

    public CombatSession CreateSession(World world, CombatEventHub events)
    {
        Validate();
        CombatSession session = CombatSession.Create(world, events, Enemy, PlayerHealth, Seed);
        ref HP enemyHealth = ref world.Get<HP>(session.Enemy);
        float multiplier = EnemyHealthMultiplier;
        enemyHealth.Max = Math.Max(1, (int)Math.Round(enemyHealth.Max * multiplier));
        enemyHealth.Current = Math.Clamp(
            Math.Max(1, (int)Math.Round(enemyHealth.Current * multiplier)),
            1,
            enemyHealth.Max);
        enemyHealth.UnscarredMax = enemyHealth.Max;
        return session;
    }
}

/// <summary>
/// Deterministic one-shot materialization for the battle scene. Runtime systems continue
/// to own state updates; this author creates the initial presentation graph only.
/// </summary>
public static class CombatPresentationAuthoring
{
    private const int MaximumOwnedEntities = 64;

    private static readonly CardId[] SharedStarterCards =
    [
        CardId.Absolution,
        CardId.Courageous,
        CardId.ForgeStrike,
        CardId.LitanyOfWrath,
        CardId.Reckoning,
        CardId.Smite,
    ];

    private static readonly CardId[] SwordCommonCards = [CardId.Fervor, CardId.HoldTheLine, CardId.Stab];
    private static readonly CardId[] SwordSingleCards = [CardId.Exaltation, CardId.IncreaseFaith];
    private static readonly CardId[] DaggerCommonCards = [CardId.Seize, CardId.RallyTheFaithful, CardId.Whirlwind];
    private static readonly CardId[] DaggerSingleCards = [CardId.RazorStorm, CardId.IncreaseFaith];
    private static readonly CardId[] HammerCommonCards = [CardId.Mantlet, CardId.StokeTheFurnace, CardId.SteadfastResolve];
    private static readonly CardId[] HammerSingleCards = [CardId.UnburdenedStrike, CardId.IncreaseFaith];

    public static CombatPresentationAuthoringHandle Materialize(CombatSession session) =>
        Materialize(session, fixture: null);

    public static CombatPresentationAuthoringHandle MaterializeTestFight(
        CombatSession session,
        in DataOrientedTestFightFixture fixture)
    {
        fixture.Validate();
        if (session.World.Get<Enemy>(session.Enemy).Definition != fixture.Enemy)
            throw new ArgumentException("The test-fight fixture enemy must match the combat session.", nameof(fixture));
        return Materialize(session, fixture);
    }

    private static CombatPresentationAuthoringHandle Materialize(
        CombatSession session,
        DataOrientedTestFightFixture? fixture)
    {
        ArgumentNullException.ThrowIfNull(session);
        World world = session.World;
        ValidateActor(world, session.Player, "player");
        ValidateActor(world, session.Enemy, "enemy");

        var owned = new CombatPresentationEntity[MaximumOwnedEntities];
        var ownedCount = 0;
        var deck = EntityId.Null;
        var ownsEquippedWeapon = false;

        try
        {
            AttachActor(world, session.Player, new Vector2(355f, 520f), CombatPresentationTextureIds.Player);
            EnemyId enemyDefinition = world.Get<Enemy>(session.Enemy).Definition;
            AttachActor(world, session.Enemy, new Vector2(1480f, 455f), CombatPresentationTextureIds.Enemy(enemyDefinition));

            AddOwned(owned, ref ownedCount, CreateSprite(
                world,
                new Vector2(960f, 540f),
                new Vector2(1920f, 1080f),
                zOrder: -1000,
                CombatPresentationTextureIds.BattleBackdrop,
                Color.White), CombatPresentationEntityKind.Backdrop);

            CreateHud(world, owned, ref ownedCount);
            CreateCardZones(world, owned, ref ownedCount);

            if (fixture.HasValue)
            {
                CreateTestFightDeck(
                    world,
                    session.Player,
                    fixture.Value,
                    owned,
                    ref ownedCount,
                    out deck);
                ownsEquippedWeapon = true;
                AddOwned(owned, ref ownedCount, CreateText(
                    world,
                    TextContentIds.TestFight,
                    TextStyleIds.Heading,
                    new Vector2(960f, 40f),
                    zOrder: 1500), CombatPresentationEntityKind.Text);
            }

            return new CombatPresentationAuthoringHandle(
                world,
                session.Battle,
                session.Player,
                session.Enemy,
                owned,
                ownedCount,
                deck,
                ownsEquippedWeapon);
        }
        catch
        {
            for (var index = ownedCount - 1; index >= 0; index--)
                if (world.IsAlive(owned[index].Entity)) world.Destroy(owned[index].Entity);
            CombatPresentationAuthoringHandle.DetachActor(world, session.Enemy, removeEquippedWeapon: false);
            CombatPresentationAuthoringHandle.DetachActor(world, session.Player, ownsEquippedWeapon);
            throw;
        }
    }

    private static void ValidateActor(World world, EntityId actor, string name)
    {
        if (!world.IsAlive(actor)) throw new InvalidOperationException($"The combat {name} is not alive.");
        if (world.Has<Transform>(actor) || world.Has<Sprite>(actor) || world.Has<ActorPresentationState>(actor))
            throw new InvalidOperationException($"The combat {name} already has presentation materialized.");
    }

    private static void AttachActor(World world, EntityId actor, Vector2 position, TextureAssetId texture)
    {
        var additions = new SpawnBundle(4);
        additions.Add(new Transform { Position = position, Scale = Vector2.One, ZOrder = 200 });
        additions.Add(VisibleSprite(texture, Color.White));
        additions.Add(new ActorPresentationState { ScaleMultiplier = Vector2.One, TintColor = Color.White });
        additions.Add(new JigglePulseState());
        world.Transition(actor, in additions, default);
    }

    private static void CreateHud(
        World world,
        CombatPresentationEntity[] owned,
        ref int count)
    {
        ReadOnlySpan<PlayerHudRegionKind> regions =
        [
            PlayerHudRegionKind.Health,
            PlayerHudRegionKind.ActionPoints,
            PlayerHudRegionKind.Courage,
            PlayerHudRegionKind.Temperance,
        ];
        ReadOnlySpan<TextureAssetId> textures =
        [
            CombatPresentationTextureIds.Health,
            CombatPresentationTextureIds.ActionPoints,
            CombatPresentationTextureIds.Courage,
            CombatPresentationTextureIds.Temperance,
        ];
        ReadOnlySpan<StringId> labels =
        [
            TextContentIds.Health,
            TextContentIds.ActionPoints,
            TextContentIds.Courage,
            TextContentIds.Temperance,
        ];

        for (var index = 0; index < regions.Length; index++)
        {
            var bundle = new SpawnBundle(5);
            bundle.Add(new Transform
            {
                Position = new Vector2(70f + index * 170f, 80f),
                Scale = new Vector2(150f, 48f),
                ZOrder = 1000 + index,
            });
            bundle.Add(VisibleSprite(textures[index], Color.White));
            bundle.Add(new PlayerHudRegion { Kind = regions[index], Order = index });
            bundle.Add(VisibleText(labels[index], TextStyleIds.Hud, RenderLayer.Hud));
            bundle.Add(new OwnedByScene { Scene = SceneGroup.Battle });
            AddOwned(owned, ref count, world.Create(in bundle), CombatPresentationEntityKind.Hud);
        }
    }

    private static void CreateCardZones(
        World world,
        CombatPresentationEntity[] owned,
        ref int count)
    {
        ReadOnlySpan<Vector2> positions =
        [
            new Vector2(1690f, 850f),
            new Vector2(960f, 890f),
            new Vector2(230f, 850f),
            new Vector2(230f, 650f),
        ];
        ReadOnlySpan<TextureAssetId> textures =
        [
            CombatPresentationTextureIds.DrawPile,
            CombatPresentationTextureIds.Hand,
            CombatPresentationTextureIds.DiscardPile,
            CombatPresentationTextureIds.ExhaustPile,
        ];

        for (var index = 0; index < positions.Length; index++)
        {
            var bundle = new SpawnBundle(4);
            var scale = new Vector2(index == 1 ? 1120f : 180f, 230f);
            bundle.Add(new Transform { Position = positions[index], Scale = scale, ZOrder = 100 + index });
            bundle.Add(VisibleSprite(textures[index], new Color(255, 255, 255, 180)));
            bundle.Add(new BattlePresentationTransform { Scale = scale });
            bundle.Add(new OwnedByScene { Scene = SceneGroup.Battle });
            AddOwned(owned, ref count, world.Create(in bundle), CombatPresentationEntityKind.CardZone);
        }
    }

    private static void CreateTestFightDeck(
        World world,
        EntityId player,
        in DataOrientedTestFightFixture fixture,
        CombatPresentationEntity[] owned,
        ref int count,
        out EntityId deck)
    {
        deck = CardGameplayFactory.CreateDeck(world, player, fixture.Seed ^ 0x5445535446494748UL);
        world.Add(deck, new OwnedByScene { Scene = SceneGroup.Battle });
        AddOwned(owned, ref count, deck, CombatPresentationEntityKind.Deck);

        var weaponBundle = new SpawnBundle(3);
        weaponBundle.Add(CardGameplayFactory.BuildCardData(deck, fixture.Weapon, upgraded: false));
        weaponBundle.Add(new CardZoneLocation { Deck = deck, Zone = CardZone.None, Index = -1 });
        weaponBundle.Add(new OwnedByScene { Scene = SceneGroup.Battle });
        EntityId weapon = world.Create(in weaponBundle);
        AddOwned(owned, ref count, weapon, CombatPresentationEntityKind.Weapon);
        world.Add(player, new EquippedWeapon { Card = weapon });

        Span<CardId> definitions = stackalloc CardId[20];
        BuildStarterDeck(fixture.Weapon, fixture.Seed, definitions);
        for (var index = 0; index < definitions.Length; index++)
        {
            CardZone zone = index < 4 ? CardZone.Hand : CardZone.DrawPile;
            EntityId card = CardGameplayFactory.CreateCard(world, deck, definitions[index], zone);
            var additions = new SpawnBundle(3);
            additions.Add(new Transform
            {
                Position = zone == CardZone.Hand
                    ? new Vector2(570f + index * 260f, 855f)
                    : new Vector2(1690f, 850f),
                Scale = new Vector2(0.42f),
                ZOrder = 300 + index,
            });
            Sprite sprite = VisibleSprite(CombatPresentationTextureIds.Card(definitions[index]), Color.White);
            if (zone != CardZone.Hand) sprite.Flags = SpriteFlags.None;
            additions.Add(sprite);
            additions.Add(new OwnedByScene { Scene = SceneGroup.Battle });
            world.Transition(card, in additions, default);
            AddOwned(owned, ref count, card, CombatPresentationEntityKind.Card);
        }
    }

    internal static void BuildStarterDeck(CardId weapon, ulong seed, Span<CardId> destination)
    {
        if (destination.Length < 20)
            throw new ArgumentException("A test-fight starter deck requires twenty output slots.", nameof(destination));

        ReadOnlySpan<CardId> common = weapon switch
        {
            CardId.Sword => SwordCommonCards,
            CardId.Dagger => DaggerCommonCards,
            CardId.Hammer => HammerCommonCards,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
        };
        ReadOnlySpan<CardId> single = weapon switch
        {
            CardId.Sword => SwordSingleCards,
            CardId.Dagger => DaggerSingleCards,
            CardId.Hammer => HammerSingleCards,
            _ => throw new ArgumentOutOfRangeException(nameof(weapon)),
        };

        var cursor = 0;
        for (var index = 0; index < SharedStarterCards.Length; index++)
        {
            destination[cursor++] = SharedStarterCards[index];
            destination[cursor++] = SharedStarterCards[index];
        }
        for (var index = 0; index < common.Length; index++)
        {
            destination[cursor++] = common[index];
            destination[cursor++] = common[index];
        }
        for (var index = 0; index < single.Length; index++) destination[cursor++] = single[index];

        var state = RuleRandomState.FromSeed(seed);
        var random = new DeterministicRuleRandom(ref state);
        random.Shuffle(destination[..20]);
        for (var index = 0; index < 20; index++)
            _ = GeneratedCardCatalog.GetDefinition(destination[index]);
    }

    private static EntityId CreateSprite(
        World world,
        Vector2 position,
        Vector2 scale,
        int zOrder,
        TextureAssetId texture,
        Color tint)
    {
        var bundle = new SpawnBundle(3);
        bundle.Add(new Transform { Position = position, Scale = scale, ZOrder = zOrder });
        bundle.Add(VisibleSprite(texture, tint));
        bundle.Add(new OwnedByScene { Scene = SceneGroup.Battle });
        return world.Create(in bundle);
    }

    private static EntityId CreateText(
        World world,
        StringId content,
        TextStyleId style,
        Vector2 position,
        int zOrder)
    {
        var bundle = new SpawnBundle(3);
        bundle.Add(new Transform { Position = position, Scale = Vector2.One, ZOrder = zOrder });
        bundle.Add(VisibleText(content, style, RenderLayer.Overlay));
        bundle.Add(new OwnedByScene { Scene = SceneGroup.Battle });
        return world.Create(in bundle);
    }

    private static Sprite VisibleSprite(TextureAssetId texture, Color tint) => new()
    {
        Texture = texture,
        Tint = tint,
        Flags = SpriteFlags.Visible,
    };

    private static TextPresentation VisibleText(StringId content, TextStyleId style, RenderLayer layer) => new()
    {
        Content = content,
        Style = style,
        Scale = Vector2.One,
        Tint = Color.White,
        Layer = layer,
        Alignment = TextAlignment.TopCenter,
        Flags = TextPresentationFlags.Visible | TextPresentationFlags.DropShadow,
    };

    private static void AddOwned(
        CombatPresentationEntity[] owned,
        ref int count,
        EntityId entity,
        CombatPresentationEntityKind kind)
    {
        if (count == owned.Length)
            throw new InvalidOperationException("Battle authoring exceeded its fixed entity capacity.");
        owned[count++] = new CombatPresentationEntity(entity, kind);
    }
}
