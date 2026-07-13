#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Events;

public sealed class EventRuntimeTests
{
    [Fact]
    public void Scheduler_attaches_event_runtime_as_world_owned_instance()
    {
        World world = CreateWorld();
        var runtime = new EventRuntime(new EventRoutingEndpoint());

        _ = new SystemScheduler(world, runtime, profilingEnabled: false);

        Assert.True(world.HasEventRuntime);
        Assert.True(runtime.IsAttachedToWorld);
        Assert.Same(runtime, world.Events);
        Assert.Same(world, runtime.World);
    }

    [Fact]
    public void Scheduler_rejects_different_event_runtime_for_same_world()
    {
        World world = CreateWorld();
        var first = new EventRuntime(new EventRoutingEndpoint());
        var second = new EventRuntime(new EventRoutingEndpoint());
        _ = new SystemScheduler(world, first, profilingEnabled: false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new SystemScheduler(world, second, profilingEnabled: false));

        Assert.Contains("different event runtime", exception.Message);
        Assert.Same(first, world.Events);
    }

    [Fact]
    public void Scheduler_rejects_sharing_event_runtime_between_worlds()
    {
        World firstWorld = CreateWorld();
        World secondWorld = CreateWorld();
        var runtime = new EventRuntime(new EventRoutingEndpoint());
        _ = new SystemScheduler(firstWorld, runtime, profilingEnabled: false);

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new SystemScheduler(secondWorld, runtime, profilingEnabled: false));

        Assert.Contains("different world", exception.Message);
        Assert.False(secondWorld.HasEventRuntime);
        Assert.Same(firstWorld, runtime.World);
    }

    [Fact]
    public void Routes_consumers_by_priority_and_nested_publication_in_later_wave()
    {
        var trace = new List<string>();
        var ping = new EventStream<Ping>();
        var pong = new EventStream<Pong>();
        var pingRoute = new EventRoute<Ping>(
            1,
            "Ping",
            ping,
            new EventConsumerRegistration<Ping>(
                0,
                "low-system",
                new PingConsumer("low", trace)),
            new EventConsumerRegistration<Ping>(
                100,
                "high-system",
                new PingConsumer("high", trace, pong)));
        var pongRoute = new EventRoute<Pong>(
            2,
            "Pong",
            pong,
            new EventConsumerRegistration<Pong>(0, "pong-system", new PongConsumer(trace)));
        var runtime = new EventRuntime(new EventRoutingEndpoint(pingRoute, pongRoute));
        ping.Publish(new Ping(7));

        runtime.DrainBarrier();

        Assert.Equal(["high:7:wave1", "low:7:wave1", "pong:8:wave2"], trace);
        Assert.Equal(2, runtime.LastBarrierEventCount);
        Assert.Equal(2, runtime.LastBarrierWaveCount);
        Assert.Equal(0, runtime.PendingEventCount);
    }

    [Fact]
    public void Equal_priority_consumers_preserve_declaration_order()
    {
        var trace = new List<string>();
        var stream = new EventStream<Ping>();
        var route = new EventRoute<Ping>(
            1,
            "Ping",
            stream,
            new EventConsumerRegistration<Ping>(5, "first", new PingConsumer("first", trace)),
            new EventConsumerRegistration<Ping>(5, "second", new PingConsumer("second", trace)));
        var runtime = new EventRuntime(new EventRoutingEndpoint(route));
        stream.Publish(new Ping(1));

        runtime.DrainBarrier();

        Assert.Equal(["first:1:wave1", "second:1:wave1"], trace);
    }

    [Fact]
    public void Cascade_guard_reports_recent_event_and_system_chain_and_discards_pending_events()
    {
        var stream = new EventStream<Ping>();
        var consumer = new RepublishConsumer(stream);
        var route = new EventRoute<Ping>(
            1,
            "Ping",
            stream,
            new EventConsumerRegistration<Ping>(0, "republisher-system", consumer));
        var runtime = new EventRuntime(
            new EventRoutingEndpoint(route),
            maximumEventsPerBarrier: 5);
        stream.Publish(new Ping(1));

        EventCascadeException exception = Assert.Throws<EventCascadeException>(runtime.DrainBarrier);

        Assert.Equal(6, exception.EventCount);
        Assert.Equal(5, exception.MaximumEvents);
        Assert.Contains("Ping", exception.Message);
        Assert.Contains("republisher-system", exception.Message);
        Assert.Equal(0, runtime.PendingEventCount);
    }

    [Fact]
    public void Warmed_stream_and_routing_allocate_zero_bytes_at_established_capacity()
    {
        var stream = new EventStream<Ping>(initialCapacity: 4);
        var consumer = new CountOnlyConsumer();
        var route = new EventRoute<Ping>(
            1,
            "Ping",
            stream,
            new EventConsumerRegistration<Ping>(0, "counter", consumer));
        var runtime = new EventRuntime(new EventRoutingEndpoint(route));
        stream.Publish(new Ping(0));
        runtime.DrainBarrier();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 100; index++)
        {
            stream.Publish(new Ping(index));
            runtime.DrainBarrier();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(101, consumer.Count);
        Assert.Equal(0, allocated);
    }

    private readonly record struct Ping(int Value);
    private readonly record struct Pong(int Value);

    private sealed class PingConsumer : IEventConsumer<Ping>
    {
        private readonly string name;
        private readonly List<string> trace;
        private readonly EventStream<Pong>? pong;

        public PingConsumer(string name, List<string> trace, EventStream<Pong>? pong = null)
        {
            this.name = name;
            this.trace = trace;
            this.pong = pong;
        }

        public void Consume(in Ping value, ref EventDispatchContext context)
        {
            trace.Add($"{name}:{value.Value}:wave{context.Wave}");
            if (pong is not null)
            {
                pong.Publish(new Pong(value.Value + 1));
            }
        }
    }

    private sealed class PongConsumer : IEventConsumer<Pong>
    {
        private readonly List<string> trace;
        public PongConsumer(List<string> trace) => this.trace = trace;
        public void Consume(in Pong value, ref EventDispatchContext context) =>
            trace.Add($"pong:{value.Value}:wave{context.Wave}");
    }

    private sealed class RepublishConsumer : IEventConsumer<Ping>
    {
        private readonly EventStream<Ping> stream;
        public RepublishConsumer(EventStream<Ping> stream) => this.stream = stream;
        public void Consume(in Ping value, ref EventDispatchContext context) =>
            stream.Publish(new Ping(value.Value + 1));
    }

    private sealed class CountOnlyConsumer : IEventConsumer<Ping>
    {
        public int Count { get; private set; }
        public void Consume(in Ping value, ref EventDispatchContext context) => Count++;
    }

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.Seal();
        return new World(registry);
    }
}
