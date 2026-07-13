using System;
using System.Runtime.CompilerServices;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class DataOrientedStorageTests
{
    [Fact]
    public void Dynamic_buffer_preserves_order_across_growth_insert_and_removal()
    {
        using var store = new DynamicBufferStore();
        var owner = new EntityId(7, 2);
        DynamicBufferHandle<int> handle = store.Create<int>(owner, initialCapacity: 2);
        DynamicBuffer<int> buffer = store.Get(handle);

        buffer.Add(10);
        buffer.Add(30);
        buffer.Insert(1, 20);
        buffer.AddRange([40, 50]);
        buffer.RemoveAt(3);
        buffer[0] = 5;

        Assert.Equal([5, 20, 30, 50], buffer.AsReadOnlySpan().ToArray());
        Assert.True(buffer.Capacity >= 5);
    }

    [Fact]
    public void Released_handle_is_stale_and_reused_slot_increments_generation_and_reuses_capacity()
    {
        using var store = new DynamicBufferStore();
        var owner = new EntityId(3, 1);
        DynamicBufferHandle<long> first = store.Create<long>(owner, initialCapacity: 9);
        int firstCapacity = store.Get(first).Capacity;

        store.Release(first);
        DynamicBufferHandle<long> second = store.Create<long>(owner, initialCapacity: 9);

        Assert.Equal(first.Index, second.Index);
        Assert.NotEqual(first.Generation, second.Generation);
        Assert.Equal(firstCapacity, store.Get(second).Capacity);
        Assert.False(store.TryGet(first, out _));
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => store.Get(first));
        Assert.Contains("dead or stale", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Destroying_owner_releases_all_owned_buffer_types_only()
    {
        var registry = new ComponentTypeRegistry();
        registry.Seal();
        var world = new World(registry);
        var empty = new SpawnBundle(0, 0);
        EntityId firstOwner = world.Create(in empty);
        EntityId secondOwner = world.Create(in empty);
        DynamicBufferHandle<int> firstInts = world.CreateDynamicBuffer<int>(firstOwner, 4);
        DynamicBufferHandle<short> firstShorts = world.CreateDynamicBuffer<short>(firstOwner, 4);
        DynamicBufferHandle<int> secondInts = world.CreateDynamicBuffer<int>(secondOwner, 4);

        world.Destroy(firstOwner);

        Assert.False(world.TryGetDynamicBuffer(firstInts, out _));
        Assert.False(world.TryGetDynamicBuffer(firstShorts, out _));
        Assert.True(world.TryGetDynamicBuffer(secondInts, out _));
        Assert.Single(world.GetDynamicBufferDebugSnapshot());
    }

    [Fact]
    public void Established_capacity_mutation_allocates_zero_bytes()
    {
        using var store = new DynamicBufferStore();
        DynamicBufferHandle<int> handle = store.Create<int>(new EntityId(1, 1), initialCapacity: 64);
        DynamicBuffer<int> buffer = store.Get(handle);
        for (var warmup = 0; warmup < 10; warmup++)
        {
            ExerciseEstablishedCapacity(buffer);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            ExerciseEstablishedCapacity(buffer);
        }

        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Buffer_debug_snapshot_reports_owner_type_count_and_capacity()
    {
        using var store = new DynamicBufferStore();
        var owner = new EntityId(9, 4);
        DynamicBufferHandle<int> handle = store.Create<int>(owner, 3);
        DynamicBuffer<int> buffer = store.Get(handle);
        buffer.AddRange([1, 2]);

        DynamicBufferDebugInfo info = Assert.Single(store.GetDebugSnapshot());
        Assert.Equal(typeof(int), info.ElementType);
        Assert.Equal(handle.Index, info.Index);
        Assert.Equal(handle.Generation, info.Generation);
        Assert.Equal(owner, info.Owner);
        Assert.Equal(2, info.Count);
        Assert.True(info.Capacity >= 3);
    }

    [Fact]
    public void Cached_unmanaged_mutation_handler_plays_operations_against_owning_world()
    {
        var registry = new ComponentTypeRegistry();
        registry.Seal();
        var world = new World(registry);
        var empty = new SpawnBundle(0, 0);
        EntityId owner = world.Create(in empty);
        DynamicBufferHandle<int> handle = world.CreateDynamicBuffer<int>(owner, 4);
        IDynamicBufferCommandHandler<DynamicBufferMutation<int>> handler =
            world.GetDynamicBufferMutationHandler<int>();

        Assert.Same(handler, world.GetDynamicBufferMutationHandler<int>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<DynamicBufferMutation<int>>());

        DynamicBufferMutation<int> add = DynamicBufferMutation<int>.Add(handle, 10);
        DynamicBufferMutation<int> insert = DynamicBufferMutation<int>.Insert(handle, 0, 5);
        DynamicBufferMutation<int> set = DynamicBufferMutation<int>.Set(handle, 1, 15);
        var commands = new CommandBuffer();
        commands.RecordDynamicBufferMutation(handler, in add);
        commands.RecordDynamicBufferMutation(handler, in insert);
        commands.RecordDynamicBufferMutation(handler, in set);
        commands.Playback(world);

        Assert.Equal([5, 15], world.GetDynamicBuffer(handle).AsReadOnlySpan().ToArray());

        var otherWorld = new World(registry);
        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() =>
            handler.Playback(otherWorld, in add));
        Assert.Contains("owning world", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Unique_index_rejects_collision_with_both_entities_and_cleans_up_on_destroy()
    {
        var index = new UniqueIndex<int>();
        var first = new EntityId(1, 1);
        var second = new EntityId(2, 1);
        index.Add(42, first);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => index.Add(42, second));
        Assert.Contains(first.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(second.ToString(), exception.Message, StringComparison.Ordinal);

        index.OnEntityDestroyed(first);
        Assert.False(index.TryGet(42, out _));
    }

    [Fact]
    public void Unique_tag_index_rejects_duplicate_and_releases_destroyed_entity()
    {
        var index = new UniqueEntityIndex<UniqueTag>();
        var first = new EntityId(4, 1);
        var second = new EntityId(8, 2);
        index.Register(first);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(() => index.Register(second));
        Assert.Contains(first.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(second.ToString(), exception.Message, StringComparison.Ordinal);

        index.OnEntityDestroyed(first);
        Assert.False(index.TryGet(out _));
    }

    [Fact]
    public void World_unique_tag_registration_get_and_missing_are_world_integrated()
    {
        World world = CreateUniqueTagWorld();
        EntityId unique = CreateUniqueTaggedEntity(world);

        Assert.False(world.TryGetUnique<UniqueTag>(out _));
        Assert.Throws<InvalidOperationException>(() => world.GetUnique<UniqueTag>());

        world.RegisterUnique<UniqueTag>(unique);

        Assert.Equal(unique, world.GetUnique<UniqueTag>());
        Assert.True(world.TryGetUnique<UniqueTag>(out EntityId found));
        Assert.Equal(unique, found);
    }

    [Fact]
    public void World_unique_tag_rejects_duplicate_and_names_both_entities()
    {
        World world = CreateUniqueTagWorld();
        EntityId first = CreateUniqueTaggedEntity(world);
        EntityId second = CreateUniqueTaggedEntity(world);
        world.RegisterUnique<UniqueTag>(first);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => world.RegisterUnique<UniqueTag>(second));

        Assert.Contains(first.ToString(), exception.Message, StringComparison.Ordinal);
        Assert.Contains(second.ToString(), exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void World_unique_tag_rejects_missing_tag_and_stale_entity()
    {
        World world = CreateUniqueTagWorld();
        var empty = new SpawnBundle(0, 0);
        EntityId withoutTag = world.Create(in empty);
        InvalidOperationException missingTag = Assert.Throws<InvalidOperationException>(
            () => world.RegisterUnique<UniqueTag>(withoutTag));
        Assert.Contains("does not carry tag", missingTag.Message, StringComparison.Ordinal);

        EntityId stale = CreateUniqueTaggedEntity(world);
        world.Destroy(stale);
        InvalidOperationException staleHandle = Assert.Throws<InvalidOperationException>(
            () => world.RegisterUnique<UniqueTag>(stale));
        Assert.Contains("dead or stale", staleHandle.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void World_unique_tag_cleans_up_on_destroy_and_accepts_replacement()
    {
        World world = CreateUniqueTagWorld();
        EntityId first = CreateUniqueTaggedEntity(world);
        world.RegisterUnique<UniqueTag>(first);

        world.Destroy(first);

        Assert.False(world.TryGetUnique<UniqueTag>(out _));
        Assert.Throws<InvalidOperationException>(() => world.GetUnique<UniqueTag>());
        EntityId replacement = CreateUniqueTaggedEntity(world);
        world.RegisterUnique<UniqueTag>(replacement);
        Assert.Equal(replacement, world.GetUnique<UniqueTag>());
    }

    [Fact]
    public void Unique_tag_slots_are_isolated_between_worlds()
    {
        World firstWorld = CreateUniqueTagWorld();
        World secondWorld = CreateUniqueTagWorld();
        EntityId first = CreateUniqueTaggedEntity(firstWorld);
        EntityId second = CreateUniqueTaggedEntity(secondWorld);

        firstWorld.RegisterUnique<UniqueTag>(first);
        secondWorld.RegisterUnique<UniqueTag>(second);

        Assert.Equal(first, firstWorld.GetUnique<UniqueTag>());
        Assert.Equal(second, secondWorld.GetUnique<UniqueTag>());
        firstWorld.Destroy(first);
        Assert.False(firstWorld.TryGetUnique<UniqueTag>(out _));
        Assert.Equal(second, secondWorld.GetUnique<UniqueTag>());
    }

    [Fact]
    public void String_table_and_name_index_provide_stable_ids_and_generation_checked_entity_cleanup()
    {
        var strings = new StringTable();
        var names = new EntityNameIndex(strings);
        var entity = new EntityId(5, 3);
        StringId first = strings.Intern("Player");
        StringId repeated = strings.Intern("Player");
        StringId indexed = names.Add(entity, "Player");

        Assert.Equal(first, repeated);
        Assert.Equal(first, indexed);
        Assert.Equal("Player", strings.GetRequired(first));
        Assert.True(names.TryGet("Player", out EntityId found));
        Assert.Equal(entity, found);

        names.OnEntityDestroyed(new EntityId(entity.Index, entity.Generation + 1));
        Assert.True(names.TryGet("Player", out _));
        names.OnEntityDestroyed(entity);
        Assert.False(names.TryGet("Player", out _));
    }

    [Fact]
    public void Compact_resource_ids_are_unmanaged_and_reserve_default_as_null()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<StringId>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TextureAssetId>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<SoundId>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<VisualEffectRecipeId>());
        Assert.True(default(StringId).IsNull);
        Assert.True(default(TextureAssetId).IsNull);
        Assert.True(default(SoundId).IsNull);
        Assert.True(default(VisualEffectRecipeId).IsNull);
        Assert.False(new StringId(1).IsNull);
    }

    private static void ExerciseEstablishedCapacity(DynamicBuffer<int> buffer)
    {
        buffer.Clear();
        for (var value = 0; value < 32; value++)
        {
            buffer.Add(value);
        }

        buffer.RemoveAt(4);
        buffer.Insert(4, 4);
        buffer.Resize(16);
        buffer.Resize(32);
    }

    private static World CreateUniqueTagWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.RegisterTag<UniqueTag>(0);
        registry.Seal();
        return new World(registry);
    }

    private static EntityId CreateUniqueTaggedEntity(World world)
    {
        var bundle = new SpawnBundle(0, 0);
        bundle.AddTag<UniqueTag>();
        return world.Create(in bundle);
    }

    private readonly struct UniqueTag : ITag
    {
    }
}
