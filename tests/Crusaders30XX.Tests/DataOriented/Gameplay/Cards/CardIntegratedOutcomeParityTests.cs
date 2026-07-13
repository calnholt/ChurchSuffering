#nullable enable

using System;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Cards;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Cards;

public sealed class CardIntegratedOutcomeParityTests
{
    [Fact]
    public void Autopay_backtracks_any_slot_to_preserve_a_later_colored_requirement()
    {
        World world = CreateWorld();
        EntityId player = CreatePlayer(world, 2);
        EntityId deck = CardGameplayFactory.CreateDeck(world, player, 101);
        EntityId played = CardGameplayFactory.CreateCard(world, deck, CardId.Reap, CardZone.Hand, color: RuleCardColor.Black);
        EntityId red = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.Red);
        EntityId white = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.White);
        ref CardData data = ref world.Get<CardData>(played);
        data.CostCount = 2;
        data.Cost0 = CardCostColor.Any;
        data.Cost1 = CardCostColor.Red;
        Span<EntityId> selected = stackalloc EntityId[4];

        bool satisfied = CardCostRules.TrySelectPayment(
            world,
            in data,
            [red, white],
            played,
            deck,
            selected,
            out int count);

        Assert.True(satisfied);
        Assert.Equal(2, count);
        Assert.Equal([white, red], selected[..count].ToArray());
    }

    [Fact]
    public void Autopay_skips_ineligible_cards_and_passes_actual_scorched_payment_to_handler()
    {
        World world = CreateWorld();
        EntityId player = CreatePlayer(world, 2);
        EntityId deck = CardGameplayFactory.CreateDeck(world, player, 102);
        EntityId played = CardGameplayFactory.CreateCard(world, deck, CardId.EmberHarvest, CardZone.Hand, color: RuleCardColor.Red);
        EntityId weapon = CardGameplayFactory.CreateCard(world, deck, CardId.Hammer, CardZone.Hand, color: RuleCardColor.Black);
        EntityId pledged = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.White);
        world.Add(pledged, new Pledge { CanPlay = 1 });
        EntityId scorched = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.Black);
        world.AddTag<Scorched>(scorched);
        var events = new CardGameplayEventHub();
        var rules = new RuleCommandBuffer(8);
        var structural = new CommandBuffer();
        var resultStorage = new RuleHandlerResult[8];
        var resultState = new RuleResultWriterState();
        CardHandlerInput input = ResolveInput(world, player, deck, played, CardId.EmberHarvest);

        CardPlayValidation result = new CardPlaySystem(world, events).TryPlayAuto(
            player,
            deck,
            played,
            Zone(world, deck, CardZone.Hand),
            ReadOnlySpan<AlternatePlayResult>.Empty,
            in input,
            rules,
            structural,
            resultStorage,
            ref resultState);
        structural.Playback(world);

        Assert.True(result.Allowed);
        Assert.Equal([weapon, pledged], Zone(world, deck, CardZone.Hand));
        Assert.Equal([scorched, played], Zone(world, deck, CardZone.DiscardPile));
        Assert.Equal(1, world.Get<Player>(player).ActionPoints);
        RuleCommand might = Assert.Single(rules.AsReadOnlySpan().ToArray(), command =>
            command.Kind == RuleCommandKind.ApplyEffect &&
            command.Payload.Effect.Effect.Id == RuleEffectIds.Might);
        Assert.Equal(2, might.Payload.Effect.Effect.Magnitude);
        Assert.Equal(1, events.CardDiscardedForCost.PendingCount);
        Assert.Equal(1, events.CardPlayed.PendingCount);
    }

    [Fact]
    public void Same_definition_payment_cards_keep_distinct_identity_and_colored_outcome()
    {
        World world = CreateWorld();
        EntityId player = CreatePlayer(world, 2);
        EntityId deck = CardGameplayFactory.CreateDeck(world, player, 103);
        EntityId played = CardGameplayFactory.CreateCard(world, deck, CardId.Reap, CardZone.Hand, upgraded: true, color: RuleCardColor.Black);
        EntityId firstRed = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.Red);
        EntityId secondRed = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.Red);
        var events = new CardGameplayEventHub();
        var rules = new RuleCommandBuffer(8);
        var structural = new CommandBuffer();
        var results = new RuleHandlerResult[8];
        var resultState = new RuleResultWriterState();
        CardHandlerInput input = ResolveInput(world, player, deck, played, CardId.Reap);

        CardPlayValidation result = new CardPlaySystem(world, events).TryPlayAuto(
            player, deck, played, [played, firstRed, secondRed],
            ReadOnlySpan<AlternatePlayResult>.Empty, in input, rules, structural,
            results, ref resultState);
        structural.Playback(world);

        Assert.True(result.Allowed);
        Assert.NotEqual(firstRed, secondRed);
        Assert.Equal([firstRed, secondRed, played], Zone(world, deck, CardZone.DiscardPile));
        Assert.Equal(3, CardZoneOperations.Count(world, deck, CardZone.MasterDeck));
        Assert.All([firstRed, secondRed], card => Assert.Equal(RuleCardColor.Red, world.Get<CardData>(card).RuntimeColor));
        RuleCommand courage = Assert.Single(rules.AsReadOnlySpan().ToArray(), command =>
            command.Kind == RuleCommandKind.ModifyStat &&
            command.Payload.ResourceDelta.Stat == RuleStatIds.Courage);
        Assert.Equal(2, courage.Payload.ResourceDelta.Amount);
    }

    [Fact]
    public void Duplicate_out_of_zone_and_cross_deck_payments_are_rejected_atomically()
    {
        AssertRejected(PaymentViolation.Duplicate);
        AssertRejected(PaymentViolation.DiscardPile);
        AssertRejected(PaymentViolation.OtherDeck);
    }

    [Fact]
    public void Pledge_lock_unlock_play_and_once_per_phase_state_form_one_lifecycle()
    {
        World world = CreateWorld();
        EntityId player = CreatePlayer(world, 2);
        EntityId deck = CardGameplayFactory.CreateDeck(world, player, 104);
        EntityId played = CardGameplayFactory.CreateCard(world, deck, CardId.EmberHarvest, CardZone.Hand, color: RuleCardColor.Red);
        EntityId payment = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.White);
        EntityId nextCandidate = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.Black);
        var state = new PledgeAvailabilityState { Enabled = 1 };
        var commands = new CommandBuffer();
        var events = new CardGameplayEventHub();
        var pledges = new PledgeManagementSystem(world, events);

        Assert.Equal(PledgeFailure.None, pledges.TryPledge(deck, played, ref state, commands));
        commands.Playback(world);
        Assert.Equal(1, state.PledgedThisActionPhase);
        Assert.Equal(0, world.Get<Pledge>(played).CanPlay);
        Assert.Equal(2, DrawHandSystem.CalculateCardsToDraw(4, Hand(world, deck), world));
        Assert.Equal(
            CardPaymentFailure.PledgeLocked,
            CardPlayRules.Validate(world, player, deck, played, [payment], ReadOnlySpan<AlternatePlayResult>.Empty).Failure);

        pledges.UnlockHand(deck);
        var ruleCommands = new RuleCommandBuffer(8);
        var results = new RuleHandlerResult[8];
        var resultState = new RuleResultWriterState();
        CardHandlerInput input = ResolveInput(world, player, deck, played, CardId.EmberHarvest);
        CardPlayValidation result = new CardPlaySystem(world, events).TryPlayAuto(
            player, deck, played, [payment], ReadOnlySpan<AlternatePlayResult>.Empty,
            in input, ruleCommands, commands, results, ref resultState);
        commands.Playback(world);

        Assert.True(result.Allowed);
        Assert.False(world.Has<Pledge>(played));
        Assert.Equal(1, state.PledgedThisActionPhase);
        Assert.Equal(PledgeFailure.AlreadyPledgedThisAction,
            pledges.TryPledge(deck, nextCandidate, ref state, commands));
    }

    [Fact]
    public void Spawn_rule_preserves_requested_color_and_distinct_entity_identity()
    {
        World world = CreateWorld();
        EntityId deck = CardGameplayFactory.CreateDeck(world, EntityId.Null, 105);
        var rules = new RuleCommandBuffer();
        rules.Writer.Append(RuleCommand.SpawnCard(
            TargetHandle.None, TargetHandle.None, deck, CardId.Kunai,
            CardZone.Hand, RuleCardColor.Black, isUpgraded: false, count: 2));
        var structural = new CommandBuffer();
        var events = new CardGameplayEventHub();
        RuleRandomState random = RuleRandomState.FromSeed(105);

        CardRuleCommandExecutor.Execute(world, rules.AsReadOnlySpan(), structural, events, ref random);
        structural.Playback(world);
        new DeckManagementSystem(world, events).FinalizePendingSpawns(structural);
        structural.Playback(world);

        EntityId[] hand = Zone(world, deck, CardZone.Hand);
        Assert.Equal(2, hand.Length);
        Assert.NotEqual(hand[0], hand[1]);
        Assert.All(hand, card =>
        {
            Assert.Equal(RuleCardColor.Black, world.Get<CardData>(card).PrintedColor);
            Assert.Equal(RuleCardColor.Black, world.Get<CardData>(card).RuntimeColor);
        });
    }

    private static void AssertRejected(PaymentViolation violation)
    {
        World world = CreateWorld();
        EntityId player = CreatePlayer(world, 2);
        EntityId deck = CardGameplayFactory.CreateDeck(world, player, (ulong)(200 + (int)violation));
        EntityId otherDeck = CardGameplayFactory.CreateDeck(world, player, (ulong)(300 + (int)violation));
        EntityId played = CardGameplayFactory.CreateCard(world, deck, CardId.Reap, CardZone.Hand, color: RuleCardColor.Black);
        EntityId valid = CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.Hand, color: RuleCardColor.Red);
        EntityId invalid = violation switch
        {
            PaymentViolation.Duplicate => valid,
            PaymentViolation.DiscardPile => CardGameplayFactory.CreateCard(world, deck, CardId.Strike, CardZone.DiscardPile, color: RuleCardColor.White),
            PaymentViolation.OtherDeck => CardGameplayFactory.CreateCard(world, otherDeck, CardId.Strike, CardZone.Hand, color: RuleCardColor.White),
            _ => throw new ArgumentOutOfRangeException(nameof(violation)),
        };
        ReadOnlySpan<EntityId> payment = violation == PaymentViolation.Duplicate
            ? [valid, valid]
            : [valid, invalid];
        var events = new CardGameplayEventHub();
        var ruleCommands = new RuleCommandBuffer();
        var structural = new CommandBuffer();
        var results = new RuleHandlerResult[4];
        var resultState = new RuleResultWriterState();
        CardHandlerInput input = ResolveInput(world, player, deck, played, CardId.Reap);

        CardPlayValidation result = new CardPlaySystem(world, events).TryPlay(
            player, deck, played, payment, ReadOnlySpan<AlternatePlayResult>.Empty,
            in input, ruleCommands, structural, results, ref resultState);

        Assert.False(result.Allowed);
        Assert.Equal(CardPaymentFailure.InsufficientPaymentCards, result.Failure);
        Assert.Equal(2, world.Get<Player>(player).ActionPoints);
        Assert.Equal(CardZone.Hand, world.Get<CardZoneLocation>(played).Zone);
        Assert.Equal(CardZone.Hand, world.Get<CardZoneLocation>(valid).Zone);
        Assert.Empty(ruleCommands.AsReadOnlySpan().ToArray());
        Assert.Equal(0, structural.Count);
    }

    private static CardHandlerInput ResolveInput(
        World world,
        EntityId player,
        EntityId deck,
        EntityId card,
        CardId definition)
    {
        CardData data = world.Get<CardData>(card);
        var payload = new CardTriggerPayload(
            card, player, definition, RuleCardEventKind.Played, data.RuntimeColor, RuleCardTraits.Attack);
        return new CardHandlerInput(
            new RuleInvocationId(1),
            card,
            player,
            definition,
            new RuleTriggerEnvelope(RuleTriggerKind.Card, RuleTriggerIds.CardResolvePlay, RuleTriggerPayload.From(in payload)),
            CardHandlerFlags.None,
            new CardPhaseSnapshot(RulePhase.Action, 1, 1, 1),
            default,
            new CombatResourceSnapshot(0, 0, world.Get<Player>(player).ActionPoints, 0, 0, 0, 0),
            default,
            new DeckStateSnapshot(
                deck,
                EntityId.Null,
                CardZoneOperations.Count(world, deck, CardZone.DrawPile),
                CardZoneOperations.Count(world, deck, CardZone.Hand),
                CardZoneOperations.Count(world, deck, CardZone.DiscardPile),
                CardZoneOperations.Count(world, deck, CardZone.ExhaustPile)),
            data.Damage,
            data.Damage,
            TargetHandle.PrimaryEnemy);
    }

    private static EntityId CreatePlayer(World world, int actionPoints)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new Player { ActionPoints = actionPoints, Health = 20, MaximumHealth = 20 });
        return world.Create(in bundle);
    }

    private static EntityId[] Zone(World world, EntityId deck, CardZone zone)
    {
        var cards = new EntityId[CardZoneOperations.Count(world, deck, zone)];
        for (var index = 0; index < cards.Length; index++)
            cards[index] = CardZoneOperations.At(world, deck, zone, index);
        return cards;
    }

    private static ReadOnlySpan<HandCard> Hand(World world, EntityId deck) =>
        world.GetDynamicBuffer<HandCard>(world.Get<Deck>(deck).Hand).AsReadOnlySpan();

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private enum PaymentViolation : byte { Duplicate, DiscardPile, OtherDeck }
}
