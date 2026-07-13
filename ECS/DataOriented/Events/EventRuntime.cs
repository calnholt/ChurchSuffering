#nullable enable

using System;
using System.Text;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Events;

public sealed class EventRuntime
{
    private readonly EventRoutingEndpoint endpoint;
    private readonly EventCascadeGuard guard;
    private World? world;

    public EventRuntime(EventRoutingEndpoint endpoint, int maximumEventsPerBarrier = 100_000)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (maximumEventsPerBarrier <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumEventsPerBarrier));
        }

        this.endpoint = endpoint;
        guard = new EventCascadeGuard(maximumEventsPerBarrier);
    }

    public int PendingEventCount => endpoint.PendingEventCount;

    public int RouteCount => endpoint.RouteCount;

    public bool IsAttachedToWorld => world is not null;

    public World World => world ??
        throw new InvalidOperationException("This event runtime is not attached to a world.");

    public int LastBarrierEventCount { get; private set; }

    public int LastBarrierWaveCount { get; private set; }

    internal void AttachToWorld(World owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (world is not null && !ReferenceEquals(world, owner))
        {
            throw new InvalidOperationException(
                "This event runtime is already owned by a different world and cannot be shared.");
        }

        world = owner;
    }

    public void DrainBarrier()
    {
        guard.Reset();
        var waveCount = 0;
        try
        {
            while (endpoint.PendingEventCount > 0)
            {
                waveCount++;
                endpoint.BeginWave();
                try
                {
                    var context = new EventDispatchContext(guard, waveCount);
                    endpoint.RouteWave(ref context);
                }
                finally
                {
                    endpoint.EndWave();
                }
            }

            LastBarrierEventCount = guard.EventCount;
            LastBarrierWaveCount = waveCount;
        }
        catch
        {
            endpoint.DiscardAll();
            LastBarrierEventCount = guard.EventCount;
            LastBarrierWaveCount = waveCount;
            throw;
        }
    }
}

public readonly ref struct EventDispatchContext
{
    private readonly EventCascadeGuard guard;

    internal EventDispatchContext(EventCascadeGuard guard, int wave)
    {
        this.guard = guard;
        Wave = wave;
    }

    public int Wave { get; }

    internal void BeginEvent(string eventName) => guard.BeginEvent(eventName, Wave);

    internal void RecordConsumer(string consumerName) => guard.RecordConsumer(consumerName);
}

public sealed class EventCascadeException : InvalidOperationException
{
    internal EventCascadeException(int eventCount, int maximumEvents, string message)
        : base(message)
    {
        EventCount = eventCount;
        MaximumEvents = maximumEvents;
    }

    public int EventCount { get; }

    public int MaximumEvents { get; }
}

internal sealed class EventCascadeGuard
{
    private const int RecentChainCapacity = 32;

    private readonly int maximumEvents;
    private readonly string?[] recentEvents = new string?[RecentChainCapacity];
    private readonly string?[] recentConsumers = new string?[RecentChainCapacity];
    private readonly int[] recentWaves = new int[RecentChainCapacity];
    private int recentStart;
    private int recentCount;
    private int currentSlot = -1;

    public EventCascadeGuard(int maximumEvents)
    {
        this.maximumEvents = maximumEvents;
    }

    public int EventCount { get; private set; }

    public void Reset()
    {
        EventCount = 0;
        recentStart = 0;
        recentCount = 0;
        currentSlot = -1;
        Array.Clear(recentEvents);
        Array.Clear(recentConsumers);
        Array.Clear(recentWaves);
    }

    public void BeginEvent(string eventName, int wave)
    {
        EventCount++;
        int slot;
        if (recentCount < RecentChainCapacity)
        {
            slot = (recentStart + recentCount) % RecentChainCapacity;
            recentCount++;
        }
        else
        {
            slot = recentStart;
            recentStart = (recentStart + 1) % RecentChainCapacity;
        }

        currentSlot = slot;
        recentEvents[slot] = eventName;
        recentConsumers[slot] = null;
        recentWaves[slot] = wave;
        if (EventCount > maximumEvents)
        {
            throw BuildException();
        }
    }

    public void RecordConsumer(string consumerName)
    {
        if (currentSlot >= 0)
        {
            recentConsumers[currentSlot] = consumerName;
        }
    }

    private EventCascadeException BuildException()
    {
        var message = new StringBuilder()
            .Append("Event cascade exceeded the configured guard of ")
            .Append(maximumEvents)
            .Append(" events at event ")
            .Append(EventCount)
            .AppendLine(". Recent event/system chain:");
        for (var index = 0; index < recentCount; index++)
        {
            int slot = (recentStart + index) % RecentChainCapacity;
            message.Append("  wave ")
                .Append(recentWaves[slot])
                .Append(": ")
                .Append(recentEvents[slot]);
            if (recentConsumers[slot] is { } consumer)
            {
                message.Append(" -> ").Append(consumer);
            }
            message.AppendLine();
        }

        return new EventCascadeException(EventCount, maximumEvents, message.ToString());
    }
}
