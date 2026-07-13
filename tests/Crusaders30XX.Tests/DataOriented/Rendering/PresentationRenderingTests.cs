#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rendering;

public sealed class PresentationRenderingTests
{
    [Fact]
    public void Packet_layers_sort_by_z_then_stable_order_and_only_resort_when_dirty()
    {
        var packets = new RenderPacketStore(4);
        packets.BeginExtraction();
        packets.Add(Packet(3, 20, 2));
        packets.Add(Packet(1, 10, 4));
        packets.Add(Packet(2, 10, 1));
        packets.EndExtraction();

        ReadOnlySpan<RenderPacket> layer = packets.GetLayer(RenderLayer.World);
        Assert.Equal(2, layer[0].Entity.Index);
        Assert.Equal(1, layer[1].Entity.Index);
        Assert.Equal(3, layer[2].Entity.Index);
        Assert.Equal(1, packets.SortCount);

        // Extraction order is now the stable sorted order; animation-only changes do not sort.
        packets.BeginExtraction();
        packets.Add(Packet(2, 10, 1, 0.25f));
        packets.Add(Packet(1, 10, 4, 0.5f));
        packets.Add(Packet(3, 20, 2, 0.75f));
        packets.EndExtraction();
        Assert.Equal(1, packets.SortCount);
    }

    [Fact]
    public void Warm_packet_extraction_allocates_zero_bytes()
    {
        var packets = new RenderPacketStore(8);
        for (var warmup = 0; warmup < 8; warmup++) ExtractStable(packets);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 256; iteration++) ExtractStable(packets);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Draw_consumer_is_read_only_for_world_and_packet_state()
    {
        World world = CreateWorld();
        EntityId entity = CreateSprite(world, new Vector2(40, 70), z: 6);
        var packets = new RenderPacketStore();
        var extraction = new SpriteRenderExtractionSystem(world, packets);
        extraction.Extract();
        Transform before = world.Get<Transform>(entity);
        long moves = world.StructuralMoveCount;
        long version = packets.ExtractionVersion;
        var sink = new CountingSink();

        new RenderPacketDrawConsumer().Draw(packets, sink);

        Assert.Equal(1, sink.Count);
        Assert.Equal(before, world.Get<Transform>(entity));
        Assert.Equal(moves, world.StructuralMoveCount);
        Assert.Equal(version, packets.ExtractionVersion);
    }

    [Fact]
    public void Position_tween_and_parent_transform_are_deterministic()
    {
        World world = CreateWorld();
        var parentBundle = new SpawnBundle(1);
        parentBundle.Add(new Transform { Position = new Vector2(100, 50), Scale = Vector2.One, ZOrder = 3 });
        EntityId parent = world.Create(parentBundle);
        var childBundle = new SpawnBundle(3);
        childBundle.Add(new Transform { Position = Vector2.Zero, Scale = Vector2.One, ZOrder = 2 });
        childBundle.Add(new PositionTween { Target = new Vector2(10, 0), Speed = 5 });
        childBundle.Add(new ParentTransform { Parent = parent });
        EntityId child = world.Create(childBundle);

        var tween = new PositionTweenPresentationSystem(world);
        tween.Update(1f);
        Transform local = world.Get<Transform>(child);
        Transform resolved = TransformResolver.Resolve(world.AsReadOnly(), child, in local);

        Assert.Equal(new Vector2(5, 0), local.Position);
        Assert.Equal(new Vector2(105, 50), resolved.Position);
        Assert.Equal(5, resolved.ZOrder);
    }

    [Fact]
    public void Parallax_tracks_external_anchor_writes_without_exposing_internal_layer_state()
    {
        World world = CreateWorld();
        var bundle = new SpawnBundle(3);
        bundle.Add(new Transform { Position = new Vector2(100, 200), Scale = Vector2.One });
        bundle.Add(new ParallaxLayer { MultiplierX = 1, MultiplierY = 1, MaxOffset = 10, SmoothTime = 0 });
        bundle.Add(new ParallaxPresentationState());
        EntityId entity = world.Create(bundle);
        var parallax = new ParallaxPresentationSystem(world);
        var input = new PresentationFrameInput(new Vector2(1920, 1080), new Vector2(1920, 1080));

        parallax.Update(1f, in input);
        Assert.Equal(new Vector2(110, 210), world.Get<Transform>(entity).Position);
        ref Transform transform = ref world.Get<Transform>(entity);
        transform.Position = new Vector2(400, 500);
        parallax.Update(1f, in input);
        Assert.Equal(new Vector2(410, 510), world.Get<Transform>(entity).Position);
    }

    [Fact]
    public void Tooltip_modal_overlay_effect_shader_and_audio_contracts_are_unmanaged_and_queue_typed_requests()
    {
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<POITitleTooltipSource>());
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<PauseMenuOverlay>());
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ActiveVisualEffect>());
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<ShaderRequest>());
        Assert.False(System.Runtime.CompilerServices.RuntimeHelpers.IsReferenceOrContainsReferences<AudioRequest>());

        World world = CreateWorld();
        var requests = new PresentationRequestQueues(4);
        var consumer = new PresentationRequestConsumer(requests);
        var hub = new PresentationEventHub();
        var runtime = new EventRuntime(new EventRoutingEndpoint(hub.BuildRoutes(consumer)));
        world.AttachEventRuntime(runtime);
        hub.PlaySfx.Publish(new PlaySfxEvent(new SoundId(7), 0.8f, 0.1f));
        hub.Shockwave.Publish(new ShockwaveEvent(new Vector2(5), 12, 0.4f, 2));
        hub.StartDebuffAnimation.Publish(new StartDebuffAnimation(EntityId.Null, new VisualEffectRecipeId(3), 9));
        runtime.DrainBarrier();

        Assert.Equal(1, requests.Audio.Length);
        Assert.Equal(1, requests.Shaders.Length);
        Assert.Equal(1, requests.VisualEffects.Length);
        Assert.Equal(AudioRequestKind.PlaySound, requests.Audio[0].Kind);
        Assert.Equal(ShaderRequestKind.Shockwave, requests.Shaders[0].Kind);
    }

    [Fact]
    public void Diagnostics_and_new_world_snapshot_host_expose_data_without_baseline_mutation()
    {
        World world = CreateWorld();
        EntityId entity = CreateSprite(world, Vector2.One, 0);
        var diagnostics = new PresentationDiagnosticsStore();
        diagnostics.Inspect(world, [entity, new EntityId(999, 1)]);
        var host = new NewWorldSnapshotFixtureHost();

        Assert.Equal(2, diagnostics.Entities.Length);
        Assert.Equal(1, diagnostics.Entities[0].Alive);
        Assert.Equal(0, diagnostics.Entities[1].Alive);
        Assert.True(host.TryResolve("enemy-attack-banner", out NewWorldSnapshotFixture fixture));
        Assert.Equal(6, fixture.VariantCount);
        Assert.True(host.Registered.Length >= 38);
    }

    private static RenderPacket Packet(int entity, int z, int stable, float effect = 0) => new(
        new EntityId(entity, 1), new TextureAssetId(1), default, Vector2.Zero, Vector2.Zero,
        Vector2.One, Color.White, 0, effect, 0, z, stable, RenderLayer.World,
        RenderPacketKind.Sprite, RenderPacketFlags.None);

    private static void ExtractStable(RenderPacketStore packets)
    {
        packets.BeginExtraction();
        packets.Add(Packet(1, 1, 1));
        packets.Add(Packet(2, 2, 2));
        packets.Add(Packet(3, 3, 3));
        packets.EndExtraction();
    }

    private static EntityId CreateSprite(World world, Vector2 position, int z)
    {
        var bundle = new SpawnBundle(2);
        bundle.Add(new Transform { Position = position, Scale = Vector2.One, ZOrder = z });
        bundle.Add(new Sprite { Texture = new TextureAssetId(1), Tint = Color.White, Flags = SpriteFlags.Visible });
        return world.Create(bundle);
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private sealed class CountingSink : IRenderPacketSink
    {
        public int Count { get; private set; }
        public void Draw(in RenderPacket packet) => Count++;
    }
}
