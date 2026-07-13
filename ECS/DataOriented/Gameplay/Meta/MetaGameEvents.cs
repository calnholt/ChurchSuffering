#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

// Fixed-size ECS-044 event contracts. Strings and variable payloads are represented by IDs/buffers.
public readonly record struct AchievementCompletedEvent(AchievementId Achievement);
public readonly record struct AchievementProgressUpdatedEvent(AchievementId Achievement, int Delta, int Absolute, byte SetAbsolute);
public readonly record struct AchievementSeenEvent(AchievementId Achievement);
public readonly record struct AchievementAnimationComplete(AchievementId Achievement);
public readonly record struct AchievementAnimationsComplete;
public readonly record struct AchievementGridItemHovered(EntityId Item, AchievementId Achievement);
public readonly record struct AchievementRevealClickedEvent(EntityId Item, AchievementId Achievement);
public readonly record struct ShuffleDeckAnimationRequested(EntityId Deck);
public readonly record struct ReserveAssignedBlockReturnRequested(EntityId Card);
public readonly record struct ActivateEquipmentRequested(EntityId Equipment, EntityId Owner);
public readonly record struct ApplyPledgeToCardRequested(EntityId Card, EntityId Source);
public readonly record struct AssignCardAsBlockRequested(EntityId Card, EntityId Attack);
public readonly record struct AssignEquipmentAsBlockRequested(EntityId Equipment, EntityId Attack);
public readonly record struct CardMoveFinalizeRequested(EntityId Card, int Destination);
public readonly record struct CardMoveRequested(EntityId Card, int Source, int Destination, int Index);
public readonly record struct CardRestrictionMutationAnimationRequested(EntityId Card, int Restriction, byte Added);
public readonly record struct ConfirmBlocksRequested(EntityId Battle);
public readonly record struct EndTurnRequested(EntityId Battle);
public readonly record struct ModifyCourageRequestEvent(EntityId Player, int Delta);
public readonly record struct PayCostCancelRequested(EntityId Player);
public readonly record struct PlayCardRequested(EntityId Card, EntityId Player);
public readonly record struct PlayCardToDiscardAnimationRequested(EntityId Card);
public readonly record struct PlayCardToDrawPileAnimationRequested(EntityId Card);
public readonly record struct PledgeCardRequested(EntityId Card);
public readonly record struct PledgeRandomCardFromDiscardRequested(EntityId Deck);
public readonly record struct QuestSelectRequested(StringId Quest, int Index);
public readonly record struct RemovePledgeFromCardRequested(EntityId Card);
public readonly record struct RemoveTopCardFromDrawPileRequested(EntityId Deck);
public readonly record struct RequestDrawCardsEvent(EntityId Deck, int Count);
public readonly record struct UnassignCardAsBlockRequested(EntityId Card, EntityId Attack);
public readonly record struct ClimbCardMutationAnimationRequested(EntityId Card, int Mutation);
public readonly record struct ClimbCardUpgradeAnimationRequested(EntityId Card);
public readonly record struct ClimbEncounterSlotSelectedEvent(EntityId Root, int SlotIndex);
public readonly record struct ClimbEventContextIds(StringId RunMapEvent, StringId EventType, StringId Resolution, StringId Content);
public readonly record struct ClimbEventSlotSelectedEvent(EntityId Root, int SlotIndex);
public readonly record struct ClimbLoadoutOpenRequestedEvent(EntityId Root);
public readonly record struct ClimbPreviewClearedEvent(EntityId Root);
public readonly record struct ClimbPreviewStartedEvent(EntityId Root, int SlotIndex);
public readonly record struct ClimbResourceAcquisitionAnimationRequested(EntityId Root, int Resource, int Amount);
public readonly record struct ClimbResourceHeaderPulseRequested(EntityId Root, int Resource);
public readonly record struct ClimbShopSlotSelectedEvent(EntityId Slot, EntityId Buyer);
public readonly record struct DialogueSequenceCompleted(StringId Sequence);
public readonly record struct DialogueSequenceRequested(EntityId Overlay, StringId Sequence);
public readonly record struct NarrativeModalChoiceRequested(EntityId Root, int Choice);
public readonly record struct NarrativeModalContent(ClimbEventContextIds Context, int OptionCount);
public readonly record struct DialogEnded(EntityId Overlay);
public readonly record struct DialogSkipRequested(EntityId Overlay);
public readonly record struct GoldChanged(EntityId Player, int Previous, int Current);
public readonly record struct ModifyGoldRequestEvent(EntityId Player, int Delta);
public readonly record struct ModifyHpRequestEvent(EntityId Player, int Delta);
public readonly record struct RunEndSequenceRequested(EntityId Run, byte Victory);
public readonly record struct LoadoutCardAdded(CardId Card, byte Upgraded);
public readonly record struct LoadoutCardRemoved(CardId Card, byte Upgraded);
public readonly record struct PlunderDiscardAnimationRequested(EntityId Card);
public readonly record struct PlunderRescueAnimationRequested(EntityId Card);
public readonly record struct PlunderSnatchAnimationRequested(EntityId Card);
public readonly record struct ReplaceableEffectRequest(EntityId Source, EntityId Target, int Effect, int Amount);
public readonly record struct RumbleRequested(float Strength, float DurationSeconds);
public readonly record struct BoosterPackOpeningDismissedEvent(EntityId Overlay);
public readonly record struct ClaimPendingClimbPointsEvent(EntityId Run);
public readonly record struct ClimbEndedEvent(EntityId Run, byte Victory);
public readonly record struct ClimbPointsSegmentAwardedEvent(EntityId Run, int Points);
public readonly record struct CloseBoosterPackOpeningOverlayEvent(EntityId Overlay);
public readonly record struct OpenWayStationClimbSettingsModalEvent(EntityId WayStation);
public readonly record struct QuestSelected(StringId Quest, int Index);
public readonly record struct SceneTransitionRequested(SceneGroup Scene);
public readonly record struct ShowBoosterPackOpeningOverlayEvent(EntityId Overlay, int RewardCount);
public readonly record struct ShowQuestRewardOverlay(EntityId Overlay, StringId Quest);
public readonly record struct StartBattleRequested(EntityId Run, StringId Encounter);
public readonly record struct WayStationDialoguePoiSelectedEvent(EntityId WayStation, StringId Sequence);
public readonly record struct OpenRunShopRequested(EntityId Run, StringId Shop);
public readonly record struct SetShopTitle(StringId Title);
public readonly record struct AdvanceTutorialEvent(EntityId Tutorial, int Action);
public readonly record struct AllTutorialsCompletedEvent(EntityId Tutorial);
public readonly record struct GuidedTutorialRestartRequested(EntityId Tutorial);
public readonly record struct GuidedTutorialSkipRequested(EntityId Tutorial);
public readonly record struct TutorialCompletedEvent(EntityId Tutorial, int TutorialId);
public readonly record struct TutorialStartedEvent(EntityId Tutorial, int TutorialId);
public readonly record struct PixelBurstAnimationRequested(EntityId Source, int Recipe);
public readonly record struct VisualEffectRequested(EntityId Source, int Recipe);

public static class MetaGameEventTypeIds
{
    public const int First = 44001;
    public const int Last = 44080;
    public const int AchievementCompleted = 44001;
    public const int AchievementProgressUpdated = 44002;
    public const int AchievementSeen = 44003;
    public const int AchievementRevealClicked = 44007;
    public const int ClimbEncounterSelected = 44033;
    public const int ClimbEventSelected = 44035;
    public const int ClimbShopSelected = 44041;
    public const int DialogueCompleted = 44042;
    public const int DialogueRequested = 44043;
    public const int NarrativeChoice = 44044;
    public const int DialogEnded = 44046;
    public const int DialogSkip = 44047;
    public const int LoadoutCardAdded = 44052;
    public const int LoadoutCardRemoved = 44053;
    public const int BoosterDismissed = 44059;
    public const int ClimbEnded = 44061;
    public const int ClimbPointsAwarded = 44062;
    public const int OpenClimbSettings = 44064;
    public const int QuestSelected = 44065;
    public const int ShowBooster = 44067;
    public const int ShowQuestReward = 44068;
    public const int DialoguePoiSelected = 44070;
    public const int AdvanceTutorial = 44073;
    public const int RestartTutorial = 44075;
    public const int SkipTutorial = 44076;
    public const int TutorialCompleted = 44077;
    public const int TutorialStarted = 44078;
}

public sealed class MetaGameEventHub
{
    public EventStream<AchievementCompletedEvent> AchievementCompleted { get; } = new();
    public EventStream<AchievementProgressUpdatedEvent> AchievementProgressUpdated { get; } = new();
    public EventStream<AchievementSeenEvent> AchievementSeen { get; } = new();
    public EventStream<AchievementAnimationComplete> AchievementAnimationComplete { get; } = new();
    public EventStream<AchievementAnimationsComplete> AchievementAnimationsComplete { get; } = new();
    public EventStream<AchievementGridItemHovered> AchievementGridItemHovered { get; } = new();
    public EventStream<AchievementRevealClickedEvent> AchievementRevealClicked { get; } = new();
    public EventStream<ShuffleDeckAnimationRequested> ShuffleDeckAnimationRequested { get; } = new();
    public EventStream<ReserveAssignedBlockReturnRequested> ReserveAssignedBlockReturnRequested { get; } = new();
    public EventStream<ActivateEquipmentRequested> ActivateEquipmentRequested { get; } = new();
    public EventStream<ApplyPledgeToCardRequested> ApplyPledgeToCardRequested { get; } = new();
    public EventStream<AssignCardAsBlockRequested> AssignCardAsBlockRequested { get; } = new();
    public EventStream<AssignEquipmentAsBlockRequested> AssignEquipmentAsBlockRequested { get; } = new();
    public EventStream<CardMoveFinalizeRequested> CardMoveFinalizeRequested { get; } = new();
    public EventStream<CardMoveRequested> CardMoveRequested { get; } = new();
    public EventStream<CardRestrictionMutationAnimationRequested> CardRestrictionMutationAnimationRequested { get; } = new();
    public EventStream<ConfirmBlocksRequested> ConfirmBlocksRequested { get; } = new();
    public EventStream<EndTurnRequested> EndTurnRequested { get; } = new();
    public EventStream<ModifyCourageRequestEvent> ModifyCourageRequestEvent { get; } = new();
    public EventStream<PayCostCancelRequested> PayCostCancelRequested { get; } = new();
    public EventStream<PlayCardRequested> PlayCardRequested { get; } = new();
    public EventStream<PlayCardToDiscardAnimationRequested> PlayCardToDiscardAnimationRequested { get; } = new();
    public EventStream<PlayCardToDrawPileAnimationRequested> PlayCardToDrawPileAnimationRequested { get; } = new();
    public EventStream<PledgeCardRequested> PledgeCardRequested { get; } = new();
    public EventStream<PledgeRandomCardFromDiscardRequested> PledgeRandomCardFromDiscardRequested { get; } = new();
    public EventStream<QuestSelectRequested> QuestSelectRequested { get; } = new();
    public EventStream<RemovePledgeFromCardRequested> RemovePledgeFromCardRequested { get; } = new();
    public EventStream<RemoveTopCardFromDrawPileRequested> RemoveTopCardFromDrawPileRequested { get; } = new();
    public EventStream<RequestDrawCardsEvent> RequestDrawCardsEvent { get; } = new();
    public EventStream<UnassignCardAsBlockRequested> UnassignCardAsBlockRequested { get; } = new();
    public EventStream<ClimbCardMutationAnimationRequested> ClimbCardMutationAnimationRequested { get; } = new();
    public EventStream<ClimbCardUpgradeAnimationRequested> ClimbCardUpgradeAnimationRequested { get; } = new();
    public EventStream<ClimbEncounterSlotSelectedEvent> ClimbEncounterSlotSelected { get; } = new();
    public EventStream<ClimbEventContextIds> ClimbEventContextIds { get; } = new();
    public EventStream<ClimbEventSlotSelectedEvent> ClimbEventSlotSelected { get; } = new();
    public EventStream<ClimbLoadoutOpenRequestedEvent> ClimbLoadoutOpenRequested { get; } = new();
    public EventStream<ClimbPreviewClearedEvent> ClimbPreviewCleared { get; } = new();
    public EventStream<ClimbPreviewStartedEvent> ClimbPreviewStarted { get; } = new();
    public EventStream<ClimbResourceAcquisitionAnimationRequested> ClimbResourceAcquisitionAnimationRequested { get; } = new();
    public EventStream<ClimbResourceHeaderPulseRequested> ClimbResourceHeaderPulseRequested { get; } = new();
    public EventStream<ClimbShopSlotSelectedEvent> ClimbShopSlotSelected { get; } = new();
    public EventStream<DialogueSequenceCompleted> DialogueSequenceCompleted { get; } = new();
    public EventStream<DialogueSequenceRequested> DialogueSequenceRequested { get; } = new();
    public EventStream<NarrativeModalChoiceRequested> NarrativeModalChoiceRequested { get; } = new();
    public EventStream<NarrativeModalContent> NarrativeModalContent { get; } = new();
    public EventStream<DialogEnded> DialogEnded { get; } = new();
    public EventStream<DialogSkipRequested> DialogSkipRequested { get; } = new();
    public EventStream<GoldChanged> GoldChanged { get; } = new();
    public EventStream<ModifyGoldRequestEvent> ModifyGoldRequestEvent { get; } = new();
    public EventStream<ModifyHpRequestEvent> ModifyHpRequestEvent { get; } = new();
    public EventStream<RunEndSequenceRequested> RunEndSequenceRequested { get; } = new();
    public EventStream<LoadoutCardAdded> LoadoutCardAdded { get; } = new();
    public EventStream<LoadoutCardRemoved> LoadoutCardRemoved { get; } = new();
    public EventStream<PlunderDiscardAnimationRequested> PlunderDiscardAnimationRequested { get; } = new();
    public EventStream<PlunderRescueAnimationRequested> PlunderRescueAnimationRequested { get; } = new();
    public EventStream<PlunderSnatchAnimationRequested> PlunderSnatchAnimationRequested { get; } = new();
    public EventStream<ReplaceableEffectRequest> ReplaceableEffectRequest { get; } = new();
    public EventStream<RumbleRequested> RumbleRequested { get; } = new();
    public EventStream<BoosterPackOpeningDismissedEvent> BoosterPackOpeningDismissed { get; } = new();
    public EventStream<ClaimPendingClimbPointsEvent> ClaimPendingClimbPoints { get; } = new();
    public EventStream<ClimbEndedEvent> ClimbEnded { get; } = new();
    public EventStream<ClimbPointsSegmentAwardedEvent> ClimbPointsSegmentAwarded { get; } = new();
    public EventStream<CloseBoosterPackOpeningOverlayEvent> CloseBoosterPackOpeningOverlay { get; } = new();
    public EventStream<OpenWayStationClimbSettingsModalEvent> OpenWayStationClimbSettingsModal { get; } = new();
    public EventStream<QuestSelected> QuestSelected { get; } = new();
    public EventStream<SceneTransitionRequested> SceneTransitionRequested { get; } = new();
    public EventStream<ShowBoosterPackOpeningOverlayEvent> ShowBoosterPackOpeningOverlay { get; } = new();
    public EventStream<ShowQuestRewardOverlay> ShowQuestRewardOverlay { get; } = new();
    public EventStream<StartBattleRequested> StartBattleRequested { get; } = new();
    public EventStream<WayStationDialoguePoiSelectedEvent> WayStationDialoguePoiSelected { get; } = new();
    public EventStream<OpenRunShopRequested> OpenRunShopRequested { get; } = new();
    public EventStream<SetShopTitle> SetShopTitle { get; } = new();
    public EventStream<AdvanceTutorialEvent> AdvanceTutorial { get; } = new();
    public EventStream<AllTutorialsCompletedEvent> AllTutorialsCompleted { get; } = new();
    public EventStream<GuidedTutorialRestartRequested> GuidedTutorialRestartRequested { get; } = new();
    public EventStream<GuidedTutorialSkipRequested> GuidedTutorialSkipRequested { get; } = new();
    public EventStream<TutorialCompletedEvent> TutorialCompleted { get; } = new();
    public EventStream<TutorialStartedEvent> TutorialStarted { get; } = new();
    public EventStream<PixelBurstAnimationRequested> PixelBurstAnimationRequested { get; } = new();
    public EventStream<VisualEffectRequested> VisualEffectRequested { get; } = new();

    /// <summary>Root-composable routes in stable ledger order; this method never attaches a private runtime.</summary>
    public IEventRoute[] BuildRoutes(MetaGameRouteConsumers? local = null, MetaGameRouteConsumers? root = null)
    {
        local ??= new MetaGameRouteConsumers();
        root ??= new MetaGameRouteConsumers();
        return
        [
            Route(44001, nameof(AchievementCompletedEvent), AchievementCompleted, local, root),
            Route(44002, nameof(AchievementProgressUpdatedEvent), AchievementProgressUpdated, local, root),
            Route(44003, nameof(AchievementSeenEvent), AchievementSeen, local, root),
            Route(44004, nameof(AchievementAnimationComplete), AchievementAnimationComplete, local, root),
            Route(44005, nameof(AchievementAnimationsComplete), AchievementAnimationsComplete, local, root),
            Route(44006, nameof(AchievementGridItemHovered), AchievementGridItemHovered, local, root),
            Route(44007, nameof(AchievementRevealClickedEvent), AchievementRevealClicked, local, root),
            Route(44008, nameof(ShuffleDeckAnimationRequested), ShuffleDeckAnimationRequested, local, root),
            Route(44009, nameof(ReserveAssignedBlockReturnRequested), ReserveAssignedBlockReturnRequested, local, root),
            Route(44010, nameof(ActivateEquipmentRequested), ActivateEquipmentRequested, local, root),
            Route(44011, nameof(ApplyPledgeToCardRequested), ApplyPledgeToCardRequested, local, root),
            Route(44012, nameof(AssignCardAsBlockRequested), AssignCardAsBlockRequested, local, root),
            Route(44013, nameof(AssignEquipmentAsBlockRequested), AssignEquipmentAsBlockRequested, local, root),
            Route(44014, nameof(CardMoveFinalizeRequested), CardMoveFinalizeRequested, local, root),
            Route(44015, nameof(CardMoveRequested), CardMoveRequested, local, root),
            Route(44016, nameof(CardRestrictionMutationAnimationRequested), CardRestrictionMutationAnimationRequested, local, root),
            Route(44017, nameof(ConfirmBlocksRequested), ConfirmBlocksRequested, local, root),
            Route(44018, nameof(EndTurnRequested), EndTurnRequested, local, root),
            Route(44019, nameof(ModifyCourageRequestEvent), ModifyCourageRequestEvent, local, root),
            Route(44020, nameof(PayCostCancelRequested), PayCostCancelRequested, local, root),
            Route(44021, nameof(PlayCardRequested), PlayCardRequested, local, root),
            Route(44022, nameof(PlayCardToDiscardAnimationRequested), PlayCardToDiscardAnimationRequested, local, root),
            Route(44023, nameof(PlayCardToDrawPileAnimationRequested), PlayCardToDrawPileAnimationRequested, local, root),
            Route(44024, nameof(PledgeCardRequested), PledgeCardRequested, local, root),
            Route(44025, nameof(PledgeRandomCardFromDiscardRequested), PledgeRandomCardFromDiscardRequested, local, root),
            Route(44026, nameof(QuestSelectRequested), QuestSelectRequested, local, root),
            Route(44027, nameof(RemovePledgeFromCardRequested), RemovePledgeFromCardRequested, local, root),
            Route(44028, nameof(RemoveTopCardFromDrawPileRequested), RemoveTopCardFromDrawPileRequested, local, root),
            Route(44029, nameof(RequestDrawCardsEvent), RequestDrawCardsEvent, local, root),
            Route(44030, nameof(UnassignCardAsBlockRequested), UnassignCardAsBlockRequested, local, root),
            Route(44031, nameof(ClimbCardMutationAnimationRequested), ClimbCardMutationAnimationRequested, local, root),
            Route(44032, nameof(ClimbCardUpgradeAnimationRequested), ClimbCardUpgradeAnimationRequested, local, root),
            Route(44033, nameof(ClimbEncounterSlotSelectedEvent), ClimbEncounterSlotSelected, local, root),
            Route(44034, nameof(ClimbEventContextIds), ClimbEventContextIds, local, root),
            Route(44035, nameof(ClimbEventSlotSelectedEvent), ClimbEventSlotSelected, local, root),
            Route(44036, nameof(ClimbLoadoutOpenRequestedEvent), ClimbLoadoutOpenRequested, local, root),
            Route(44037, nameof(ClimbPreviewClearedEvent), ClimbPreviewCleared, local, root),
            Route(44038, nameof(ClimbPreviewStartedEvent), ClimbPreviewStarted, local, root),
            Route(44039, nameof(ClimbResourceAcquisitionAnimationRequested), ClimbResourceAcquisitionAnimationRequested, local, root),
            Route(44040, nameof(ClimbResourceHeaderPulseRequested), ClimbResourceHeaderPulseRequested, local, root),
            Route(44041, nameof(ClimbShopSlotSelectedEvent), ClimbShopSlotSelected, local, root),
            Route(44042, nameof(DialogueSequenceCompleted), DialogueSequenceCompleted, local, root),
            Route(44043, nameof(DialogueSequenceRequested), DialogueSequenceRequested, local, root),
            Route(44044, nameof(NarrativeModalChoiceRequested), NarrativeModalChoiceRequested, local, root),
            Route(44045, nameof(NarrativeModalContent), NarrativeModalContent, local, root),
            Route(44046, nameof(DialogEnded), DialogEnded, local, root),
            Route(44047, nameof(DialogSkipRequested), DialogSkipRequested, local, root),
            Route(44048, nameof(GoldChanged), GoldChanged, local, root),
            Route(44049, nameof(ModifyGoldRequestEvent), ModifyGoldRequestEvent, local, root),
            Route(44050, nameof(ModifyHpRequestEvent), ModifyHpRequestEvent, local, root),
            Route(44051, nameof(RunEndSequenceRequested), RunEndSequenceRequested, local, root),
            Route(44052, nameof(LoadoutCardAdded), LoadoutCardAdded, local, root),
            Route(44053, nameof(LoadoutCardRemoved), LoadoutCardRemoved, local, root),
            Route(44054, nameof(PlunderDiscardAnimationRequested), PlunderDiscardAnimationRequested, local, root),
            Route(44055, nameof(PlunderRescueAnimationRequested), PlunderRescueAnimationRequested, local, root),
            Route(44056, nameof(PlunderSnatchAnimationRequested), PlunderSnatchAnimationRequested, local, root),
            Route(44057, nameof(ReplaceableEffectRequest), ReplaceableEffectRequest, local, root),
            Route(44058, nameof(RumbleRequested), RumbleRequested, local, root),
            Route(44059, nameof(BoosterPackOpeningDismissedEvent), BoosterPackOpeningDismissed, local, root),
            Route(44060, nameof(ClaimPendingClimbPointsEvent), ClaimPendingClimbPoints, local, root),
            Route(44061, nameof(ClimbEndedEvent), ClimbEnded, local, root),
            Route(44062, nameof(ClimbPointsSegmentAwardedEvent), ClimbPointsSegmentAwarded, local, root),
            Route(44063, nameof(CloseBoosterPackOpeningOverlayEvent), CloseBoosterPackOpeningOverlay, local, root),
            Route(44064, nameof(OpenWayStationClimbSettingsModalEvent), OpenWayStationClimbSettingsModal, local, root),
            Route(44065, nameof(QuestSelected), QuestSelected, local, root),
            Route(44066, nameof(SceneTransitionRequested), SceneTransitionRequested, local, root),
            Route(44067, nameof(ShowBoosterPackOpeningOverlayEvent), ShowBoosterPackOpeningOverlay, local, root),
            Route(44068, nameof(ShowQuestRewardOverlay), ShowQuestRewardOverlay, local, root),
            Route(44069, nameof(StartBattleRequested), StartBattleRequested, local, root),
            Route(44070, nameof(WayStationDialoguePoiSelectedEvent), WayStationDialoguePoiSelected, local, root),
            Route(44071, nameof(OpenRunShopRequested), OpenRunShopRequested, local, root),
            Route(44072, nameof(SetShopTitle), SetShopTitle, local, root),
            Route(44073, nameof(AdvanceTutorialEvent), AdvanceTutorial, local, root),
            Route(44074, nameof(AllTutorialsCompletedEvent), AllTutorialsCompleted, local, root),
            Route(44075, nameof(GuidedTutorialRestartRequested), GuidedTutorialRestartRequested, local, root),
            Route(44076, nameof(GuidedTutorialSkipRequested), GuidedTutorialSkipRequested, local, root),
            Route(44077, nameof(TutorialCompletedEvent), TutorialCompleted, local, root),
            Route(44078, nameof(TutorialStartedEvent), TutorialStarted, local, root),
            Route(44079, nameof(PixelBurstAnimationRequested), PixelBurstAnimationRequested, local, root),
            Route(44080, nameof(VisualEffectRequested), VisualEffectRequested, local, root),
        ];
    }

    private static EventRoute<T> Route<T>(int id, string name, EventStream<T> stream, MetaGameRouteConsumers local, MetaGameRouteConsumers root)
        where T : unmanaged
    {
        EventConsumerRegistration<T>[] first = local.Get<T>();
        EventConsumerRegistration<T>[] second = root.Get<T>();
        if (second.Length == 0) return new EventRoute<T>(id, name, stream, first);
        var combined = new EventConsumerRegistration<T>[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);
        return new EventRoute<T>(id, name, stream, combined);
    }
}

public sealed class MetaGameRouteConsumers
{
    private readonly Dictionary<Type, object> registrations = new();

    public MetaGameRouteConsumers Add<T>(IEventConsumer<T> consumer, int priority = 0, string? name = null) where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (!registrations.TryGetValue(typeof(T), out object? value))
        {
            value = new List<EventConsumerRegistration<T>>();
            registrations.Add(typeof(T), value);
        }
        ((List<EventConsumerRegistration<T>>)value).Add(new(priority, name ?? consumer.GetType().Name, consumer));
        return this;
    }

    internal EventConsumerRegistration<T>[] Get<T>() where T : unmanaged =>
        registrations.TryGetValue(typeof(T), out object? value)
            ? ((List<EventConsumerRegistration<T>>)value).ToArray()
            : [];
}
