#nullable enable

using System;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Cards;

public sealed class CardGameplayRuntimeTests
{
    [Fact]
    public void Ordered_zone_trace_preserves_duplicate_entity_identity()
    {
        World world = CreateWorld();
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, 7);
        EntityId firstStrike = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DrawPile);
        EntityId fervor = CardGameplayFactory.CreateCard(world, deck, CardId.Fervor, CardZone.DrawPile);
        EntityId secondStrike = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DrawPile);
        var events = new CardGameplayEventHub();
        var system = new DeckManagementSystem(world, events);

        Assert.Equal(2, system.Draw(deck, 2));
        Assert.Equal([firstStrike, fervor], Zone(world, deck, CardZone.Hand));
        Assert.Equal([secondStrike], Zone(world, deck, CardZone.DrawPile));
        Assert.Equal(firstStrike, CardZoneOperations.At(world, deck, CardZone.Hand, 0));
        Assert.Equal(secondStrike, CardZoneOperations.At(world, deck, CardZone.DrawPile, 0));

        Assert.True(CardZoneOperations.Move(world, deck, firstStrike, CardZone.Hand, CardZone.DiscardPile, -1, CardMoveReason.Payment));
        Assert.Equal([fervor], Zone(world, deck, CardZone.Hand));
        Assert.Equal([firstStrike], Zone(world, deck, CardZone.DiscardPile));
        Assert.Equal(3, CardZoneOperations.Count(world, deck, CardZone.MasterDeck));
    }

    [Fact]
    public void Shuffle_is_deterministic_for_equal_seed_and_initial_order()
    {
        World left = CreateWorld();
        World right = CreateWorld();
        EntityId leftDeck = Populate(left, 0xC30UL);
        EntityId rightDeck = Populate(right, 0xC30UL);

        CardZoneOperations.ShuffleDrawPile(left, leftDeck);
        CardZoneOperations.ShuffleDrawPile(right, rightDeck);

        Assert.Equal(
            Zone(left, leftDeck, CardZone.DrawPile).Select(entity => left.Get<CardData>(entity).Definition),
            Zone(right, rightDeck, CardZone.DrawPile).Select(entity => right.Get<CardData>(entity).Definition));
        Assert.Equal(left.Get<Deck>(leftDeck).Random, right.Get<Deck>(rightDeck).Random);
    }

    [Fact]
    public void Empty_draw_does_not_implicitly_reshuffle_discard()
    {
        World world = CreateWorld();
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, 9);
        EntityId discarded = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DiscardPile);
        var events = new CardGameplayEventHub();

        int drawn = new DeckManagementSystem(world, events).Draw(deck, 1);

        Assert.Equal(0, drawn);
        Assert.Equal([discarded], Zone(world, deck, CardZone.DiscardPile));
        Assert.Empty(Zone(world, deck, CardZone.DrawPile));
        Assert.Equal(1, events.DrawPileEmpty.PendingCount);
    }

    [Fact]
    public void Upgrade_colorless_and_pledge_rules_preserve_card_semantics()
    {
        World world = CreateWorld();
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, 10);
        EntityId baseFervor = CardGameplayFactory.CreateCard(world, deck, CardId.Fervor, CardZone.Hand);
        EntityId upgradedFervor = CardGameplayFactory.CreateCard(world, deck, CardId.Fervor, CardZone.Hand, upgraded: true);
        EntityId payment = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand);
        CardData baseData = world.Get<CardData>(baseFervor);
        CardData upgradedData = world.Get<CardData>(upgradedFervor);

        Assert.Equal(CardCostColor.Red, baseData.Cost0);
        Assert.Equal(CardCostColor.Any, upgradedData.Cost0);
        world.AddTag<Colorless>(payment);
        Assert.False(CardCostRules.IsColorQualified(world, payment, CardCostColor.Red));
        Assert.True(CardCostRules.IsColorQualified(world, payment, CardCostColor.Any));

        var state = new PledgeAvailabilityState { Enabled = 1 };
        Assert.Equal(PledgeFailure.None, PledgeRules.Evaluate(world, deck, baseFervor, in state));
        world.Add(baseFervor, new Pledge { CanPlay = 0 });
        Assert.Equal(PledgeFailure.ExistingPledge, PledgeRules.Evaluate(world, deck, upgradedFervor, in state));
    }

    [Fact]
    public void Cursed_conversion_restores_the_exact_instance_and_upgrade()
    {
        World world = CreateWorld();
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, 11);
        EntityId card = CardGameplayFactory.CreateCard(world, deck, CardId.Tempest, CardZone.Hand, upgraded: true);
        var cursed = new CursedManagementSystem(world);
        var commands = new CommandBuffer();

        Assert.True(cursed.Apply(card, commands));
        commands.Playback(world);
        Assert.True(world.Has<Cursed>(card));
        Assert.Equal(CardId.Curse, world.Get<CardData>(card).Definition);
        Assert.Equal(CardId.Tempest, world.Get<CursedOriginalCard>(card).Definition);

        Assert.True(cursed.Remove(card, commands));
        commands.Playback(world);
        Assert.False(world.Has<Cursed>(card));
        Assert.False(world.Has<CursedOriginalCard>(card));
        Assert.Equal(CardId.Tempest, world.Get<CardData>(card).Definition);
        Assert.True(world.Get<CardData>(card).IsUpgraded);
    }

    [Fact]
    public void Typed_spawn_command_uses_deferred_entity_then_finalizes_ordered_zone()
    {
        World world = CreateWorld();
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, 12);
        var structural = new CommandBuffer();
        var rules = new RuleCommandBuffer();
        rules.Writer.Append(RuleCommand.SpawnCard(
            TargetHandle.None, TargetHandle.None, deck, CardId.Kunai,
            CardZone.Hand, RuleCardColor.Red, isUpgraded: true, count: 2));
        RuleRandomState random = RuleRandomState.FromSeed(12);
        var events = new CardGameplayEventHub();

        Assert.Equal(1, CardRuleCommandExecutor.Execute(world, rules.AsReadOnlySpan(), structural, events, ref random));
        structural.Playback(world);
        var deckSystem = new DeckManagementSystem(world, events);
        deckSystem.FinalizePendingSpawns(structural);
        structural.Playback(world);

        EntityId[] hand = Zone(world, deck, CardZone.Hand);
        Assert.Equal(2, hand.Length);
        Assert.All(hand, card => Assert.Equal(CardId.Kunai, world.Get<CardData>(card).Definition));
        Assert.All(hand, card => Assert.True(world.Get<CardData>(card).IsUpgraded));
        Assert.NotEqual(hand[0], hand[1]);
    }

    [Fact]
    public void Composition_exposes_operational_allowlist_all_responsibilities_and_typed_deck_consumers()
    {
        World world = CreateWorld();
        EntityId player = CreatePlayer(world, actionPoints: 2);
        EntityId deck = CardGameplayFactory.CreateDeck(world, player, 13);
        EntityId card = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DrawPile);
        var events = new CardGameplayEventHub();
        CardGameplayComposition composition = CardGameplayComposition.Create(world, events);
        var runtime = new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes()));
        world.AttachEventRuntime(runtime);

        Assert.Equal(2, composition.Systems.Length);
        Assert.IsType<DeckManagementSystem>(composition.Systems[0]);
        Assert.IsType<BattlePileInputSystem>(composition.Systems[1]);
        Assert.Equal(26, composition.CompatibilitySystems.Length);
        Assert.Equal(26, composition.CompatibilitySystems.ToArray().Select(system => system.Descriptor.Id).Distinct().Count());
        Assert.Equal(58, composition.Routes.Length);
        events.DeckShuffleDraw.Publish(new DeckShuffleDrawEvent(deck, 1));
        events.ModifyActionPoints.Publish(new ModifyActionPointsEvent(player, -1));
        runtime.DrainBarrier();

        Assert.Equal([card], Zone(world, deck, CardZone.Hand));
        Assert.Equal(1, world.Get<Player>(player).ActionPoints);
    }

    [Fact]
    public void Route_fragment_has_every_stable_event_id_and_injected_consumers_run_after_local_consumers()
    {
        World world = CreateWorld();
        EntityId player = CreatePlayer(world, actionPoints: 3);
        var events = new CardGameplayEventHub();
        var observer = new ActionPointObserver(world);
        var injected = new CardGameplayRouteConsumers()
            .Add<ModifyActionPointsEvent>(observer, priority: 0, name: "root-tracking-observer");
        CardGameplayComposition composition = CardGameplayComposition.Create(world, events, injected);
        IEventRoute[] routes = composition.GetRoutes();
        var runtime = new EventRuntime(new EventRoutingEndpoint(routes));
        world.AttachEventRuntime(runtime);

        Assert.Equal(CardGameplayEventTypeIds.Count, routes.Length);
        Assert.Equal(
            Enumerable.Range(CardGameplayEventTypeIds.First, CardGameplayEventTypeIds.Count),
            routes.Select(route => route.EventTypeId).OrderBy(id => id));
        Assert.Equal(routes.Length, routes.Select(route => route.EventTypeId).Distinct().Count());
        Assert.Equal(routes.Length, routes.Select(route => route.EventName).Distinct(StringComparer.Ordinal).Count());

        events.ModifyActionPoints.Publish(new ModifyActionPointsEvent(player, -2));
        runtime.DrainBarrier();

        Assert.Equal(1, world.Get<Player>(player).ActionPoints);
        Assert.Equal(1, observer.ObservedActionPoints);
        Assert.Equal(1, observer.CallCount);
    }

    [Fact]
    public void Operational_scheduler_system_is_non_noop_complete_and_finalizes_deferred_spawns()
    {
        World world = CreateWorld();
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, 15);
        var events = new CardGameplayEventHub();
        CardGameplayComposition composition = CardGameplayComposition.Create(world, events);
        IGameSystem operational = Assert.Single(
            composition.Systems.ToArray(),
            system => system is DeckManagementSystem);
        SystemDescriptor descriptor = operational.Descriptor;

        Assert.Equal(typeof(DeckManagementSystem), operational.GetType());
        Assert.Equal(typeof(DeckManagementSystem), operational.GetType().GetMethod(nameof(IGameSystem.Update))!.DeclaringType);
        Assert.False(descriptor.ReadComponents.IsEmpty);
        Assert.False(descriptor.WriteComponents.IsEmpty);
        Assert.NotEmpty(descriptor.ReadDynamicBufferTypes.ToArray());
        Assert.NotEmpty(descriptor.WriteDynamicBufferTypes.ToArray());
        Assert.NotEmpty(descriptor.ConsumedEventTypeIds.ToArray());
        Assert.NotEmpty(descriptor.EmittedEventTypeIds.ToArray());
        Assert.True(descriptor.RecordsStructuralCommands);

        var structural = new CommandBuffer();
        CardGameplayFactory.RecordCardSpawn(structural, deck, CardId.Kunai, CardZone.Hand, false);
        structural.Playback(world);
        var runtime = new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes()));
        var scheduler = new SystemScheduler(world, runtime) { ActiveScene = SceneGroup.Battle };
        scheduler.Register(operational);
        scheduler.Build();
        scheduler.Update(TimeSpan.FromMilliseconds(16));

        EntityId card = Assert.Single(Zone(world, deck, CardZone.Hand));
        Assert.Equal(CardId.Kunai, world.Get<CardData>(card).Definition);
        Assert.False(world.Has<PendingCardSpawn>(card));
    }

    [Fact]
    public void Warmed_card_query_enumeration_allocates_zero_bytes()
    {
        World world = CreateWorld();
        EntityId deck = Populate(world, 14);
        Query<CardData> query = world.Query<CardData>();
        Assert.Equal(5, Count(query));

        long before = GC.GetAllocatedBytesForCurrentThread();
        int count = Count(query);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(5, count);
        Assert.Equal(0, allocated);
        Assert.False(deck.IsNull);
    }

    private static int Count(Query<CardData> query) { var count = 0; foreach (QueryChunk<CardData> chunk in query) foreach (int row in chunk.Rows) { _ = chunk.Component1[row]; count++; } return count; }

    private static EntityId Populate(World world, ulong seed)
    {
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, seed);
        CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DrawPile);
        CardGameplayFactory.CreateCard(world, deck, CardId.Fervor, CardZone.DrawPile);
        CardGameplayFactory.CreateCard(world, deck, CardId.Tempest, CardZone.DrawPile);
        CardGameplayFactory.CreateCard(world, deck, CardId.Kunai, CardZone.DrawPile);
        CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DrawPile);
        return deck;
    }

    private static EntityId CreatePlayer(World world, int actionPoints)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new Player { ActionPoints = actionPoints, Health = 20, MaximumHealth = 20 });
        return world.Create(in bundle);
    }

    private static EntityId[] Zone(World world, EntityId deck, CardZone zone)
    {
        var result = new EntityId[CardZoneOperations.Count(world, deck, zone)];
        for (var index = 0; index < result.Length; index++) result[index] = CardZoneOperations.At(world, deck, zone, index);
        return result;
    }

    private static World CreateWorld()
    {
        return new World(GeneratedComponentRegistry.Create());
    }

    private sealed class ActionPointObserver(World world) : IEventConsumer<ModifyActionPointsEvent>
    {
        public int CallCount { get; private set; }
        public int ObservedActionPoints { get; private set; }

        public void Consume(in ModifyActionPointsEvent value, ref EventDispatchContext context)
        {
            CallCount++;
            ObservedActionPoints = world.Get<Player>(value.Player).ActionPoints;
        }
    }
}
