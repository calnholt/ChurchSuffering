#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Systems;

public sealed class SystemSchedulerTests
{
    [Fact]
    public void Executes_frozen_phases_and_explicit_dependencies_in_deterministic_order()
    {
        World world = CreateWorld();
        var trace = new List<string>();
        var scheduler = CreateScheduler(world);
        scheduler.Register(System(3, "gameplay", SystemPhase.Gameplay, SceneGroup.Global, trace));
        scheduler.Register(System(
            2,
            "input-second",
            SystemPhase.Input,
            SceneGroup.Global,
            trace,
            runsAfter: [new SystemId(1)]));
        scheduler.Register(System(1, "input-first", SystemPhase.Input, SceneGroup.Global, trace));

        scheduler.Update(TimeSpan.FromSeconds(1d / 120d));

        Assert.Equal(["input-first", "input-second", "gameplay"], trace);
        Assert.Equal(1, scheduler.FrameIndex);
    }

    [Fact]
    public void Cycle_diagnostic_names_complete_dependency_path()
    {
        var scheduler = CreateScheduler(CreateWorld());
        scheduler.Register(System(1, "alpha", runsAfter: [new SystemId(3)]));
        scheduler.Register(System(2, "beta", runsAfter: [new SystemId(1)]));
        scheduler.Register(System(3, "gamma", runsAfter: [new SystemId(2)]));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(scheduler.Build);

        Assert.Contains("alpha -> beta -> gamma -> alpha", exception.Message);
    }

    [Fact]
    public void Runs_only_global_and_active_scene_cached_arrays()
    {
        var trace = new List<string>();
        var scheduler = CreateScheduler(CreateWorld());
        scheduler.Register(System(1, "global", scene: SceneGroup.Global, trace: trace));
        scheduler.Register(System(2, "battle", scene: SceneGroup.Battle, trace: trace));
        scheduler.Register(System(3, "climb", scene: SceneGroup.Climb, trace: trace));

        scheduler.ActiveScene = SceneGroup.Battle;
        scheduler.Update(TimeSpan.Zero);
        scheduler.ActiveScene = SceneGroup.Climb;
        scheduler.Update(TimeSpan.Zero);

        Assert.Equal(["global", "battle", "global", "climb"], trace);
        Assert.Equal(["global", "battle"], scheduler.GetExecutionOrder(SceneGroup.Battle, SystemPhase.Gameplay));
    }

    [Fact]
    public void Rejects_unordered_conflicting_component_access()
    {
        World world = CreateWorld();
        ComponentSignature type = ComponentSignature.Empty.With(ComponentType<ValueComponent>.Id);
        var scheduler = CreateScheduler(world);
        scheduler.Register(new CallbackSystem(
            Descriptor(1, "writer", writeComponents: type),
            static _ => { }));
        scheduler.Register(new CallbackSystem(
            Descriptor(2, "reader", readComponents: type),
            static _ => { }));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(scheduler.Build);

        Assert.Contains("conflicting declared access", exception.Message);
        Assert.Contains("writer", exception.Message);
        Assert.Contains("reader", exception.Message);
    }

    [Fact]
    public void Exclusive_world_access_requires_explicit_same_phase_ordering()
    {
        var unordered = CreateScheduler(CreateWorld());
        unordered.Register(new CallbackSystem(
            Descriptor(1, "ordinary"),
            static _ => { }));
        unordered.Register(new CallbackSystem(
            Descriptor(2, "exclusive", requiresExclusiveWorldAccess: true),
            static _ => { }));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(unordered.Build);
        Assert.Contains("conflicting declared access", exception.Message);

        var ordered = CreateScheduler(CreateWorld());
        ordered.Register(new CallbackSystem(Descriptor(1, "ordinary"), static _ => { }));
        ordered.Register(new CallbackSystem(
            Descriptor(2, "exclusive", runsAfter: [new SystemId(1)], requiresExclusiveWorldAccess: true),
            static _ => { }));
        ordered.Build();
        Assert.Equal(["ordinary", "exclusive"], ordered.GetExecutionOrder(SceneGroup.Battle, SystemPhase.Gameplay));
    }

    [Fact]
    public void Plays_commands_after_each_system_before_the_next_system()
    {
        World world = CreateWorld();
        var observedEntityCount = -1;
        var scheduler = CreateScheduler(world);
        scheduler.Register(new CallbackSystem(
            Descriptor(
                1,
                "creator",
                runsBefore: [new SystemId(2)],
                recordsStructuralCommands: true),
            context =>
            {
                var bundle = new SpawnBundle(1);
                bundle.Add(new ValueComponent { Value = 7 });
                context.Commands.Create(in bundle);
            }));
        scheduler.Register(new CallbackSystem(
            Descriptor(2, "observer"),
            context => observedEntityCount = context.World.EntityCount));

        scheduler.Update(TimeSpan.Zero);

        Assert.Equal(1, observedEntityCount);
        Assert.Equal(1, world.EntityCount);
    }

    [Fact]
    public void Rejects_commands_when_system_metadata_declares_no_structural_writes()
    {
        World world = CreateWorld();
        var scheduler = CreateScheduler(world);
        scheduler.Register(new CallbackSystem(
            Descriptor(1, "undeclared-creator", recordsStructuralCommands: false),
            context =>
            {
                var bundle = new SpawnBundle(1);
                bundle.Add(new ValueComponent { Value = 7 });
                context.Commands.Create(in bundle);
            }));

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => scheduler.Update(TimeSpan.Zero));

        Assert.Contains("undeclared-creator", exception.Message);
        Assert.Contains("RecordsStructuralCommands=false", exception.Message);
        Assert.Equal(0, world.EntityCount);
    }

    [Fact]
    public void After_system_event_barrier_drains_before_next_system()
    {
        var stream = new EventStream<TestEvent>();
        var consumer = new CountingEventConsumer();
        var route = new EventRoute<TestEvent>(
            1,
            "TestEvent",
            stream,
            new EventConsumerRegistration<TestEvent>(0, "counter", consumer));
        var events = new EventRuntime(new EventRoutingEndpoint(route));
        var scheduler = new SystemScheduler(CreateWorld(), events, profilingEnabled: false);
        var observed = -1;
        scheduler.Register(new CallbackSystem(
            Descriptor(1, "publisher", runsBefore: [new SystemId(2)], eventBarrier: EventBarrier.AfterSystem),
            _ => stream.Publish(new TestEvent(1))));
        scheduler.Register(new CallbackSystem(
            Descriptor(2, "observer"),
            _ => observed = consumer.Count));

        scheduler.Update(TimeSpan.Zero);

        Assert.Equal(1, observed);
    }

    [Fact]
    public void Profiles_elapsed_ticks_and_allocated_bytes_per_system()
    {
        var scheduler = new SystemScheduler(CreateWorld(), EmptyEvents(), profilingEnabled: true);
        scheduler.Register(new CallbackSystem(
            Descriptor(1, "allocator"),
            static _ => GC.KeepAlive(new byte[256])));

        scheduler.Update(TimeSpan.Zero);

        Assert.True(scheduler.TryGetProfile(new SystemId(1), out SystemProfileSnapshot profile));
        Assert.Equal(1, profile.InvocationCount);
        Assert.True(profile.LastElapsedTicks >= 0);
        Assert.True(profile.LastAllocatedBytes >= 256);
    }

    [Fact]
    public void Warmed_scheduler_with_profiling_disabled_allocates_zero_bytes()
    {
        var scheduler = new SystemScheduler(CreateWorld(), EmptyEvents(), profilingEnabled: false);
        scheduler.Register(new CallbackSystem(Descriptor(1, "noop"), static _ => { }));
        scheduler.Update(TimeSpan.Zero);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            scheduler.Update(TimeSpan.Zero);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Warmed_scheduler_with_profiling_enabled_allocates_zero_bytes()
    {
        var scheduler = new SystemScheduler(CreateWorld(), EmptyEvents(), profilingEnabled: true);
        scheduler.Register(new CallbackSystem(Descriptor(1, "profiled-noop"), static _ => { }));
        scheduler.Update(TimeSpan.Zero);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            scheduler.Update(TimeSpan.Zero);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
        Assert.True(scheduler.TryGetProfile(new SystemId(1), out SystemProfileSnapshot profile));
        Assert.Equal(101, profile.InvocationCount);
        Assert.Equal(0, profile.LastAllocatedBytes);
    }

    private static SystemScheduler CreateScheduler(World world) =>
        new(world, EmptyEvents(), profilingEnabled: false);

    private static EventRuntime EmptyEvents() => new(new EventRoutingEndpoint());

    private static CallbackSystem System(
        int id,
        string name,
        SystemPhase phase = SystemPhase.Gameplay,
        SceneGroup scene = SceneGroup.Global,
        List<string>? trace = null,
        SystemId[]? runsAfter = null) => new(
            Descriptor(id, name, phase, scene, runsAfter: runsAfter),
            _ => trace?.Add(name));

    private static SystemDescriptor Descriptor(
        int id,
        string name,
        SystemPhase phase = SystemPhase.Gameplay,
        SceneGroup scene = SceneGroup.Global,
        ComponentSignature readComponents = default,
        ComponentSignature writeComponents = default,
        SystemId[]? runsBefore = null,
        SystemId[]? runsAfter = null,
        bool recordsStructuralCommands = false,
        EventBarrier eventBarrier = EventBarrier.None,
        bool requiresExclusiveWorldAccess = false) => new(
            new SystemId(id),
            name,
            phase,
            scene,
            readComponents,
            writeComponents,
            runsBefore: runsBefore,
            runsAfter: runsAfter,
            recordsStructuralCommands: recordsStructuralCommands,
            eventBarrier: eventBarrier,
            requiresExclusiveWorldAccess: requiresExclusiveWorldAccess);

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.RegisterComponent<ValueComponent>(300);
        registry.Seal();
        return new World(registry);
    }

    private sealed class CallbackSystem : IGameSystem
    {
        private readonly Action<SystemContext> callback;

        public CallbackSystem(SystemDescriptor descriptor, Action<SystemContext> callback)
        {
            Descriptor = descriptor;
            this.callback = callback;
        }

        public SystemDescriptor Descriptor { get; }

        public void Update(ref SystemContext context) => callback(context);
    }

    private sealed class CountingEventConsumer : IEventConsumer<TestEvent>
    {
        public int Count { get; private set; }
        public void Consume(in TestEvent value, ref EventDispatchContext context) => Count++;
    }

    private readonly record struct TestEvent(int Value);
    private struct ValueComponent : IComponent { public int Value; }
}
