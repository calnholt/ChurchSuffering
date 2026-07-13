#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.DataOriented.Core;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Core;

public sealed class WorldStorageTests
{
    [Fact]
    public void Component_signature_is_exactly_512_bits_and_supports_all_words()
    {
        var signature = ComponentSignature.Empty
            .With(0)
            .With(63)
            .With(64)
            .With(255)
            .With(511);

        Assert.Equal(64, Unsafe.SizeOf<ComponentSignature>());
        Assert.True(signature.Contains(0));
        Assert.True(signature.Contains(63));
        Assert.True(signature.Contains(64));
        Assert.True(signature.Contains(255));
        Assert.True(signature.Contains(511));
        Assert.False(signature.Contains(510));
        Assert.True(signature.ContainsAll(ComponentSignature.Empty.With(64).With(511)));
        Assert.True(signature.Intersects(ComponentSignature.Empty.With(255)));
        Assert.False(signature.Without(255).Contains(255));
        Assert.Throws<ArgumentOutOfRangeException>(() => signature.With(512));
    }

    [Fact]
    public void Create_get_set_and_tags_use_final_archetype_placement()
    {
        var world = CreateWorld();
        var bundle = Bundle(new Position { X = 4, Y = 7 });
        bundle.Add(new Velocity { X = 2, Y = 3 });
        bundle.AddTag<Selected>();

        var entity = world.Create(in bundle);

        Assert.Equal(1, world.EntityCount);
        Assert.Equal(2, world.ArchetypeCount);
        Assert.True(world.Has<Position>(entity));
        Assert.True(world.Has<Velocity>(entity));
        Assert.True(world.Has<Selected>(entity));
        Assert.Equal(4, world.Get<Position>(entity).X);
        world.Set(entity, new Position { X = 11, Y = 12 });
        Assert.Equal(11, world.Get<Position>(entity).X);
        Assert.True(world.TryGet(entity, out Velocity velocity));
        Assert.Equal(3, velocity.Y);
        Assert.False(world.TryGet(entity, out Health _));
    }

    [Fact]
    public void Destroyed_indexes_are_reused_with_new_generations_and_stale_handles_rejected()
    {
        var world = CreateWorld();
        var firstBundle = Bundle(new Position { X = 1 });
        var first = world.Create(in firstBundle);

        world.Destroy(first);
        var secondBundle = Bundle(new Position { X = 2 });
        var second = world.Create(in secondBundle);

        Assert.Equal(first.Index, second.Index);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.False(world.IsAlive(first));
        Assert.True(world.IsAlive(second));
        Assert.Throws<InvalidOperationException>(() => world.Get<Position>(first));
        Assert.Throws<InvalidOperationException>(() => world.Destroy(first));
        Assert.False(world.TryGet(first, out Position _));
    }

    [Fact]
    public void Archetype_moves_preserve_retained_values_and_cache_transition_targets()
    {
        var world = CreateWorld();
        var bundle = Bundle(new Position { X = 5, Y = 6 });
        var entity = world.Create(in bundle);

        world.Add(entity, new Velocity { X = 8, Y = 9 });
        var archetypesAfterFirstAdd = world.ArchetypeCount;
        world.Remove<Velocity>(entity);
        world.Add(entity, new Velocity { X = 10, Y = 11 });

        Assert.Equal(archetypesAfterFirstAdd, world.ArchetypeCount);
        Assert.Equal(3, world.StructuralMoveCount);
        Assert.Equal(5, world.Get<Position>(entity).X);
        Assert.Equal(10, world.Get<Velocity>(entity).X);
    }

    [Fact]
    public void Batch_transition_adds_multiple_types_with_one_move()
    {
        var world = CreateWorld();
        var initial = Bundle(new Position { X = 3 });
        var entity = world.Create(in initial);
        var additions = Bundle(new Velocity { X = 4 });
        additions.Add(new Health { Current = 20 });
        additions.AddTag<Selected>();

        world.Transition(entity, in additions, ComponentSignature.Empty);

        Assert.Equal(1, world.StructuralMoveCount);
        Assert.Equal(3, world.Get<Position>(entity).X);
        Assert.Equal(4, world.Get<Velocity>(entity).X);
        Assert.Equal(20, world.Get<Health>(entity).Current);
        Assert.True(world.Has<Selected>(entity));
    }

    [Fact]
    public void Swap_removal_updates_the_moved_entity_location()
    {
        var world = CreateWorld();
        var firstBundle = Bundle(new Position { X = 1 });
        var secondBundle = Bundle(new Position { X = 2 });
        var thirdBundle = Bundle(new Position { X = 3 });
        var first = world.Create(in firstBundle);
        var second = world.Create(in secondBundle);
        var third = world.Create(in thirdBundle);

        world.Destroy(first);

        Assert.Equal(2, world.Get<Position>(second).X);
        Assert.Equal(3, world.Get<Position>(third).X);
        world.Set(third, new Position { X = 30 });
        Assert.Equal(30, world.Get<Position>(third).X);
    }

    [Fact]
    public void Enable_and_disable_update_location_and_chunk_state()
    {
        var world = CreateWorld();
        var bundle = Bundle(new Position());
        var entity = world.Create(in bundle);

        world.Disable(entity);
        Assert.False(world.IsEnabled(entity));
        var archetype = Assert.Single(
            world.Archetypes,
            value => value.Signature.Contains(ComponentType<Position>.Id));
        Assert.False(Assert.Single(archetype.Chunks).IsEnabled(0));
        world.Disable(entity);
        world.Enable(entity);
        Assert.True(world.IsEnabled(entity));
        Assert.True(Assert.Single(archetype.Chunks).IsEnabled(0));
    }

    [Fact]
    public void Empty_chunks_are_reused_without_allocating_new_chunk_storage()
    {
        var world = CreateWorld();
        var sampleBundle = Bundle(new Position());
        var sample = world.Create(in sampleBundle);
        var archetype = Assert.Single(
            world.Archetypes,
            value => value.Signature.Contains(ComponentType<Position>.Id));
        var entityCount = archetype.ChunkCapacity + 1;
        world.Destroy(sample);

        var entities = new EntityId[entityCount];
        for (var index = 0; index < entities.Length; index++)
        {
            var bundle = Bundle(new Position { X = index });
            entities[index] = world.Create(in bundle);
        }

        var allocatedBefore = archetype.AllocatedChunkCount;
        world.Destroy(entities);
        Assert.Equal(0, world.EntityCount);
        Assert.Equal(allocatedBefore, archetype.ReusableChunkCount);

        for (var index = 0; index < entities.Length; index++)
        {
            var bundle = Bundle(new Position { X = index });
            entities[index] = world.Create(in bundle);
        }

        Assert.Equal(allocatedBefore, archetype.AllocatedChunkCount);
    }

    [Fact]
    public void Chunk_capacity_uses_16_kib_row_budget_and_clamps_to_1024()
    {
        var world = CreateWorld();
        var smallBundle = Bundle(new Position());
        _ = world.Create(in smallBundle);
        var small = Assert.Single(
            world.Archetypes,
            value => value.Signature.Contains(ComponentType<Position>.Id));

        var largeBundle = Bundle(new LargeComponent());
        _ = world.Create(in largeBundle);
        var large = Assert.Single(
            world.Archetypes,
            value => value.Signature.Contains(ComponentType<LargeComponent>.Id));

        Assert.Equal(1024, small.ChunkCapacity);
        Assert.Equal(16384 / (Unsafe.SizeOf<EntityId>() + Unsafe.SizeOf<LargeComponent>()), large.ChunkCapacity);
    }

    [Fact]
    public void Bulk_destruction_can_filter_by_signature_then_tear_down_the_world()
    {
        var world = CreateWorld();
        var selectedBundle = Bundle(new Position());
        selectedBundle.AddTag<Selected>();
        var plainBundle = Bundle(new Position());
        var selectedOne = world.Create(in selectedBundle);
        var selectedTwo = world.Create(in selectedBundle);
        var plain = world.Create(in plainBundle);

        var selectedMask = ComponentSignature.Empty.With(ComponentType<Selected>.Id);
        Assert.Equal(2, world.DestroyAll(in selectedMask));
        Assert.False(world.IsAlive(selectedOne));
        Assert.False(world.IsAlive(selectedTwo));
        Assert.True(world.IsAlive(plain));
        Assert.Equal(1, world.DestroyAll());
        Assert.Equal(0, world.EntityCount);
    }

    [Fact]
    public void Spawn_bundle_requires_every_data_component_value_and_rejects_duplicates()
    {
        var world = CreateWorld();
        var bundle = Bundle(new Position());
        Assert.Throws<InvalidOperationException>(() => bundle.Add(new Position()));

        var invalidSignature = bundle.Signature.With(ComponentType<Velocity>.Id);
        var invalid = ReplaceSignatureForTest(in bundle, invalidSignature);
        Assert.Throws<InvalidOperationException>(() => world.Create(in invalid));
        Assert.Equal(0, world.EntityCount);
    }

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.RegisterComponent<Position>(0);
        registry.RegisterComponent<Velocity>(1);
        registry.RegisterComponent<Health>(2);
        registry.RegisterTag<Selected>(3);
        registry.RegisterComponent<LargeComponent>(4);
        registry.Seal();
        return new World(registry, initialEntityCapacity: 4);
    }

    private static SpawnBundle Bundle<T>(in T component)
        where T : unmanaged, IComponent
    {
        var bundle = new SpawnBundle(4, 128);
        bundle.Add(in component);
        return bundle;
    }

    private static SpawnBundle ReplaceSignatureForTest(
        in SpawnBundle source,
        ComponentSignature signature)
    {
        var result = source;
        var signatureField = typeof(SpawnBundle).GetField(
            "signature",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        object boxed = result;
        signatureField.SetValue(boxed, signature);
        return (SpawnBundle)boxed;
    }

    private struct Position : IComponent
    {
        public int X;
        public int Y;
    }

    private struct Velocity : IComponent
    {
        public int X;
        public int Y;
    }

    private struct Health : IComponent
    {
        public int Current;
    }

    private struct Selected : ITag { }

    [StructLayout(LayoutKind.Sequential, Size = 64)]
    private struct LargeComponent : IComponent { }
}
