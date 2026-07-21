using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Climb;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems;

public sealed class ClimbV2LayoutSystem : Core.System
{
	public const string RootName = "ClimbV2_Root";
	public const string TitleName = "ClimbV2_Title";
	public const string TimelineName = "ClimbV2_Timeline";
	public const string ResourcesName = "ClimbV2_Resources";
	public const string OverviewName = "ClimbV2_Overview";
	public const string ShopContainerName = "ClimbV2_ShopContainer";
	public const string EncounterContainerName = "ClimbV2_EncounterContainer";
	public const string EventContainerName = "ClimbV2_EventContainer";

	public static readonly Rectangle TitleBounds = new(34, 26, 205, 64);
	public static readonly Rectangle TimelineBounds = new(274, 32, 1264, 64);
	public static readonly Rectangle ResourcesBounds = new(1578, 32, 250, 52);
	public static readonly Rectangle OverviewBounds = new(1834, 32, 52, 52);
	public static readonly Rectangle ShopBounds = new(36, 120, 315, 674);
	public static readonly Rectangle EncounterBounds = new(365, 110, 1190, 915);
	public static readonly Rectangle EventBounds = new(1569, 135, 315, 465);

	private readonly Action<LoadSceneEvent> _loadHandler;
	private readonly Action<ClimbShopSlotSelectedEvent> _shopSelectedHandler;
	private readonly Action<SceneDeactivating> _sceneDeactivatingHandler;
	private readonly Action<ClimbCardUpgradeAnimationRequested> _upgradeAnimationRequestedHandler;
	private readonly Action<ClimbCardBoonAnimationRequested> _boonAnimationRequestedHandler;
	private readonly Action<ClimbCardUpgradeAnimationCompleted> _upgradeAnimationCompletedHandler;
	private readonly Action<ClimbResourceAcquisitionAnimationRequested> _resourceAnimationRequestedHandler;
	private readonly Action<ClimbResourceAcquisitionAnimationCompleted> _resourceAnimationCompletedHandler;
	private bool _freshEntranceRequested;
	private ClimbSaveState _returnSnapshot;
	private bool _restoreReturnSnapshotRequested;
	private bool _returnSnapshotActive;
	private int _turnoverHoldCount;
	internal bool IsTurnoverHeld => _turnoverHoldCount > 0;

	public ClimbV2LayoutSystem(EntityManager entityManager) : base(entityManager)
	{
		_loadHandler = OnLoadScene;
		_shopSelectedHandler = OnShopSelected;
		_sceneDeactivatingHandler = OnSceneDeactivating;
		_upgradeAnimationRequestedHandler = evt => HoldTurnover(evt?.DelayClimbTurnoverUntilComplete == true
			&& !string.IsNullOrWhiteSpace(evt.BaseCardKey)
			&& !string.IsNullOrWhiteSpace(evt.UpgradedCardKey));
		_boonAnimationRequestedHandler = evt => HoldTurnover(evt?.DelayClimbTurnoverUntilComplete == true
			&& !string.IsNullOrWhiteSpace(evt.CardKey));
		_upgradeAnimationCompletedHandler = evt => ReleaseTurnover(evt?.ReleasesClimbTurnover == true);
		_resourceAnimationRequestedHandler = evt => HoldTurnover(evt?.DelayClimbTurnoverUntilComplete == true && HasResources(evt.Resources));
		_resourceAnimationCompletedHandler = evt => ReleaseTurnover(evt?.ReleasesClimbTurnover == true);
		EventManager.Subscribe(_loadHandler);
		EventManager.Subscribe(_shopSelectedHandler, 100);
		EventManager.Subscribe(_sceneDeactivatingHandler);
		EventManager.Subscribe(_upgradeAnimationRequestedHandler);
		EventManager.Subscribe(_boonAnimationRequestedHandler);
		EventManager.Subscribe(_upgradeAnimationCompletedHandler);
		EventManager.Subscribe(_resourceAnimationRequestedHandler);
		EventManager.Subscribe(_resourceAnimationCompletedHandler);
	}

	protected override IEnumerable<Entity> GetRelevantEntities() =>
		EntityManager.GetEntitiesWithComponent<SceneState>();

	protected override void UpdateEntity(Entity entity, GameTime gameTime)
	{
		if (entity.GetComponent<SceneState>()?.Current != SceneId.Climb) return;
		var climb = SaveCache.GetClimbState();
		var root = EnsureRoot();
		EnsureChrome();
		if (_restoreReturnSnapshotRequested && _returnSnapshot != null)
		{
			SyncChoices(_returnSnapshot);
			root.GetComponent<ClimbPreviewState>()?.Clear();
			_restoreReturnSnapshotRequested = false;
			_returnSnapshotActive = true;
			return;
		}
		if (_turnoverHoldCount > 0)
		{
			if (_returnSnapshotActive && _returnSnapshot != null) SyncChoices(_returnSnapshot);
			root.GetComponent<ClimbPreviewState>()?.Clear();
			return;
		}
		SyncChoices(climb);
		if (_returnSnapshotActive)
		{
			_returnSnapshotActive = false;
			_returnSnapshot = null;
		}
		UpdatePreview(root.GetComponent<ClimbPreviewState>(), climb);
		UpdateRails(climb, root.GetComponent<ClimbPreviewState>());

		var state = root.GetComponent<ClimbV2SceneState>();
		if (_freshEntranceRequested && !state.FreshEntranceStarted)
		{
			state.FreshEntranceRequested = true;
			state.FreshEntranceStarted = true;
			StartFreshEntrance();
			_freshEntranceRequested = false;
		}
	}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt?.Scene != SceneId.Climb) return;
			var climb = SaveCache.GetClimbState();
		_restoreReturnSnapshotRequested = evt.PreviousScene == SceneId.Battle && _returnSnapshot != null;
		_returnSnapshotActive = false;
		if (!_restoreReturnSnapshotRequested) _returnSnapshot = null;
			_freshEntranceRequested = evt.PreviousScene == SceneId.WayStation
				&& ClimbRuleService.ClampTime(climb, climb?.time ?? 0) == 0;
	}

	private void OnSceneDeactivating(SceneDeactivating evt)
	{
		if (evt?.From != SceneId.Climb) return;
		_restoreReturnSnapshotRequested = false;
		_returnSnapshotActive = false;
		_turnoverHoldCount = 0;
		var queued = EntityManager.GetEntitiesWithComponent<QueuedEvents>()
			.FirstOrDefault()?.GetComponent<QueuedEvents>();
		bool enteringClimbEncounter = ShouldCaptureReturnSnapshot(evt, queued);
		_returnSnapshot = enteringClimbEncounter ? SaveCache.GetClimbState() : null;
	}

	internal static bool ShouldCaptureReturnSnapshot(SceneDeactivating evt, QueuedEvents queued) =>
		evt?.From == SceneId.Climb && evt.To == SceneId.Battle && queued?.IsClimbEncounter == true;

	private void HoldTurnover(bool shouldHold)
	{
		if (shouldHold) _turnoverHoldCount++;
	}

	private void ReleaseTurnover(bool shouldRelease)
	{
		if (shouldRelease) _turnoverHoldCount = Math.Max(0, _turnoverHoldCount - 1);
	}

	private void SyncChoices(ClimbSaveState climb)
	{
		SyncShop(climb);
		SyncEncounters(climb);
		SyncEvents(climb);
	}

	public void PrepareForLoad(LoadSceneEvent evt) => OnLoadScene(evt);

	private void OnShopSelected(ClimbShopSlotSelectedEvent evt)
	{
		if (evt == null || evt.SlotIndex < 0) return;
		var climb = SaveCache.GetClimbState();
		var save = climb?.shopSlots != null && evt.SlotIndex < climb.shopSlots.Count ? climb.shopSlots[evt.SlotIndex] : null;
		if (save == null || save.isSold || string.Equals(save.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase)
			|| string.Equals(save.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase)) return;
		var motion = EntityManager.GetEntity($"ClimbV2_Shop_{evt.SlotIndex}")?.GetComponent<ClimbV2ChoiceMotion>();
		if (motion == null) return;
		motion.Phase = ClimbV2MotionPhase.Purchasing;
		motion.ElapsedSeconds = 0f;
		motion.DelaySeconds = 0f;
	}

	private Entity EnsureRoot()
	{
		var root = EnsureEntity(RootName);
		if (root.GetComponent<ClimbSceneRoot>() == null) EntityManager.AddComponent(root, new ClimbSceneRoot());
		if (root.GetComponent<ClimbPreviewState>() == null) EntityManager.AddComponent(root, new ClimbPreviewState());
		if (root.GetComponent<ClimbV2SceneState>() == null) EntityManager.AddComponent(root, new ClimbV2SceneState());
		return root;
	}

	private void EnsureChrome()
	{
		EnsureMarker<ClimbV2TitlePresentation>(TitleName, TitleBounds, 2000, false);
		EnsureMarker<DistanceClimbedTimelinePresentation>(TimelineName, TimelineBounds, 2000, false);
		EnsureMarker<PlayerResourcesPresentation>(ResourcesName, ResourcesBounds, 2000, false);
		var overview = EnsureMarker<ClimbOverviewButton>(OverviewName, OverviewBounds, 2100, true, UIElementEventType.OpenLoadout, parallax: true);
		if (overview.GetComponent<ClimbLoadoutButton>() == null) EntityManager.AddComponent(overview, new ClimbLoadoutButton());
		EnsureSection(ShopContainerName, ShopBounds, ClimbV2SectionKind.Shop);
		EnsureSection(EncounterContainerName, EncounterBounds, ClimbV2SectionKind.Encounter);
		EnsureSection(EventContainerName, EventBounds, ClimbV2SectionKind.Event);
	}

	private void EnsureSection(string name, Rectangle bounds, ClimbV2SectionKind kind)
	{
		var entity = EnsureMarker<ClimbV2SectionPresentation>(name, bounds, 1200, false);
		entity.GetComponent<ClimbV2SectionPresentation>().Kind = kind;
	}

	private void SyncShop(ClimbSaveState climb)
	{
		for (int index = 0; index < ClimbRuleService.ShopSlotCount; index++)
		{
			var existing = EntityManager.GetEntity($"ClimbV2_Shop_{index}");
			if (existing?.GetComponent<ClimbV2ChoiceMotion>()?.Phase == ClimbV2MotionPhase.Purchasing) continue;
			var save = climb?.shopSlots != null && index < climb.shopSlots.Count ? climb.shopSlots[index] : null;
			bool hidden = save == null || save.isSold || string.Equals(save.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase);
			var rect = new Rectangle(36, 120 + index * 113, 315, 105);
			var entity = EnsureEntity($"ClimbV2_Shop_{index}");
			ConfigureUiParallax(entity);
			bool awaitingPurchaseReconciliation = entity.GetComponent<ClimbV2ChoiceMotion>()?.Phase
				== ClimbV2MotionPhase.AwaitingPurchaseReconciliation;
			string itemAsset = ResolveShopAsset(save);
			string title = ResolveShopTitle(save);
			string fingerprint = Fingerprint(save?.id ?? $"shop_{index}", title, Math.Max(0, save?.timeCost ?? 0), Math.Max(0, save?.generatedAtTime ?? 0), hidden, itemAsset);
			if (!TryAdoptPresentation(entity, fingerprint, index)) continue;
			var slot = EnsureComponent<ClimbSlotPresentation>(entity);
			var item = EnsureComponent<ClimbShopItemPresentation>(entity);
			EnsureComponent<ClimbShopSlotAction>(entity).SlotIndex = index;
			slot.Kind = ClimbSlotKind.Shop;
			slot.SlotIndex = index;
			slot.SlotId = save?.id ?? $"shop_{index}";
			slot.Title = title;
			slot.Label = ClimbSceneDrawHelpers.ResolveShopLabel(save);
			slot.GeneratedAtTime = Math.Max(0, save?.generatedAtTime ?? 0);
			slot.TimeCost = Math.Max(0, save?.timeCost ?? 0);
			slot.Cost = Clone(save?.cost);
			slot.Reward = new ClimbResourceSave();
			slot.IsSold = save?.isSold == true;
			slot.IsUnavailable = hidden;
			slot.IsAffordable = hidden || ClimbRuleService.CanAfford(climb?.resources, save?.cost);
			slot.Opacity = 1f;
			item.ItemKind = save?.kind ?? string.Empty;
			item.ItemAsset = itemAsset;
			SetBounds(entity, hidden ? Rectangle.Empty : rect, !hidden && slot.IsAffordable, UIElementEventType.ClimbShopSlotSelect, 1600, hidden);
			string tooltipFingerprint = $"{save?.id}|{save?.kind}|{save?.itemId}|{save?.cardKey}|{hidden}";
			if (!string.Equals(item.TooltipFingerprint, tooltipFingerprint, StringComparison.Ordinal))
			{
				item.TooltipFingerprint = tooltipFingerprint;
				SyncShopTooltip(entity, save, hidden);
			}
			SyncRail(entity, hidden ? Rectangle.Empty : rect, new Rectangle(rect.X + 86, rect.Bottom - 53, rect.Width - 99, 42),
				ClimbChoiceRailOutcomeKind.Price, slot.Cost, slot.TimeCost, showTime: true, stays: -1);
			if (awaitingPurchaseReconciliation) ReconcilePurchasedPresentation(entity.GetComponent<ClimbV2ChoiceMotion>(), hidden);
		}
	}

	private void SyncEncounters(ClimbSaveState climb)
	{
		Rectangle[] rects =
		{
			new(407, 475, 330, 480),
			new(795, 145, 330, 480),
			new(1183, 497, 330, 480),
		};
		for (int index = 0; index < ClimbRuleService.EncounterSlotCount; index++)
		{
			var save = climb?.encounterSlots != null && index < climb.encounterSlots.Count ? climb.encounterSlots[index] : null;
			var rect = rects[index];
			bool hidden = save == null || save.isCompleted || string.IsNullOrWhiteSpace(save.enemyId);
			var entity = EnsureEntity($"ClimbV2_Encounter_{index}");
			ConfigureUiParallax(entity);
			var enemy = EnemyFactory.Create(save?.enemyId);
			string title = enemy?.Name ?? "Encounter";
			string portraitAsset = EnemyPortraitContent.ToAssetName(save?.enemyId ?? string.Empty);
			string fingerprint = Fingerprint(save?.id ?? $"encounter_{index}", title, Math.Max(0, save?.timeCost ?? 0), Math.Max(0, save?.generatedAtTime ?? 0), hidden, portraitAsset);
			if (!TryAdoptPresentation(entity, fingerprint, index + 5)) continue;
			var slot = EnsureComponent<ClimbSlotPresentation>(entity);
			EnsureComponent<ClimbEncounterPresentation>(entity);
			EnsureComponent<ClimbEncounterSlotAction>(entity).SlotId = save?.id ?? string.Empty;
			slot.Kind = ClimbSlotKind.Encounter;
			slot.SlotIndex = index;
			slot.SlotId = save?.id ?? $"encounter_{index}";
			slot.Title = title;
			slot.Label = save?.isFinal == true ? "Final" : "Fight";
			slot.GeneratedAtTime = Math.Max(0, save?.generatedAtTime ?? 0);
			slot.Duration = Math.Max(0, save?.duration ?? 0);
			slot.TimeCost = Math.Max(0, save?.timeCost ?? 0);
			slot.Reward = Clone(save?.rewardResources);
			slot.Cost = new ClimbResourceSave();
			slot.IsCompleted = save?.isCompleted == true;
			slot.IsUnavailable = hidden;
			slot.IsAffordable = true;
			slot.IsFinal = save?.isFinal == true;
			slot.BattleLocation = save?.battleLocation ?? BattleLocation.Desert;
			slot.PortraitAsset = portraitAsset;
			slot.Opacity = 1f;
			SetBounds(entity, hidden ? Rectangle.Empty : rect, !hidden, UIElementEventType.ClimbEncounterSlotSelect, 1600, hidden);
			ClearNonShopTooltips(entity);
			int stays = Remaining(slot, climb?.time ?? 0);
			SyncRail(entity, hidden ? Rectangle.Empty : rect, new Rectangle(rect.X + 18, rect.Bottom - 53, rect.Width - 36, 42),
				ClimbChoiceRailOutcomeKind.Reward, slot.Reward, slot.TimeCost, showTime: true, stays);
		}
	}

	private void SyncEvents(ClimbSaveState climb)
	{
		var active = climb?.eventSlots?.Where(IsPresentedEvent)
			.OrderBy(x => x.activatedAtTime).ThenBy(x => x.id, StringComparer.Ordinal).Take(2).ToList()
			?? new List<ClimbEventSlotSave>();
		for (int index = 0; index < 2; index++)
		{
			var save = index < active.Count ? active[index] : null;
			var rect = new Rectangle(1569, 135 + index * 240, 315, 225);
			bool hidden = save == null;
			var entity = EnsureEntity($"ClimbV2_Event_{index}");
			ConfigureUiParallax(entity);
			var definition = ClimbEventCatalog.Get(save?.definitionId);
			ClimbEventKind eventKind = save?.kind ?? ClimbEventKind.Hazard;
			string title = eventKind == ClimbEventKind.Hazard ? "Unknown Hazard" : ResolveCharacterTitle(definition?.Actor);
			string portraitAsset = eventKind == ClimbEventKind.Character ? definition?.PortraitAsset ?? string.Empty : string.Empty;
			string fingerprint = Fingerprint(save?.id ?? $"event_{index}", title, Math.Max(0, save?.timeCost ?? 0), Math.Max(0, save?.activatedAtTime ?? 0), hidden, portraitAsset);
			if (!TryAdoptPresentation(entity, fingerprint, index + 8)) continue;
			var slot = EnsureComponent<ClimbSlotPresentation>(entity);
			var item = EnsureComponent<ClimbEventPresentation>(entity);
			EnsureComponent<ClimbEventSlotAction>(entity).SlotId = save?.id ?? string.Empty;
			slot.Kind = ClimbSlotKind.Event;
			slot.SlotIndex = index;
			slot.SlotId = save?.id ?? $"event_{index}";
			slot.EventKind = eventKind;
			slot.Title = title;
			slot.Label = slot.EventKind == ClimbEventKind.Hazard ? "Hazard" : "Character";
			slot.GeneratedAtTime = Math.Max(0, save?.activatedAtTime ?? 0);
			slot.Duration = Math.Max(0, save?.duration ?? 0);
			slot.TimeCost = Math.Max(0, save?.timeCost ?? 0);
			slot.Reward = Clone(save?.rewardResources);
			slot.IsUnavailable = hidden;
			slot.IsCompleted = save?.status is ClimbEventStatus.Completed or ClimbEventStatus.Expired;
			slot.PortraitAsset = portraitAsset;
			slot.GainLine1 = definition?.GainLine1 ?? string.Empty;
			slot.GainLine2 = definition?.GainLine2 ?? string.Empty;
			slot.Opacity = 1f;
			item.Description = slot.EventKind == ClimbEventKind.Hazard
				? "Something waits beyond the road. Its nature—and its price—remain hidden."
				: BuildCharacterDescription(definition);
			bool interactable = !hidden && save?.status == ClimbEventStatus.Active;
			SetBounds(entity, hidden ? Rectangle.Empty : rect, interactable, UIElementEventType.ClimbEventSlotSelect, 1600, hidden);
			ClearNonShopTooltips(entity);
			int railLeft = slot.EventKind == ClimbEventKind.Character ? 115 : 95;
			int stays = Remaining(slot, climb?.time ?? 0);
			bool hazard = slot.EventKind == ClimbEventKind.Hazard;
			SyncRail(entity, hidden ? Rectangle.Empty : rect, new Rectangle(rect.X + railLeft, rect.Bottom - 83, rect.Width - railLeft - 16, 42),
				hazard ? ClimbChoiceRailOutcomeKind.Reward : ClimbChoiceRailOutcomeKind.None,
				hazard ? slot.Reward : new ClimbResourceSave(), slot.TimeCost, showTime: !hazard, stays);
		}
	}

	private void SyncRail(Entity source, Rectangle sourceBounds, Rectangle bounds, ClimbChoiceRailOutcomeKind outcome,
		ClimbResourceSave resources, int time, bool showTime, int stays)
	{
		var rail = EnsureEntity(source.Name + "_Rail");
		ConfigureUiParallax(rail);
		var presentation = EnsureComponent<ClimbChoiceRailPresentation>(rail);
		presentation.SourceSlotId = source.GetComponent<ClimbSlotPresentation>()?.SlotId ?? string.Empty;
		presentation.OutcomeKind = outcome;
		presentation.Resources = Clone(resources);
		presentation.Time = Math.Max(0, time);
		presentation.ShowTime = showTime;
		presentation.Stays = stays;
		presentation.ProjectedStays = stays;
		SetBounds(rail, sourceBounds.IsEmpty ? Rectangle.Empty : bounds, false, UIElementEventType.None, 1700, sourceBounds.IsEmpty);
	}

	private void UpdateRails(ClimbSaveState climb, ClimbPreviewState preview)
	{
		foreach (var entity in EntityManager.GetEntitiesWithComponent<ClimbChoiceRailPresentation>())
		{
			var rail = entity.GetComponent<ClimbChoiceRailPresentation>();
			if (rail.Stays < 0) continue;
			rail.ProjectedStays = rail.Stays;
			if (preview?.IsActive != true || string.Equals(preview.SourceSlotId, rail.SourceSlotId, StringComparison.OrdinalIgnoreCase)) continue;
			rail.ProjectedStays = Math.Max(0, rail.Stays - preview.Amount);
		}
	}

	private void UpdatePreview(ClimbPreviewState preview, ClimbSaveState climb)
	{
		if (preview == null) return;
		int maxTime = ClimbRuleService.GetMaxTime(climb);
		int current = ClimbRuleService.ClampTime(climb, climb?.time ?? 0);
		var hovered = EntityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
			.Select(e => new { Slot = e.GetComponent<ClimbSlotPresentation>(), Ui = e.GetComponent<UIElement>() })
			.FirstOrDefault(x => x.Ui?.IsHovered == true && !x.Ui.IsHidden && x.Slot?.IsUnavailable != true
				&& !x.Slot.IsCompleted && !x.Slot.IsSold && (x.Slot.Kind != ClimbSlotKind.Shop || x.Slot.IsAffordable));
		if (hovered == null)
		{
			preview.Clear();
			preview.ProjectedUsedTime = current;
			preview.ProjectedRemainingTime = maxTime - current;
			preview.ProjectedResources = Clone(climb?.resources);
			FillAffordable(preview, climb);
			return;
		}

		int projected = ClimbRuleService.ClampTime(climb, current + hovered.Slot.TimeCost);
		preview.IsActive = projected > current || hovered.Slot.Kind == ClimbSlotKind.Event;
		preview.SourceSlotId = hovered.Slot.SlotId;
		preview.Amount = projected - current;
		preview.ProjectedUsedTime = projected;
		preview.ProjectedRemainingTime = maxTime - projected;
		preview.ProjectedResources = Clone(climb?.resources);
		if (hovered.Slot.Kind == ClimbSlotKind.Shop) ClimbRuleService.TrySpend(preview.ProjectedResources, hovered.Slot.Cost);
		else if (hovered.Slot.Kind == ClimbSlotKind.Encounter || hovered.Slot.EventKind == ClimbEventKind.Hazard)
			ClimbRuleService.AddResources(preview.ProjectedResources, hovered.Slot.Reward);
		FillWouldVanish(preview, climb, projected);
		FillAffordable(preview, climb);
	}

	private static void FillWouldVanish(ClimbPreviewState preview, ClimbSaveState climb, int projected)
	{
		preview.WouldVanishSlotIds.Clear();
		int current = ClimbRuleService.ClampTime(climb, climb?.time ?? 0);
		int shopRefreshInterval = ClimbRuleService.GetShopRefreshInterval(climb);
		int refresh = ((current / shopRefreshInterval) + 1) * shopRefreshInterval;
		if (projected >= refresh)
			foreach (var slot in climb?.shopSlots ?? new List<ClimbShopSlotSave>()) if (!string.IsNullOrWhiteSpace(slot?.id)) preview.WouldVanishSlotIds.Add(slot.id);
		foreach (var slot in climb?.encounterSlots ?? new List<ClimbEncounterSlotSave>())
			if (slot != null && !slot.isCompleted && !slot.isFinal && ClimbRuleService.IsEncounterExpired(slot, projected)) preview.WouldVanishSlotIds.Add(slot.id);
		foreach (var slot in climb?.eventSlots ?? new List<ClimbEventSlotSave>())
			if (slot?.status == ClimbEventStatus.Active && projected >= slot.activatedAtTime + Math.Max(0, slot.duration)) preview.WouldVanishSlotIds.Add(slot.id);
	}

	private static void FillAffordable(ClimbPreviewState preview, ClimbSaveState climb)
	{
		preview.AffordableShopSlotIds.Clear();
		foreach (var slot in climb?.shopSlots ?? new List<ClimbShopSlotSave>())
			if (slot != null && !slot.isSold && ClimbRuleService.CanAfford(preview.ProjectedResources, slot.cost)) preview.AffordableShopSlotIds.Add(slot.id);
	}

	private void StartFreshEntrance()
	{
		foreach (var entity in ChoiceEntities().OrderBy(ChoiceOrder))
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui?.IsHidden == true) continue;
			var motion = EnsureComponent<ClimbV2ChoiceMotion>(entity);
			motion.Phase = ClimbV2MotionPhase.Entering;
			motion.ElapsedSeconds = 0f;
			motion.DelaySeconds = ChoiceOrder(entity) * 0.075f;
		}
	}

	private bool TryAdoptPresentation(Entity entity, string fingerprint, int order)
	{
		var motion = EnsureComponent<ClimbV2ChoiceMotion>(entity);
		return TryAdoptPresentation(motion, fingerprint, order * 0.075f);
	}

	internal static bool TryAdoptPresentation(ClimbV2ChoiceMotion motion, string fingerprint, float delaySeconds)
	{
		if (motion == null) return false;
		if (!motion.Initialized)
		{
			motion.Initialized = true;
			motion.Fingerprint = fingerprint;
			return true;
		}
		if (string.Equals(motion.Fingerprint, fingerprint, StringComparison.Ordinal)) return true;
		if (motion.Phase == ClimbV2MotionPhase.Entering)
		{
			motion.Fingerprint = fingerprint;
			return true;
		}
		if (motion.Phase == ClimbV2MotionPhase.AwaitingPurchaseReconciliation)
		{
			motion.Fingerprint = fingerprint;
			return true;
		}
		if (motion.Phase == ClimbV2MotionPhase.Settled)
		{
			motion.Phase = ClimbV2MotionPhase.AshesExiting;
			motion.ElapsedSeconds = 0f;
			motion.DelaySeconds = Math.Max(0f, delaySeconds);
		}
		return false;
	}

	private IEnumerable<Entity> ChoiceEntities() => EntityManager.GetEntitiesWithComponent<ClimbV2ChoiceMotion>();
	private static int ChoiceOrder(Entity entity)
	{
		var slot = entity.GetComponent<ClimbSlotPresentation>();
		return slot?.Kind switch
		{
			ClimbSlotKind.Shop => Math.Max(0, slot.SlotIndex),
			ClimbSlotKind.Encounter => ClimbRuleService.ShopSlotCount + Math.Max(0, slot.SlotIndex),
			ClimbSlotKind.Event => ClimbRuleService.ShopSlotCount + ClimbRuleService.EncounterSlotCount + Math.Max(0, slot.SlotIndex),
			_ => ClimbRuleService.ShopSlotCount + ClimbRuleService.EncounterSlotCount + ClimbRuleService.EventSlotCount,
		};
	}

	private void SyncShopTooltip(Entity entity, ClimbShopSlotSave save, bool hidden)
	{
		var ui = entity.GetComponent<UIElement>();
		if (ui == null) return;
		RemoveTooltipComponents(entity);
		ui.Tooltip = string.Empty;
		ui.TooltipKeywordSource = string.Empty;
		ui.TooltipPosition = TooltipPosition.Right;
		ui.TooltipOffsetPx = 30;
		ui.TooltipType = TooltipType.Text;
		if (hidden || save == null) return;

		if (string.Equals(save.kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase))
		{
			var medal = MedalFactory.Create(save.itemId);
			ui.Tooltip = medal == null ? string.Empty : $"{medal.Name}\n\n{medal.Text}";
			if (medal != null) EntityManager.AddComponent(entity, new ClimbMedalTooltipSource { MedalId = medal.Id });
			return;
		}
		if (string.Equals(save.kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase))
		{
			var equipment = EquipmentFactory.Create(save.itemId);
			if (equipment == null) return;
			equipment.Initialize(EntityManager, entity);
			EntityManager.AddComponent(entity, new ClimbShopTooltipSource { EquipmentId = save.itemId });
			EntityManager.AddComponent(entity, new EquippedEquipment { Equipment = equipment });
			EntityManager.AddComponent(entity, new EquipmentZone { Zone = EquipmentZoneType.Default });
			ui.TooltipType = TooltipType.Equipment;
			return;
		}
		if (string.Equals(save.kind, ClimbShopSlotKinds.Boon, StringComparison.OrdinalIgnoreCase))
		{
			ui.Tooltip = "Permanently improves a random card in your deck. The boon is revealed after purchase.";
			return;
		}
		if (!RunDeckService.TryParseCardKey(save.cardKey, out var cardId, out var color, out bool upgraded)) return;
		var card = CardFactory.Create(cardId);
		if (card != null)
		{
			if (upgraded) CardUpgradeService.InvokeUpgradeConfirmedOnCard(card);
			ui.TooltipKeywordSource = card.GetDisplayText();
		}
		EntityManager.AddComponent(entity, new CardTooltip
		{
			CardId = cardId,
			CardColor = color,
			IsUpgraded = upgraded,
			TooltipScale = 1f,
			CrossfadeUpgradePreview = string.Equals(save.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase),
		});
		ui.TooltipType = TooltipType.Card;
	}

	private void ClearNonShopTooltips(Entity entity)
	{
		RemoveTooltipComponents(entity);
		var ui = entity.GetComponent<UIElement>();
		if (ui == null) return;
		ui.Tooltip = string.Empty;
		ui.TooltipKeywordSource = string.Empty;
		ui.TooltipType = TooltipType.Text;
	}

	private void RemoveTooltipComponents(Entity entity)
	{
		if (entity.GetComponent<CardTooltip>() != null) EntityManager.RemoveComponent<CardTooltip>(entity);
		var equipped = entity.GetComponent<EquippedEquipment>();
		equipped?.Dispose();
		if (equipped != null) EntityManager.RemoveComponent<EquippedEquipment>(entity);
		if (entity.GetComponent<EquipmentZone>() != null) EntityManager.RemoveComponent<EquipmentZone>(entity);
		if (entity.GetComponent<ClimbShopTooltipSource>() != null) EntityManager.RemoveComponent<ClimbShopTooltipSource>(entity);
		if (entity.GetComponent<ClimbMedalTooltipSource>() != null) EntityManager.RemoveComponent<ClimbMedalTooltipSource>(entity);
		if (entity.GetComponent<ClimbMedalTooltipAnchor>() != null) EntityManager.RemoveComponent<ClimbMedalTooltipAnchor>(entity);
	}

	private Entity EnsureMarker<T>(string name, Rectangle bounds, int zOrder, bool interactable,
		UIElementEventType eventType = UIElementEventType.None, bool parallax = false)
		where T : class, IComponent, new()
	{
		var entity = EnsureEntity(name);
		EnsureComponent<T>(entity);
		if (parallax) ConfigureUiParallax(entity);
		SetBounds(entity, bounds, interactable, eventType, zOrder, false);
		return entity;
	}

	private Entity EnsureEntity(string name)
	{
		var entity = EntityManager.GetEntity(name) ?? EntityManager.CreateEntity(name);
		if (entity.GetComponent<Transform>() == null) EntityManager.AddComponent(entity, new Transform());
		if (entity.GetComponent<OwnedByScene>() == null) EntityManager.AddComponent(entity, new OwnedByScene { Scene = SceneId.Climb });
		return entity;
	}

	private T EnsureComponent<T>(Entity entity) where T : class, IComponent, new()
	{
		var component = entity.GetComponent<T>();
		if (component != null) return component;
		component = new T();
		EntityManager.AddComponent(entity, component);
		return component;
	}

	private void SetBounds(Entity entity, Rectangle bounds, bool interactable, UIElementEventType eventType, int zOrder, bool hidden)
	{
		var transform = entity.GetComponent<Transform>();
		var parent = entity.GetComponent<ParentTransform>()?.Parent;
		bool usesLocalBounds = parent != null;
		if (usesLocalBounds)
		{
			Vector2 parentWorldPosition = TransformResolverService.ResolveWorldPosition(EntityManager, parent);
			transform.Position = new Vector2(bounds.X, bounds.Y) - parentWorldPosition;
		}
		else
		{
			transform.Position = new Vector2(bounds.Center.X, bounds.Center.Y);
		}
		transform.ZOrder = zOrder;
		var ui = entity.GetComponent<UIElement>();
		if (ui == null)
		{
			ui = new UIElement();
			EntityManager.AddComponent(entity, ui);
		}
		ui.Bounds = usesLocalBounds ? new Rectangle(0, 0, bounds.Width, bounds.Height) : bounds;
		ui.IsInteractable = interactable;
		ui.EventType = eventType;
		ui.IsHidden = hidden;
	}

	private void ConfigureUiParallax(Entity entity)
	{
		var settings = ParallaxLayer.GetUIParallaxLayer();
		var parallax = entity.GetComponent<ParallaxLayer>();
		if (parallax == null)
		{
			EntityManager.AddComponent(entity, settings);
		}
		else
		{
			parallax.MultiplierX = settings.MultiplierX;
			parallax.MultiplierY = settings.MultiplierY;
			parallax.MaxOffset = settings.MaxOffset;
			parallax.SmoothTime = settings.SmoothTime;
		}

		var root = EntityManager.GetEntity(RootName);
		if (root == null) return;
		var parent = entity.GetComponent<ParentTransform>();
		if (parent == null) EntityManager.AddComponent(entity, new ParentTransform { Parent = root });
		else parent.Parent = root;
	}

	internal static void ReconcilePurchasedPresentation(ClimbV2ChoiceMotion motion, bool hidden)
	{
		if (motion?.Phase != ClimbV2MotionPhase.AwaitingPurchaseReconciliation) return;
		motion.ElapsedSeconds = 0f;
		motion.DelaySeconds = 0f;
		motion.Grayscale = 0f;
		motion.Sepia = 0f;
		if (hidden)
		{
			motion.Phase = ClimbV2MotionPhase.Settled;
			motion.Offset = Vector2.Zero;
			motion.Opacity = 0f;
			motion.Brightness = 1f;
			motion.Blur = 0f;
			return;
		}

		motion.Phase = ClimbV2MotionPhase.Entering;
		motion.Offset = new Vector2(-105f, 0f);
		motion.Opacity = 0f;
		motion.Brightness = 0.68f;
		motion.Blur = 3f;
	}

	private static string ResolveShopAsset(ClimbShopSlotSave save)
	{
		if (save == null) return string.Empty;
		if (string.Equals(save.kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase)) return "Medals/" + save.itemId;
		if (string.Equals(save.kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase)) return EquipmentArtService.GetAssetName(save.itemId);
		return RunDeckService.TryParseCardKey(save.cardKey, out var cardId, out _, out _) ? "CardArt/" + cardId : string.Empty;
	}

	private static string ResolveShopTitle(ClimbShopSlotSave save)
	{
		if (string.Equals(save?.itemId, "st_luke", StringComparison.OrdinalIgnoreCase)) return "St. Luke";
		if (string.Equals(save?.itemId, "st_michael", StringComparison.OrdinalIgnoreCase)) return "St. Michael";
		return ClimbSceneDrawHelpers.ResolveShopTitle(save);
	}

	private static string ResolveCharacterTitle(string actor) => string.Equals(actor, "Nun", StringComparison.OrdinalIgnoreCase)
		? "Sister Mara"
		: actor ?? "Character";

	private static string BuildCharacterDescription(ClimbEventDefinition definition)
	{
		if (definition == null) return string.Empty;
		string gain = (definition.GainLine1 ?? string.Empty).Trim().ToLowerInvariant();
		if (gain.StartsWith("+", StringComparison.Ordinal)) gain = "Gain " + gain;
		string when = (definition.GainLine2 ?? string.Empty).Trim().ToLowerInvariant();
		if (string.Equals(when, "next battle", StringComparison.Ordinal)) when = "in the next battle";
		string sentence = $"{gain} {when}".Trim();
		return sentence.Length == 0 ? string.Empty : char.ToUpperInvariant(sentence[0]) + sentence.Substring(1) + ".";
	}

	private static int Remaining(ClimbSlotPresentation slot, int time) => slot == null || slot.Duration <= 0
		? 0
		: Math.Clamp(slot.GeneratedAtTime + slot.Duration - Math.Max(0, time), 0, slot.Duration);

	private static string Fingerprint(string slotId, string title, int timeCost, int generatedAtTime, bool unavailable, string asset) =>
		$"{slotId}|{title}|{timeCost}|{generatedAtTime}|{unavailable}|{asset}";

	private static ClimbResourceSave Clone(ClimbResourceSave value) => new()
	{
		red = Math.Max(0, value?.red ?? 0),
		white = Math.Max(0, value?.white ?? 0),
		black = Math.Max(0, value?.black ?? 0),
	};

	private static bool HasResources(ClimbResourceSave value) =>
		(value?.red ?? 0) > 0 || (value?.white ?? 0) > 0 || (value?.black ?? 0) > 0;

	internal static bool IsPresentedEvent(ClimbEventSlotSave slot) =>
		slot?.status is ClimbEventStatus.Active or ClimbEventStatus.Pending;

	public void Shutdown()
	{
		EventManager.Unsubscribe(_loadHandler);
		EventManager.Unsubscribe(_shopSelectedHandler);
		EventManager.Unsubscribe(_sceneDeactivatingHandler);
		EventManager.Unsubscribe(_upgradeAnimationRequestedHandler);
		EventManager.Unsubscribe(_boonAnimationRequestedHandler);
		EventManager.Unsubscribe(_upgradeAnimationCompletedHandler);
		EventManager.Unsubscribe(_resourceAnimationRequestedHandler);
		EventManager.Unsubscribe(_resourceAnimationCompletedHandler);
	}
}
