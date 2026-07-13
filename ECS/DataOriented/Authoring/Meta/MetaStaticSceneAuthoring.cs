#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Authoring.Meta;

/// <summary>
/// Immutable, bootstrap-time sprite authoring data. Managed arrays are intentionally kept
/// outside ECS storage; materialized entities contain only unmanaged components.
/// </summary>
public readonly record struct MetaSceneSpriteDefinition(
    TextureAssetId Texture,
    Vector2 Position,
    Vector2 Scale,
    Color Tint,
    int ZOrder)
{
    public SpriteFlags Flags { get; init; } = SpriteFlags.Visible;
}

public readonly record struct MetaSceneTextDefinition(
    StringId Content,
    TextStyleId Style,
    Vector2 Position,
    Vector2 Scale,
    Color Tint,
    int ZOrder,
    RenderLayer Layer,
    TextAlignment Alignment)
{
    public float LetterSpacing { get; init; }
}

public sealed class MetaStaticSceneDefinition
{
    private readonly MetaSceneSpriteDefinition[] sprites;
    private readonly MetaSceneTextDefinition[] texts;

    public MetaStaticSceneDefinition(SceneGroup scene, params MetaSceneSpriteDefinition[] sprites)
        : this(scene, sprites, [])
    {
    }

    public MetaStaticSceneDefinition(
        SceneGroup scene,
        MetaSceneSpriteDefinition[] sprites,
        MetaSceneTextDefinition[] texts)
    {
        if (scene == SceneGroup.Global)
            throw new ArgumentOutOfRangeException(nameof(scene), scene, "Global is not an authored scene.");
        ArgumentNullException.ThrowIfNull(sprites);
        ArgumentNullException.ThrowIfNull(texts);
        if (sprites.Length == 0)
            throw new ArgumentException("A static scene must author at least one sprite.", nameof(sprites));

        Scene = scene;
        this.sprites = (MetaSceneSpriteDefinition[])sprites.Clone();
        this.texts = (MetaSceneTextDefinition[])texts.Clone();
        for (var index = 0; index < this.sprites.Length; index++)
        {
            if (this.sprites[index].Texture.IsNull)
                throw new ArgumentException($"Sprite {index} has a null texture asset ID.", nameof(sprites));
        }
        for (var index = 0; index < this.texts.Length; index++)
        {
            if (this.texts[index].Content.IsNull || this.texts[index].Style.IsNull)
                throw new ArgumentException($"Text {index} has a null content or style ID.", nameof(texts));
        }
    }

    public SceneGroup Scene { get; }
    public ReadOnlySpan<MetaSceneSpriteDefinition> Sprites => sprites;
    public ReadOnlySpan<MetaSceneTextDefinition> Texts => texts;
}

/// <summary>
/// Stable compact IDs reserved by the data-oriented meta authoring catalog. The host resource
/// adapter must bind these IDs to production textures before the visual-parity gate.
/// </summary>
public static class MetaStaticSceneTextureIds
{
    public static readonly TextureAssetId TitleBackdrop = new(46001);
    public static readonly TextureAssetId TitleCrest = new(46002);
    public static readonly TextureAssetId TitleMenuPlate = new(46003);
    public static readonly TextureAssetId ClimbBackdrop = new(46011);
    public static readonly TextureAssetId ClimbTimeline = new(46012);
    public static readonly TextureAssetId ClimbHeader = new(46013);
    public static readonly TextureAssetId WayStationBackdrop = new(46021);
    public static readonly TextureAssetId WayStationBanner = new(46022);
    public static readonly TextureAssetId WayStationPoiPlate = new(46023);
    public static readonly TextureAssetId AchievementBackdrop = new(46031);
    public static readonly TextureAssetId AchievementHeader = new(46032);
    public static readonly TextureAssetId AchievementGrid = new(46033);
}

/// <summary>Deterministic static composition for the non-battle scene shells.</summary>
public static class MetaStaticSceneAuthoringCatalog
{
    private static readonly MetaStaticSceneDefinition TitleMenuDefinition = new(
        SceneGroup.TitleMenu,
        [
        Sprite(MetaStaticSceneTextureIds.TitleBackdrop, 960f, 540f, 1920f, 1080f, -100),
        Sprite(MetaStaticSceneTextureIds.TitleCrest, 960f, 270f, 480f, 360f, 0),
        Sprite(MetaStaticSceneTextureIds.TitleMenuPlate, 960f, 730f, 560f, 280f, 10),
        ],
        [Text(TextContentIds.Title, TextStyleIds.Title, 960f, 110f, 100)]);

    private static readonly MetaStaticSceneDefinition ClimbDefinition = new(
        SceneGroup.Climb,
        [
        Sprite(MetaStaticSceneTextureIds.ClimbBackdrop, 960f, 540f, 1920f, 1080f, -100),
        Sprite(MetaStaticSceneTextureIds.ClimbTimeline, 960f, 600f, 1500f, 620f, 0),
        Sprite(MetaStaticSceneTextureIds.ClimbHeader, 960f, 105f, 1680f, 150f, 10),
        ],
        [Text(TextContentIds.Climb, TextStyleIds.Heading, 960f, 105f, 100)]);

    private static readonly MetaStaticSceneDefinition WayStationDefinition = new(
        SceneGroup.WayStation,
        [
        Sprite(MetaStaticSceneTextureIds.WayStationBackdrop, 960f, 540f, 1920f, 1080f, -100),
        Sprite(MetaStaticSceneTextureIds.WayStationBanner, 960f, 150f, 900f, 180f, 0),
        Sprite(MetaStaticSceneTextureIds.WayStationPoiPlate, 960f, 650f, 1320f, 460f, 10),
        ],
        [Text(TextContentIds.WayStation, TextStyleIds.Heading, 960f, 145f, 100)]);

    private static readonly MetaStaticSceneDefinition AchievementDefinition = new(
        SceneGroup.Achievement,
        [
        Sprite(MetaStaticSceneTextureIds.AchievementBackdrop, 960f, 540f, 1920f, 1080f, -100),
        Sprite(MetaStaticSceneTextureIds.AchievementHeader, 960f, 120f, 980f, 150f, 0),
        Sprite(MetaStaticSceneTextureIds.AchievementGrid, 960f, 590f, 1560f, 720f, 10),
        ],
        [Text(TextContentIds.Achievements, TextStyleIds.Heading, 960f, 115f, 100)]);

    public static MetaStaticSceneDefinition Get(SceneGroup scene) => scene switch
    {
        SceneGroup.TitleMenu => TitleMenuDefinition,
        SceneGroup.Climb => ClimbDefinition,
        SceneGroup.WayStation => WayStationDefinition,
        SceneGroup.Achievement => AchievementDefinition,
        _ => throw new ArgumentOutOfRangeException(nameof(scene), scene, "No static meta scene is registered."),
    };

    private static MetaSceneSpriteDefinition Sprite(
        TextureAssetId texture,
        float x,
        float y,
        float width,
        float height,
        int zOrder) => new(
            texture,
            new Vector2(x, y),
            new Vector2(width, height),
            Color.White,
            zOrder);

    private static MetaSceneTextDefinition Text(
        StringId content,
        TextStyleId style,
        float x,
        float y,
        int zOrder) => new(
            content,
            style,
            new Vector2(x, y),
            Vector2.One,
            Color.White,
            zOrder,
            RenderLayer.Hud,
            TextAlignment.TopCenter);
}

/// <summary>
/// Root-owned lifetime handle for one authored scene or fixture. Hiding affects extraction
/// immediately; disposal destroys only the entities created by this materialization.
/// </summary>
public sealed class MetaAuthoredScene : IDisposable
{
    private readonly World world;
    private readonly EntityId[] entities;
    private bool disposed;

    internal MetaAuthoredScene(World world, SceneGroup scene, EntityId[] entities)
    {
        this.world = world;
        Scene = scene;
        this.entities = entities;
    }

    public SceneGroup Scene { get; }
    public ReadOnlySpan<EntityId> Entities => entities;
    public bool IsVisible { get; private set; } = true;
    public bool IsDisposed => disposed;

    public void SetVisible(bool visible)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (visible == IsVisible) return;

        for (var index = 0; index < entities.Length; index++)
        {
            EntityId entity = entities[index];
            if (!world.TryGet(entity, out Sprite sprite)) continue;
            sprite.Flags = visible ? sprite.Flags | SpriteFlags.Visible : sprite.Flags & ~SpriteFlags.Visible;
            world.Set(entity, in sprite);
        }
        for (var index = 0; index < entities.Length; index++)
        {
            EntityId entity = entities[index];
            if (!world.TryGet(entity, out TextPresentation text)) continue;
            text.Flags = visible
                ? text.Flags | TextPresentationFlags.Visible
                : text.Flags & ~TextPresentationFlags.Visible;
            world.Set(entity, in text);
        }

        IsVisible = visible;
    }

    public void Dispose()
    {
        if (disposed) return;
        for (var index = entities.Length - 1; index >= 0; index--)
        {
            if (world.IsAlive(entities[index])) world.Destroy(entities[index]);
        }

        disposed = true;
        IsVisible = false;
    }
}

/// <summary>Materializes catalog data into real Transform + Sprite scene entities.</summary>
public static class MetaStaticSceneMaterializer
{
    public static MetaAuthoredScene Materialize(World world, SceneGroup scene) =>
        Materialize(world, MetaStaticSceneAuthoringCatalog.Get(scene));

    public static MetaAuthoredScene Materialize(World world, MetaStaticSceneDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(definition);

        ReadOnlySpan<MetaSceneSpriteDefinition> sprites = definition.Sprites;
        ReadOnlySpan<MetaSceneTextDefinition> texts = definition.Texts;
        var entities = new EntityId[sprites.Length];
        try
        {
            for (var index = 0; index < sprites.Length; index++)
                entities[index] = CreateSprite(world, definition.Scene, in sprites[index]);
            for (var index = 0; index < texts.Length; index++)
                AttachText(world, entities[index % entities.Length], in texts[index]);
            return new MetaAuthoredScene(world, definition.Scene, entities);
        }
        catch
        {
            for (var index = entities.Length - 1; index >= 0; index--)
            {
                if (!entities[index].IsNull && world.IsAlive(entities[index])) world.Destroy(entities[index]);
            }
            throw;
        }
    }

    internal static EntityId CreateSprite(
        World world,
        SceneGroup scene,
        in MetaSceneSpriteDefinition definition)
    {
        var bundle = new SpawnBundle(3);
        var transform = new Transform
        {
            Position = definition.Position,
            Scale = definition.Scale,
            Rotation = 0f,
            ZOrder = definition.ZOrder,
        };
        var sprite = new Sprite
        {
            Texture = definition.Texture,
            Tint = definition.Tint,
            Flags = definition.Flags,
        };
        var owner = new OwnedByScene { Scene = scene };
        bundle.Add(in transform);
        bundle.Add(in sprite);
        bundle.Add(in owner);
        return world.Create(in bundle);
    }

    internal static void AttachText(
        World world,
        EntityId entity,
        in MetaSceneTextDefinition definition)
    {
        Transform transform = world.Get<Transform>(entity);
        world.Add(entity, new TextPresentation
        {
            Content = definition.Content,
            Style = definition.Style,
            Offset = definition.Position - transform.Position,
            Scale = definition.Scale,
            Tint = definition.Tint,
            ZOffset = definition.ZOrder - transform.ZOrder,
            Layer = definition.Layer,
            Alignment = definition.Alignment,
            LetterSpacing = definition.LetterSpacing,
            Flags = TextPresentationFlags.Visible,
        });
    }
}
