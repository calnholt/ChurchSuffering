#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Events;

public interface IEventConsumer<T>
    where T : unmanaged
{
    void Consume(in T value, ref EventDispatchContext context);
}

public readonly record struct EventConsumerRegistration<T>(
    int Priority,
    string Name,
    IEventConsumer<T> Consumer)
    where T : unmanaged;

public interface IEventRoute
{
    int EventTypeId { get; }

    string EventName { get; }

    int PendingCount { get; }

    void BeginWave();

    void RouteCurrent(ref EventDispatchContext context);

    void EndWave();

    void DiscardAll();
}

/// <summary>
/// A typed route constructed explicitly by generated routing code. Consumer priority is
/// descending; equal priorities preserve declaration order.
/// </summary>
public sealed class EventRoute<T> : IEventRoute
    where T : unmanaged
{
    private readonly EventConsumerRegistration<T>[] consumers;

    public EventRoute(
        int eventTypeId,
        string eventName,
        EventStream<T> stream,
        params EventConsumerRegistration<T>[] consumers)
    {
        if (eventTypeId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(eventTypeId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(eventName);
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(consumers);
        EventTypeId = eventTypeId;
        EventName = eventName;
        Stream = stream;
        this.consumers = (EventConsumerRegistration<T>[])consumers.Clone();
        for (var index = 0; index < this.consumers.Length; index++)
        {
            EventConsumerRegistration<T> registration = this.consumers[index];
            ArgumentException.ThrowIfNullOrWhiteSpace(registration.Name);
            ArgumentNullException.ThrowIfNull(registration.Consumer);
        }

        // Stable insertion sort is initialization-only and avoids delegate discovery.
        for (var index = 1; index < this.consumers.Length; index++)
        {
            EventConsumerRegistration<T> value = this.consumers[index];
            var destination = index;
            while (destination > 0 && this.consumers[destination - 1].Priority < value.Priority)
            {
                this.consumers[destination] = this.consumers[destination - 1];
                destination--;
            }

            this.consumers[destination] = value;
        }
    }

    public int EventTypeId { get; }

    public string EventName { get; }

    public EventStream<T> Stream { get; }

    public int PendingCount => Stream.PendingCount;

    public void BeginWave() => Stream.BeginWave();

    public void RouteCurrent(ref EventDispatchContext context)
    {
        ReadOnlySpan<T> events = Stream.Current;
        for (var eventIndex = 0; eventIndex < events.Length; eventIndex++)
        {
            context.BeginEvent(EventName);
            ref readonly T value = ref events[eventIndex];
            for (var consumerIndex = 0; consumerIndex < consumers.Length; consumerIndex++)
            {
                EventConsumerRegistration<T> consumer = consumers[consumerIndex];
                context.RecordConsumer(consumer.Name);
                consumer.Consumer.Consume(in value, ref context);
            }
        }
    }

    public void EndWave() => Stream.EndWave();

    public void DiscardAll() => Stream.DiscardAll();
}

/// <summary>
/// Explicit generated-code endpoint. Routes are supplied in stable generated type order;
/// no assembly scanning, reflection, or runtime delegate discovery occurs.
/// </summary>
public sealed class EventRoutingEndpoint
{
    private readonly IEventRoute[] routes;

    public EventRoutingEndpoint(params IEventRoute[] routes)
    {
        ArgumentNullException.ThrowIfNull(routes);
        this.routes = (IEventRoute[])routes.Clone();
        for (var index = 0; index < this.routes.Length; index++)
        {
            ArgumentNullException.ThrowIfNull(this.routes[index]);
            for (var previous = 0; previous < index; previous++)
            {
                if (this.routes[previous].EventTypeId == this.routes[index].EventTypeId)
                {
                    throw new InvalidOperationException(
                        $"Generated event type ID {this.routes[index].EventTypeId} is routed more than once.");
                }
            }
        }
    }

    public int RouteCount => routes.Length;

    public int PendingEventCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < routes.Length; index++)
            {
                count += routes[index].PendingCount;
            }

            return count;
        }
    }

    internal void BeginWave()
    {
        for (var index = 0; index < routes.Length; index++)
        {
            routes[index].BeginWave();
        }
    }

    internal void RouteWave(ref EventDispatchContext context)
    {
        for (var index = 0; index < routes.Length; index++)
        {
            routes[index].RouteCurrent(ref context);
        }
    }

    internal void EndWave()
    {
        for (var index = 0; index < routes.Length; index++)
        {
            routes[index].EndWave();
        }
    }

    internal void DiscardAll()
    {
        for (var index = 0; index < routes.Length; index++)
        {
            routes[index].DiscardAll();
        }
    }
}
