#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Authoring.Combat;
using Crusaders30XX.ECS.DataOriented.Authoring.Meta;
using Crusaders30XX.ECS.DataOriented.Authoring.Text;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Integration;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rendering;

public sealed class Ecs052TextRenderingTests
{
    public static TheoryData<SceneGroup> StaticScenes => new()
    {
        SceneGroup.TitleMenu,
        SceneGroup.Climb,
        SceneGroup.WayStation,
        SceneGroup.Achievement,
    };

    [Fact]
    public void Text_component_is_unmanaged_generated_and_appended_without_renumbering_prior_types()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TextPresentation>());
        Assert.True(GeneratedComponentRegistry.TryGetDescriptor(
            typeof(TextPresentation).FullName!,
            out GeneratedComponentDescriptor descriptor));
        Assert.Equal(GeneratedComponentRegistry.Count - 1, descriptor.Id);
        Assert.False(descriptor.IsTag);
    }

    [Fact]
    public void Extraction_resolves_parent_transform_and_orders_by_layer_z_then_entity()
    {
        World world = CreateWorld();
        var parentBundle = new SpawnBundle(1);
        parentBundle.Add(new Transform { Position = new Vector2(100, 50), Scale = Vector2.One, ZOrder = 5 });
        EntityId parent = world.Create(in parentBundle);
        EntityId later = CreateText(world, TextContentIds.Courage, new Vector2(5, 7), 10, RenderLayer.Hud);
        EntityId earlier = CreateText(world, TextContentIds.Health, new Vector2(2, 3), 10, RenderLayer.Hud);
        world.Add(earlier, new ParentTransform { Parent = parent });
        EntityId overlay = CreateText(world, TextContentIds.TestFight, Vector2.Zero, 0, RenderLayer.Overlay);
        var packets = new TextRenderPacketStore();

        new TextRenderExtractionSystem(world, packets).Extract();

        ReadOnlySpan<TextRenderPacket> hud = packets.GetLayer(RenderLayer.Hud);
        Assert.Equal(2, hud.Length);
        Assert.Equal(earlier.Index < later.Index ? earlier : later, hud[0].Entity);
        TextRenderPacket parented = hud[0].Entity == earlier ? hud[0] : hud[1];
        Assert.Equal(new Vector2(102, 53), parented.Position);
        Assert.Equal(15, parented.ZOrder);
        Assert.Equal(overlay, Assert.Single(packets.GetLayer(RenderLayer.Overlay).ToArray()).Entity);
    }

    [Fact]
    public void Warm_text_extraction_allocates_zero_bytes()
    {
        World world = CreateWorld();
        for (var index = 0; index < 8; index++)
            CreateText(world, TextContentIds.Health, new Vector2(index), index, RenderLayer.Hud);
        var packets = new TextRenderPacketStore(16);
        var extraction = new TextRenderExtractionSystem(world, packets);
        for (var warmup = 0; warmup < 32; warmup++) extraction.Extract();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 256; iteration++) extraction.Extract();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void External_draw_resolves_catalog_text_and_is_read_only()
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = MetaStaticSceneMaterializer.Materialize(world, SceneGroup.TitleMenu);
        var packets = new TextRenderPacketStore();
        new TextRenderExtractionSystem(world, packets).Extract();
        var sink = new TextSink();
        int entities = world.EntityCount;
        long moves = world.StructuralMoveCount;
        long version = packets.ExtractionVersion;

        int drawn = new TextRenderPacketDrawConsumer().Draw(
            packets,
            new StaticTextPresentationCatalog(),
            sink);

        Assert.Equal(1, drawn);
        Assert.Equal(new[] { "CRUSADERS 30XX:2" }, sink.Calls);
        Assert.Equal(entities, world.EntityCount);
        Assert.Equal(moves, world.StructuralMoveCount);
        Assert.Equal(version, packets.ExtractionVersion);
    }

    [Theory]
    [MemberData(nameof(StaticScenes))]
    public void Every_static_meta_scene_authors_nonzero_owned_text(SceneGroup scene)
    {
        World world = CreateWorld();
        using MetaAuthoredScene authored = MetaStaticSceneMaterializer.Materialize(world, scene);
        var packets = new TextRenderPacketStore();

        new TextRenderExtractionSystem(world, packets).Extract();

        Assert.True(packets.Count > 0);
        Assert.All(AllTextPackets(packets), packet =>
            Assert.Equal(scene, world.Get<OwnedByScene>(packet.Entity).Scene));
    }

    [Fact]
    public void Battle_hud_and_test_fight_author_representative_text()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create();
        var fixture = new DataOrientedTestFightFixture(
            CardId.Hammer,
            EnemyId.Skeleton,
            ClimbDifficulty.Hard,
            Seed: 42);
        runtime.BeginTestCombat(fixture);
        var packets = new TextRenderPacketStore();

        new TextRenderExtractionSystem(runtime.World, packets).Extract();

        Assert.True(packets.Count >= 5);
        var catalog = new StaticTextPresentationCatalog();
        Assert.Contains(AllTextPackets(packets), packet => packet.Content == TextContentIds.Health);
        Assert.Contains(AllTextPackets(packets), packet => packet.Content == TextContentIds.ActionPoints);
        Assert.Contains(AllTextPackets(packets), packet => packet.Content == TextContentIds.TestFight);
        Assert.All(AllTextPackets(packets), packet => Assert.True(catalog.TryResolve(packet.Content, out _)));
    }

    [Fact]
    public void Every_snapshot_fixture_variant_authors_only_resolvable_nonzero_text()
    {
        var registry = new NewWorldSnapshotFixtureHost();
        var materializer = new SnapshotFixtureMaterializer(registry);
        var catalog = new StaticTextPresentationCatalog(registry);
        foreach (NewWorldSnapshotFixture fixture in registry.Registered)
        {
            for (var variant = 0; variant < fixture.VariantCount; variant++)
            {
                World world = CreateWorld();
                using MetaAuthoredScene authored = materializer.Materialize(world, fixture.Id, variant);
                var packets = new TextRenderPacketStore();
                new TextRenderExtractionSystem(world, packets).Extract();

                TextRenderPacket[] fixturePackets = AllTextPackets(packets);
                bool intentionallyTextless =
                    fixture.Id == "guardian-angel" && variant == 0 ||
                    fixture.Id == "enemy-damage-meter" && variant == 3;
                if (intentionallyTextless) Assert.Empty(fixturePackets);
                else Assert.NotEmpty(fixturePackets);
                Assert.All(fixturePackets, packet =>
                {
                    Assert.True(catalog.TryResolve(packet.Content, out string? text));
                    Assert.False(string.IsNullOrWhiteSpace(text));
                    Assert.Equal(fixture.Scene, world.Get<OwnedByScene>(packet.Entity).Scene);
                });
            }
        }
    }

    [Fact]
    public void Scene_transition_lifecycle_removes_old_text_and_materializes_next_scene_text()
    {
        using DataOrientedGameRuntime runtime = DataOrientedGameRuntime.Create(SceneGroup.TitleMenu);
        EntityId[] titleText = runtime.MetaSceneAuthoring.Current!.Entities.ToArray()
            .Where(entity => runtime.World.Has<TextPresentation>(entity))
            .ToArray();
        Assert.NotEmpty(titleText);

        runtime.RequestScene(SceneGroup.Climb);
        runtime.Update(TimeSpan.FromMilliseconds(16));
        runtime.Update(TimeSpan.FromMilliseconds(16));

        Assert.All(titleText, entity => Assert.False(runtime.World.IsAlive(entity)));
        var packets = new TextRenderPacketStore();
        new TextRenderExtractionSystem(runtime.World, packets).Extract();
        Assert.Contains(AllTextPackets(packets), packet => packet.Content == TextContentIds.Climb);
        Assert.All(AllTextPackets(packets), packet => Assert.Equal(
            SceneGroup.Climb,
            runtime.World.Get<OwnedByScene>(packet.Entity).Scene));
    }

    private static EntityId CreateText(
        World world,
        StringId content,
        Vector2 position,
        int z,
        RenderLayer layer)
    {
        var bundle = new SpawnBundle(2);
        bundle.Add(new Transform { Position = position, Scale = Vector2.One, ZOrder = z });
        bundle.Add(new TextPresentation
        {
            Content = content,
            Style = TextStyleIds.Hud,
            Scale = Vector2.One,
            Tint = Color.White,
            Layer = layer,
            Alignment = TextAlignment.TopLeft,
            Flags = TextPresentationFlags.Visible,
        });
        return world.Create(in bundle);
    }

    private static TextRenderPacket[] AllTextPackets(TextRenderPacketStore packets) =>
        Enumerable.Range((int)RenderLayer.Background, 8)
            .SelectMany(layer => packets.GetLayer((RenderLayer)layer).ToArray())
            .ToArray();

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private sealed class TextSink : ITextRenderPacketSink
    {
        public List<string> Calls { get; } = [];
        public void Draw(in TextRenderPacket packet, string text, in TextStyleDefinition style) =>
            Calls.Add($"{text}:{style.Font.Value}");
    }
}
