#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

/// <summary>
/// Explicit cross-domain subscriptions for the ECS-044 ledger. The root passes these registrations
/// to the hub that owns each canonical stream; no second stream or private runtime is created.
/// </summary>
public sealed class MetaGameCrossDomainRoutes :
    IEventConsumer<CardPlayedEvent>, IEventConsumer<ApplyCardApplicationEvent>,
    IEventConsumer<CardUpgradeConfirmedEvent>, IEventConsumer<DrawRandomCardFromDiscardEvent>,
    IEventConsumer<EnemyKilledEvent>, IEventConsumer<ChangeBattlePhaseEvent>,
    IEventConsumer<ApplyPassiveEvent>, IEventConsumer<Crusaders30XX.ECS.DataOriented.Gameplay.Effects.FrostbiteTriggered>,
    IEventConsumer<LoadSceneEvent>, IEventConsumer<PrepareSceneEvent>, IEventConsumer<DeleteCachesEvent>,
    IEventConsumer<UIActionEvent>
{
    public const int Priority = 0;

    private readonly World world;
    private readonly MetaGameEventHub events;
    private readonly WayStationRuntimeSystem wayStation;

    public MetaGameCrossDomainRoutes(World world, MetaGameEventHub events, WayStationRuntimeSystem wayStation)
    {
        this.world = world;
        this.events = events;
        this.wayStation = wayStation;
    }

    public CardGameplayRouteConsumers RegisterCards(CardGameplayRouteConsumers? routes = null)
    {
        routes ??= new CardGameplayRouteConsumers();
        return routes
            .Add<CardPlayedEvent>(this, Priority, "meta.achievement-card-played")
            .Add<ApplyCardApplicationEvent>(this, Priority, "meta.achievement-card-application")
            .Add<CardUpgradeConfirmedEvent>(this, Priority, "meta.achievement-card-upgrade")
            .Add<DrawRandomCardFromDiscardEvent>(this, Priority, "meta.achievement-discard-draw");
    }

    public CombatRouteConsumers RegisterCombat(CombatRouteConsumers? routes = null)
    {
        routes ??= new CombatRouteConsumers();
        return routes
            .Add<EnemyKilledEvent>(this, Priority, "meta.achievement-enemy-killed")
            .Add<ChangeBattlePhaseEvent>(this, Priority, "meta.achievement-phase");
    }

    public EffectGameplayRouteConsumers RegisterEffects(EffectGameplayRouteConsumers? routes = null)
    {
        routes ??= new EffectGameplayRouteConsumers();
        return routes
            .Add<ApplyPassiveEvent>(this, Priority, "meta.achievement-passive")
            .Add<Crusaders30XX.ECS.DataOriented.Gameplay.Effects.FrostbiteTriggered>(this, Priority, "meta.achievement-frostbite");
    }

    public GlobalUiRouteConsumers RegisterGlobal(GlobalUiRouteConsumers? routes = null)
    {
        routes ??= new GlobalUiRouteConsumers();
        return routes
            .Add<LoadSceneEvent>(this, Priority, "meta.scene-loaded")
            .Add<PrepareSceneEvent>(this, Priority, "meta.scene-prepare")
            .Add<DeleteCachesEvent>(this, Priority, "meta.scene-cache-delete")
            .Add<UIActionEvent>(this, Priority, "meta.input-action-routing")
            .Add<OpenWayStationSaintsMedalsModalEvent>(wayStation, Priority, "meta.way-station-medals");
    }

    public void Consume(in CardPlayedEvent value, ref EventDispatchContext context)
    {
        Advance(AchievementId.CardPlayer);
        Advance(AchievementId.KunaiStorm);
        Advance(AchievementId.RedCardApprentice);
        Advance(AchievementId.Relentless);
    }

    public void Consume(in ApplyCardApplicationEvent value, ref EventDispatchContext context) => Advance(AchievementId.HexedHoard);
    public void Consume(in CardUpgradeConfirmedEvent value, ref EventDispatchContext context) => Advance(AchievementId.MasterArtificer);
    public void Consume(in DrawRandomCardFromDiscardEvent value, ref EventDispatchContext context) => Advance(AchievementId.MassRevival, value.Count);

    public void Consume(in EnemyKilledEvent value, ref EventDispatchContext context)
    {
        Advance(AchievementId.FadedSpectrum);
        Advance(AchievementId.Kenosis);
        Advance(AchievementId.LivingOnTheEdge);
        Advance(AchievementId.Slayer);
        Advance(AchievementId.Unshackled);
        if (value.Enemy == EnemyId.Skeleton) Advance(AchievementId.SkeletonSlayer);
    }

    public void Consume(in ChangeBattlePhaseEvent value, ref EventDispatchContext context)
    {
        Advance(AchievementId.KunaiStorm);
        Advance(AchievementId.MassRevival);
        Advance(AchievementId.Relentless);
    }

    public void Consume(in ApplyPassiveEvent value, ref EventDispatchContext context) => Advance(AchievementId.Archangel, value.Stacks);
    public void Consume(in Crusaders30XX.ECS.DataOriented.Gameplay.Effects.FrostbiteTriggered value, ref EventDispatchContext context) => Advance(AchievementId.FrozenButUnbroken);

    public void Consume(in LoadSceneEvent value, ref EventDispatchContext context)
    {
        if (value.Scene == Crusaders30XX.ECS.DataOriented.Systems.SceneGroup.WayStation)
        {
            Query<WayStationArrivalContextState> arrivals = world.Query<WayStationArrivalContextState>();
            foreach (QueryChunk<WayStationArrivalContextState> chunk in arrivals)
            foreach (int row in chunk.Rows)
                chunk.Component1[row].Visit++;
        }
    }

    public void Consume(in PrepareSceneEvent value, ref EventDispatchContext context)
    {
        if (value.Scene != Crusaders30XX.ECS.DataOriented.Systems.SceneGroup.Climb) return;
        Query<ClimbColumnTransitionState> climbs = world.Query<ClimbColumnTransitionState>();
        foreach (QueryChunk<ClimbColumnTransitionState> chunk in climbs)
        foreach (int row in chunk.Rows)
        {
            chunk.Component1[row].SelectedSlot = -1;
            chunk.Component1[row].Progress = 0f;
        }
    }

    public void Consume(in DeleteCachesEvent value, ref EventDispatchContext context)
    {
        Query<ClimbPreviewState> previews = world.Query<ClimbPreviewState>();
        foreach (QueryChunk<ClimbPreviewState> chunk in previews)
        foreach (int row in chunk.Rows)
        {
            chunk.Component1[row].Active = 0;
            chunk.Component1[row].HoveredSlot = -1;
        }
    }

    public void Consume(in UIActionEvent value, ref EventDispatchContext context)
    {
        switch (value.Action)
        {
            case UIElementEventType.CardClicked:
                RoutePrimaryCard(value.Entity);
                break;
            case UIElementEventType.PlayCardRequested:
                RoutePlayCard(value.Entity);
                break;
            case UIElementEventType.AssignCardAsBlock:
                RouteAssignCard(value.Entity);
                break;
            case UIElementEventType.UnassignCardAsBlock:
                RouteUnassignCard(value.Entity);
                break;
            case UIElementEventType.PledgeCard:
                RoutePledgeCard(value.Entity);
                break;
            case UIElementEventType.ConfirmBlocks:
                if (TryGetBattle(out EntityId confirmBattle, out _, out _))
                    events.ConfirmBlocksRequested.Publish(new(confirmBattle));
                break;
            case UIElementEventType.EndTurn:
                if (TryGetBattle(out EntityId endBattle, out _, out _))
                    events.EndTurnRequested.Publish(new(endBattle));
                break;
            case UIElementEventType.PayCostCancel:
                if (TryGetBattle(out _, out BattleInfo payInfo, out _))
                    events.PayCostCancelRequested.Publish(new(payInfo.Player));
                break;
            case UIElementEventType.SkipTutorial:
                if (TryResolveTutorial(value.Entity, out EntityId tutorial))
                    events.GuidedTutorialSkipRequested.Publish(new(tutorial));
                break;
            case UIElementEventType.SkipDialog:
                if (TryResolveDialog(value.Entity, out EntityId dialog))
                    events.DialogSkipRequested.Publish(new(dialog));
                break;
            case UIElementEventType.BoosterPackOpeningClose:
                if (TryResolveDismissibleBooster(value.Entity, out EntityId booster))
                    events.CloseBoosterPackOpeningOverlay.Publish(new(booster));
                break;
        }
    }

    private void RoutePrimaryCard(EntityId card)
    {
        if (!TryGetHandCard(card, out _) || !TryGetBattle(out EntityId battle, out BattleInfo info, out CombatPhase phase))
            return;
        if (phase == CombatPhase.Block)
            events.AssignCardAsBlockRequested.Publish(new(card, battle));
        else if (phase == CombatPhase.Action)
            events.PlayCardRequested.Publish(new(card, info.Player));
    }

    private void RoutePlayCard(EntityId card)
    {
        if (!TryGetHandCard(card, out _) ||
            !TryGetBattle(out _, out BattleInfo info, out CombatPhase phase) ||
            phase != CombatPhase.Action)
            return;
        events.PlayCardRequested.Publish(new(card, info.Player));
    }

    private void RouteAssignCard(EntityId card)
    {
        if (!TryGetHandCard(card, out _) ||
            !TryGetBattle(out EntityId battle, out _, out CombatPhase phase) ||
            phase != CombatPhase.Block)
            return;
        events.AssignCardAsBlockRequested.Publish(new(card, battle));
    }

    private void RouteUnassignCard(EntityId card)
    {
        if (!world.Has<Crusaders30XX.ECS.DataOriented.Gameplay.Combat.AssignedBlockCard>(card) ||
            !TryGetBattle(out EntityId battle, out _, out CombatPhase phase) ||
            phase != CombatPhase.Block)
            return;
        events.UnassignCardAsBlockRequested.Publish(new(card, battle));
    }

    private void RoutePledgeCard(EntityId card)
    {
        if (!TryGetHandCard(card, out _) ||
            !TryGetBattle(out _, out _, out CombatPhase phase) ||
            phase != CombatPhase.Action)
            return;
        events.PledgeCardRequested.Publish(new(card));
    }

    private bool TryGetHandCard(EntityId card, out CardZoneLocation location)
    {
        if (world.TryGet(card, out location) && location.Zone == CardZone.Hand && world.Has<CardData>(card))
            return true;
        location = default;
        return false;
    }

    private bool TryGetBattle(out EntityId battle, out BattleInfo info, out CombatPhase phase)
    {
        Query<BattleInfo, PhaseState> battles = world.Query<BattleInfo, PhaseState>();
        foreach (QueryChunk<BattleInfo, PhaseState> chunk in battles)
        foreach (int row in chunk.Rows)
        {
            battle = chunk.Entities[row];
            info = chunk.Component1[row];
            phase = chunk.Component2[row].Current;
            return true;
        }
        battle = default;
        info = default;
        phase = default;
        return false;
    }

    private bool TryResolveTutorial(EntityId candidate, out EntityId tutorial)
    {
        if (world.Has<GuidedTutorial>(candidate))
        {
            tutorial = candidate;
            return true;
        }
        Query<GuidedTutorial> tutorials = world.Query<GuidedTutorial>();
        foreach (QueryChunk<GuidedTutorial> chunk in tutorials)
        foreach (int row in chunk.Rows)
        {
            if (chunk.Component1[row].State != TutorialState.Running) continue;
            tutorial = chunk.Entities[row];
            return true;
        }
        tutorial = default;
        return false;
    }

    private bool TryResolveDialog(EntityId candidate, out EntityId dialog)
    {
        if (world.Has<DialogOverlayState>(candidate))
        {
            dialog = candidate;
            return true;
        }
        Query<DialogOverlayState> dialogs = world.Query<DialogOverlayState>();
        foreach (QueryChunk<DialogOverlayState> chunk in dialogs)
        foreach (int row in chunk.Rows)
        {
            if (chunk.Component1[row].State is DialogueState.Hidden or DialogueState.Complete) continue;
            dialog = chunk.Entities[row];
            return true;
        }
        dialog = default;
        return false;
    }

    private bool TryResolveDismissibleBooster(EntityId candidate, out EntityId booster)
    {
        if (world.TryGet(candidate, out BoosterPackOpeningOverlayState direct) && CanDismiss(in direct))
        {
            booster = candidate;
            return true;
        }
        Query<BoosterPackOpeningOverlayState> boosters = world.Query<BoosterPackOpeningOverlayState>();
        foreach (QueryChunk<BoosterPackOpeningOverlayState> chunk in boosters)
        foreach (int row in chunk.Rows)
        {
            if (!CanDismiss(in chunk.Component1[row])) continue;
            booster = chunk.Entities[row];
            return true;
        }
        booster = default;
        return false;
    }

    private bool CanDismiss(in BoosterPackOpeningOverlayState state)
    {
        if (state.Open == 0 || state.Rewards.IsNull) return false;
        return state.RevealedCount >= world.GetDynamicBuffer(state.Rewards).Count;
    }

    private void Advance(AchievementId achievement, int delta = 1) =>
        events.AchievementProgressUpdated.Publish(new(achievement, delta, 0, 0));
}

public sealed class MetaAchievementTriggerConsumer :
    IEventConsumer<StartBattleRequested>, IEventConsumer<ModifyCourageRequestEvent>, IEventConsumer<ShowQuestRewardOverlay>,
    IEventConsumer<QuestSelected>, IEventConsumer<ClimbEndedEvent>,
    IEventConsumer<ClimbPointsSegmentAwardedEvent>, IEventConsumer<BoosterPackOpeningDismissedEvent>,
    IEventConsumer<AchievementAnimationsComplete>
{
    private readonly MetaGameEventHub events;
    public int CompletedAnimationBatches { get; private set; }
    public MetaAchievementTriggerConsumer(MetaGameEventHub events) => this.events = events;

    public void Consume(in StartBattleRequested value, ref EventDispatchContext context)
    {
        Advance(AchievementId.BoldInvestment);
        Advance(AchievementId.HexedHoard);
    }
    public void Consume(in ModifyCourageRequestEvent value, ref EventDispatchContext context) =>
        Advance(AchievementId.BoldInvestment, Math.Max(0, value.Delta));
    public void Consume(in ShowQuestRewardOverlay value, ref EventDispatchContext context)
    {
        Advance(AchievementId.FirstVictory);
        Advance(AchievementId.JustGettingStarted);
        Advance(AchievementId.QuestMaster);
    }
    public void Consume(in QuestSelected value, ref EventDispatchContext context) => Advance(AchievementId.FrozenButUnbroken);
    public void Consume(in ClimbEndedEvent value, ref EventDispatchContext context) => Advance(AchievementId.JustGettingStarted);
    public void Consume(in ClimbPointsSegmentAwardedEvent value, ref EventDispatchContext context) => Advance(AchievementId.BoldInvestment, value.Points);
    public void Consume(in BoosterPackOpeningDismissedEvent value, ref EventDispatchContext context) => Advance(AchievementId.MasterArtificer);
    public void Consume(in AchievementAnimationsComplete value, ref EventDispatchContext context) => CompletedAnimationBatches++;
    private void Advance(AchievementId id, int delta = 1) => events.AchievementProgressUpdated.Publish(new(id, delta, 0, 0));
}
