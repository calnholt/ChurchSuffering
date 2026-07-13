#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Cards;

public enum CardApplicationKind : byte { None, Brittle, Cursed, Frozen, Scorched, Thorned, Colorless }
public enum CardApplicationTarget : byte { ExactCard, Hand, DrawPile, DiscardPile, AllZones }
public enum SealTarget : byte { Hand, TopOfDrawPile }
public enum CardMoveReason : byte { None, Draw, Play, Payment, Block, Mill, Exhaust, EndTurn, Pledge, Plunder, Reset, RuleCommand }

public readonly record struct AlertEvent(int MessageId);
public readonly record struct ChangeBattleLocationEvent(int LocationId);
public readonly record struct ApplyCardApplicationEvent(EntityId Card, EntityId Deck, CardApplicationKind Type, CardApplicationTarget Target, int Amount);
public readonly record struct ApplyRecoilEvent(EntityId Deck, int Amount);
public readonly record struct CantPlayCardMessage(EntityId Card, CardPaymentFailure Failure, int MessageId);
public readonly record struct CardDiscardedForCostEvent(EntityId Card, EntityId CardPlayed, EntityId Deck);
public readonly record struct CardInHandHoveredEvent(EntityId Card, byte IsHovered);
public readonly record struct CardListModalCardSelectedEvent(EntityId Modal, EntityId Card, int Context);
public readonly record struct CardListSelectionContexts(EntityId Modal, int Context, int Minimum, int Maximum);
public readonly record struct CardMoved(EntityId Card, EntityId Deck, CardZone From, CardZone To, int FromIndex, int ToIndex, CardMoveReason Reason);
public readonly record struct CardPlayedEvent(EntityId Card, EntityId Player, EntityId Deck, int PaymentCount, byte WasAlternatePlay);
public readonly record struct CardShaderPassEvent(EntityId Card, int Pass, float Progress);
public readonly record struct CardUpgradeConfirmedEvent(EntityId Card);
public readonly record struct CardsDrawnEvent(EntityId Deck, int Requested, int Drawn);
public readonly record struct CloseCardListModalEvent(EntityId Modal);
public readonly record struct ClosePayCostOverlayEvent(EntityId Overlay, byte Cancelled);
public readonly record struct DebugCommandEvent(int Command, int Value);
public readonly record struct DeckShuffleDrawEvent(EntityId Deck, int Count);
public readonly record struct DeckShuffleEvent(EntityId Deck);
public readonly record struct DiscardAllCardsEvent(EntityId Deck, CardMoveReason Reason);
public readonly record struct DiscardMarkedForSpecificDiscardEvent(EntityId Deck);
public readonly record struct DrawPileEmptyEvent(EntityId Deck);
public readonly record struct DrawRandomCardFromDiscardEvent(EntityId Deck, int Count);
public readonly record struct EndTurnDisplayEvent(int Turn);
public readonly record struct HandLayoutEvent(EntityId Deck);
public readonly record struct HotKeyHoldCompletedEvent(EntityId Entity, int Key);
public readonly record struct IntimidateEvent(EntityId Deck, int Count);
public readonly record struct MarkedForSpecificDiscardEvent(EntityId Card, byte Marked);
public readonly record struct MillCardEvent(EntityId Deck, int Count);
public readonly record struct ModifyActionPointsEvent(EntityId Player, int Delta);
public readonly record struct ModifyCourageEvent(EntityId Player, int Delta);
public readonly record struct ModifySealsEvent(EntityId Deck, int Delta);
public readonly record struct OpenCardListModalEvent(EntityId Modal, EntityId Deck, CardZone Zone, int Context);
public readonly record struct OpenPayCostOverlayEvent(EntityId Overlay, EntityId Card, EntityId Deck);
public readonly record struct PayCostCandidateClicked(EntityId Overlay, EntityId Card);
public readonly record struct PayCostSatisfied(EntityId Overlay, EntityId Card, EntityId Deck);
public readonly record struct PledgeAddedEvent(EntityId Card);
public readonly record struct RedrawHandEvent(EntityId Deck, int DrawCount);
public readonly record struct RemoveCardApplication(EntityId Card, CardApplicationKind Type);
public readonly record struct RemoveCardApplications(EntityId Deck, CardApplicationKind Type, CardApplicationTarget Target, int Amount);
public readonly record struct RemoveRandomCardEvent(EntityId Deck, int Count, byte StartersOnly);
public readonly record struct ResetDeckEvent(EntityId Deck);
public readonly record struct SealCardsEvent(EntityId Deck, SealTarget Target, int Amount);
public readonly record struct SetActionPointsEvent(EntityId Player, int Amount);
public readonly record struct SetCourageEvent(EntityId Player, int Amount);
public readonly record struct ShackleEvent(EntityId Deck, int Count);
public readonly record struct ShuffleRandomCardsFromDiscardToDrawPileEvent(EntityId Deck, int Count);
public readonly record struct ShuffleSealedIntoDrawPileEvent(EntityId Deck);
public readonly record struct StartOfTurnDrawResolvedEvent(EntityId Deck, int Requested, int Drawn);
public readonly record struct TopCardRemovedForMillEvent(EntityId Deck, EntityId Card);
public readonly record struct CursorStateEvent(float X, float Y, byte Pressed);
public readonly record struct HotKeySelectEvent(EntityId Entity, int Key);
public readonly record struct UIElementHoverEnteredEvent(EntityId Entity);
public readonly record struct PlunderCardEvent(EntityId Card, int DamageThreshold);
public readonly record struct PlunderForceDiscardEvent(EntityId Deck);
public readonly record struct PlunderRescueEvent(EntityId Card);
public readonly record struct PlunderTriggerEvent(EntityId Deck, EntityId Enemy);
public readonly record struct TrackingEvent(int Type, int Delta, EntityId Subject);

/// <summary>
/// Explicit world-owned streams for every ECS-041 card event. The hub only builds route
/// fragments; the application root owns the single endpoint and runtime.
/// </summary>
public sealed class CardGameplayEventHub
{
    public EventStream<AlertEvent> Alerts { get; } = new();
    public EventStream<ChangeBattleLocationEvent> BattleLocations { get; } = new();
    public EventStream<ApplyCardApplicationEvent> ApplyCardApplications { get; } = new();
    public EventStream<ApplyRecoilEvent> ApplyRecoil { get; } = new();
    public EventStream<CantPlayCardMessage> CantPlay { get; } = new();
    public EventStream<CardDiscardedForCostEvent> CardDiscardedForCost { get; } = new();
    public EventStream<CardInHandHoveredEvent> CardHovered { get; } = new();
    public EventStream<CardListModalCardSelectedEvent> ModalCardSelected { get; } = new();
    public EventStream<CardListSelectionContexts> ModalSelectionContexts { get; } = new();
    public EventStream<CardMoved> CardMoves { get; } = new();
    public EventStream<CardPlayedEvent> CardPlayed { get; } = new();
    public EventStream<CardShaderPassEvent> CardShaderPasses { get; } = new();
    public EventStream<CardUpgradeConfirmedEvent> CardUpgrades { get; } = new();
    public EventStream<CardsDrawnEvent> CardsDrawn { get; } = new();
    public EventStream<CloseCardListModalEvent> CloseCardListModal { get; } = new();
    public EventStream<ClosePayCostOverlayEvent> ClosePayCostOverlay { get; } = new();
    public EventStream<DebugCommandEvent> DebugCommands { get; } = new();
    public EventStream<DeckShuffleDrawEvent> DeckShuffleDraw { get; } = new();
    public EventStream<DeckShuffleEvent> DeckShuffle { get; } = new();
    public EventStream<DiscardAllCardsEvent> DiscardAllCards { get; } = new();
    public EventStream<DiscardMarkedForSpecificDiscardEvent> DiscardMarkedForSpecificDiscard { get; } = new();
    public EventStream<DrawPileEmptyEvent> DrawPileEmpty { get; } = new();
    public EventStream<DrawRandomCardFromDiscardEvent> DrawRandomFromDiscard { get; } = new();
    public EventStream<EndTurnDisplayEvent> EndTurnDisplay { get; } = new();
    public EventStream<HandLayoutEvent> HandLayout { get; } = new();
    public EventStream<HotKeyHoldCompletedEvent> HotKeyHoldCompleted { get; } = new();
    public EventStream<IntimidateEvent> Intimidate { get; } = new();
    public EventStream<MarkedForSpecificDiscardEvent> MarkedForSpecificDiscard { get; } = new();
    public EventStream<MillCardEvent> MillCard { get; } = new();
    public EventStream<ModifyActionPointsEvent> ModifyActionPoints { get; } = new();
    public EventStream<ModifyCourageEvent> ModifyCourage { get; } = new();
    public EventStream<ModifySealsEvent> ModifySeals { get; } = new();
    public EventStream<OpenCardListModalEvent> OpenCardListModal { get; } = new();
    public EventStream<OpenPayCostOverlayEvent> OpenPayCostOverlay { get; } = new();
    public EventStream<PayCostCandidateClicked> PayCostCandidateClicked { get; } = new();
    public EventStream<PayCostSatisfied> PayCostSatisfied { get; } = new();
    public EventStream<PledgeAddedEvent> PledgeAdded { get; } = new();
    public EventStream<RedrawHandEvent> RedrawHand { get; } = new();
    public EventStream<RemoveCardApplication> RemoveCardApplication { get; } = new();
    public EventStream<RemoveCardApplications> RemoveCardApplications { get; } = new();
    public EventStream<RemoveRandomCardEvent> RemoveRandomCard { get; } = new();
    public EventStream<ResetDeckEvent> ResetDeck { get; } = new();
    public EventStream<SealCardsEvent> SealCards { get; } = new();
    public EventStream<SetActionPointsEvent> SetActionPoints { get; } = new();
    public EventStream<SetCourageEvent> SetCourage { get; } = new();
    public EventStream<ShackleEvent> Shackle { get; } = new();
    public EventStream<ShuffleRandomCardsFromDiscardToDrawPileEvent> ShuffleRandomFromDiscard { get; } = new();
    public EventStream<ShuffleSealedIntoDrawPileEvent> ShuffleSealedIntoDrawPile { get; } = new();
    public EventStream<StartOfTurnDrawResolvedEvent> StartOfTurnDrawResolved { get; } = new();
    public EventStream<TopCardRemovedForMillEvent> TopCardRemovedForMill { get; } = new();
    public EventStream<CursorStateEvent> CursorState { get; } = new();
    public EventStream<HotKeySelectEvent> HotKeySelect { get; } = new();
    public EventStream<UIElementHoverEnteredEvent> UIElementHoverEntered { get; } = new();
    public EventStream<PlunderCardEvent> Plundered { get; } = new();
    public EventStream<PlunderForceDiscardEvent> PlunderForceDiscard { get; } = new();
    public EventStream<PlunderRescueEvent> PlunderRescued { get; } = new();
    public EventStream<PlunderTriggerEvent> PlunderTrigger { get; } = new();
    public EventStream<TrackingEvent> Tracking { get; } = new();

    public IEventRoute[] BuildRoutes(
        CardGameplayRouteConsumers? consumers = null,
        CardGameplayRouteConsumers? additionalConsumers = null)
    {
        consumers ??= new CardGameplayRouteConsumers();
        return
        [
            Route(CardGameplayEventTypeIds.Alert, nameof(AlertEvent), Alerts, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ChangeBattleLocation, nameof(ChangeBattleLocationEvent), BattleLocations, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ApplyCardApplication, nameof(ApplyCardApplicationEvent), ApplyCardApplications, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ApplyRecoil, nameof(ApplyRecoilEvent), ApplyRecoil, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CantPlay, nameof(CantPlayCardMessage), CantPlay, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardDiscardedForCost, nameof(CardDiscardedForCostEvent), CardDiscardedForCost, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardInHandHovered, nameof(CardInHandHoveredEvent), CardHovered, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardListModalCardSelected, nameof(CardListModalCardSelectedEvent), ModalCardSelected, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardListSelectionContexts, nameof(CardListSelectionContexts), ModalSelectionContexts, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardMoved, nameof(CardMoved), CardMoves, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardPlayed, nameof(CardPlayedEvent), CardPlayed, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardShaderPass, nameof(CardShaderPassEvent), CardShaderPasses, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardUpgradeConfirmed, nameof(CardUpgradeConfirmedEvent), CardUpgrades, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CardsDrawn, nameof(CardsDrawnEvent), CardsDrawn, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CloseCardListModal, nameof(CloseCardListModalEvent), CloseCardListModal, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ClosePayCostOverlay, nameof(ClosePayCostOverlayEvent), ClosePayCostOverlay, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.DebugCommand, nameof(DebugCommandEvent), DebugCommands, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.DeckShuffleDraw, nameof(DeckShuffleDrawEvent), DeckShuffleDraw, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.DeckShuffle, nameof(DeckShuffleEvent), DeckShuffle, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.DiscardAllCards, nameof(DiscardAllCardsEvent), DiscardAllCards, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.DiscardMarkedForSpecificDiscard, nameof(DiscardMarkedForSpecificDiscardEvent), DiscardMarkedForSpecificDiscard, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.DrawPileEmpty, nameof(DrawPileEmptyEvent), DrawPileEmpty, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.DrawRandomCardFromDiscard, nameof(DrawRandomCardFromDiscardEvent), DrawRandomFromDiscard, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.EndTurnDisplay, nameof(EndTurnDisplayEvent), EndTurnDisplay, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.HandLayout, nameof(HandLayoutEvent), HandLayout, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.HotKeyHoldCompleted, nameof(HotKeyHoldCompletedEvent), HotKeyHoldCompleted, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.Intimidate, nameof(IntimidateEvent), Intimidate, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.MarkedForSpecificDiscard, nameof(MarkedForSpecificDiscardEvent), MarkedForSpecificDiscard, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.MillCard, nameof(MillCardEvent), MillCard, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ModifyActionPoints, nameof(ModifyActionPointsEvent), ModifyActionPoints, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ModifyCourage, nameof(ModifyCourageEvent), ModifyCourage, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ModifySeals, nameof(ModifySealsEvent), ModifySeals, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.OpenCardListModal, nameof(OpenCardListModalEvent), OpenCardListModal, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.OpenPayCostOverlay, nameof(OpenPayCostOverlayEvent), OpenPayCostOverlay, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.PayCostCandidateClicked, nameof(PayCostCandidateClicked), PayCostCandidateClicked, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.PayCostSatisfied, nameof(PayCostSatisfied), PayCostSatisfied, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.PledgeAdded, nameof(PledgeAddedEvent), PledgeAdded, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.RedrawHand, nameof(RedrawHandEvent), RedrawHand, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.RemoveCardApplication, nameof(RemoveCardApplication), RemoveCardApplication, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.RemoveCardApplications, nameof(RemoveCardApplications), RemoveCardApplications, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.RemoveRandomCard, nameof(RemoveRandomCardEvent), RemoveRandomCard, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ResetDeck, nameof(ResetDeckEvent), ResetDeck, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.SealCards, nameof(SealCardsEvent), SealCards, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.SetActionPoints, nameof(SetActionPointsEvent), SetActionPoints, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.SetCourage, nameof(SetCourageEvent), SetCourage, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.Shackle, nameof(ShackleEvent), Shackle, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ShuffleRandomFromDiscard, nameof(ShuffleRandomCardsFromDiscardToDrawPileEvent), ShuffleRandomFromDiscard, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.ShuffleSealedIntoDrawPile, nameof(ShuffleSealedIntoDrawPileEvent), ShuffleSealedIntoDrawPile, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.StartOfTurnDrawResolved, nameof(StartOfTurnDrawResolvedEvent), StartOfTurnDrawResolved, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.TopCardRemovedForMill, nameof(TopCardRemovedForMillEvent), TopCardRemovedForMill, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.CursorState, nameof(CursorStateEvent), CursorState, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.HotKeySelect, nameof(HotKeySelectEvent), HotKeySelect, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.UIElementHoverEntered, nameof(UIElementHoverEnteredEvent), UIElementHoverEntered, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.PlunderCard, nameof(PlunderCardEvent), Plundered, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.PlunderForceDiscard, nameof(PlunderForceDiscardEvent), PlunderForceDiscard, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.PlunderRescue, nameof(PlunderRescueEvent), PlunderRescued, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.PlunderTrigger, nameof(PlunderTriggerEvent), PlunderTrigger, consumers, additionalConsumers),
            Route(CardGameplayEventTypeIds.Tracking, nameof(TrackingEvent), Tracking, consumers, additionalConsumers),
        ];
    }

    private static EventRoute<T> Route<T>(
        int id,
        string name,
        EventStream<T> stream,
        CardGameplayRouteConsumers consumers,
        CardGameplayRouteConsumers? additionalConsumers)
        where T : unmanaged
    {
        EventConsumerRegistration<T>[] primary = consumers.Get<T>();
        if (additionalConsumers is null) return new EventRoute<T>(id, name, stream, primary);
        EventConsumerRegistration<T>[] additional = additionalConsumers.Get<T>();
        if (additional.Length == 0) return new EventRoute<T>(id, name, stream, primary);
        var combined = new EventConsumerRegistration<T>[primary.Length + additional.Length];
        primary.CopyTo(combined, 0);
        additional.CopyTo(combined, primary.Length);
        return new EventRoute<T>(id, name, stream, combined);
    }
}

/// <summary>Initialization-only, explicitly typed consumer registrations for card route fragments.</summary>
public sealed class CardGameplayRouteConsumers
{
    private readonly Dictionary<Type, object> registrations = new();

    public CardGameplayRouteConsumers Add<T>(IEventConsumer<T> consumer, int priority = 0, string? name = null)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(consumer);
        Type type = typeof(T);
        if (!registrations.TryGetValue(type, out object? value))
        {
            value = new List<EventConsumerRegistration<T>>();
            registrations.Add(type, value);
        }
        ((List<EventConsumerRegistration<T>>)value).Add(new EventConsumerRegistration<T>(
            priority,
            name ?? consumer.GetType().Name,
            consumer));
        return this;
    }

    internal EventConsumerRegistration<T>[] Get<T>() where T : unmanaged
    {
        if (!registrations.TryGetValue(typeof(T), out object? value)) return [];
        return ((List<EventConsumerRegistration<T>>)value).ToArray();
    }
}

public static class CardGameplayEventTypeIds
{
    public const int First = 41001;
    public const int Alert = First;
    public const int ChangeBattleLocation = First + 1;
    public const int ApplyCardApplication = First + 2;
    public const int ApplyRecoil = First + 3;
    public const int CantPlay = First + 4;
    public const int CardDiscardedForCost = First + 5;
    public const int CardInHandHovered = First + 6;
    public const int CardListModalCardSelected = First + 7;
    public const int CardListSelectionContexts = First + 8;
    public const int CardMoved = First + 9;
    public const int CardPlayed = First + 10;
    public const int CardShaderPass = First + 11;
    public const int CardUpgradeConfirmed = First + 12;
    public const int CardsDrawn = First + 13;
    public const int CloseCardListModal = First + 14;
    public const int ClosePayCostOverlay = First + 15;
    public const int DebugCommand = First + 16;
    public const int DeckShuffleDraw = First + 17;
    public const int DeckShuffle = First + 18;
    public const int DiscardAllCards = First + 19;
    public const int DiscardMarkedForSpecificDiscard = First + 20;
    public const int DrawPileEmpty = First + 21;
    public const int DrawRandomCardFromDiscard = First + 22;
    public const int EndTurnDisplay = First + 23;
    public const int HandLayout = First + 24;
    public const int HotKeyHoldCompleted = First + 25;
    public const int Intimidate = First + 26;
    public const int MarkedForSpecificDiscard = First + 27;
    public const int MillCard = First + 28;
    public const int ModifyActionPoints = First + 29;
    public const int ModifyCourage = First + 30;
    public const int ModifySeals = First + 31;
    public const int OpenCardListModal = First + 32;
    public const int OpenPayCostOverlay = First + 33;
    public const int PayCostCandidateClicked = First + 34;
    public const int PayCostSatisfied = First + 35;
    public const int PledgeAdded = First + 36;
    public const int RedrawHand = First + 37;
    public const int RemoveCardApplication = First + 38;
    public const int RemoveCardApplications = First + 39;
    public const int RemoveRandomCard = First + 40;
    public const int ResetDeck = First + 41;
    public const int SealCards = First + 42;
    public const int SetActionPoints = First + 43;
    public const int SetCourage = First + 44;
    public const int Shackle = First + 45;
    public const int ShuffleRandomFromDiscard = First + 46;
    public const int ShuffleSealedIntoDrawPile = First + 47;
    public const int StartOfTurnDrawResolved = First + 48;
    public const int TopCardRemovedForMill = First + 49;
    public const int CursorState = First + 50;
    public const int HotKeySelect = First + 51;
    public const int UIElementHoverEntered = First + 52;
    public const int PlunderCard = First + 53;
    public const int PlunderForceDiscard = First + 54;
    public const int PlunderRescue = First + 55;
    public const int PlunderTrigger = First + 56;
    public const int Tracking = First + 57;
    public const int Count = 58;
}
