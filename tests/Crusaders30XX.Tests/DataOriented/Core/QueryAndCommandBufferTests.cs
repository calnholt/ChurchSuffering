#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Core;

public sealed class QueryAndCommandBufferTests
{
    [Fact]
    public void Query_filters_all_any_none_and_appends_new_matching_archetypes()
    {
        var world = CreateWorld();
        var filter = new QueryFilter(
            ComponentSignature.Empty.With(ComponentType<A>.Id),
            ComponentSignature.Empty.With(ComponentType<B>.Id).With(ComponentType<C>.Id),
            ComponentSignature.Empty.With(ComponentType<Excluded>.Id));
        var query = world.Query<A>(filter);

        Create(world, new A { Value = 1 }, new B { Value = 10 });
        Create(world, new A { Value = 2 }, new C { Value = 20 });
        Create(world, new A { Value = 3 });
        var excluded = Bundle(new A { Value = 4 });
        excluded.Add(new B());
        excluded.AddTag<Excluded>();
        world.Create(in excluded);

        var values = new List<int>();
        foreach (var chunk in query)
        {
            foreach (var row in chunk.Rows)
            {
                values.Add(chunk.Component1[row].Value);
            }
        }

        Assert.Equal([1, 2], values);
    }

    [Fact]
    public void Disabled_entities_are_excluded_by_default_and_can_be_included()
    {
        var world = CreateWorld();
        var enabled = Create(world, new A { Value = 1 });
        var disabled = Create(world, new A { Value = 2 });
        world.Disable(disabled);
        var ordinary = world.Query<A>();
        var includingDisabled = world.Query<A>(new QueryFilter(IncludeDisabled: true));

        Assert.Equal(1, CountRows(ordinary));
        Assert.Equal(2, CountRows(includingDisabled));
        Assert.True(world.IsEnabled(enabled));
    }

    [Fact]
    public void Query_spans_return_arities_one_through_eight_and_allow_component_updates()
    {
        var world = CreateWorld();
        var bundle = Bundle(new A { Value = 1 });
        bundle.Add(new B { Value = 2 });
        bundle.Add(new C { Value = 3 });
        bundle.Add(new D { Value = 4 });
        bundle.Add(new E { Value = 5 });
        bundle.Add(new F { Value = 6 });
        bundle.Add(new G { Value = 7 });
        bundle.Add(new H { Value = 8 });
        var entity = world.Create(in bundle);

        Assert.Equal(1, CountRows(world.Query<A>()));
        Assert.NotNull(world.Query<A, B>());
        Assert.NotNull(world.Query<A, B, C>());
        Assert.NotNull(world.Query<A, B, C, D>());
        Assert.NotNull(world.Query<A, B, C, D, E>());
        Assert.NotNull(world.Query<A, B, C, D, E, F>());
        Assert.NotNull(world.Query<A, B, C, D, E, F, G>());
        var query8 = world.Query<A, B, C, D, E, F, G, H>();
        foreach (var chunk in query8)
        {
            foreach (var row in chunk.Rows)
            {
                chunk.Component1[row].Value += chunk.Component8[row].Value;
            }
        }

        Assert.Equal(9, world.Get<A>(entity).Value);
    }

    [Fact]
    public void Structural_writes_throw_during_active_iteration_and_guard_is_released()
    {
        var world = CreateWorld();
        var entity = Create(world, new A());
        var query = world.Query<A>(new QueryFilter(DebugName: "guard-test"));

        var exception = Assert.Throws<InvalidOperationException>(() =>
        {
            foreach (var chunk in query)
            {
                foreach (var _ in chunk.Rows)
                {
                    world.Destroy(entity);
                }
            }
        });

        Assert.Contains("guard-test", exception.Message);
        world.Destroy(entity);
    }

    [Fact]
    public void Warmed_query_iteration_allocates_zero_bytes()
    {
        var world = CreateWorld();
        for (var index = 0; index < 128; index++)
        {
            Create(world, new A { Value = index });
        }

        var query = world.Query<A>();
        _ = Sum(query);
        var before = GC.GetAllocatedBytesForCurrentThread();
        var sum = 0;
        for (var iteration = 0; iteration < 100; iteration++)
        {
            sum += Sum(query);
        }
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.True(sum > 0);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Command_buffer_preserves_order_and_resolves_deferred_entities()
    {
        var world = CreateWorld();
        var commands = new CommandBuffer();
        var spawn = Bundle(new A { Value = 1 });
        var deferred = commands.Create(in spawn);
        commands.Set(deferred, new A { Value = 2 });
        commands.Add(deferred, new B { Value = 3 });
        commands.AddTag<Marked>(deferred);
        commands.Disable(deferred);
        commands.Enable(deferred);

        commands.Playback(world);
        var entity = commands.Resolve(deferred);

        Assert.Equal(2, world.Get<A>(entity).Value);
        Assert.Equal(3, world.Get<B>(entity).Value);
        Assert.True(world.Has<Marked>(entity));
        Assert.True(world.IsEnabled(entity));
        Assert.Equal(0, commands.Count);
    }

    [Fact]
    public void Command_buffer_snapshots_bundles_and_batch_transition_moves_once()
    {
        var world = CreateWorld();
        var entity = Create(world, new A { Value = 1 });
        var additions = Bundle(new B { Value = 2 });
        additions.Add(new C { Value = 3 });
        var commands = new CommandBuffer();
        commands.Transition(entity, in additions, ComponentSignature.Empty);
        additions.Clear();

        commands.Playback(world);

        Assert.Equal(1, world.StructuralMoveCount);
        Assert.Equal(2, world.Get<B>(entity).Value);
        Assert.Equal(3, world.Get<C>(entity).Value);
    }

    [Fact]
    public void Command_buffer_plays_remove_component_remove_tag_and_destroy()
    {
        var world = CreateWorld();
        var firstBundle = Bundle(new A());
        firstBundle.Add(new B());
        firstBundle.AddTag<Marked>();
        var first = world.Create(in firstBundle);
        var second = Create(world, new A());
        var commands = new CommandBuffer();
        commands.Remove<B>(first);
        commands.RemoveTag<Marked>(first);
        commands.Destroy(second);

        commands.Playback(world);

        Assert.False(world.Has<B>(first));
        Assert.False(world.Has<Marked>(first));
        Assert.False(world.IsAlive(second));
    }

    [Fact]
    public void Dynamic_buffer_mutation_endpoint_plays_in_record_order()
    {
        var world = CreateWorld();
        var entity = Create(world, new A { Value = 1 });
        var handler = new MutationHandler();
        var commands = new CommandBuffer();
        commands.Set(entity, new A { Value = 5 });
        commands.RecordDynamicBufferMutation(handler, new TestMutation { Entity = entity });
        commands.Set(entity, new A { Value = 9 });

        commands.Playback(world);

        Assert.Equal([5], handler.ObservedValues);
        Assert.Equal(9, world.Get<A>(entity).Value);
    }

    private static int Sum(Query<A> query)
    {
        var sum = 0;
        foreach (var chunk in query)
        {
            foreach (var row in chunk.Rows)
            {
                sum += chunk.Component1[row].Value;
            }
        }
        return sum;
    }

    private static int CountRows(Query<A> query)
    {
        var count = 0;
        foreach (var chunk in query)
        {
            foreach (var _ in chunk.Rows) count++;
        }
        return count;
    }

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.RegisterComponent<A>(10);
        registry.RegisterComponent<B>(11);
        registry.RegisterComponent<C>(12);
        registry.RegisterComponent<D>(13);
        registry.RegisterComponent<E>(14);
        registry.RegisterComponent<F>(15);
        registry.RegisterComponent<G>(16);
        registry.RegisterComponent<H>(17);
        registry.RegisterTag<Marked>(18);
        registry.RegisterTag<Excluded>(19);
        registry.Seal();
        return new World(registry);
    }

    private static EntityId Create<T>(World world, in T value) where T : unmanaged, IComponent
    {
        var bundle = Bundle(in value);
        return world.Create(in bundle);
    }

    private static EntityId Create<T1, T2>(World world, in T1 first, in T2 second)
        where T1 : unmanaged, IComponent where T2 : unmanaged, IComponent
    {
        var bundle = Bundle(in first);
        bundle.Add(in second);
        return world.Create(in bundle);
    }

    private static SpawnBundle Bundle<T>(in T value) where T : unmanaged, IComponent
    {
        var bundle = new SpawnBundle(8, 128);
        bundle.Add(in value);
        return bundle;
    }

    private struct A : IComponent { public int Value; }
    private struct B : IComponent { public int Value; }
    private struct C : IComponent { public int Value; }
    private struct D : IComponent { public int Value; }
    private struct E : IComponent { public int Value; }
    private struct F : IComponent { public int Value; }
    private struct G : IComponent { public int Value; }
    private struct H : IComponent { public int Value; }
    private struct Marked : ITag { }
    private struct Excluded : ITag { }
    private struct TestMutation { public EntityId Entity; }

    private sealed class MutationHandler : IDynamicBufferCommandHandler<TestMutation>
    {
        public List<int> ObservedValues { get; } = new();
        public void Playback(World world, in TestMutation command) =>
            ObservedValues.Add(world.Get<A>(command.Entity).Value);
    }
}
