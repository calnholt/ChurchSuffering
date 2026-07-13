#nullable enable

using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented;

public sealed class Ecs020ComponentRoundTripTests
{
    [Fact]
    public void Shared_components_round_trip_through_spawn_bundle_and_world_storage()
    {
        World world = CreateWorld();
        var parentBundle = new SpawnBundle(1);
        parentBundle.Add(new EntityMetadata { Name = new StringId(1) });
        EntityId parent = world.Create(in parentBundle);

        var actionPoints = new ActionPoints { Current = 3 };
        var actorPresentation = new ActorPresentationState
        {
            DrawOffset = new Vector2(2f, 3f),
            ScaleMultiplier = new Vector2(1.1f, 0.9f),
            TintColor = new Color(10, 20, 30, 40),
            DamageFlashTimer = 0.25f,
        };
        var animation = new Animation
        {
            Duration = 1.5f,
            CurrentTime = 0.5f,
            Type = AnimationType.Scale,
            Flags = AnimationFlags.Playing | AnimationFlags.Looping,
        };
        var battlePresentation = new BattlePresentationTransform
        {
            Offset = new Vector2(7f, 8f),
            Scale = new Vector2(0.8f, 0.9f),
        };
        var courage = new Courage { Amount = 4 };
        var metadata = new EntityMetadata { Name = new StringId(2) };
        var hp = new HP { Max = 40, Current = 31, UnscarredMax = 36 };
        var inputContext = new InputContext
        {
            Id = new StringId(3),
            Priority = 900,
            Flags = InputContextFlags.Active | InputContextFlags.AcceptsCommands,
        };
        var intellect = new Intellect { Value = 2 };
        var maxHandSize = new MaxHandSize { Value = 5 };
        var ownedByScene = new OwnedByScene { Scene = SceneGroup.Battle };
        var parallax = ParallaxLayer.Ui;
        var parentTransform = new ParentTransform { Parent = parent };
        var tween = new PositionTween
        {
            Target = new Vector2(100f, 200f),
            Current = new Vector2(90f, 190f),
            Speed = 10f,
            Initialized = true,
        };
        var scene = new SceneState { Current = SceneGroup.Battle };
        var sprite = new Sprite
        {
            SourceRectangle = new Rectangle(1, 2, 30, 40),
            Texture = new TextureAssetId(4),
            Tint = new Color(200, 210, 220, 230),
            Flags = SpriteFlags.HasSourceRectangle | SpriteFlags.Visible,
        };
        var temperance = new Temperance { Amount = 6 };
        var threat = new Threat { Amount = 7 };
        var tooltip = new TooltipMetadata
        {
            Text = new StringId(5),
            KeywordSource = new StringId(6),
            OffsetPixels = 12,
            Type = TooltipType.Text,
            Position = TooltipPosition.Above,
        };
        var transform = new Transform
        {
            Position = new Vector2(11f, 12f),
            Scale = new Vector2(1.2f, 1.3f),
            Rotation = 0.4f,
            ZOrder = 42,
        };
        var uiElement = new UIElement
        {
            Bounds = new Rectangle(10, 20, 300, 80),
            SuppressCount = 0,
            Flags = UIInteractionFlags.BaseInteractable | UIInteractionFlags.Hovered,
            EventType = UIElementEventType.PlayCardRequested,
            SecondaryEventType = UIElementEventType.CardClicked,
            LayerType = UILayerType.Overlay,
        };

        var bundle = new SpawnBundle(23, 512);
        Add(ref bundle, in actionPoints);
        Add(ref bundle, in actorPresentation);
        Add(ref bundle, in animation);
        Add(ref bundle, in battlePresentation);
        Add(ref bundle, in courage);
        Add(ref bundle, in metadata);
        Add(ref bundle, in hp);
        Add(ref bundle, in inputContext);
        Add(ref bundle, in intellect);
        Add(ref bundle, in maxHandSize);
        Add(ref bundle, in ownedByScene);
        Add(ref bundle, in parallax);
        Add(ref bundle, in parentTransform);
        Add(ref bundle, in tween);
        Add(ref bundle, in scene);
        Add(ref bundle, in sprite);
        Add(ref bundle, in temperance);
        Add(ref bundle, in threat);
        Add(ref bundle, in tooltip);
        Add(ref bundle, in transform);
        Add(ref bundle, in uiElement);
        bundle.AddTag<DontDestroyOnLoad>();
        bundle.AddTag<DontDestroyOnReload>();

        EntityId entity = world.Create(in bundle);

        AssertRoundTrip(world, entity, in actionPoints);
        AssertRoundTrip(world, entity, in actorPresentation);
        AssertRoundTrip(world, entity, in animation);
        AssertRoundTrip(world, entity, in battlePresentation);
        AssertRoundTrip(world, entity, in courage);
        AssertRoundTrip(world, entity, in metadata);
        AssertRoundTrip(world, entity, in hp);
        AssertRoundTrip(world, entity, in inputContext);
        AssertRoundTrip(world, entity, in intellect);
        AssertRoundTrip(world, entity, in maxHandSize);
        AssertRoundTrip(world, entity, in ownedByScene);
        AssertRoundTrip(world, entity, in parallax);
        AssertRoundTrip(world, entity, in parentTransform);
        AssertRoundTrip(world, entity, in tween);
        AssertRoundTrip(world, entity, in scene);
        AssertRoundTrip(world, entity, in sprite);
        AssertRoundTrip(world, entity, in temperance);
        AssertRoundTrip(world, entity, in threat);
        AssertRoundTrip(world, entity, in tooltip);
        AssertRoundTrip(world, entity, in transform);
        AssertRoundTrip(world, entity, in uiElement);
        Assert.True(world.Has<DontDestroyOnLoad>(entity));
        Assert.True(world.Has<DontDestroyOnReload>(entity));
        Assert.True(world.Get<Animation>(entity).IsPlaying);
        Assert.True(world.Get<Animation>(entity).IsLooping);
        Assert.True(world.Get<Sprite>(entity).HasSourceRectangle);
        Assert.True(world.Get<Sprite>(entity).IsVisible);
        Assert.True(world.Get<UIElement>(entity).IsInteractable);
    }

    [Fact]
    public void Shared_components_and_persistence_tags_participate_in_cached_queries()
    {
        World world = CreateWorld();
        var persistentBundle = new SpawnBundle(3);
        persistentBundle.Add(new Transform { Position = new Vector2(10f, 20f), Scale = Vector2.One });
        persistentBundle.Add(new HP { Max = 40, Current = 30 });
        persistentBundle.Add(new OwnedByScene { Scene = SceneGroup.Battle });
        persistentBundle.AddTag<DontDestroyOnLoad>();
        EntityId persistent = world.Create(in persistentBundle);

        var transientBundle = new SpawnBundle(3);
        transientBundle.Add(new Transform { Position = new Vector2(50f, 60f), Scale = Vector2.One });
        transientBundle.Add(new HP { Max = 20, Current = 10 });
        transientBundle.Add(new OwnedByScene { Scene = SceneGroup.Battle });
        EntityId transient = world.Create(in transientBundle);

        ComponentSignature persistentMask = ComponentSignature.Empty
            .With(ComponentType<DontDestroyOnLoad>.Id);
        Query<Transform, HP> query = world.Query<Transform, HP>(new QueryFilter(All: persistentMask));
        var matched = 0;
        foreach (QueryChunk<Transform, HP> chunk in query)
        {
            foreach (int row in chunk.Rows)
            {
                chunk.Component1[row].Position.X += 5f;
                chunk.Component2[row].Current -= 3;
                matched++;
            }
        }

        Assert.Equal(1, matched);
        Assert.Equal(15f, world.Get<Transform>(persistent).Position.X);
        Assert.Equal(27, world.Get<HP>(persistent).Current);
        Assert.Equal(50f, world.Get<Transform>(transient).Position.X);
        Assert.Equal(10, world.Get<HP>(transient).Current);
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private static void Add<T>(ref SpawnBundle bundle, in T component)
        where T : unmanaged, IComponent => bundle.Add(in component);

    private static void AssertRoundTrip<T>(World world, EntityId entity, in T expected)
        where T : unmanaged, IComponent => Assert.Equal(expected, world.Get<T>(entity));
}
