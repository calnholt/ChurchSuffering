#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

public static class CardGameplaySystemIds
{
    public static readonly SystemId ActionPointManagement = new(411);
    public static readonly SystemId AssignedBlocksToDiscard = new(412);
    public static readonly SystemId DrawHand = new(413);
    public static readonly SystemId CanPlayHighlight = new(414);
    public static readonly SystemId CantPlayMessage = new(415);
    public static readonly SystemId CardApplicationManagement = new(416);
    public static readonly SystemId CardHoverDetection = new(417);
    public static readonly SystemId CardPlay = new(418);
    public static readonly SystemId CardZone = new(419);
    public static readonly SystemId CursedManagement = new(420);
    public static readonly SystemId DeckEmptyDeathCheck = new(421);
    public static readonly SystemId DeckManagement = new(422);
    public static readonly SystemId DiscardSpecificHighlight = new(423);
    public static readonly SystemId HandBlockInteraction = new(424);
    public static readonly SystemId HandCardBoundsLate = new(425);
    public static readonly SystemId MarkedForExhaust = new(426);
    public static readonly SystemId MarkedForSpecificDiscard = new(427);
    public static readonly SystemId MillCard = new(428);
    public static readonly SystemId PledgeManagement = new(429);
    public static readonly SystemId PlunderManagement = new(430);
    public static readonly SystemId RecoilManagement = new(431);
    public static readonly SystemId SealManagement = new(432);
    public static readonly SystemId ShackleManagement = new(433);
    public static readonly SystemId CardListModal = new(434);
    public static readonly SystemId CardShaderCompositor = new(435);
    public static readonly SystemId CardUsageTracking = new(436);
    public static readonly SystemId BattlePileInput = new(437);
}

public abstract class CardGameplaySystem : IGameSystem
{
    protected CardGameplaySystem(
        SystemId id,
        string name,
        SystemPhase phase,
        EventBarrier barrier = EventBarrier.None,
        ComponentSignature readComponents = default,
        ComponentSignature writeComponents = default,
        Type[]? readDynamicBufferTypes = null,
        Type[]? writeDynamicBufferTypes = null,
        int[]? consumedEventTypeIds = null,
        int[]? emittedEventTypeIds = null,
        SystemId[]? runsAfter = null,
        bool recordsStructuralCommands = false)
    {
        Descriptor = new SystemDescriptor(
            id,
            name,
            phase,
            SceneGroup.Battle,
            readComponents,
            writeComponents,
            readDynamicBufferTypes,
            writeDynamicBufferTypes,
            consumedEventTypeIds,
            emittedEventTypeIds,
            runsAfter: runsAfter,
            recordsStructuralCommands: recordsStructuralCommands,
            eventBarrier: barrier);
    }

    public SystemDescriptor Descriptor { get; }
    public virtual void Update(ref SystemContext context) { }
}

public sealed class ActionPointManagementSystem : CardGameplaySystem,
    IEventConsumer<ModifyActionPointsEvent>, IEventConsumer<SetActionPointsEvent>
{
    private readonly World world;
    public ActionPointManagementSystem(World world) : base(CardGameplaySystemIds.ActionPointManagement, nameof(ActionPointManagementSystem), SystemPhase.Gameplay) => this.world = world;
    public void Consume(in ModifyActionPointsEvent value, ref EventDispatchContext context) => Modify(value.Player, value.Delta);
    public void Consume(in SetActionPointsEvent value, ref EventDispatchContext context) { if (world.TryGet(value.Player, out Player p)) { p.ActionPoints = Math.Max(0, value.Amount); world.Set(value.Player, p); } }
    public void Modify(EntityId player, int delta) { if (world.TryGet(player, out Player p)) { p.ActionPoints = Math.Max(0, p.ActionPoints + delta); world.Set(player, p); } }
}

public sealed class DeckManagementSystem : CardGameplaySystem,
    IEventConsumer<DeckShuffleEvent>, IEventConsumer<DeckShuffleDrawEvent>,
    IEventConsumer<DrawRandomCardFromDiscardEvent>, IEventConsumer<ShuffleRandomCardsFromDiscardToDrawPileEvent>,
    IEventConsumer<DiscardAllCardsEvent>, IEventConsumer<RedrawHandEvent>, IEventConsumer<ResetDeckEvent>
{
    private readonly World world;
    private readonly CardGameplayEventHub events;
    private Query<PendingCardSpawn>? pendingSpawnQuery;

    public DeckManagementSystem(World world, CardGameplayEventHub events)
        : base(
            CardGameplaySystemIds.DeckManagement,
            nameof(DeckManagementSystem),
            SystemPhase.Gameplay,
            EventBarrier.AfterSystem,
            readComponents: DeckReadComponents(),
            writeComponents: DeckWriteComponents(),
            readDynamicBufferTypes:
            [
                typeof(MasterDeckCard), typeof(DrawPileCard), typeof(HandCard),
                typeof(DiscardPileCard), typeof(ExhaustPileCard),
            ],
            writeDynamicBufferTypes:
            [
                typeof(MasterDeckCard), typeof(DrawPileCard), typeof(HandCard),
                typeof(DiscardPileCard), typeof(ExhaustPileCard),
            ],
            consumedEventTypeIds:
            [
                CardGameplayEventTypeIds.DeckShuffleDraw,
                CardGameplayEventTypeIds.DeckShuffle,
                CardGameplayEventTypeIds.DiscardAllCards,
                CardGameplayEventTypeIds.DrawRandomCardFromDiscard,
                CardGameplayEventTypeIds.RedrawHand,
                CardGameplayEventTypeIds.ResetDeck,
                CardGameplayEventTypeIds.ShuffleRandomFromDiscard,
            ],
            emittedEventTypeIds:
            [
                CardGameplayEventTypeIds.CardMoved,
                CardGameplayEventTypeIds.CardsDrawn,
                CardGameplayEventTypeIds.DrawPileEmpty,
            ],
            recordsStructuralCommands: true)
    {
        this.world = world;
        this.events = events;
    }

    public override void Update(ref SystemContext context)
    {
        FinalizePendingSpawns(context.Commands);
    }

    public void FinalizePendingSpawns(CommandBuffer commands)
    {
        pendingSpawnQuery ??= world.Query<PendingCardSpawn>();
        foreach (QueryChunk<PendingCardSpawn> chunk in pendingSpawnQuery)
        {
            foreach (int row in chunk.Rows)
            {
                EntityId card = chunk.Entities[row];
                PendingCardSpawn pending = chunk.Component1[row];
                CardZoneOperations.AddToMasterDeck(world, pending.Deck, card);
                CardZoneOperations.Insert(world, pending.Deck, pending.Destination, card, pending.DestinationIndex);
                ref CardZoneLocation location = ref world.Get<CardZoneLocation>(card);
                location.Zone = pending.Destination;
                CardZoneOperations.RefreshIndices(world, pending.Deck, pending.Destination);
                commands.Remove<PendingCardSpawn>(card);
            }
        }
    }

    public int Draw(EntityId deckEntity, int requested)
    {
        int drawn = 0;
        int attempts = Math.Max(0, requested);
        while (drawn < attempts)
        {
            if (CardZoneOperations.Count(world, deckEntity, CardZone.DrawPile) == 0)
            {
                events.DrawPileEmpty.Publish(new DrawPileEmptyEvent(deckEntity));
                break;
            }
            EntityId card = CardZoneOperations.At(world, deckEntity, CardZone.DrawPile, 0);
            if (world.Get<CardData>(card).IsWeapon)
            {
                CardZoneOperations.Move(world, deckEntity, card, CardZone.DrawPile, CardZone.DiscardPile, -1, CardMoveReason.Draw, events);
                continue;
            }
            CardZoneOperations.Move(world, deckEntity, card, CardZone.DrawPile, CardZone.Hand, -1, CardMoveReason.Draw, events);
            drawn++;
        }
        events.CardsDrawn.Publish(new CardsDrawnEvent(deckEntity, requested, drawn));
        return drawn;
    }

    public int DrawRandomFromDiscard(EntityId deckEntity, int requested)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        var random = new DeterministicRuleRandom(ref deck.Random);
        int moved = 0;
        while (moved < requested)
        {
            DynamicBuffer<DiscardPileCard> discard = world.GetDynamicBuffer<DiscardPileCard>(deck.DiscardPile);
            int eligible = CountEligibleNonWeapons(discard.AsReadOnlySpan());
            if (eligible == 0) break;
            int selectedEligible = random.NextInt(eligible);
            int index = FindEligibleNonWeapon(discard.AsReadOnlySpan(), selectedEligible);
            EntityId card = discard[index].Card;
            CardZoneOperations.Move(world, deckEntity, card, CardZone.DiscardPile, CardZone.Hand, -1, CardMoveReason.Draw, events);
            moved++;
        }
        events.CardsDrawn.Publish(new CardsDrawnEvent(deckEntity, requested, moved));
        return moved;
    }

    public int ShuffleRandomFromDiscard(EntityId deckEntity, int requested)
    {
        ref Deck deck = ref world.Get<Deck>(deckEntity);
        var random = new DeterministicRuleRandom(ref deck.Random);
        int moved = 0;
        while (moved < requested)
        {
            DynamicBuffer<DiscardPileCard> discard = world.GetDynamicBuffer<DiscardPileCard>(deck.DiscardPile);
            int eligible = CountEligibleNonWeapons(discard.AsReadOnlySpan());
            if (eligible == 0) break;
            int index = FindEligibleNonWeapon(discard.AsReadOnlySpan(), random.NextInt(eligible));
            EntityId card = discard[index].Card;
            CardZoneOperations.Move(world, deckEntity, card, CardZone.DiscardPile, CardZone.DrawPile, -1, CardMoveReason.Reset, events);
            moved++;
        }
        if (moved > 0) CardZoneOperations.ShuffleDrawPile(world, deckEntity);
        return moved;
    }

    public int DiscardHand(EntityId deckEntity, CardMoveReason reason)
    {
        int moved = 0;
        while (CardZoneOperations.Count(world, deckEntity, CardZone.Hand) > 0)
        {
            EntityId card = CardZoneOperations.At(world, deckEntity, CardZone.Hand, 0);
            CardZone destination = world.Has<MarkedForExhaust>(card) ? CardZone.ExhaustPile : CardZone.DiscardPile;
            if (CardZoneOperations.Move(world, deckEntity, card, CardZone.Hand, destination, -1, reason, events)) moved++;
        }
        return moved;
    }

    public void Reset(EntityId deckEntity)
    {
        MoveAll(deckEntity, CardZone.Hand, CardZone.DrawPile);
        MoveAll(deckEntity, CardZone.DiscardPile, CardZone.DrawPile);
        MoveAll(deckEntity, CardZone.ExhaustPile, CardZone.DrawPile);
        CardZoneOperations.ShuffleDrawPile(world, deckEntity);
    }

    public void Consume(in DeckShuffleEvent value, ref EventDispatchContext context) => CardZoneOperations.ShuffleDrawPile(world, value.Deck);
    public void Consume(in DeckShuffleDrawEvent value, ref EventDispatchContext context) { CardZoneOperations.ShuffleDrawPile(world, value.Deck); Draw(value.Deck, value.Count); }
    public void Consume(in DrawRandomCardFromDiscardEvent value, ref EventDispatchContext context) => DrawRandomFromDiscard(value.Deck, value.Count);
    public void Consume(in ShuffleRandomCardsFromDiscardToDrawPileEvent value, ref EventDispatchContext context) => ShuffleRandomFromDiscard(value.Deck, value.Count);
    public void Consume(in DiscardAllCardsEvent value, ref EventDispatchContext context) => DiscardHand(value.Deck, value.Reason);
    public void Consume(in RedrawHandEvent value, ref EventDispatchContext context) { DiscardHand(value.Deck, CardMoveReason.Reset); CardZoneOperations.ShuffleDrawPile(world, value.Deck); Draw(value.Deck, value.DrawCount); }
    public void Consume(in ResetDeckEvent value, ref EventDispatchContext context) => Reset(value.Deck);

    private int CountEligibleNonWeapons(ReadOnlySpan<DiscardPileCard> cards) { var count = 0; for (var i = 0; i < cards.Length; i++) if (!world.Get<CardData>(cards[i].Card).IsWeapon) count++; return count; }
    private int FindEligibleNonWeapon(ReadOnlySpan<DiscardPileCard> cards, int selected) { for (var i = 0; i < cards.Length; i++) if (!world.Get<CardData>(cards[i].Card).IsWeapon && selected-- == 0) return i; return -1; }
    private void MoveAll(EntityId deck, CardZone source, CardZone destination) { while (CardZoneOperations.Count(world, deck, source) > 0) CardZoneOperations.Move(world, deck, CardZoneOperations.At(world, deck, source, 0), source, destination, -1, CardMoveReason.Reset, events); }

    private static ComponentSignature DeckReadComponents() => ComponentSignature.Empty
        .With(ComponentType<PendingCardSpawn>.Id)
        .With(ComponentType<CardData>.Id);

    private static ComponentSignature DeckWriteComponents() => ComponentSignature.Empty
        .With(ComponentType<Deck>.Id)
        .With(ComponentType<CardZoneLocation>.Id);
}

public sealed class CardZoneSystem : CardGameplaySystem
{
    private readonly World world;
    private readonly CardGameplayEventHub events;
    public CardZoneSystem(World world, CardGameplayEventHub events) : base(CardGameplaySystemIds.CardZone, nameof(CardZoneSystem), SystemPhase.Gameplay, EventBarrier.AfterSystem) { this.world = world; this.events = events; }
    public bool Move(EntityId deck, EntityId card, CardZone source, CardZone destination, int index = -1, CardMoveReason reason = CardMoveReason.None) => CardZoneOperations.Move(world, deck, card, source, destination, index, reason, events);
}

public sealed class CardPlaySystem : CardGameplaySystem
{
    private readonly World world;
    private readonly CardGameplayEventHub events;
    public CardPlaySystem(World world, CardGameplayEventHub events) : base(CardGameplaySystemIds.CardPlay, nameof(CardPlaySystem), SystemPhase.Rules, EventBarrier.AfterSystem) { this.world = world; this.events = events; }

    public CardPlayValidation TryPlay(
        EntityId player,
        EntityId deck,
        EntityId card,
        ReadOnlySpan<EntityId> paymentCards,
        ReadOnlySpan<AlternatePlayResult> alternateResults,
        in CardHandlerInput resolveInput,
        RuleCommandBuffer ruleCommands,
        CommandBuffer structuralCommands,
        Span<RuleHandlerResult> resultStorage,
        ref RuleResultWriterState resultState)
    {
        CardPlayValidation validation = CardPlayRules.Validate(world, player, deck, card, paymentCards, alternateResults);
        if (!validation.Allowed) { events.CantPlay.Publish(new CantPlayCardMessage(card, validation.Failure, 0)); return validation; }

        ref CardData cardData = ref world.Get<CardData>(card);
        if (paymentCards.Length != cardData.CostCount ||
            !CardCostRules.CanSatisfy(world, in cardData, paymentCards, card, deck))
            return new CardPlayValidation(false, CardPaymentFailure.InsufficientPaymentCards, validation.AlternatePlay, cardData.CostCount);

        for (var index = 0; index < paymentCards.Length; index++)
        {
            EntityId payment = paymentCards[index];
            if (!CardZoneOperations.Move(world, deck, payment, CardZone.Hand, CardZone.DiscardPile, -1, CardMoveReason.Payment, events))
                continue;
            events.CardDiscardedForCost.Publish(new CardDiscardedForCostEvent(payment, card, deck));
        }

        CardHandlerFlags stateFlags = resolveInput.Flags &
            ~(CardHandlerFlags.Upgraded | CardHandlerFlags.Pledged | CardHandlerFlags.Scorched | CardHandlerFlags.Weapon);
        if (cardData.IsUpgraded) stateFlags |= CardHandlerFlags.Upgraded;
        if (world.Has<Pledge>(card)) stateFlags |= CardHandlerFlags.Pledged;
        if (world.Has<Scorched>(card)) stateFlags |= CardHandlerFlags.Scorched;
        if (cardData.IsWeapon) stateFlags |= CardHandlerFlags.Weapon;
        CardHandlerInput effectiveInput = resolveInput with
        {
            Card = card,
            Player = player,
            Definition = cardData.Definition,
            Flags = stateFlags,
            Payment = CardCostRules.BuildSnapshot(world, paymentCards),
        };

        ref Deck deckData = ref world.Get<Deck>(deck);
        RuleRandomState random = deckData.Random;
        CardLifecycleDispatcher.Dispatch(world, in effectiveInput, ruleCommands, ReadOnlySpan<RuleFact>.Empty, paymentCards, ReadOnlySpan<EntityId>.Empty, resultStorage, ref resultState, ref random, out _);
        deckData.Random = random;
        CardRuleCommandExecutor.Execute(world, ruleCommands.AsReadOnlySpan(), structuralCommands, events, ref deckData.Random);

        CardZone destination = (cardData.Flags & CardRuntimeFlags.ExhaustsOnEndTurn) != 0 || world.Has<MarkedForExhaust>(card)
            ? CardZone.ExhaustPile : CardZone.DiscardPile;
        CardZoneOperations.Move(world, deck, card, CardZone.Hand, destination, -1, CardMoveReason.Play, events);
        if (!cardData.IsFreeAction && !validation.AlternatePlay.FreeAction)
        {
            ref Player playerData = ref world.Get<Player>(player);
            playerData.ActionPoints = Math.Max(0, playerData.ActionPoints - 1);
        }
        if (world.Has<Frozen>(card)) world.Get<Player>(player).Frostbite++;
        if (world.Has<Pledge>(card)) structuralCommands.Remove<Pledge>(card);
        events.CardPlayed.Publish(new CardPlayedEvent(card, player, deck, paymentCards.Length, validation.AlternatePlay.IsApplicable ? (byte)1 : (byte)0));
        return validation;
    }

    public CardPlayValidation TryPlayAuto(
        EntityId player,
        EntityId deck,
        EntityId card,
        ReadOnlySpan<EntityId> paymentCandidates,
        ReadOnlySpan<AlternatePlayResult> alternateResults,
        in CardHandlerInput resolveInput,
        RuleCommandBuffer ruleCommands,
        CommandBuffer structuralCommands,
        Span<RuleHandlerResult> resultStorage,
        ref RuleResultWriterState resultState)
    {
        CardPlayValidation validation = CardPlayRules.Validate(
            world,
            player,
            deck,
            card,
            paymentCandidates,
            alternateResults);
        if (!validation.Allowed)
        {
            events.CantPlay.Publish(new CantPlayCardMessage(card, validation.Failure, 0));
            return validation;
        }

        ref CardData cardData = ref world.Get<CardData>(card);
        Span<EntityId> selected = stackalloc EntityId[4];
        if (!CardCostRules.TrySelectPayment(
                world,
                in cardData,
                paymentCandidates,
                card,
                deck,
                selected,
                out int selectedCount))
        {
            events.CantPlay.Publish(new CantPlayCardMessage(card, CardPaymentFailure.InsufficientPaymentCards, 0));
            return new CardPlayValidation(
                false,
                CardPaymentFailure.InsufficientPaymentCards,
                validation.AlternatePlay,
                cardData.CostCount);
        }

        return TryPlay(
            player,
            deck,
            card,
            selected[..selectedCount],
            alternateResults,
            in resolveInput,
            ruleCommands,
            structuralCommands,
            resultStorage,
            ref resultState);
    }
}

public static class CardRuleCommandExecutor
{
    public static int Execute(World world, ReadOnlySpan<RuleCommand> commands, CommandBuffer structural, CardGameplayEventHub events, ref RuleRandomState randomState)
    {
        int handled = 0;
        for (var index = 0; index < commands.Length; index++)
        {
            ref readonly RuleCommand command = ref commands[index];
            RuleCommandPayload payload = command.Payload;
            switch (command.Kind)
            {
                case RuleCommandKind.MoveCard:
                    CardZoneRuleCommand move = payload.CardZone;
                    if (CardZoneOperations.Move(world, move.Deck, move.Card, move.SourceZone, move.DestinationZone, move.DestinationIndex, CardMoveReason.RuleCommand, events)) handled++;
                    break;
                case RuleCommandKind.SpawnCard:
                    SpawnCardRuleCommand spawn = payload.SpawnCard;
                    for (var count = 0; count < spawn.Count; count++)
                        CardGameplayFactory.RecordCardSpawn(
                            structural,
                            spawn.Deck,
                            spawn.Card,
                            spawn.DestinationZone,
                            spawn.IsUpgraded,
                            spawn.DestinationIndex,
                            spawn.Color);
                    handled++;
                    break;
                case RuleCommandKind.RandomCardZone:
                    RandomCardZoneRuleCommand randomZone = payload.RandomCardZone;
                    if (ExecuteRandomZone(world, in randomZone, events, ref randomState) > 0) handled++;
                    break;
                case RuleCommandKind.RemovePledge:
                    EntityId pledged = payload.RemovePledge.Card;
                    if (world.IsAlive(pledged) && world.Has<Pledge>(pledged)) structural.Remove<Pledge>(pledged);
                    handled++;
                    break;
                case RuleCommandKind.MutateCard:
                    CardMutationRuleCommand mutation = payload.CardMutation;
                    ExecuteMutation(world, in mutation, structural);
                    handled++;
                    break;
            }
        }
        return handled;
    }

    private static int ExecuteRandomZone(World world, in RandomCardZoneRuleCommand command, CardGameplayEventHub events, ref RuleRandomState randomState)
    {
        int moved = 0;
        var random = new DeterministicRuleRandom(ref randomState);
        while (moved < command.Count)
        {
            int count = CardZoneOperations.Count(world, command.Deck, command.SourceZone);
            if (count == 0) break;
            int eligible = CountMatching(world, command.Deck, command.SourceZone, command.Filter, count);
            if (eligible == 0) break;
            int selected = command.Operation is RandomCardZoneOperation.MoveTop or RandomCardZoneOperation.Mill
                ? 0
                : random.NextInt(eligible);
            int sourceIndex = FindMatchingIndex(world, command.Deck, command.SourceZone, command.Filter, count, selected);
            EntityId card = CardZoneOperations.At(world, command.Deck, command.SourceZone, sourceIndex);
            if (CardZoneOperations.Move(world, command.Deck, card, command.SourceZone, command.DestinationZone, command.DestinationIndex, CardMoveReason.RuleCommand, events)) moved++;
        }
        if (command.Operation == RandomCardZoneOperation.ShuffleInto && moved > 0 && command.DestinationZone == CardZone.DrawPile) CardZoneOperations.ShuffleDrawPile(world, command.Deck);
        return moved;
    }

    private static int CountMatching(World world, EntityId deck, CardZone zone, RuleCardFilter filter, int count)
    {
        var result = 0;
        for (var index = 0; index < count; index++)
            if (MatchesFilter(world, CardZoneOperations.At(world, deck, zone, index), filter)) result++;
        return result;
    }

    private static int FindMatchingIndex(World world, EntityId deck, CardZone zone, RuleCardFilter filter, int count, int selected)
    {
        for (var index = 0; index < count; index++)
            if (MatchesFilter(world, CardZoneOperations.At(world, deck, zone, index), filter) && selected-- == 0) return index;
        return -1;
    }

    private static bool MatchesFilter(World world, EntityId card, RuleCardFilter filter)
    {
        ref CardData data = ref world.Get<CardData>(card);
        if ((filter & RuleCardFilter.ExcludeColorless) != 0 && world.Has<Colorless>(card)) return false;
        if ((filter & RuleCardFilter.ExcludePledged) != 0 && world.Has<Pledge>(card)) return false;
        if ((filter & RuleCardFilter.ExcludeWeapon) != 0 && data.IsWeapon) return false;
        if ((filter & RuleCardFilter.Sealed) != 0 && !world.Has<Sealed>(card)) return false;
        RuleCardFilter colors = filter & (RuleCardFilter.Red | RuleCardFilter.White | RuleCardFilter.Black);
        if (colors == 0) return true;
        if (world.Has<Colorless>(card)) return false;
        return (data.RuntimeColor == RuleCardColor.Red && (colors & RuleCardFilter.Red) != 0)
            || (data.RuntimeColor == RuleCardColor.White && (colors & RuleCardFilter.White) != 0)
            || (data.RuntimeColor == RuleCardColor.Black && (colors & RuleCardFilter.Black) != 0);
    }

    private static void ExecuteMutation(World world, in CardMutationRuleCommand mutation, CommandBuffer structural)
    {
        if (!world.IsAlive(mutation.Card) || !world.TryGet(mutation.Card, out CardData data)) return;
        switch (mutation.Kind)
        {
            case CardMutationKind.SetColor: data.RuntimeColor = mutation.Color; break;
            case CardMutationKind.ModifyBlock: data.Block += mutation.Amount; break;
            case CardMutationKind.SetBlock: data.Block = mutation.Amount; break;
            case CardMutationKind.ModifyCost: data.CostCount = checked((byte)Math.Max(0, data.CostCount + mutation.Amount)); break;
            case CardMutationKind.SetCost: data.CostCount = checked((byte)Math.Max(0, mutation.Amount)); break;
            case CardMutationKind.SetUpgraded: if (mutation.Amount != 0) data.Flags |= CardRuntimeFlags.Upgraded; else data.Flags &= ~CardRuntimeFlags.Upgraded; break;
            case CardMutationKind.SetExhaust: if (mutation.Amount != 0) data.Flags |= CardRuntimeFlags.ExhaustsOnEndTurn; else data.Flags &= ~CardRuntimeFlags.ExhaustsOnEndTurn; break;
            case CardMutationKind.SetFreeAction: if (mutation.Amount != 0) data.Flags |= CardRuntimeFlags.FreeAction; else data.Flags &= ~CardRuntimeFlags.FreeAction; break;
            case CardMutationKind.SetWeapon: if (mutation.Amount != 0) data.Flags |= CardRuntimeFlags.Weapon; else data.Flags &= ~CardRuntimeFlags.Weapon; break;
            case CardMutationKind.ModifyDamage: data.Damage += mutation.Amount; break;
            case CardMutationKind.SetDamage: data.Damage = mutation.Amount; break;
            case CardMutationKind.SetType: data.Type = mutation.CardType; break;
        }
        world.Set(mutation.Card, data);
    }
}

public sealed class PledgeManagementSystem : CardGameplaySystem
{
    private readonly World world;
    private readonly CardGameplayEventHub events;
    public PledgeManagementSystem(World world, CardGameplayEventHub events) : base(CardGameplaySystemIds.PledgeManagement, nameof(PledgeManagementSystem), SystemPhase.Gameplay) { this.world = world; this.events = events; }
    public PledgeFailure TryPledge(EntityId deck, EntityId card, ref PledgeAvailabilityState state, CommandBuffer commands)
    {
        PledgeFailure failure = PledgeRules.Evaluate(world, deck, card, in state);
        if (failure != PledgeFailure.None) return failure;
        commands.Add(card, new Pledge { CanPlay = 0 });
        state.PledgedThisActionPhase = 1;
        events.PledgeAdded.Publish(new PledgeAddedEvent(card));
        return PledgeFailure.None;
    }
    public void UnlockHand(EntityId deck) { ref Deck d = ref world.Get<Deck>(deck); ReadOnlySpan<HandCard> hand = world.GetDynamicBuffer<HandCard>(d.Hand).AsReadOnlySpan(); for (var i = 0; i < hand.Length; i++) if (world.Has<Pledge>(hand[i].Card)) world.Get<Pledge>(hand[i].Card).CanPlay = 1; }
}

public sealed class RecoilManagementSystem : CardGameplaySystem
{
    private readonly World world;
    public RecoilManagementSystem(World world) : base(CardGameplaySystemIds.RecoilManagement, nameof(RecoilManagementSystem), SystemPhase.Gameplay) => this.world = world;
    public EntityId Apply(EntityId deck, int stacks, CommandBuffer commands)
    {
        ref Deck d = ref world.Get<Deck>(deck); ReadOnlySpan<HandCard> hand = world.GetDynamicBuffer<HandCard>(d.Hand).AsReadOnlySpan();
        Span<EntityId> candidates = hand.Length <= 64 ? stackalloc EntityId[hand.Length] : throw new InvalidOperationException("Hand capacity exceeded.");
        int count = 0; for (var i = 0; i < hand.Length; i++) if (!world.Has<Recoil>(hand[i].Card)) candidates[count++] = hand[i].Card;
        if (count == 0) return EntityId.Null;
        var random = new DeterministicRuleRandom(ref d.Random); EntityId selected = candidates[random.NextInt(count)]; commands.Add(selected, new Recoil { Stacks = stacks }); return selected;
    }
    public int Resolve(CommandBuffer commands) { int damage = 0; foreach (QueryChunk<Recoil> chunk in world.Query<Recoil>()) foreach (int row in chunk.Rows) { damage += chunk.Component1[row].Stacks; commands.Remove<Recoil>(chunk.Entities[row]); } return damage; }
}

public sealed class SealManagementSystem : CardGameplaySystem
{
    private readonly World world;
    public SealManagementSystem(World world) : base(CardGameplaySystemIds.SealManagement, nameof(SealManagementSystem), SystemPhase.Gameplay) => this.world = world;
    public EntityId Seal(EntityId deck, SealTarget target, int amount, CommandBuffer commands)
    {
        CardZone zone = target == SealTarget.Hand ? CardZone.Hand : CardZone.DrawPile; int count = CardZoneOperations.Count(world, deck, zone); if (count == 0) return EntityId.Null;
        ref Deck d = ref world.Get<Deck>(deck); var random = new DeterministicRuleRandom(ref d.Random); int start = target == SealTarget.Hand ? random.NextInt(count) : 0;
        for (var step = 0; step < count; step++) { EntityId card = CardZoneOperations.At(world, deck, zone, (start + step) % count); if (world.Has<Pledge>(card) || world.Get<CardData>(card).IsWeapon) continue; if (world.Has<Sealed>(card)) world.Get<Sealed>(card).Seals += amount; else commands.Add(card, new Sealed { Seals = amount }); return card; }
        return EntityId.Null;
    }
    public void ModifyAll(EntityId deck, int delta, CommandBuffer commands) { foreach (QueryChunk<Sealed> chunk in world.Query<Sealed>()) foreach (int row in chunk.Rows) { EntityId card = chunk.Entities[row]; if (world.Get<CardData>(card).Deck != deck) continue; int seals = Math.Max(0, chunk.Component1[row].Seals + delta); if (seals == 0) commands.Remove<Sealed>(card); else chunk.Component1[row].Seals = seals; } }
}

public sealed class CursedManagementSystem : CardGameplaySystem
{
    private readonly World world;
    public CursedManagementSystem(World world) : base(CardGameplaySystemIds.CursedManagement, nameof(CursedManagementSystem), SystemPhase.Gameplay) => this.world = world;
    public bool Apply(EntityId card, CommandBuffer commands) { if (!world.TryGet(card, out CardData data) || data.IsWeapon || world.Has<Cursed>(card)) return false; commands.Add(card, new CursedOriginalCard { Definition = data.Definition, Color = data.RuntimeColor, Flags = data.Flags }); bool upgraded = data.IsUpgraded; data.Definition = Crusaders30XX.ECS.Data.Ids.CardId.Curse; CardData curse = CardGameplayFactory.BuildCardData(data.Deck, data.Definition, upgraded, data.RuntimeColor); curse.PrintedDefinition = data.PrintedDefinition; commands.Set(card, curse); commands.AddTag<Cursed>(card); return true; }
    public bool Remove(EntityId card, CommandBuffer commands) { if (!world.Has<Cursed>(card) || !world.TryGet(card, out CursedOriginalCard original)) return false; CardData restored = CardGameplayFactory.BuildCardData(world.Get<CardData>(card).Deck, original.Definition, (original.Flags & CardRuntimeFlags.Upgraded) != 0, original.Color); commands.Set(card, restored); commands.Remove<CursedOriginalCard>(card); commands.RemoveTag<Cursed>(card); return true; }
}

public sealed class PlunderManagementSystem : CardGameplaySystem
{
    private readonly World world;
    public PlunderManagementSystem(World world) : base(CardGameplaySystemIds.PlunderManagement, nameof(PlunderManagementSystem), SystemPhase.Gameplay) => this.world = world;
    public EntityId Plunder(EntityId deck, CommandBuffer commands) { ref Deck d = ref world.Get<Deck>(deck); int count = CardZoneOperations.Count(world, deck, CardZone.DrawPile); if (count == 0) return EntityId.Null; var random = new DeterministicRuleRandom(ref d.Random); for (var attempt = 0; attempt < count; attempt++) { EntityId card = CardZoneOperations.At(world, deck, CardZone.DrawPile, random.NextInt(count)); if (world.Get<CardData>(card).IsWeapon || world.Has<Plundered>(card)) continue; CardZoneOperations.Move(world, deck, card, CardZone.DrawPile, CardZone.Removed, -1, CardMoveReason.Plunder); commands.Add(card, new Plundered { DamageThreshold = 4 + random.NextInt(5) }); return card; } return EntityId.Null; }
    public bool ApplyDamage(EntityId card, int damage, CommandBuffer commands) { if (!world.TryGet(card, out Plundered p)) return false; p.DamageDealt += Math.Max(0, damage); if (p.DamageDealt < p.DamageThreshold) { world.Set(card, p); return false; } EntityId deck = world.Get<CardData>(card).Deck; CardZoneOperations.Insert(world, deck, CardZone.Hand, card); ref CardZoneLocation l = ref world.Get<CardZoneLocation>(card); l.Zone = CardZone.Hand; CardZoneOperations.RefreshIndices(world, deck, CardZone.Hand); commands.Remove<Plundered>(card); return true; }
}

public sealed class ShackleManagementSystem : CardGameplaySystem
{
    private readonly World world;
    public ShackleManagementSystem(World world) : base(CardGameplaySystemIds.ShackleManagement, nameof(ShackleManagementSystem), SystemPhase.Gameplay) => this.world = world;
    public int Apply(EntityId deck, int requested, CommandBuffer commands) { ref Deck d = ref world.Get<Deck>(deck); ReadOnlySpan<HandCard> hand = world.GetDynamicBuffer<HandCard>(d.Hand).AsReadOnlySpan(); Span<EntityId> candidates = hand.Length <= 64 ? stackalloc EntityId[hand.Length] : throw new InvalidOperationException("Hand capacity exceeded."); int candidateCount = 0; for (var i = 0; i < hand.Length; i++) if (!world.Has<Intimidated>(hand[i].Card) && !world.Has<Shackle>(hand[i].Card)) candidates[candidateCount++] = hand[i].Card; var random = new DeterministicRuleRandom(ref d.Random); int added = 0; while (candidateCount > 0 && added < requested) { int selected = random.NextInt(candidateCount); EntityId card = candidates[selected]; candidates[selected] = candidates[--candidateCount]; commands.Add(card, new Shackle { Group = 1 }); added++; } return added; }
}

public sealed class MarkedForExhaustSystem : CardGameplaySystem
{
    private readonly World world; private readonly CardGameplayEventHub events;
    public MarkedForExhaustSystem(World world, CardGameplayEventHub events) : base(CardGameplaySystemIds.MarkedForExhaust, nameof(MarkedForExhaustSystem), SystemPhase.Gameplay) { this.world = world; this.events = events; }
    public int Resolve(EntityId deck) { int moved = 0; for (int i = CardZoneOperations.Count(world, deck, CardZone.Hand) - 1; i >= 0; i--) { EntityId card = CardZoneOperations.At(world, deck, CardZone.Hand, i); if (!world.Has<MarkedForExhaust>(card) && (world.Get<CardData>(card).Flags & CardRuntimeFlags.ExhaustsOnEndTurn) == 0) continue; if (CardZoneOperations.Move(world, deck, card, CardZone.Hand, CardZone.ExhaustPile, -1, CardMoveReason.Exhaust, events)) moved++; } return moved; }
}

public sealed class MillCardSystem : CardGameplaySystem
{
    private readonly World world; private readonly CardGameplayEventHub events;
    public MillCardSystem(World world, CardGameplayEventHub events) : base(CardGameplaySystemIds.MillCard, nameof(MillCardSystem), SystemPhase.Gameplay) { this.world = world; this.events = events; }
    public EntityId Mill(EntityId deck) { if (CardZoneOperations.Count(world, deck, CardZone.DrawPile) == 0) return EntityId.Null; EntityId card = CardZoneOperations.At(world, deck, CardZone.DrawPile, 0); CardZoneOperations.Move(world, deck, card, CardZone.DrawPile, CardZone.DiscardPile, -1, CardMoveReason.Mill, events); world.Get<Deck>(deck).CardsMilled++; events.TopCardRemovedForMill.Publish(new TopCardRemovedForMillEvent(deck, card)); return card; }
}

public sealed class AssignedBlocksToDiscardSystem : CardGameplaySystem { public AssignedBlocksToDiscardSystem() : base(CardGameplaySystemIds.AssignedBlocksToDiscard, nameof(AssignedBlocksToDiscardSystem), SystemPhase.Gameplay) { } }
public sealed class DrawHandSystem : CardGameplaySystem { public DrawHandSystem() : base(CardGameplaySystemIds.DrawHand, nameof(DrawHandSystem), SystemPhase.Gameplay) { } public static int CalculateCardsToDraw(int maximum, ReadOnlySpan<HandCard> hand, World world) { int count = 0; for (var i = 0; i < hand.Length; i++) { EntityId card = hand[i].Card; CardData data = world.Get<CardData>(card); if (!data.IsWeapon && !data.IsToken && !world.Has<Pledge>(card)) count++; } return Math.Max(0, maximum - count); } }
public sealed class CanPlayCardHighlightSystem : CardGameplaySystem { public CanPlayCardHighlightSystem() : base(CardGameplaySystemIds.CanPlayHighlight, nameof(CanPlayCardHighlightSystem), SystemPhase.Presentation) { } }
public sealed class CantPlayCardMessageSystem : CardGameplaySystem { public CantPlayCardMessageSystem() : base(CardGameplaySystemIds.CantPlayMessage, nameof(CantPlayCardMessageSystem), SystemPhase.Presentation) { } }
public sealed class CardApplicationManagementSystem : CardGameplaySystem { public CardApplicationManagementSystem() : base(CardGameplaySystemIds.CardApplicationManagement, nameof(CardApplicationManagementSystem), SystemPhase.Gameplay) { } }
public sealed class CardHoverDetectionSystem : CardGameplaySystem { public CardHoverDetectionSystem() : base(CardGameplaySystemIds.CardHoverDetection, nameof(CardHoverDetectionSystem), SystemPhase.Interaction) { } }
public sealed class DeckEmptyDeathCheckSystem : CardGameplaySystem { public DeckEmptyDeathCheckSystem() : base(CardGameplaySystemIds.DeckEmptyDeathCheck, nameof(DeckEmptyDeathCheckSystem), SystemPhase.Gameplay) { } public static bool IsDead(World world, EntityId deck) => CardZoneOperations.Count(world, deck, CardZone.DrawPile) == 0 && CardZoneOperations.Count(world, deck, CardZone.Hand) == 0 && CardZoneOperations.Count(world, deck, CardZone.DiscardPile) == 0; }
public sealed class DiscardSpecificCardHighlightSystem : CardGameplaySystem { public DiscardSpecificCardHighlightSystem() : base(CardGameplaySystemIds.DiscardSpecificHighlight, nameof(DiscardSpecificCardHighlightSystem), SystemPhase.Presentation) { } }
public sealed class HandBlockInteractionSystem : CardGameplaySystem { public HandBlockInteractionSystem() : base(CardGameplaySystemIds.HandBlockInteraction, nameof(HandBlockInteractionSystem), SystemPhase.Interaction) { } }
public sealed class HandCardBoundsLateSystem : CardGameplaySystem { public HandCardBoundsLateSystem() : base(CardGameplaySystemIds.HandCardBoundsLate, nameof(HandCardBoundsLateSystem), SystemPhase.LatePresentation) { } }
public sealed class MarkedForSpecificDiscardSystem : CardGameplaySystem { public MarkedForSpecificDiscardSystem() : base(CardGameplaySystemIds.MarkedForSpecificDiscard, nameof(MarkedForSpecificDiscardSystem), SystemPhase.Gameplay) { } }
public static class BattlePileInputRules
{
    public const int DrawPileTitleId = 43401;
    public const int DiscardPileTitleId = 43402;
    public const int DisplayOnlyContext = 43410;

    public static bool IsDrawPileVisible(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return !TryGetActiveTutorial(world, out GuidedTutorial tutorial) || tutorial.Section == 8;
    }

    public static bool IsDiscardPileVisible(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        return !TryGetActiveTutorial(world, out _);
    }

    public static bool IsBattleInputFrozen(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        Query<BattleInfo, BattleStateInfo, PhaseState> battles = world.Query<BattleInfo, BattleStateInfo, PhaseState>();
        foreach (QueryChunk<BattleInfo, BattleStateInfo, PhaseState> chunk in battles)
        foreach (int row in chunk.Rows)
        {
            ref readonly BattleInfo info = ref chunk.Component1[row];
            ref readonly BattleStateInfo state = ref chunk.Component2[row];
            CombatPhase phase = chunk.Component3[row].Current;
            const CombatFlags frozenFlags = CombatFlags.AwaitingPresentation |
                CombatFlags.PhaseTransitionPending |
                CombatFlags.EnemyDefeated |
                CombatFlags.PlayerDefeated;
            if ((state.Flags & frozenFlags) != 0 ||
                phase is CombatPhase.Victory or CombatPhase.Defeat or CombatPhase.PhaseTransition)
                return true;
            if (!info.Enemy.IsNull && world.TryGet(info.Enemy, out HP hp) && hp.Current <= 0)
                return true;
        }
        return false;
    }

    private static bool TryGetActiveTutorial(World world, out GuidedTutorial tutorial)
    {
        Query<GuidedTutorial> tutorials = world.Query<GuidedTutorial>();
        foreach (QueryChunk<GuidedTutorial> chunk in tutorials)
        foreach (int row in chunk.Rows)
        {
            tutorial = chunk.Component1[row];
            if (tutorial.State is TutorialState.Inactive or TutorialState.Running) return true;
        }
        tutorial = default;
        return false;
    }
}

public sealed class BattlePileInputSystem : CardGameplaySystem
{
    private readonly World world;
    private readonly CardGameplayEventHub events;
    private readonly EntityId modal;
    private readonly Query<Deck> decks;
    private readonly Query<UIElement> elements;
    private readonly Query<GuidedTutorial> tutorials;
    private readonly Query<BattleInfo, BattleStateInfo, PhaseState> battles;

    public BattlePileInputSystem(World world, CardGameplayEventHub events, EntityId modal)
        : base(
            CardGameplaySystemIds.BattlePileInput,
            nameof(BattlePileInputSystem),
            SystemPhase.Interaction,
            EventBarrier.AfterSystem,
            readComponents: ComponentSignature.Empty
                .With(ComponentType<PlayerInputState>.Id)
                .With(ComponentType<Deck>.Id)
                .With(ComponentType<CardListModal>.Id)
                .With(ComponentType<GuidedTutorial>.Id)
                .With(ComponentType<BattleInfo>.Id)
                .With(ComponentType<BattleStateInfo>.Id)
                .With(ComponentType<PhaseState>.Id)
                .With(ComponentType<UIElement>.Id)
                .With(ComponentType<HP>.Id),
            emittedEventTypeIds:
            [
                CardGameplayEventTypeIds.OpenCardListModal,
                CardGameplayEventTypeIds.CloseCardListModal,
            ],
            runsAfter: [GlobalUiSystemIds.UIInteraction, GlobalUiSystemIds.HotKey])
    {
        this.world = world;
        this.events = events;
        this.modal = modal;
        decks = world.Query<Deck>(new QueryFilter(DebugName: "ECS052.BattlePileDeck"));
        elements = world.Query<UIElement>(new QueryFilter(DebugName: "ECS052.CardListCloseInput"));
        tutorials = world.Query<GuidedTutorial>(new QueryFilter(DebugName: "ECS052.BattlePileTutorial"));
        battles = world.Query<BattleInfo, BattleStateInfo, PhaseState>(
            new QueryFilter(DebugName: "ECS052.BattlePileGate"));
    }

    public override void Update(ref SystemContext context)
    {
        if (!world.IsAlive(modal) || !world.Has<CardListModal>(modal)) return;
        ref readonly PlayerInputState input = ref world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        PlayerInputFrame frame = input.Frame;
        if (!input.IsInputEnabled || !frame.IsWindowActive ||
            frame.Device != PlayerInputDevice.Gamepad || IsBattleInputFrozen())
            return;

        foreach (QueryChunk<UIElement> chunk in elements)
        foreach (int row in chunk.Rows)
        {
            ref readonly UIElement element = ref chunk.Component1[row];
            if ((element.Flags & UIInteractionFlags.Clicked) != 0 &&
                element.EventType == UIElementEventType.CardListModalClose)
            {
                events.CloseCardListModal.Publish(new(modal));
                break;
            }
        }

        if (frame.WasPressed(PlayerInputButton.LeftShoulder))
            Toggle(CardZone.DiscardPile, IsPileVisible(CardZone.DiscardPile));
        if (frame.WasPressed(PlayerInputButton.RightShoulder))
            Toggle(CardZone.DrawPile, IsPileVisible(CardZone.DrawPile));
    }

    private void Toggle(CardZone zone, bool visible)
    {
        if (!visible) return;
        ref readonly CardListModal state = ref world.Get<CardListModal>(modal);
        if (state.IsOpen != 0)
        {
            if (state.SourceZone is not (CardZone.DrawPile or CardZone.DiscardPile)) return;
            events.CloseCardListModal.Publish(new(modal));
            if (state.SourceZone == zone) return;
        }

        EntityId deck = ResolveDeck();
        if (!deck.IsNull)
            events.OpenCardListModal.Publish(new(modal, deck, zone, BattlePileInputRules.DisplayOnlyContext));
    }

    private EntityId ResolveDeck()
    {
        foreach (QueryChunk<Deck> chunk in decks)
        foreach (int row in chunk.Rows)
            return chunk.Entities[row];
        return EntityId.Null;
    }

    private bool IsPileVisible(CardZone zone)
    {
        foreach (QueryChunk<GuidedTutorial> chunk in tutorials)
        foreach (int row in chunk.Rows)
        {
            ref readonly GuidedTutorial tutorial = ref chunk.Component1[row];
            if (tutorial.State is not (TutorialState.Inactive or TutorialState.Running)) continue;
            return zone == CardZone.DrawPile && tutorial.Section == 8;
        }
        return true;
    }

    private bool IsBattleInputFrozen()
    {
        foreach (QueryChunk<BattleInfo, BattleStateInfo, PhaseState> chunk in battles)
        foreach (int row in chunk.Rows)
        {
            ref readonly BattleInfo info = ref chunk.Component1[row];
            ref readonly BattleStateInfo state = ref chunk.Component2[row];
            CombatPhase phase = chunk.Component3[row].Current;
            const CombatFlags frozenFlags = CombatFlags.AwaitingPresentation |
                CombatFlags.PhaseTransitionPending |
                CombatFlags.EnemyDefeated |
                CombatFlags.PlayerDefeated;
            if ((state.Flags & frozenFlags) != 0 ||
                phase is CombatPhase.Victory or CombatPhase.Defeat or CombatPhase.PhaseTransition)
                return true;
            if (!info.Enemy.IsNull && world.TryGet(info.Enemy, out HP hp) && hp.Current <= 0)
                return true;
        }
        return false;
    }
}

public sealed class CardListModalSystem : CardGameplaySystem,
    IEventConsumer<OpenCardListModalEvent>, IEventConsumer<CloseCardListModalEvent>
{
    private readonly World world;

    public CardListModalSystem(World world)
        : base(
            CardGameplaySystemIds.CardListModal,
            nameof(CardListModalSystem),
            SystemPhase.Interaction,
            writeComponents: ComponentSignature.Empty.With(ComponentType<CardListModal>.Id),
            readDynamicBufferTypes:
            [
                typeof(DrawPileCard), typeof(DiscardPileCard), typeof(HandCard),
                typeof(ExhaustPileCard),
            ],
            writeDynamicBufferTypes: [typeof(ModalCardEntry)],
            consumedEventTypeIds:
            [
                CardGameplayEventTypeIds.OpenCardListModal,
                CardGameplayEventTypeIds.CloseCardListModal,
            ])
    {
        this.world = world;
        Modal = CreateModal(world);
    }

    public EntityId Modal { get; }

    public void Consume(in OpenCardListModalEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Modal, out CardListModal modal) || !world.Has<Deck>(value.Deck)) return;
        DynamicBuffer<ModalCardEntry> cards = world.GetDynamicBuffer(modal.Cards);
        cards.Clear();
        int count = CardZoneOperations.Count(world, value.Deck, value.Zone);
        for (var index = 0; index < count; index++)
            cards.Add(new(CardZoneOperations.At(world, value.Deck, value.Zone, index), value.Context));
        modal.Deck = value.Deck;
        modal.SourceZone = value.Zone;
        modal.TitleId = value.Zone == CardZone.DrawPile
            ? BattlePileInputRules.DrawPileTitleId
            : BattlePileInputRules.DiscardPileTitleId;
        modal.Context = value.Context;
        modal.IsOpen = 1;
        world.Set(value.Modal, in modal);
    }

    public void Consume(in CloseCardListModalEvent value, ref EventDispatchContext context)
    {
        if (!world.TryGet(value.Modal, out CardListModal modal)) return;
        modal.IsOpen = 0;
        world.Set(value.Modal, in modal);
    }

    private static EntityId CreateModal(World world)
    {
        EntityId modal = world.Create(default);
        DynamicBufferHandle<ModalCardEntry> cards = world.CreateDynamicBuffer<ModalCardEntry>(modal, 32);
        world.Add(modal, new CardListModal { Cards = cards, SourceZone = CardZone.None });
        return modal;
    }
}
public sealed class CardShaderCompositorSystem : CardGameplaySystem { public CardShaderCompositorSystem() : base(CardGameplaySystemIds.CardShaderCompositor, nameof(CardShaderCompositorSystem), SystemPhase.RenderExtraction) { } }
public sealed class CardUsageTrackingSystem : CardGameplaySystem { public CardUsageTrackingSystem() : base(CardGameplaySystemIds.CardUsageTracking, nameof(CardUsageTrackingSystem), SystemPhase.Gameplay) { } }
