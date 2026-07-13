#nullable enable

using System;
using System.Linq;
using Crusaders30XX.ECS.DataOriented.Authoring.Meta;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Integration;

public sealed class Ecs050MetaSceneAuthoringTests
{
    public static TheoryData<SceneGroup> StaticScenes => new()
    {
        SceneGroup.TitleMenu,
        SceneGroup.Climb,
        SceneGroup.WayStation,
        SceneGroup.Achievement,
    };

    [Theory]
    [MemberData(nameof(StaticScenes))]
    public void Static_meta_scene_materialization_produces_owned_visible_render_packets(SceneGroup scene)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = MetaStaticSceneMaterializer.Materialize(world, scene);
        var packets = new RenderPacketStore();
        var extraction = new SpriteRenderExtractionSystem(world, packets);

        extraction.Extract();

        Assert.NotEmpty(authored.Entities.ToArray());
        Assert.Equal(authored.Entities.Length, packets.Count);
        foreach (EntityId entity in authored.Entities)
        {
            Assert.True(world.Has<Transform>(entity));
            Assert.True(world.Has<Sprite>(entity));
            Assert.Equal(scene, world.Get<OwnedByScene>(entity).Scene);
            Assert.True(world.Get<Sprite>(entity).IsVisible);
        }
    }

    [Fact]
    public void Visibility_and_disposal_prevent_packet_leakage_between_scenes()
    {
        World world = CreateWorld();
        var packets = new RenderPacketStore();
        var extraction = new SpriteRenderExtractionSystem(world, packets);
        using MetaAuthoredScene title = MetaStaticSceneMaterializer.Materialize(world, SceneGroup.TitleMenu);
        EntityId[] titleEntities = title.Entities.ToArray();

        title.SetVisible(false);
        using MetaAuthoredScene climb = MetaStaticSceneMaterializer.Materialize(world, SceneGroup.Climb);
        extraction.Extract();

        Assert.Equal(climb.Entities.Length, packets.Count);
        Assert.All(AllPackets(packets), packet => Assert.Equal(
            SceneGroup.Climb,
            world.Get<OwnedByScene>(packet.Entity).Scene));

        title.Dispose();
        Assert.All(titleEntities, entity => Assert.False(world.IsAlive(entity)));
        Assert.All(climb.Entities.ToArray(), entity => Assert.True(world.IsAlive(entity)));
    }

    [Fact]
    public void Every_registered_snapshot_fixture_variant_materializes_nonzero_packets()
    {
        var fixtureRegistry = new NewWorldSnapshotFixtureHost();
        var materializer = new SnapshotFixtureMaterializer(fixtureRegistry);
        NewWorldSnapshotFixture[] fixtures = materializer.Registered.ToArray();

        Assert.Equal(fixtureRegistry.Registered.Length, fixtures.Length);
        Assert.Equal(46, fixtures.Length);
        foreach (NewWorldSnapshotFixture fixture in fixtures)
        {
            for (var variant = 0; variant < fixture.VariantCount; variant++)
            {
                World world = CreateWorld();
                using MetaAuthoredScene authored = materializer.Materialize(world, fixture.Id, variant);
                var packets = new RenderPacketStore();
                new SpriteRenderExtractionSystem(world, packets).Extract();

                Assert.True(packets.Count > 0, $"{fixture.Id}[{variant}] produced no packets.");
                Assert.All(AllPackets(packets), packet =>
                {
                    Assert.False(packet.Texture.IsNull);
                    Assert.Equal(fixture.Scene, world.Get<OwnedByScene>(packet.Entity).Scene);
                });
            }
        }
    }

    [Fact]
    public void Fixture_materialization_is_deterministic_and_rejects_unknown_inputs()
    {
        var materializer = new SnapshotFixtureMaterializer();
        World firstWorld = CreateWorld();
        World secondWorld = CreateWorld();
        using MetaAuthoredScene first = materializer.Materialize(firstWorld, "enemy-attack-banner", 5);
        using MetaAuthoredScene second = materializer.Materialize(secondWorld, "enemy-attack-banner", 5);
        var firstPackets = new RenderPacketStore();
        var secondPackets = new RenderPacketStore();
        new SpriteRenderExtractionSystem(firstWorld, firstPackets).Extract();
        new SpriteRenderExtractionSystem(secondWorld, secondPackets).Extract();

        Assert.Equal(AllPackets(firstPackets), AllPackets(secondPackets));
        Assert.Throws<ArgumentException>(() => materializer.Materialize(CreateWorld(), "missing-fixture"));
        Assert.Throws<ArgumentOutOfRangeException>(() => materializer.Materialize(CreateWorld(), "waystation", 1));
    }

    [Fact]
    public void Root_route_hook_materializes_static_scenes_on_prepare_events()
    {
        World world = CreateWorld();
        var hub = new GlobalUiEventHub();
        using var consumer = new MetaSceneAuthoringConsumer(world);
        var runtime = new EventRuntime(new EventRoutingEndpoint(hub.BuildRoutes(world, consumer.Register())));
        world.AttachEventRuntime(runtime);

        hub.PrepareScene.Publish(new PrepareSceneEvent(Guid.NewGuid(), SceneGroup.WayStation));
        runtime.DrainBarrier();

        Assert.NotNull(consumer.Current);
        Assert.Equal(SceneGroup.WayStation, consumer.Current.Scene);
        var packets = new RenderPacketStore();
        new SpriteRenderExtractionSystem(world, packets).Extract();
        Assert.True(packets.Count > 0);

        EntityId[] oldEntities = consumer.Current.Entities.ToArray();
        hub.PrepareScene.Publish(new PrepareSceneEvent(Guid.NewGuid(), SceneGroup.Battle));
        runtime.DrainBarrier();

        Assert.Null(consumer.Current);
        Assert.All(oldEntities, entity => Assert.False(world.IsAlive(entity)));
    }

    private static RenderPacket[] AllPackets(RenderPacketStore packets) =>
        Enumerable.Range((int)RenderLayer.Background, (int)RenderLayer.Debug + 1)
            .SelectMany(layer => packets.GetLayer((RenderLayer)layer).ToArray())
            .ToArray();

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());
}
