#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Authoring.Combat;
using Crusaders30XX.ECS.DataOriented.Authoring.Meta;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Content.Enemies;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Integration.Host;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Integration.Host.Resources;

public enum HostTextureSourceKind : byte
{
    ContentAsset,
    GeneratedPrimitive,
}

public enum GeneratedTextureRecipeKind : byte
{
    SolidPanel,
    RoundedPanel,
    FixtureBackdrop,
    FixtureShell,
    ParallelogramMask,
    PassiveTrapezoidMask,
    MissingCardArt,
    MissingEnemyArt,
}

/// <summary>A stable host-side recipe. Colors are semantic constants, never ID-derived hashes.</summary>
public readonly record struct GeneratedTextureRecipe(
    GeneratedTextureRecipeKind Kind,
    int Width,
    int Height,
    int CornerRadius,
    Color Fill,
    Color Border);

public readonly record struct HostTextureAssetBinding(
    TextureAssetId Id,
    HostTextureSourceKind Source,
    string? ContentAssetName,
    GeneratedTextureRecipe GeneratedRecipe)
{
    public static HostTextureAssetBinding Content(TextureAssetId id, string assetName) =>
        new(id, HostTextureSourceKind.ContentAsset, assetName, default);

    public static HostTextureAssetBinding Generated(TextureAssetId id, in GeneratedTextureRecipe recipe) =>
        new(id, HostTextureSourceKind.GeneratedPrimitive, null, recipe);
}

/// <summary>
/// Immutable compact-ID catalog used by the production host. Construction validates the complete
/// authored ID set so a newly-authored texture cannot silently fall back at draw time.
/// </summary>
public sealed class HostTextureAssetCatalog
{
    private readonly HostTextureAssetBinding[] bindings;
    private readonly Dictionary<int, int> indexById;

    public HostTextureAssetCatalog(
        ReadOnlySpan<TextureAssetId> requiredIds,
        ReadOnlySpan<HostTextureAssetBinding> bindings)
    {
        this.bindings = bindings.ToArray();
        indexById = new Dictionary<int, int>(this.bindings.Length);
        for (var index = 0; index < this.bindings.Length; index++)
        {
            HostTextureAssetBinding binding = this.bindings[index];
            if (binding.Id.IsNull)
                throw new ArgumentException("Texture bindings cannot use the null ID.", nameof(bindings));
            if (!indexById.TryAdd(binding.Id.Value, index))
                throw new ArgumentException($"Duplicate TextureAssetId {binding.Id.Value}.", nameof(bindings));
            if (binding.Source == HostTextureSourceKind.ContentAsset &&
                string.IsNullOrWhiteSpace(binding.ContentAssetName))
                throw new ArgumentException($"TextureAssetId {binding.Id.Value} has no content asset name.", nameof(bindings));
            if (binding.Source == HostTextureSourceKind.GeneratedPrimitive &&
                (binding.GeneratedRecipe.Width <= 0 || binding.GeneratedRecipe.Height <= 0))
                throw new ArgumentException($"TextureAssetId {binding.Id.Value} has an invalid generated recipe.", nameof(bindings));
        }

        var required = new HashSet<int>();
        for (var index = 0; index < requiredIds.Length; index++)
        {
            TextureAssetId id = requiredIds[index];
            if (id.IsNull) throw new ArgumentException("Required texture IDs cannot be null.", nameof(requiredIds));
            if (!required.Add(id.Value))
                throw new ArgumentException($"Duplicate required TextureAssetId {id.Value}.", nameof(requiredIds));
            if (!indexById.ContainsKey(id.Value))
                throw new InvalidOperationException($"Missing production binding for TextureAssetId {id.Value}.");
        }

        if (required.Count != this.bindings.Length)
            throw new InvalidOperationException("The production texture catalog contains an unowned binding.");
    }

    public ReadOnlySpan<HostTextureAssetBinding> Bindings => bindings;

    public bool TryGet(TextureAssetId id, out HostTextureAssetBinding binding)
    {
        if (indexById.TryGetValue(id.Value, out int index))
        {
            binding = bindings[index];
            return true;
        }
        binding = default;
        return false;
    }
}

public static class ProductionHostTextureCatalog
{
    private static readonly GeneratedTextureRecipe Backdrop = new(
        GeneratedTextureRecipeKind.SolidPanel, 1, 1, 0,
        new Color(24, 20, 31), new Color(24, 20, 31));
    private static readonly GeneratedTextureRecipe Panel = new(
        GeneratedTextureRecipeKind.RoundedPanel, 512, 256, 24,
        new Color(45, 38, 54), new Color(184, 153, 94));
    private static readonly GeneratedTextureRecipe FixtureBackdrop = new(
        GeneratedTextureRecipeKind.FixtureBackdrop, 1, 1, 0,
        new Color(28, 30, 40), new Color(28, 30, 40));
    private static readonly GeneratedTextureRecipe FixtureShell = new(
        GeneratedTextureRecipeKind.FixtureShell, 1440, 760, 28,
        new Color(53, 55, 68), new Color(207, 185, 132));
    private static readonly GeneratedTextureRecipe SnapshotPixel = new(
        GeneratedTextureRecipeKind.SolidPanel, 1, 1, 0,
        Color.White, Color.White);
    private static readonly GeneratedTextureRecipe SnapshotRounded = new(
        GeneratedTextureRecipeKind.RoundedPanel, 56, 56, 28,
        Color.White, Color.White);
    private static readonly GeneratedTextureRecipe SnapshotKeyboardRounded = new(
        GeneratedTextureRecipeKind.RoundedPanel, 56, 56, 10,
        Color.White, Color.White);
    private static readonly GeneratedTextureRecipe MissingCard = new(
        GeneratedTextureRecipeKind.MissingCardArt, 360, 520, 18,
        new Color(60, 54, 68), new Color(192, 168, 112));
    private static readonly GeneratedTextureRecipe MissingEnemy = new(
        GeneratedTextureRecipeKind.MissingEnemyArt, 300, 420, 12,
        new Color(65, 48, 44), new Color(179, 129, 91));

    public static HostTextureAssetCatalog Create()
    {
        var required = new List<TextureAssetId>(384);
        var bindings = new List<HostTextureAssetBinding>(384);

        AddMeta(required, bindings);
        AddSnapshots(required, bindings);
        AddCombat(required, bindings);
        return new HostTextureAssetCatalog(required.ToArray(), bindings.ToArray());
    }

    private static void AddMeta(List<TextureAssetId> required, List<HostTextureAssetBinding> bindings)
    {
        AddGenerated(MetaStaticSceneTextureIds.TitleBackdrop, Backdrop, required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.TitleCrest, Panel, required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.TitleMenuPlate, Panel, required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.ClimbBackdrop, Backdrop, required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.ClimbTimeline, Panel, required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.ClimbHeader, Panel, required, bindings);
        AddContent(MetaStaticSceneTextureIds.WayStationBackdrop, "waystation", required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.WayStationBanner, Panel, required, bindings);
        AddContent(MetaStaticSceneTextureIds.WayStationPoiPlate, "waystation/climb-poi", required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.AchievementBackdrop, Backdrop, required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.AchievementHeader, Panel, required, bindings);
        AddGenerated(MetaStaticSceneTextureIds.AchievementGrid, Panel, required, bindings);
    }

    private static void AddSnapshots(List<TextureAssetId> required, List<HostTextureAssetBinding> bindings)
    {
        foreach (PlayerHudSnapshotMaskSpec spec in PlayerHudSnapshotMaskIds.Specs)
        {
            AddGenerated(spec.Id, new GeneratedTextureRecipe(
                    GeneratedTextureRecipeKind.ParallelogramMask,
                    spec.Width * 2,
                    spec.Height * 2,
                    spec.Slant * 2,
                    Color.White,
                    Color.White),
                required,
                bindings);
        }
        foreach (PlayerHudPassiveMaskSpec spec in PlayerHudPassiveMaskIds.Specs)
        {
            AddGenerated(spec.Id, new GeneratedTextureRecipe(
                    GeneratedTextureRecipeKind.PassiveTrapezoidMask,
                    spec.Width * 2,
                    70,
                    0,
                    Color.White,
                    Color.White),
                required,
                bindings);
        }
        ReadOnlySpan<NewWorldSnapshotFixture> fixtures = new NewWorldSnapshotFixtureHost().Registered;
        for (var fixtureIndex = 0; fixtureIndex < fixtures.Length; fixtureIndex++)
        {
            for (var variantIndex = 0; variantIndex < fixtures[fixtureIndex].VariantCount; variantIndex++)
            {
                TextureAssetId primary = SnapshotFixtureTextureIds.Primary(fixtureIndex, variantIndex);
                TextureAssetId secondary = SnapshotFixtureTextureIds.Secondary(fixtureIndex, variantIndex);
                if (fixtures[fixtureIndex].Id == "guardian-angel")
                {
                    AddContent(primary, "Battle_Backgrounds/gothic-battle-background", required, bindings);
                    AddContent(secondary, "guardian_angel", required, bindings);
                    AddGenerated(SnapshotFixtureTextureIds.Tertiary(fixtureIndex, variantIndex),
                        SnapshotPixel, required, bindings);
                    continue;
                }
                if (fixtures[fixtureIndex].Id == "player-hud")
                {
                    AddGenerated(primary, SnapshotPixel, required, bindings);
                    AddContent(secondary, variantIndex == 5 ? "Skeleton" : "crusader_sword",
                        required, bindings);
                    AddContent(SnapshotFixtureTextureIds.Tertiary(fixtureIndex, variantIndex),
                        "pledge", required, bindings);
                    continue;
                }
                bool fixtureSpecific = fixtures[fixtureIndex].Id is
                    "pause-menu" or "hotkey-hints" or "equipment-tooltip" or
                    "enemy-damage-meter";
                AddGenerated(primary, fixtureSpecific ? SnapshotPixel : FixtureBackdrop, required, bindings);
                if (fixtures[fixtureIndex].Id == "equipment-tooltip")
                {
                    AddContent(secondary,
                        variantIndex == 1
                            ? "Equipment/knightly_grieves"
                            : "Equipment/bulwark_plate",
                        required,
                        bindings);
                    continue;
                }
                GeneratedTextureRecipe fixtureSecondary =
                    fixtures[fixtureIndex].Id == "hotkey-hints" && variantIndex == 0
                        ? SnapshotKeyboardRounded
                        : SnapshotRounded;
                AddGenerated(secondary, fixtureSpecific ? fixtureSecondary : FixtureShell, required, bindings);
            }
        }
    }

    private static void AddCombat(List<TextureAssetId> required, List<HostTextureAssetBinding> bindings)
    {
        AddContent(CombatPresentationTextureIds.BattleBackdrop,
            "Battle_Backgrounds/desert-battle-background", required, bindings);
        AddContent(CombatPresentationTextureIds.Player, "crusader_sword", required, bindings);
        AddGenerated(CombatPresentationTextureIds.Health, Panel, required, bindings);
        AddGenerated(CombatPresentationTextureIds.ActionPoints, Panel, required, bindings);
        AddGenerated(CombatPresentationTextureIds.Courage, Panel, required, bindings);
        AddGenerated(CombatPresentationTextureIds.Temperance, Panel, required, bindings);
        AddContent(CombatPresentationTextureIds.DrawPile, "Battle_UI/draw_pile", required, bindings);
        AddGenerated(CombatPresentationTextureIds.Hand, Panel, required, bindings);
        AddContent(CombatPresentationTextureIds.DiscardPile, "Battle_UI/discard_pile", required, bindings);
        AddContent(CombatPresentationTextureIds.ExhaustPile, "card_back", required, bindings);

        ReadOnlySpan<Crusaders30XX.ECS.DataOriented.Definitions.DefinitionDebugMetadata<EnemyId>> enemies =
            GeneratedEnemyCatalog.DebugMetadata;
        for (var index = 0; index < enemies.Length; index++)
        {
            EnemyId id = enemies[index].Id;
            TextureAssetId texture = CombatPresentationTextureIds.Enemy(id);
            string? asset = EnemyAsset(id);
            if (asset is null) AddGenerated(texture, MissingEnemy, required, bindings);
            else AddContent(texture, asset, required, bindings);
        }

        ReadOnlySpan<Crusaders30XX.ECS.DataOriented.Definitions.DefinitionDebugMetadata<CardId>> cards =
            GeneratedCardCatalog.DebugMetadata;
        for (var index = 0; index < cards.Length; index++)
        {
            CardId id = cards[index].Id;
            TextureAssetId texture = CombatPresentationTextureIds.Card(id);
            string key = GeneratedCardCatalog.GetDefinition(id).Key;
            if (id is CardId.Colorless3Block or CardId.SwordIntoShield)
                AddGenerated(texture, MissingCard, required, bindings);
            else
                AddContent(texture, $"CardArt/{key}", required, bindings);
        }
    }

    private static string? EnemyAsset(EnemyId id) => id switch
    {
        EnemyId.Demon => "Demon",
        EnemyId.Horde => "Horde",
        EnemyId.Mummy => "Mummy",
        EnemyId.Ninja => "Ninja",
        EnemyId.Ogre => "Ogre",
        EnemyId.SandCorpse => null,
        EnemyId.SandGolem => "Sand_Golem",
        EnemyId.Skeleton => "Skeleton",
        EnemyId.SkeletalArcher => "Skeletal_Archer",
        EnemyId.Spider => "Spider",
        EnemyId.Succubus => "Succubus",
        EnemyId.Thornreaver => "Thornreaver",
        EnemyId.DustWuurm => "Dust_Wuurm",
        EnemyId.Sorcerer => "Sorcerer",
        EnemyId.IceDemon => "Ice_Demon",
        EnemyId.GlacialGuardian => "Glacial_Guardian",
        EnemyId.CinderboltDemon => "Cinderbolt_Demon",
        EnemyId.FireSkeleton => "Fire_Skeleton",
        EnemyId.Berserker => "Berserker",
        EnemyId.Shadow => "Shadow",
        EnemyId.EarthDemon => "Earth_Demon",
        EnemyId.Medusa => "Medusa",
        EnemyId.Wyvern => "Wyvern",
        EnemyId.FallenShepherd => "Fallen_Shepherd",
        EnemyId.TrainingDemon => "Training_Demon",
        _ => throw new ArgumentOutOfRangeException(nameof(id), id, null),
    };

    private static void AddContent(TextureAssetId id, string asset,
        List<TextureAssetId> required, List<HostTextureAssetBinding> bindings)
    {
        required.Add(id);
        bindings.Add(HostTextureAssetBinding.Content(id, asset));
    }

    private static void AddGenerated(TextureAssetId id, in GeneratedTextureRecipe recipe,
        List<TextureAssetId> required, List<HostTextureAssetBinding> bindings)
    {
        required.Add(id);
        bindings.Add(HostTextureAssetBinding.Generated(id, in recipe));
    }
}

/// <summary>Implemented by the MonoGame host using ContentManager and GraphicsDevice.</summary>
public interface IHostTextureResourceFactory<TTexture> where TTexture : class
{
    TTexture LoadContentAsset(string contentAssetName);
    TTexture CreateGeneratedPrimitive(in GeneratedTextureRecipe recipe);
}

/// <summary>Catalog-backed resolver; TTexture remains wholly outside the ECS assembly boundary.</summary>
public sealed class CatalogHostTextureResolver<TTexture> : IHostTextureResolver<TTexture>
    where TTexture : class
{
    private readonly HostTextureAssetCatalog catalog;
    private readonly IHostTextureResourceFactory<TTexture> factory;
    private readonly Dictionary<int, TTexture> loaded = [];
    private readonly Dictionary<string, TTexture> contentResources = new(StringComparer.Ordinal);
    private readonly Dictionary<GeneratedTextureRecipe, TTexture> generatedResources = [];

    public CatalogHostTextureResolver(
        HostTextureAssetCatalog catalog,
        IHostTextureResourceFactory<TTexture> factory)
    {
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>Resolve every binding during LoadContent so missing pipeline assets fail startup.</summary>
    public void Preload()
    {
        ReadOnlySpan<HostTextureAssetBinding> bindings = catalog.Bindings;
        for (var index = 0; index < bindings.Length; index++)
            _ = Resolve(bindings[index]);
    }

    public bool TryResolve(TextureAssetId id, out TTexture? texture)
    {
        if (loaded.TryGetValue(id.Value, out texture)) return true;
        if (!catalog.TryGet(id, out HostTextureAssetBinding binding))
        {
            texture = null;
            return false;
        }
        texture = Resolve(binding);
        return true;
    }

    private TTexture Resolve(in HostTextureAssetBinding binding)
    {
        if (loaded.TryGetValue(binding.Id.Value, out TTexture? texture)) return texture;
        if (binding.Source == HostTextureSourceKind.ContentAsset)
        {
            string assetName = binding.ContentAssetName!;
            if (!contentResources.TryGetValue(assetName, out texture))
            {
                texture = factory.LoadContentAsset(assetName);
                contentResources.Add(assetName, texture);
            }
        }
        else if (binding.Source == HostTextureSourceKind.GeneratedPrimitive)
        {
            GeneratedTextureRecipe recipe = binding.GeneratedRecipe;
            if (!generatedResources.TryGetValue(recipe, out texture))
            {
                texture = factory.CreateGeneratedPrimitive(in recipe);
                generatedResources.Add(recipe, texture);
            }
        }
        else
        {
            throw new ArgumentOutOfRangeException(nameof(binding), binding.Source, null);
        }
        if (texture is null)
            throw new InvalidOperationException($"Texture factory returned null for TextureAssetId {binding.Id.Value}.");
        loaded.Add(binding.Id.Value, texture);
        return texture;
    }
}

public readonly record struct HostShaderAssetBinding(ShaderRequestKind Kind, string ContentAssetName);

public static class ProductionHostShaderCatalog
{
    private static readonly HostShaderAssetBinding[] Bindings =
    [
        new(ShaderRequestKind.RectangularShockwave, "Shaders/RectangularShockwave"),
        new(ShaderRequestKind.Shockwave, "Shaders/Shockwave"),
        new(ShaderRequestKind.Poison, "Shaders/Poison"),
    ];

    public static ReadOnlySpan<HostShaderAssetBinding> Registered => Bindings;

    public static bool TryGet(ShaderRequestKind kind, out HostShaderAssetBinding binding)
    {
        for (var index = 0; index < Bindings.Length; index++)
        {
            if (Bindings[index].Kind != kind) continue;
            binding = Bindings[index];
            return true;
        }
        binding = default;
        return false;
    }
}

/// <summary>No non-null SoundId is emitted by the converted runtime yet.</summary>
public static class ProductionHostSoundCatalog
{
    public static ReadOnlySpan<SoundId> Emitted => ReadOnlySpan<SoundId>.Empty;
}
