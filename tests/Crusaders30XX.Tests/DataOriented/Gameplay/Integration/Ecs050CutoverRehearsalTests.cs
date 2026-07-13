#nullable enable

using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Integration;

/// <summary>
/// Positive cutover gates for the coordinator-owned data-oriented host seam.
/// </summary>
public sealed class Ecs050CutoverRehearsalTests
{
    [Fact]
    public void Game1_hosts_only_the_data_oriented_root_and_external_packet_adapters()
    {
        string root = FindRepositoryRoot();
        string game = File.ReadAllText(Path.Combine(root, "Game1.cs"));

        Assert.Contains("DataOrientedGameRuntime", game, StringComparison.Ordinal);
        Assert.Contains("RenderPacketHostAdapter", game, StringComparison.Ordinal);
        Assert.Contains("CentralInputFrameAdapter", game, StringComparison.Ordinal);
        Assert.DoesNotContain("Crusaders30XX.ECS.Core", game, StringComparison.Ordinal);
        Assert.DoesNotContain("EventManager", game, StringComparison.Ordinal);
        Assert.DoesNotContain("GetComponent<", game, StringComparison.Ordinal);
        Assert.DoesNotContain("GetEntitiesWithComponent<", game, StringComparison.Ordinal);
        Assert.DoesNotContain("new World()", game, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_routes_compose_into_one_root_owned_event_runtime_without_attaching()
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var cardHub = new CardGameplayEventHub();
        CardGameplayComposition cards = CardGameplayComposition.Create(world, cardHub);
        var rootStream = new EventStream<RootProbeEvent>();
        var rootConsumer = new RootProbeConsumer();
        IEventRoute[] cardRoutes = cards.GetRoutes();
        var routes = new IEventRoute[cardRoutes.Length + 1];
        cardRoutes.CopyTo(routes, 0);
        routes[^1] = new EventRoute<RootProbeEvent>(
            99001,
            nameof(RootProbeEvent),
            rootStream,
            new EventConsumerRegistration<RootProbeEvent>(0, nameof(RootProbeConsumer), rootConsumer));
        var runtime = new EventRuntime(new EventRoutingEndpoint(routes));
        var scheduler = new SystemScheduler(world, runtime);

        Assert.False(cardHub.BuildRoutes()[0].PendingCount > 0);
        Assert.Same(runtime, world.Events);
        Assert.Equal(59, routes.Length);
        rootStream.Publish(new RootProbeEvent(7));
        runtime.DrainBarrier();
        Assert.Equal(7, rootConsumer.Total);
        Assert.Equal(0, scheduler.Count);
    }

    [Fact]
    public void Combat_hub_exposes_routes_without_an_independent_attach_api()
    {
        string root = FindRepositoryRoot();
        string worldEvents = File.ReadAllText(Path.Combine(root, "ECS", "DataOriented", "Events", "World.Events.cs"));
        Assert.Contains("event runtimes cannot be replaced or shared", worldEvents, StringComparison.Ordinal);

        string combatSource = File.ReadAllText(Path.Combine(
            root, "ECS", "DataOriented", "Gameplay", "Combat", "CombatEventHub.cs"));
        Assert.Contains("IEventRoute[] BuildRoutes", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("public EventRuntime Attach(", combatSource, StringComparison.Ordinal);
        Assert.DoesNotContain("world.AttachEventRuntime", combatSource, StringComparison.Ordinal);
    }

    [Fact]
    public void Card_route_fragment_covers_the_full_ledger_and_accepts_root_consumers()
    {
        string root = FindRepositoryRoot();
        int cardLedgerEvents = File.ReadAllLines(Path.Combine(
                root, "docs", "migration", "data-oriented-ecs", "events.csv"))
            .Skip(1)
            .Select(line => line.Split(','))
            .Count(columns => columns.Contains("ECS-041", StringComparer.Ordinal));

        var world = new World(GeneratedComponentRegistry.Create());
        var cardHub = new CardGameplayEventHub();
        var consumer = new TrackingConsumer();
        var rootConsumers = new CardGameplayRouteConsumers()
            .Add<TrackingEvent>(consumer, priority: -10, name: "meta-tracking");
        CardGameplayComposition cards = CardGameplayComposition.Create(world, cardHub, rootConsumers);
        int cardHubStreams = typeof(CardGameplayEventHub)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Count(property => IsEventStream(property.PropertyType));

        Assert.Equal(58, cardLedgerEvents);
        Assert.Equal(58, cardHubStreams);
        Assert.Equal(58, cards.Routes.Length);
        Assert.Equal(58, cards.Routes.ToArray().Select(route => route.EventTypeId).Distinct().Count());
        Assert.Equal(1, GetConsumerCount(cards.Routes.ToArray().Single(route => route.EventTypeId == CardGameplayEventTypeIds.Tracking)));

        var runtime = new EventRuntime(new EventRoutingEndpoint(cards.GetRoutes()));
        world.AttachEventRuntime(runtime);
        cardHub.Tracking.Publish(new TrackingEvent(2, 3, default));
        runtime.DrainBarrier();
        Assert.Equal(3, consumer.Total);
    }

    [Fact]
    public void Combat_routes_cover_all_streams_with_unique_ids_and_use_the_root_runtime()
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var hub = new CombatEventHub();
        var owned = new CombatOwnedEventConsumers(world);
        IEventRoute[] combatRoutes = hub.BuildRoutes(owned.RegisterRoutes());
        var probeStream = new EventStream<RootProbeEvent>();
        var probe = new RootProbeConsumer();
        IEventRoute[] rootRoutes =
        [
            .. combatRoutes,
            new EventRoute<RootProbeEvent>(99001, nameof(RootProbeEvent), probeStream,
                new EventConsumerRegistration<RootProbeEvent>(0, nameof(RootProbeConsumer), probe)),
        ];
        var runtime = new EventRuntime(new EventRoutingEndpoint(rootRoutes));
        world.AttachEventRuntime(runtime);
        CombatSession session = CombatSession.Create(world, hub, EnemyId.TrainingDemon);
        owned.Bind(session);

        Assert.Equal(CombatEventTypeIds.Count, combatRoutes.Length);
        Assert.Equal(combatRoutes.Length, combatRoutes.Select(route => route.EventTypeId).Distinct().Count());
        Assert.Equal(13, combatRoutes.Sum(GetConsumerCount));
        Assert.All(combatRoutes.SelectMany(GetConsumerPriorities), priority =>
            Assert.Equal(CombatOwnedEventConsumers.Priority, priority));
        Assert.Same(runtime, world.Events);
        Assert.Same(hub, session.EventHub);

        hub.SetThreat.Publish(new SetThreatEvent(session.Player, 7));
        probeStream.Publish(new RootProbeEvent(5));
        runtime.DrainBarrier();
        Assert.Equal(7, world.Get<Threat>(session.Player).Amount);
        Assert.Equal(5, probe.Total);
    }

    [Fact]
    public void Card_composition_only_schedules_non_noop_systems_with_complete_access_metadata()
    {
        var world = new World(GeneratedComponentRegistry.Create());
        CardGameplayComposition cards = CardGameplayComposition.Create(world, new CardGameplayEventHub());
        IGameSystem[] cardSystems = cards.Systems.ToArray();

        Assert.Equal(2, cardSystems.Length);
        Assert.Contains(cardSystems, system => system is DeckManagementSystem);
        Assert.Contains(cardSystems, system => system is BattlePileInputSystem);
        Assert.Equal(26, cards.CompatibilitySystems.Length);
        Assert.All(cardSystems, system => Assert.False(HasEmptyAccessContract(system.Descriptor), system.Descriptor.Name));
        Assert.All(cardSystems, system => Assert.NotEqual(
            typeof(CardGameplaySystem),
            system.GetType().GetMethod(nameof(IGameSystem.Update))?.DeclaringType));
        Assert.True(cardSystems.Single(system => system is DeckManagementSystem)
            .Descriptor.RecordsStructuralCommands);
        Assert.False(cardSystems.Single(system => system is BattlePileInputSystem)
            .Descriptor.RecordsStructuralCommands);
    }

    [Fact]
    public void Combat_composition_only_schedules_operational_systems_with_complete_access_metadata()
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var hub = new CombatEventHub();
        var owned = new CombatOwnedEventConsumers(world);
        var runtime = new EventRuntime(new EventRoutingEndpoint(hub.BuildRoutes(owned.RegisterRoutes())));
        world.AttachEventRuntime(runtime);
        CombatSession session = CombatSession.Create(world, hub, EnemyId.TrainingDemon);
        owned.Bind(session);
        IGameSystem[] operational = CombatGameplayComposition.Create(session).Systems.ToArray();
        Type[] combatSystemTypes = typeof(CombatSystemBase).Assembly.GetTypes()
            .Where(type => !type.IsAbstract && typeof(CombatSystemBase).IsAssignableFrom(type))
            .ToArray();

        Assert.Equal(2, operational.Length);
        Assert.Equal(
            operational.Select(system => system.GetType()).OrderBy(type => type.Name),
            combatSystemTypes.OrderBy(type => type.Name));
        Assert.All(operational, system => Assert.False(HasEmptyAccessContract(system.Descriptor), system.Descriptor.Name));
        Assert.All(operational, system => Assert.Equal(
            system.GetType(), system.GetType().GetMethod(nameof(IGameSystem.Update))?.DeclaringType));
        Assert.DoesNotContain(combatSystemTypes, type => type.GetConstructor(Type.EmptyTypes) is not null);
    }

    private static bool HasEmptyAccessContract(SystemDescriptor descriptor) =>
        descriptor.ReadComponents.IsEmpty &&
        descriptor.WriteComponents.IsEmpty &&
        descriptor.ReadDynamicBufferTypes.IsEmpty &&
        descriptor.WriteDynamicBufferTypes.IsEmpty &&
        descriptor.ConsumedEventTypeIds.IsEmpty &&
        descriptor.EmittedEventTypeIds.IsEmpty;

    private static bool IsEventStream(Type type) =>
        type.IsGenericType && type.GetGenericTypeDefinition() == typeof(EventStream<>);

    private static IEventRoute[] GetRoutes(EventRuntime runtime)
    {
        FieldInfo endpointField = typeof(EventRuntime).GetField("endpoint", BindingFlags.NonPublic | BindingFlags.Instance)!;
        var endpoint = (EventRoutingEndpoint)endpointField.GetValue(runtime)!;
        FieldInfo routesField = typeof(EventRoutingEndpoint).GetField("routes", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return (IEventRoute[])routesField.GetValue(endpoint)!;
    }

    private static int GetConsumerCount(IEventRoute route)
    {
        FieldInfo consumers = route.GetType().GetField("consumers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        return ((Array)consumers.GetValue(route)!).Length;
    }

    private static int[] GetConsumerPriorities(IEventRoute route)
    {
        FieldInfo consumers = route.GetType().GetField("consumers", BindingFlags.NonPublic | BindingFlags.Instance)!;
        Array values = (Array)consumers.GetValue(route)!;
        var result = new int[values.Length];
        for (var index = 0; index < values.Length; index++)
            result[index] = (int)values.GetValue(index)!.GetType().GetProperty("Priority")!.GetValue(values.GetValue(index))!;
        return result;
    }

    private readonly record struct RootProbeEvent(int Value);

    private sealed class RootProbeConsumer : IEventConsumer<RootProbeEvent>
    {
        public int Total { get; private set; }
        public void Consume(in RootProbeEvent value, ref EventDispatchContext context) => Total += value.Value;
    }

    private sealed class TrackingConsumer : IEventConsumer<TrackingEvent>
    {
        public int Total { get; private set; }
        public void Consume(in TrackingEvent value, ref EventDispatchContext context) => Total += value.Delta;
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Crusaders30XX.csproj")))
                return directory.FullName;
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
