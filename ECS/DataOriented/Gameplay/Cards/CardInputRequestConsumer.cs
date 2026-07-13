#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

/// <summary>Card-owned execution endpoint for input requests published by the meta/UI bridge.</summary>
public sealed class CardInputRequestConsumer :
    IEventConsumer<PlayCardRequested>,
    IEventConsumer<PledgeCardRequested>
{
    private readonly World world;
    private readonly CardPlaySystem cardPlay;
    private readonly PledgeManagementSystem pledges;
    private readonly RuleCommandBuffer rules = new(16);
    private readonly CommandBuffer structural = new(16);
    private readonly RuleHandlerResult[] results = new RuleHandlerResult[16];
    private int invocation;

    public CardInputRequestConsumer(World world, CardGameplayEventHub events)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        ArgumentNullException.ThrowIfNull(events);
        cardPlay = new CardPlaySystem(world, events);
        pledges = new PledgeManagementSystem(world, events);
    }

    public void Consume(in PlayCardRequested value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Card, out CardData card) ||
            !world.TryGet(value.Card, out CardZoneLocation location) ||
            location.Zone != CardZone.Hand || location.Deck != card.Deck ||
            !world.Has<Deck>(card.Deck) || !world.Has<Player>(value.Player))
            return;

        SyncCardResourcesFromCombat(value.Player);
        ref readonly Deck deck = ref world.Get<Deck>(card.Deck);
        ReadOnlySpan<HandCard> hand = world.GetDynamicBuffer<HandCard>(deck.Hand).AsReadOnlySpan();
        if (hand.Length > 128)
            throw new InvalidOperationException("Card input payment selection supports at most 128 hand entries.");
        Span<EntityId> candidates = stackalloc EntityId[hand.Length];
        for (var index = 0; index < hand.Length; index++) candidates[index] = hand[index].Card;

        rules.Clear();
        structural.Clear();
        Array.Clear(results);
        var resultState = new RuleResultWriterState();
        CardHandlerInput input = BuildInput(value.Player, value.Card, in card, in deck);
        cardPlay.TryPlayAuto(
            value.Player,
            card.Deck,
            value.Card,
            candidates,
            ReadOnlySpan<AlternatePlayResult>.Empty,
            in input,
            rules,
            structural,
            results,
            ref resultState);
        if (structural.Count > 0) structural.Playback(world);
        SyncCombatResourcesFromCard(value.Player);
    }

    public void Consume(in PledgeCardRequested value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Card, out CardData card) || !world.Has<Deck>(card.Deck)) return;
        EntityId player = world.Get<Deck>(card.Deck).Owner;
        if (!world.TryGet(player, out PledgeAvailabilityState state)) return;
        structural.Clear();
        pledges.TryPledge(card.Deck, value.Card, ref state, structural);
        world.Set(player, in state);
        if (structural.Count > 0) structural.Playback(world);
    }

    private CardHandlerInput BuildInput(
        EntityId player,
        EntityId card,
        in CardData data,
        in Deck deck)
    {
        int turn = 1;
        int battleEpoch = 1;
        EntityId enemy = EntityId.Null;
        Query<BattleInfo, BattleStateInfo> battles = world.Query<BattleInfo, BattleStateInfo>();
        foreach (QueryChunk<BattleInfo, BattleStateInfo> chunk in battles)
        foreach (int row in chunk.Rows)
        {
            if (chunk.Component1[row].Player != player) continue;
            enemy = chunk.Component1[row].Enemy;
            turn = chunk.Component2[row].Turn;
            battleEpoch = chunk.Component2[row].BattleEpoch;
            break;
        }

        var payload = new CardTriggerPayload(
            card,
            player,
            data.Definition,
            RuleCardEventKind.Played,
            data.RuntimeColor,
            RuleCardTraits.Attack);
        ref readonly Player resources = ref world.Get<Player>(player);
        return new CardHandlerInput(
            new RuleInvocationId(++invocation),
            card,
            player,
            data.Definition,
            new RuleTriggerEnvelope(
                RuleTriggerKind.Card,
                RuleTriggerIds.CardResolvePlay,
                RuleTriggerPayload.From(in payload)),
            CardHandlerFlags.None,
            new CardPhaseSnapshot(RulePhase.Action, turn, battleEpoch, invocation),
            default,
            new CombatResourceSnapshot(
                resources.Courage,
                resources.Temperance,
                resources.ActionPoints,
                0,
                0,
                0,
                resources.Frostbite),
            new CardBattleSnapshot(deck.CardsMilled, deck.ActionAttackHits, 0, 0, 0, 0, 0),
            new DeckStateSnapshot(
                data.Deck,
                EntityId.Null,
                CardZoneOperations.Count(world, data.Deck, CardZone.DrawPile),
                CardZoneOperations.Count(world, data.Deck, CardZone.Hand),
                CardZoneOperations.Count(world, data.Deck, CardZone.DiscardPile),
                CardZoneOperations.Count(world, data.Deck, CardZone.ExhaustPile)),
            data.Damage,
            data.Damage,
            TargetHandle.PrimaryEnemy);
    }

    private void SyncCardResourcesFromCombat(EntityId player)
    {
        ref Player card = ref world.Get<Player>(player);
        if (world.TryGet(player, out ActionPoints actionPoints)) card.ActionPoints = actionPoints.Current;
        if (world.TryGet(player, out Courage courage)) card.Courage = courage.Amount;
        if (world.TryGet(player, out Temperance temperance)) card.Temperance = temperance.Amount;
        if (world.TryGet(player, out HP health))
        {
            card.Health = health.Current;
            card.MaximumHealth = health.Max;
        }
    }

    private void SyncCombatResourcesFromCard(EntityId player)
    {
        ref readonly Player card = ref world.Get<Player>(player);
        if (world.Has<ActionPoints>(player)) world.Get<ActionPoints>(player).Current = card.ActionPoints;
        if (world.Has<Courage>(player)) world.Get<Courage>(player).Amount = card.Courage;
        if (world.Has<Temperance>(player)) world.Get<Temperance>(player).Amount = card.Temperance;
        if (world.Has<HP>(player)) world.Get<HP>(player).Current = card.Health;
    }
}
