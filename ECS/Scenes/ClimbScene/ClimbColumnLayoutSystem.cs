using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Climb;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Climb Column Layout")]
	public class ClimbColumnLayoutSystem : Core.System
	{
		private const string ShopColumnName = "Climb_Column_Shop";
		private const string EncounterColumnName = "Climb_Column_Encounter";
		private const string EventColumnName = "Climb_Column_Event";

		[DebugEditable(DisplayName = "Columns Bottom Padding", Step = 1, Min = 0, Max = 200)]
		public int ColumnsBottomPadding { get; set; } = 48;
		[DebugEditable(DisplayName = "Column Width", Step = 1, Min = 200, Max = 800)]
		public int ColumnWidth { get; set; } = 486;
		[DebugEditable(DisplayName = "Slot List Top Offset", Step = 1, Min = 0, Max = 120)]
		public int SlotListTopOffset { get; set; } = 59;
		[DebugEditable(DisplayName = "Shop Slot Height", Step = 1, Min = 32, Max = 160)]
		public int ShopSlotHeight { get; set; } = 131;
		[DebugEditable(DisplayName = "Column Z Order", Step = 1, Min = 0, Max = 5000)]
		public int ColumnZOrder { get; set; } = 1500;
		[DebugEditable(DisplayName = "Slot Z Order", Step = 1, Min = 0, Max = 5000)]
		public int SlotZOrder { get; set; } = 1600;
		[DebugEditable(DisplayName = "Shop Tooltip Offset Px", Step = 1, Min = 0, Max = 120)]
		public int ShopTooltipOffsetPx { get; set; } = 30;
		[DebugEditable(DisplayName = "Events Enter Seconds", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float EventsEnterSeconds { get; set; } = 0.32f;
		[DebugEditable(DisplayName = "Events Leave Seconds", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float EventsLeaveSeconds { get; set; } = 0.42f;
		[DebugEditable(DisplayName = "Events Leave Split", Step = 0.01f, Min = 0.05f, Max = 0.95f)]
		public float EventsLeaveSplit { get; set; } = 0.55f;
		[DebugEditable(DisplayName = "Slot Refresh Exit Seconds", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float SlotRefreshExitSeconds { get; set; } = 0.32f;
		[DebugEditable(DisplayName = "Slot Refresh Enter Seconds", Step = 0.01f, Min = 0.01f, Max = 2f)]
		public float SlotRefreshEnterSeconds { get; set; } = 0.34f;
		[DebugEditable(DisplayName = "Slot Refresh Stagger Seconds", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float SlotRefreshStaggerSeconds { get; set; } = 0.07f;
		[DebugEditable(DisplayName = "Slot Refresh Travel Multiplier", Step = 0.01f, Min = 0.1f, Max = 2f)]
		public float SlotRefreshTravelMultiplier { get; set; } = 1.15f;
		[DebugEditable(DisplayName = "Slot Refresh Exit Min Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
		public float SlotRefreshExitMinOpacity { get; set; } = 0.15f;

		internal static int ColumnsBottomPaddingValue { get; private set; } = 48;
		internal static int ColumnWidthValue { get; private set; } = 486;
		internal static int SlotListTopOffsetValue { get; private set; } = 59;
		internal static int ShopSlotHeightValue { get; private set; } = 131;
		internal static int ColumnZOrderValue { get; private set; } = 1500;
		internal static int SlotZOrderValue { get; private set; } = 1600;
		internal static int ShopTooltipOffsetPxValue { get; private set; } = 30;

		public ClimbColumnLayoutSystem(EntityManager entityManager)
			: base(entityManager)
		{
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			ColumnsBottomPaddingValue = ColumnsBottomPadding;
			ColumnWidthValue = ColumnWidth;
			SlotListTopOffsetValue = SlotListTopOffset;
			ShopSlotHeightValue = ShopSlotHeight;
			ColumnZOrderValue = ColumnZOrder;
			SlotZOrderValue = SlotZOrder;
			ShopTooltipOffsetPxValue = ShopTooltipOffsetPx;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene?.Current != SceneId.Climb)
			{
				RestoreTransitionInputSuppression();
				ClimbSceneSystem.DeactivateClimbUiEntities(EntityManager);
				return;
			}

			var climb = SaveCache.GetClimbState();
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			if (ClimbRuleService.EnsureEncounterMutationTargets(climb, SaveCache.GetAll()?.runMapSeed ?? 0, loadout))
			{
				SaveCache.SaveClimbState(climb);
			}
			var activeEvents = GetActiveEvents(climb);
			bool showEvents = activeEvents.Count > 0;
			var transition = EnsureTransitionState();
			var slotRefresh = EnsureSlotRefreshTransitionState();
			UpdateTransition(transition, showEvents, activeEvents, gameTime);

			var columns = ComputeAnimatedColumnsLayout(transition);
			float eventOpacity = ResolveEventOpacity(transition);
			bool eventsVisible = IsEventsVisuallyPresent(transition);
			var visibleEvents = transition.Phase == ClimbColumnTransitionPhase.LeavingEvents
				? transition.CachedEventSlots
				: activeEvents;

			SyncColumn(ShopColumnName, ClimbColumnKind.Shop, "Shop", "Spend resources before the shop refreshes", columns.Shop);
			SyncColumn(EncounterColumnName, ClimbColumnKind.Encounter, "Encounters", "Fight foes for red, white, and black resources", columns.Encounter);
			SyncColumn(EventColumnName, ClimbColumnKind.Event, "Events", "Hazards and characters appear during the climb", columns.Events, eventsVisible, eventOpacity);
			SyncSlots(climb, columns, eventsVisible, visibleEvents, eventOpacity);
			var currentSnapshots = CaptureSlotSnapshots();
			UpdateSlotRefreshTransition(slotRefresh, transition, currentSnapshots, columns, gameTime);
			ApplySlotRefreshPresentation(slotRefresh, columns);
			SyncTransitionInputSuppression(transition.IsAnimating || slotRefresh.IsAnimating);
		}

		private ClimbColumnTransitionState EnsureTransitionState()
		{
			var root = EnsureClimbRoot();
			var transition = root.GetComponent<ClimbColumnTransitionState>();
			if (transition == null)
			{
				transition = new ClimbColumnTransitionState();
				EntityManager.AddComponent(root, transition);
			}
			return transition;
		}

		private ClimbSlotRefreshTransitionState EnsureSlotRefreshTransitionState()
		{
			var root = EnsureClimbRoot();
			var transition = root.GetComponent<ClimbSlotRefreshTransitionState>();
			if (transition == null)
			{
				transition = new ClimbSlotRefreshTransitionState();
				EntityManager.AddComponent(root, transition);
			}
			return transition;
		}

		private Entity EnsureClimbRoot()
		{
			var root = EntityManager.GetEntity(ClimbHeaderLayoutSystem.RootName);
			if (root == null)
			{
				root = EntityManager.CreateEntity(ClimbHeaderLayoutSystem.RootName);
				EntityManager.AddComponent(root, new Transform());
				EntityManager.AddComponent(root, new OwnedByScene { Scene = SceneId.Climb });
			}

			if (root.GetComponent<ClimbSceneRoot>() == null)
			{
				EntityManager.AddComponent(root, new ClimbSceneRoot());
			}

			return root;
		}

		private void UpdateTransition(
			ClimbColumnTransitionState transition,
			bool targetShowEvents,
			IReadOnlyList<ClimbEventSlotSave> activeEvents,
			GameTime gameTime)
		{
			if (!transition.IsInitialized)
			{
				transition.IsInitialized = true;
				transition.CurrentShowEvents = targetShowEvents;
				transition.TargetShowEvents = targetShowEvents;
				transition.Phase = ClimbColumnTransitionPhase.Idle;
				transition.ElapsedSeconds = 0f;
				if (targetShowEvents) CacheEventSlots(transition, activeEvents);
				else transition.CachedEventSlots.Clear();
				return;
			}

			if (targetShowEvents && activeEvents.Count > 0)
			{
				CacheEventSlots(transition, activeEvents);
			}

			if (targetShowEvents != transition.TargetShowEvents
				|| (transition.Phase == ClimbColumnTransitionPhase.Idle && targetShowEvents != transition.CurrentShowEvents))
			{
				BeginTransition(transition, targetShowEvents, activeEvents);
			}

			if (!transition.IsAnimating) return;

			float dt = (float)(gameTime?.ElapsedGameTime.TotalSeconds ?? 0);
			transition.ElapsedSeconds += Math.Max(0f, dt);
			float duration = GetTransitionDuration(transition.Phase);
			if (transition.ElapsedSeconds < duration) return;

			transition.CurrentShowEvents = transition.TargetShowEvents;
			transition.Phase = ClimbColumnTransitionPhase.Idle;
			transition.ElapsedSeconds = 0f;
			if (transition.CurrentShowEvents) CacheEventSlots(transition, activeEvents);
			else transition.CachedEventSlots.Clear();
		}

		private void BeginTransition(
			ClimbColumnTransitionState transition,
			bool targetShowEvents,
			IReadOnlyList<ClimbEventSlotSave> activeEvents)
		{
			transition.TargetShowEvents = targetShowEvents;
			transition.ElapsedSeconds = 0f;

			if (targetShowEvents == transition.CurrentShowEvents)
			{
				transition.Phase = ClimbColumnTransitionPhase.Idle;
				if (targetShowEvents) CacheEventSlots(transition, activeEvents);
				else transition.CachedEventSlots.Clear();
				return;
			}

			transition.Phase = targetShowEvents
				? ClimbColumnTransitionPhase.EnteringEvents
				: ClimbColumnTransitionPhase.LeavingEvents;

			if (targetShowEvents)
			{
				CacheEventSlots(transition, activeEvents);
			}
		}

		private ClimbColumnsLayout ComputeAnimatedColumnsLayout(ClimbColumnTransitionState transition)
		{
			var twoColumn = ComputeColumnsLayout(false);
			var threeColumn = ComputeColumnsLayout(true);
			var offscreenEvents = new Rectangle(
				Game1.VirtualWidth + ClimbColumnDisplaySystem.ColumnsGapValue,
				threeColumn.Events.Y,
				threeColumn.Events.Width,
				threeColumn.Events.Height);

			if (transition.Phase == ClimbColumnTransitionPhase.EnteringEvents)
			{
				float t = Ease(Progress(transition, EventsEnterSeconds));
				return BuildColumnsLayout(
					Lerp(twoColumn.Shop, threeColumn.Shop, t),
					Lerp(twoColumn.Encounter, threeColumn.Encounter, t),
					Lerp(offscreenEvents, threeColumn.Events, t));
			}

			if (transition.Phase == ClimbColumnTransitionPhase.LeavingEvents)
			{
				float split = MathHelper.Clamp(EventsLeaveSplit, 0.05f, 0.95f);
				float p = Progress(transition, EventsLeaveSeconds);
				float eventT = Ease(MathHelper.Clamp(p / split, 0f, 1f));
				float columnT = Ease(MathHelper.Clamp((p - split) / (1f - split), 0f, 1f));
				return BuildColumnsLayout(
					Lerp(threeColumn.Shop, twoColumn.Shop, columnT),
					Lerp(threeColumn.Encounter, twoColumn.Encounter, columnT),
					Lerp(threeColumn.Events, offscreenEvents, eventT));
			}

			return transition.CurrentShowEvents
				? threeColumn
				: twoColumn;
		}

		private float ResolveEventOpacity(ClimbColumnTransitionState transition)
		{
			if (transition.Phase == ClimbColumnTransitionPhase.EnteringEvents)
			{
				return Ease(Progress(transition, EventsEnterSeconds));
			}

			if (transition.Phase == ClimbColumnTransitionPhase.LeavingEvents)
			{
				float split = MathHelper.Clamp(EventsLeaveSplit, 0.05f, 0.95f);
				float t = Ease(MathHelper.Clamp(Progress(transition, EventsLeaveSeconds) / split, 0f, 1f));
				return 1f - t;
			}

			return transition.CurrentShowEvents ? 1f : 0f;
		}

		private static bool IsEventsVisuallyPresent(ClimbColumnTransitionState transition)
		{
			return transition.CurrentShowEvents
				|| transition.TargetShowEvents
				|| transition.IsAnimating;
		}

		private float Progress(ClimbColumnTransitionState transition, float duration)
		{
			return MathHelper.Clamp(transition.ElapsedSeconds / Math.Max(0.001f, duration), 0f, 1f);
		}

		private float GetTransitionDuration(ClimbColumnTransitionPhase phase)
		{
			return phase == ClimbColumnTransitionPhase.LeavingEvents
				? Math.Max(0.001f, EventsLeaveSeconds)
				: Math.Max(0.001f, EventsEnterSeconds);
		}

		private static ClimbColumnsLayout BuildColumnsLayout(Rectangle shop, Rectangle encounter, Rectangle events)
		{
			int pad = ClimbColumnDisplaySystem.ColumnPaddingValue;
			return new ClimbColumnsLayout
			{
				Shop = shop,
				Encounter = encounter,
				Events = events,
				ShopInner = new Rectangle(shop.X + pad, shop.Y + pad, Math.Max(0, shop.Width - pad * 2), Math.Max(0, shop.Height - pad * 2)),
				EncounterInner = new Rectangle(encounter.X + pad, encounter.Y + pad, Math.Max(0, encounter.Width - pad * 2), Math.Max(0, encounter.Height - pad * 2)),
				EventInner = new Rectangle(events.X + pad, events.Y + pad, Math.Max(0, events.Width - pad * 2), Math.Max(0, events.Height - pad * 2)),
			};
		}

		private static Rectangle Lerp(Rectangle from, Rectangle to, float amount)
		{
			return new Rectangle(
				(int)Math.Round(MathHelper.Lerp(from.X, to.X, amount)),
				(int)Math.Round(MathHelper.Lerp(from.Y, to.Y, amount)),
				(int)Math.Round(MathHelper.Lerp(from.Width, to.Width, amount)),
				(int)Math.Round(MathHelper.Lerp(from.Height, to.Height, amount)));
		}

		private static float Ease(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		private static List<ClimbEventSlotSave> GetActiveEvents(ClimbSaveState climb)
		{
			return climb?.eventSlots?
				.Where(slot => slot?.status == ClimbEventStatus.Active)
				.OrderBy(slot => slot.activatedAtTime)
				.ThenBy(slot => slot.id, StringComparer.Ordinal)
				.ToList()
				?? new List<ClimbEventSlotSave>();
		}

		private static void CacheEventSlots(ClimbColumnTransitionState transition, IReadOnlyList<ClimbEventSlotSave> activeEvents)
		{
			transition.CachedEventSlots = activeEvents?
				.Select(CloneEventSlot)
				.Where(slot => slot != null)
				.ToList()
				?? new List<ClimbEventSlotSave>();
		}

		private static ClimbEventSlotSave CloneEventSlot(ClimbEventSlotSave slot)
		{
			if (slot == null) return null;
			return new ClimbEventSlotSave
			{
				id = slot.id,
				definitionId = slot.definitionId,
				kind = slot.kind,
				hazardEffect = slot.hazardEffect,
				characterReward = slot.characterReward,
				scheduledAppearanceTime = slot.scheduledAppearanceTime,
				activatedAtTime = slot.activatedAtTime,
				duration = slot.duration,
				timeCost = slot.timeCost,
				effectAmount = slot.effectAmount,
				rewardResources = Clone(slot.rewardResources),
				status = ClimbEventStatus.Active,
			};
		}

		private void SyncTransitionInputSuppression(bool suppress)
		{
			if (!suppress)
			{
				RestoreTransitionInputSuppression();
				return;
			}

			foreach (var entity in EntityManager.GetAllEntities().Where(IsClimbUiEntity).ToList())
			{
				var ui = entity.GetComponent<UIElement>();
				if (ui == null) continue;

				if (entity.GetComponent<ClimbColumnTransitionInputSuppression>() == null)
				{
					ui.Suppress();
					EntityManager.AddComponent(entity, new ClimbColumnTransitionInputSuppression());
				}
				ui.IsHovered = false;
				ui.IsClicked = false;
			}
		}

		private void RestoreTransitionInputSuppression()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<ClimbColumnTransitionInputSuppression>().ToList())
			{
				entity.GetComponent<UIElement>()?.Restore();
				EntityManager.RemoveComponent<ClimbColumnTransitionInputSuppression>(entity);
			}
		}

		private static bool IsClimbUiEntity(Entity entity)
		{
			if (entity?.GetComponent<UIElement>() == null) return false;
			return entity.GetComponent<OwnedByScene>()?.Scene == SceneId.Climb
				|| entity.GetComponent<ClimbSceneRoot>() != null
				|| entity.GetComponent<ClimbHeaderElement>() != null
				|| entity.GetComponent<ClimbTimelineElement>() != null
				|| entity.GetComponent<ClimbResourceBarElement>() != null
				|| entity.GetComponent<ClimbLoadoutButton>() != null
				|| entity.GetComponent<ClimbColumnPresentation>() != null
				|| entity.GetComponent<ClimbSlotPresentation>() != null
				|| entity.GetComponent<ClimbShopTooltipSource>() != null
				|| entity.GetComponent<ClimbMedalTooltipSource>() != null;
		}

		private void SyncSlots(ClimbSaveState climb, ClimbColumnsLayout columns, bool showEvents, IReadOnlyList<ClimbEventSlotSave> visibleEvents, float eventOpacity)
		{
			for (int i = 0; i < ClimbRuleService.ShopSlotCount; i++)
			{
				var slot = climb?.shopSlots != null && i < climb.shopSlots.Count ? climb.shopSlots[i] : null;
				bool hidden = slot == null
					|| IsSoldShopSlot(slot)
					|| string.Equals(slot.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase);
				var rect = hidden ? Rectangle.Empty : ComputeShopSlotRect(columns.ShopInner, i);
				SyncShopSlot(i, slot, rect, hidden: hidden);
			}

			for (int i = 0; i < ClimbRuleService.EncounterSlotCount; i++)
			{
				var slot = climb?.encounterSlots != null && i < climb.encounterSlots.Count ? climb.encounterSlots[i] : null;
				var rect = ComputeEncounterSlotRect(columns.EncounterInner, i);
				SyncEncounterSlot(i, slot, rect);
			}

			var activeEvents = showEvents && visibleEvents != null
				? visibleEvents.ToList()
				: new List<ClimbEventSlotSave>();
			Debug.Assert(activeEvents.Count <= 2, "Climb event scheduling exceeded the supported adjacent-band concurrency bound.");
			for (int i = 0; i < ClimbRuleService.EventSlotCount; i++)
			{
				var slot = i < activeEvents.Count ? activeEvents[i] : null;
				var rect = ComputeEventSlotRect(columns.EventInner, i);
				SyncEventSlot(i, slot, rect, showEvents && slot != null, eventOpacity);
			}
		}

		private void UpdateSlotRefreshTransition(
			ClimbSlotRefreshTransitionState slotRefresh,
			ClimbColumnTransitionState columnTransition,
			IReadOnlyList<ClimbSlotVisualSnapshot> currentSnapshots,
			ClimbColumnsLayout columns,
			GameTime gameTime)
		{
			if (slotRefresh == null) return;
			currentSnapshots ??= Array.Empty<ClimbSlotVisualSnapshot>();

			if (!slotRefresh.IsInitialized)
			{
				slotRefresh.IsInitialized = true;
				slotRefresh.PreviousSnapshots = CloneSnapshots(currentSnapshots);
				slotRefresh.Phase = ClimbSlotRefreshPhase.Idle;
				slotRefresh.ElapsedSeconds = 0f;
				slotRefresh.Jobs.Clear();
				HideAllSlotRefreshShadows();
				return;
			}

			if (slotRefresh.IsAnimating)
			{
				float dt = (float)(gameTime?.ElapsedGameTime.TotalSeconds ?? 0);
				slotRefresh.ElapsedSeconds += Math.Max(0f, dt);
				if (slotRefresh.ElapsedSeconds >= GetSlotRefreshTotalSeconds(slotRefresh.Jobs))
				{
					slotRefresh.Phase = ClimbSlotRefreshPhase.Idle;
					slotRefresh.ElapsedSeconds = 0f;
					slotRefresh.Jobs.Clear();
					slotRefresh.PreviousSnapshots = CloneSnapshots(currentSnapshots);
					HideAllSlotRefreshShadows();
				}
				return;
			}

			var jobs = BuildSlotRefreshJobs(slotRefresh.PreviousSnapshots, currentSnapshots, columnTransition);
			slotRefresh.PreviousSnapshots = CloneSnapshots(currentSnapshots);
			if (jobs.Count == 0)
			{
				HideAllSlotRefreshShadows();
				return;
			}

			slotRefresh.Jobs = jobs;
			slotRefresh.Phase = ClimbSlotRefreshPhase.Animating;
			slotRefresh.ElapsedSeconds = 0f;
			HideAllSlotRefreshShadows();

			if (jobs.Any(job => job.HasOutgoing))
			{
				EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.ClimbWidgetLeave, Volume = 0.5f });
			}
		}

		private List<ClimbSlotRefreshJob> BuildSlotRefreshJobs(
			IReadOnlyList<ClimbSlotVisualSnapshot> previousSnapshots,
			IReadOnlyList<ClimbSlotVisualSnapshot> currentSnapshots,
			ClimbColumnTransitionState columnTransition)
		{
			var previous = (previousSnapshots ?? Array.Empty<ClimbSlotVisualSnapshot>())
				.ToDictionary(SnapshotKey, snapshot => snapshot);
			var current = (currentSnapshots ?? Array.Empty<ClimbSlotVisualSnapshot>())
				.ToDictionary(SnapshotKey, snapshot => snapshot);
			var keys = previous.Keys
				.Concat(current.Keys)
				.Distinct(StringComparer.Ordinal)
				.OrderBy(SnapshotSortKey)
				.ToList();
			int previousEventCount = previous.Values.Count(snapshot => snapshot.Kind == ClimbSlotKind.Event && snapshot.IsVisible);
			int currentEventCount = current.Values.Count(snapshot => snapshot.Kind == ClimbSlotKind.Event && snapshot.IsVisible);
			bool allowEventJobs = columnTransition?.IsAnimating != true
				&& previousEventCount > 0
				&& currentEventCount > 0;

			var jobs = new List<ClimbSlotRefreshJob>();
			foreach (string key in keys)
			{
				previous.TryGetValue(key, out var outgoing);
				current.TryGetValue(key, out var incoming);
				var kind = incoming?.Kind ?? outgoing?.Kind ?? ClimbSlotKind.Shop;
				if (kind == ClimbSlotKind.Event && !allowEventJobs) continue;
				bool outgoingVisible = outgoing?.IsVisible == true;
				bool incomingVisible = incoming?.IsVisible == true;
				if (!outgoingVisible && !incomingVisible) continue;
				if (outgoingVisible == incomingVisible
					&& string.Equals(outgoing?.Fingerprint, incoming?.Fingerprint, StringComparison.Ordinal))
				{
					continue;
				}

				jobs.Add(new ClimbSlotRefreshJob
				{
					Kind = kind,
					SlotIndex = incoming?.SlotIndex ?? outgoing?.SlotIndex ?? -1,
					Outgoing = outgoingVisible ? CloneSnapshot(outgoing) : null,
					Incoming = incomingVisible ? CloneSnapshot(incoming) : null,
				});
			}

			jobs = jobs
				.OrderBy(job => KindSortOrder(job.Kind))
				.ThenBy(job => job.SlotIndex)
				.ToList();
			for (int i = 0; i < jobs.Count; i++)
			{
				jobs[i].StaggerSeconds = Math.Max(0f, SlotRefreshStaggerSeconds) * i;
			}
			return jobs;
		}

		private void ApplySlotRefreshPresentation(ClimbSlotRefreshTransitionState slotRefresh, ClimbColumnsLayout columns)
		{
			if (slotRefresh?.IsAnimating != true)
			{
				HideAllSlotRefreshShadows();
				return;
			}

			HideAllSlotRefreshShadows();
			foreach (var job in slotRefresh.Jobs)
			{
				if (job.SlotIndex < 0) continue;
				var rect = ComputeSlotRect(job.Kind, columns, job.SlotIndex);
				if (rect.Width <= 0 || rect.Height <= 0) continue;
				float travel = rect.Width * Math.Max(0.1f, SlotRefreshTravelMultiplier);

				if (job.HasOutgoing)
				{
					float exitLocal = MathHelper.Clamp((slotRefresh.ElapsedSeconds - job.StaggerSeconds) / Math.Max(0.001f, SlotRefreshExitSeconds), 0f, 1f);
					if (exitLocal < 1f)
					{
						float eased = EaseInCubic(exitLocal);
						float offset = -travel * eased;
						float opacity = MathHelper.Lerp(1f, MathHelper.Clamp(SlotRefreshExitMinOpacity, 0f, 1f), eased);
						SyncSlotRefreshShadow(job, rect, offset, opacity);
					}
				}

				if (job.HasIncoming)
				{
					float enterDelay = job.HasOutgoing ? Math.Max(0.001f, SlotRefreshExitSeconds) : 0f;
					float enterLocal = MathHelper.Clamp(
						(slotRefresh.ElapsedSeconds - job.StaggerSeconds - enterDelay) / Math.Max(0.001f, SlotRefreshEnterSeconds),
						0f,
						1f);
					float eased = EaseOutCubic(enterLocal);
					ApplyIncomingSlotAnimation(job, travel * (1f - eased), eased);
				}
			}
		}

		private void ApplyIncomingSlotAnimation(ClimbSlotRefreshJob job, float offsetX, float opacity)
		{
			var entity = EntityManager.GetEntity(GetRealSlotEntityName(job.Kind, job.SlotIndex));
			var slot = entity?.GetComponent<ClimbSlotPresentation>();
			if (slot == null) return;
			slot.AnimationOffsetX = offsetX;
			slot.AnimationOpacityMultiplier = MathHelper.Clamp(opacity, 0f, 1f);
			slot.ClipToBounds = true;
		}

		private void SyncSlotRefreshShadow(ClimbSlotRefreshJob job, Rectangle rect, float offsetX, float opacity)
		{
			var entity = EnsureEntity($"Climb_SlotRefresh_{job.Kind}_{job.SlotIndex}");
			ConfigureParallax(entity, ParallaxLayer.GetUIParallaxLayer());
			var presentation = EnsureSlotPresentation(entity);
			CopySnapshotToPresentation(job.Outgoing, presentation);
			presentation.AnimationOffsetX = offsetX;
			presentation.AnimationOpacityMultiplier = MathHelper.Clamp(opacity, 0f, 1f);
			presentation.ClipToBounds = true;
			presentation.IsRefreshShadow = true;
			SetBounds(entity, rect, interactable: false, UIElementEventType.None, SlotZOrderValue + 1);
		}

		private void HideAllSlotRefreshShadows()
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<ClimbSlotPresentation>().ToList())
			{
				var slot = entity.GetComponent<ClimbSlotPresentation>();
				if (slot?.IsRefreshShadow != true) continue;
				var ui = entity.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.Bounds = Rectangle.Empty;
					ui.IsInteractable = false;
					ui.IsHidden = true;
					ui.IsHovered = false;
					ui.IsClicked = false;
				}
			}
		}

		private IReadOnlyList<ClimbSlotVisualSnapshot> CaptureSlotSnapshots()
		{
			return EntityManager.GetEntitiesWithComponent<ClimbSlotPresentation>()
				.Select(entity => new { Entity = entity, Slot = entity.GetComponent<ClimbSlotPresentation>(), UI = entity.GetComponent<UIElement>() })
				.Where(x => x.Slot != null && !x.Slot.IsRefreshShadow)
				.Select(x => SnapshotFromPresentation(x.Slot, x.UI))
				.OrderBy(snapshot => KindSortOrder(snapshot.Kind))
				.ThenBy(snapshot => snapshot.SlotIndex)
				.ToList();
		}

		private static ClimbSlotVisualSnapshot SnapshotFromPresentation(ClimbSlotPresentation slot, UIElement ui)
		{
			bool visible = ui?.IsHidden == false && ui.Bounds.Width > 0 && ui.Bounds.Height > 0;
			var snapshot = new ClimbSlotVisualSnapshot
			{
				Kind = slot.Kind,
				SlotIndex = slot.SlotIndex,
				SlotId = slot.SlotId ?? string.Empty,
				IsVisible = visible,
				Title = slot.Title ?? string.Empty,
				Label = slot.Label ?? string.Empty,
				Meta = slot.Meta ?? string.Empty,
				GeneratedAtTime = slot.GeneratedAtTime,
				Duration = slot.Duration,
				TimeCost = slot.TimeCost,
				Cost = Clone(slot.Cost),
				Reward = Clone(slot.Reward),
				IsSold = slot.IsSold,
				IsCompleted = slot.IsCompleted,
				IsUnavailable = slot.IsUnavailable,
				IsAffordable = slot.IsAffordable,
				IsFinal = slot.IsFinal,
				BattleLocation = slot.BattleLocation,
				PortraitAsset = slot.PortraitAsset ?? string.Empty,
				EventKind = slot.EventKind,
				GainLine1 = slot.GainLine1 ?? string.Empty,
				GainLine2 = slot.GainLine2 ?? string.Empty,
				Opacity = slot.Opacity,
			};
			snapshot.Fingerprint = BuildFingerprint(snapshot);
			return snapshot;
		}

		private static string BuildFingerprint(ClimbSlotVisualSnapshot snapshot)
		{
			if (snapshot == null || !snapshot.IsVisible) return "hidden";
			return string.Join("|", new[]
			{
				snapshot.Kind.ToString(),
				snapshot.SlotId,
				snapshot.Title,
				snapshot.Label,
				snapshot.Meta,
				snapshot.GeneratedAtTime.ToString(),
				snapshot.Duration.ToString(),
				snapshot.TimeCost.ToString(),
				ResourceFingerprint(snapshot.Cost),
				ResourceFingerprint(snapshot.Reward),
				snapshot.IsSold.ToString(),
				snapshot.IsCompleted.ToString(),
				snapshot.IsUnavailable.ToString(),
				snapshot.IsFinal.ToString(),
				snapshot.BattleLocation.ToString(),
				snapshot.PortraitAsset,
				snapshot.EventKind.ToString(),
				snapshot.GainLine1,
				snapshot.GainLine2,
			});
		}

		private static string ResourceFingerprint(ClimbResourceSave resources)
		{
			return $"{Math.Max(0, resources?.red ?? 0)},{Math.Max(0, resources?.white ?? 0)},{Math.Max(0, resources?.black ?? 0)}";
		}

		private static void CopySnapshotToPresentation(ClimbSlotVisualSnapshot snapshot, ClimbSlotPresentation presentation)
		{
			if (snapshot == null || presentation == null) return;
			presentation.Kind = snapshot.Kind;
			presentation.SlotIndex = snapshot.SlotIndex;
			presentation.SlotId = snapshot.SlotId;
			presentation.Title = snapshot.Title;
			presentation.Label = snapshot.Label;
			presentation.Meta = snapshot.Meta;
			presentation.GeneratedAtTime = snapshot.GeneratedAtTime;
			presentation.Duration = snapshot.Duration;
			presentation.TimeCost = snapshot.TimeCost;
			presentation.Cost = Clone(snapshot.Cost);
			presentation.Reward = Clone(snapshot.Reward);
			presentation.IsSold = snapshot.IsSold;
			presentation.IsCompleted = snapshot.IsCompleted;
			presentation.IsUnavailable = snapshot.IsUnavailable;
			presentation.IsAffordable = snapshot.IsAffordable;
			presentation.IsFinal = snapshot.IsFinal;
			presentation.BattleLocation = snapshot.BattleLocation;
			presentation.PortraitAsset = snapshot.PortraitAsset;
			presentation.EventKind = snapshot.EventKind;
			presentation.GainLine1 = snapshot.GainLine1;
			presentation.GainLine2 = snapshot.GainLine2;
			presentation.Opacity = snapshot.Opacity;
		}

		private static List<ClimbSlotVisualSnapshot> CloneSnapshots(IReadOnlyList<ClimbSlotVisualSnapshot> snapshots)
		{
			return (snapshots ?? Array.Empty<ClimbSlotVisualSnapshot>())
				.Select(CloneSnapshot)
				.Where(snapshot => snapshot != null)
				.ToList();
		}

		private static ClimbSlotVisualSnapshot CloneSnapshot(ClimbSlotVisualSnapshot snapshot)
		{
			if (snapshot == null) return null;
			return new ClimbSlotVisualSnapshot
			{
				Kind = snapshot.Kind,
				SlotIndex = snapshot.SlotIndex,
				SlotId = snapshot.SlotId ?? string.Empty,
				Fingerprint = snapshot.Fingerprint ?? string.Empty,
				IsVisible = snapshot.IsVisible,
				Title = snapshot.Title ?? string.Empty,
				Label = snapshot.Label ?? string.Empty,
				Meta = snapshot.Meta ?? string.Empty,
				GeneratedAtTime = snapshot.GeneratedAtTime,
				Duration = snapshot.Duration,
				TimeCost = snapshot.TimeCost,
				Cost = Clone(snapshot.Cost),
				Reward = Clone(snapshot.Reward),
				IsSold = snapshot.IsSold,
				IsCompleted = snapshot.IsCompleted,
				IsUnavailable = snapshot.IsUnavailable,
				IsAffordable = snapshot.IsAffordable,
				IsFinal = snapshot.IsFinal,
				BattleLocation = snapshot.BattleLocation,
				PortraitAsset = snapshot.PortraitAsset ?? string.Empty,
				EventKind = snapshot.EventKind,
				GainLine1 = snapshot.GainLine1 ?? string.Empty,
				GainLine2 = snapshot.GainLine2 ?? string.Empty,
				Opacity = snapshot.Opacity,
			};
		}

		private float GetSlotRefreshTotalSeconds(IReadOnlyList<ClimbSlotRefreshJob> jobs)
		{
			if (jobs == null || jobs.Count == 0) return 0f;
			float max = 0f;
			foreach (var job in jobs)
			{
				float duration = job.StaggerSeconds;
				if (job.HasOutgoing) duration += Math.Max(0.001f, SlotRefreshExitSeconds);
				if (job.HasIncoming) duration += Math.Max(0.001f, SlotRefreshEnterSeconds);
				max = Math.Max(max, duration);
			}
			return max;
		}

		private static Rectangle ComputeSlotRect(ClimbSlotKind kind, ClimbColumnsLayout columns, int index)
		{
			return kind switch
			{
				ClimbSlotKind.Shop => ComputeShopSlotRect(columns.ShopInner, index),
				ClimbSlotKind.Event => ComputeEventSlotRect(columns.EventInner, index),
				_ => ComputeEncounterSlotRect(columns.EncounterInner, index),
			};
		}

		private static string GetRealSlotEntityName(ClimbSlotKind kind, int index)
		{
			return kind switch
			{
				ClimbSlotKind.Shop => $"Climb_ShopSlot_{index}",
				ClimbSlotKind.Event => $"Climb_EventSlot_{index}",
				_ => $"Climb_EncounterSlot_{index}",
			};
		}

		private static string SnapshotKey(ClimbSlotVisualSnapshot snapshot)
		{
			return $"{KindSortOrder(snapshot?.Kind ?? ClimbSlotKind.Shop)}:{snapshot?.SlotIndex ?? -1}";
		}

		private static string SnapshotSortKey(string key)
		{
			return key ?? string.Empty;
		}

		private static int KindSortOrder(ClimbSlotKind kind)
		{
			return kind switch
			{
				ClimbSlotKind.Shop => 0,
				ClimbSlotKind.Encounter => 1,
				ClimbSlotKind.Event => 2,
				_ => 3,
			};
		}

		private static float EaseInCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t * t * t;
		}

		private static float EaseOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float inv = 1f - t;
			return 1f - inv * inv * inv;
		}

		private static void ResetSlotAnimation(ClimbSlotPresentation presentation)
		{
			if (presentation == null) return;
			presentation.AnimationOffsetX = 0f;
			presentation.AnimationOpacityMultiplier = 1f;
			presentation.ClipToBounds = false;
			presentation.IsRefreshShadow = false;
		}

		private void SyncColumn(string name, ClimbColumnKind kind, string title, string subtitle, Rectangle bounds, bool visible = true, float opacity = 1f)
		{
			var entity = EnsureEntity(name);
			ConfigureParallax(entity, ParallaxLayer.GetLocationParallaxLayer());
			var column = entity.GetComponent<ClimbColumnPresentation>();
			if (column == null)
			{
				column = new ClimbColumnPresentation();
				EntityManager.AddComponent(entity, column);
			}
			column.Kind = kind;
			column.Title = title;
			column.Subtitle = subtitle;
			column.IsVisible = visible;
			column.Opacity = MathHelper.Clamp(opacity, 0f, 1f);
			int padding = ClimbColumnDisplaySystem.ColumnPaddingValue;
			column.InnerBounds = entity.HasComponent<ParentTransform>()
				? new Rectangle(padding, padding, Math.Max(0, bounds.Width - padding * 2), Math.Max(0, bounds.Height - padding * 2))
				: new Rectangle(bounds.X + padding, bounds.Y + padding, Math.Max(0, bounds.Width - padding * 2), Math.Max(0, bounds.Height - padding * 2));
			SetBounds(entity, bounds, interactable: false, UIElementEventType.None, ColumnZOrderValue);
		}

		private static bool IsSoldShopSlot(ClimbShopSlotSave slot) => slot?.isSold == true;

		private void SyncShopSlot(int index, ClimbShopSlotSave slot, Rectangle rect, bool hidden = false)
		{
			var entity = EnsureEntity($"Climb_ShopSlot_{index}");
			ConfigureParallax(entity, ParallaxLayer.GetUIParallaxLayer());
			var presentation = EnsureSlotPresentation(entity);
			presentation.Kind = ClimbSlotKind.Shop;
			presentation.SlotIndex = index;
			presentation.SlotId = slot?.id ?? $"shop_{index}";
			presentation.Title = ClimbSceneDrawHelpers.ResolveShopTitle(slot);
			presentation.Label = ClimbSceneDrawHelpers.ResolveShopLabel(slot);
			presentation.Meta = "PRICE";
			presentation.GeneratedAtTime = Math.Max(0, slot?.generatedAtTime ?? 0);
			presentation.Duration = 0;
			presentation.Cost = Clone(slot?.cost);
			presentation.Reward = new ClimbResourceSave { red = 0, white = 0, black = 0 };
			presentation.TimeCost = Math.Max(0, slot?.timeCost ?? 0);
			presentation.IsSold = slot?.isSold == true;
			presentation.IsCompleted = false;
			presentation.IsUnavailable = slot == null || string.Equals(slot.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase);
			presentation.IsAffordable = presentation.IsUnavailable || ClimbRuleService.CanAfford(SaveCache.GetClimbState()?.resources, slot?.cost);
			presentation.Opacity = 1f;
			ResetSlotAnimation(presentation);
			var action = entity.GetComponent<ClimbShopSlotAction>();
			if (action == null) EntityManager.AddComponent(entity, new ClimbShopSlotAction { SlotIndex = index });
			else action.SlotIndex = index;
			SetBounds(entity, rect, !hidden && !presentation.IsUnavailable && !presentation.IsSold, UIElementEventType.ClimbShopSlotSelect, SlotZOrderValue, hidden: hidden);
			SyncShopTooltip(entity, slot, presentation);
		}

		private void SyncEncounterSlot(int index, ClimbEncounterSlotSave slot, Rectangle rect)
		{
			var entity = EnsureEntity($"Climb_EncounterSlot_{index}");
			ConfigureParallax(entity, ParallaxLayer.GetUIParallaxLayer());
			var presentation = EnsureSlotPresentation(entity);
			var enemy = EnemyFactory.Create(slot?.enemyId);
			presentation.Kind = ClimbSlotKind.Encounter;
			presentation.SlotIndex = index;
			presentation.SlotId = slot?.id ?? $"encounter_{index}";
			presentation.Title = enemy?.Name ?? "Encounter";
			presentation.Label = slot?.isFinal == true ? "Final" : "Fight";
			presentation.Meta = "GAIN";
			presentation.GeneratedAtTime = Math.Max(0, slot?.generatedAtTime ?? 0);
			presentation.Duration = Math.Max(0, slot?.duration ?? 0);
			presentation.Cost = new ClimbResourceSave { red = 0, white = 0, black = 0 };
			presentation.Reward = Clone(slot?.rewardResources);
			presentation.TimeCost = Math.Max(0, slot?.timeCost ?? 0);
			presentation.IsSold = false;
			presentation.IsCompleted = slot?.isCompleted == true;
			presentation.IsUnavailable = slot == null || slot.isCompleted || string.IsNullOrWhiteSpace(slot.enemyId);
			presentation.IsAffordable = true;
			presentation.IsFinal = slot?.isFinal == true;
			presentation.BattleLocation = slot?.battleLocation ?? BattleLocation.Desert;
			presentation.PortraitAsset = EnemyPortraitContent.ToAssetName(slot?.enemyId ?? string.Empty);
			presentation.EventKind = ClimbEventKind.Hazard;
			presentation.GainLine1 = string.Empty;
			presentation.GainLine2 = string.Empty;
			presentation.Opacity = 1f;
			ResetSlotAnimation(presentation);
			var action = entity.GetComponent<ClimbEncounterSlotAction>();
			if (action == null) EntityManager.AddComponent(entity, new ClimbEncounterSlotAction { SlotId = presentation.SlotId });
			else action.SlotId = presentation.SlotId;
			SetBounds(entity, rect, !presentation.IsUnavailable, UIElementEventType.ClimbEncounterSlotSelect, SlotZOrderValue);
			SyncEncounterTooltip(entity, slot, presentation);
		}

		private void SyncEventSlot(int index, ClimbEventSlotSave slot, Rectangle rect, bool visible, float opacity)
		{
			var entity = EnsureEntity($"Climb_EventSlot_{index}");
			ConfigureParallax(entity, ParallaxLayer.GetUIParallaxLayer());
			var presentation = EnsureSlotPresentation(entity);
			presentation.Kind = ClimbSlotKind.Event;
			presentation.SlotIndex = index;
			presentation.SlotId = slot?.id ?? $"event_{index}";
			var definition = ClimbEventCatalog.Get(slot?.definitionId);
			presentation.EventKind = slot?.kind ?? ClimbEventKind.Hazard;
			presentation.Title = presentation.EventKind == ClimbEventKind.Hazard ? "Hazard" : definition?.Actor ?? "Character";
			presentation.Label = presentation.EventKind == ClimbEventKind.Hazard ? "Hazard" : "Character";
			presentation.Meta = "GAIN";
			presentation.GeneratedAtTime = Math.Max(0, slot?.activatedAtTime ?? 0);
			presentation.Duration = Math.Max(0, slot?.duration ?? 0);
			presentation.Cost = new ClimbResourceSave { red = 0, white = 0, black = 0 };
			presentation.Reward = Clone(slot?.rewardResources);
			presentation.TimeCost = presentation.EventKind == ClimbEventKind.Character ? 1 : 0;
			presentation.IsSold = false;
			presentation.IsCompleted = slot?.status is ClimbEventStatus.Completed or ClimbEventStatus.Expired;
			presentation.IsUnavailable = !visible || slot == null || slot.status != ClimbEventStatus.Active;
			presentation.IsAffordable = true;
			presentation.PortraitAsset = presentation.EventKind == ClimbEventKind.Character ? definition?.PortraitAsset ?? string.Empty : string.Empty;
			presentation.GainLine1 = definition?.GainLine1 ?? string.Empty;
			presentation.GainLine2 = definition?.GainLine2 ?? string.Empty;
			presentation.Opacity = MathHelper.Clamp(opacity, 0f, 1f);
			ResetSlotAnimation(presentation);
			var action = entity.GetComponent<ClimbEventSlotAction>();
			if (action == null) EntityManager.AddComponent(entity, new ClimbEventSlotAction { SlotId = presentation.SlotId });
			else action.SlotId = presentation.SlotId;
			SetBounds(entity, rect, visible && !presentation.IsUnavailable, UIElementEventType.ClimbEventSlotSelect, SlotZOrderValue, hidden: !visible);
		}

		private void SyncShopTooltip(Entity entity, ClimbShopSlotSave slot, ClimbSlotPresentation presentation)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;

			ui.Tooltip = string.Empty;
			ui.TooltipKeywordSource = string.Empty;
			ui.TooltipType = TooltipType.Text;
			if (slot == null || presentation.IsUnavailable || presentation.IsSold)
			{
				ClearShopTooltip(entity, ui);
				return;
			}

			ui.TooltipPosition = TooltipPosition.Right;
			ui.TooltipOffsetPx = ShopTooltipOffsetPxValue;

			if (string.Equals(slot.kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase))
			{
				RemoveCardTooltip(entity);
				RemoveEquipmentTooltip(entity);
				var medal = MedalFactory.Create(slot.itemId);
				ui.Tooltip = medal == null ? string.Empty : $"{medal.Name}\n\n{medal.Text}";
				ui.TooltipType = TooltipType.Text;
				SyncMedalTooltip(entity, medal?.Id);
				return;
			}

			if (string.Equals(slot.kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase))
			{
				RemoveCardTooltip(entity);
				RemoveMedalTooltip(entity);
				SyncEquipmentTooltip(entity, slot.itemId, ui);
				return;
			}

			if (string.Equals(slot.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase)
				|| string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase))
			{
				RemoveCardTooltip(entity);
				RemoveEquipmentTooltip(entity);
				RemoveMedalTooltip(entity);
				if (!RunDeckService.TryParseCardKey(slot.cardKey, out var cardId, out var color, out bool isUpgraded)) return;
				bool isUpgrade = string.Equals(slot.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase);
				var card = CardFactory.Create(cardId);
				if (card != null)
				{
					if (isUpgraded)
					{
						CardUpgradeService.InvokeUpgradeConfirmedOnCard(card);
					}
					ui.TooltipKeywordSource = card.GetDisplayText();
				}
				EntityManager.AddComponent(entity, new CardTooltip
				{
					CardId = cardId,
					CardColor = color,
					IsUpgraded = isUpgraded,
					TooltipScale = 1.0f,
					CrossfadeUpgradePreview = isUpgrade,
				});
				ui.TooltipType = TooltipType.Card;
			}
		}

		private void SyncEquipmentTooltip(Entity entity, string equipmentId, UIElement ui)
		{
			if (string.IsNullOrWhiteSpace(equipmentId)) return;
			var source = entity.GetComponent<ClimbShopTooltipSource>();
			var equipped = entity.GetComponent<EquippedEquipment>();
			if (source == null
				|| equipped == null
				|| !string.Equals(source.EquipmentId, equipmentId, StringComparison.OrdinalIgnoreCase))
			{
				equipped?.Dispose();
				if (source != null) EntityManager.RemoveComponent<ClimbShopTooltipSource>(entity);
				if (equipped != null) EntityManager.RemoveComponent<EquippedEquipment>(entity);
				if (entity.GetComponent<EquipmentZone>() != null) EntityManager.RemoveComponent<EquipmentZone>(entity);

				var equipment = EquipmentFactory.Create(equipmentId);
				if (equipment == null) return;
				equipment.Initialize(EntityManager, entity);
				EntityManager.AddComponent(entity, new ClimbShopTooltipSource { EquipmentId = equipmentId });
				EntityManager.AddComponent(entity, new EquippedEquipment { Equipment = equipment });
				EntityManager.AddComponent(entity, new EquipmentZone { Zone = EquipmentZoneType.Default });
			}

			ui.TooltipType = TooltipType.Equipment;
		}

		private void SyncMedalTooltip(Entity entity, string medalId)
		{
			if (string.IsNullOrWhiteSpace(medalId))
			{
				RemoveMedalTooltip(entity);
				return;
			}

			var source = entity.GetComponent<ClimbMedalTooltipSource>();
			if (source == null)
			{
				EntityManager.AddComponent(entity, new ClimbMedalTooltipSource { MedalId = medalId });
				return;
			}

			if (!string.Equals(source.MedalId, medalId, StringComparison.OrdinalIgnoreCase))
			{
				source.MedalId = medalId;
				if (entity.GetComponent<ClimbMedalTooltipAnchor>() != null)
				{
					EntityManager.RemoveComponent<ClimbMedalTooltipAnchor>(entity);
				}
			}
		}

		private void ClearShopTooltip(Entity entity, UIElement ui)
		{
			ui.Tooltip = string.Empty;
			ui.TooltipKeywordSource = string.Empty;
			ui.TooltipType = TooltipType.Text;

			RemoveCardTooltip(entity);
			RemoveEquipmentTooltip(entity);
			RemoveMedalTooltip(entity);
		}

		private void SyncEncounterTooltip(Entity entity, ClimbEncounterSlotSave slot, ClimbSlotPresentation presentation)
		{
			var ui = entity.GetComponent<UIElement>();
			if (ui == null) return;

			ui.Tooltip = string.Empty;
			ui.TooltipType = TooltipType.Text;
			ui.TooltipPosition = TooltipPosition.Right;
			ui.TooltipOffsetPx = ShopTooltipOffsetPxValue;
			RemoveEquipmentTooltip(entity);
			RemoveCardTooltip(entity);
		}

		private void RemoveCardTooltip(Entity entity)
		{
			if (entity.GetComponent<CardTooltip>() != null) EntityManager.RemoveComponent<CardTooltip>(entity);
		}

		private void RemoveEquipmentTooltip(Entity entity)
		{
			var equipped = entity.GetComponent<EquippedEquipment>();
			equipped?.Dispose();
			if (equipped != null) EntityManager.RemoveComponent<EquippedEquipment>(entity);
			if (entity.GetComponent<EquipmentZone>() != null) EntityManager.RemoveComponent<EquipmentZone>(entity);
			if (entity.GetComponent<ClimbShopTooltipSource>() != null) EntityManager.RemoveComponent<ClimbShopTooltipSource>(entity);
		}

		private void RemoveMedalTooltip(Entity entity)
		{
			if (entity.GetComponent<ClimbMedalTooltipAnchor>() != null) EntityManager.RemoveComponent<ClimbMedalTooltipAnchor>(entity);
			if (entity.GetComponent<ClimbMedalTooltipSource>() != null) EntityManager.RemoveComponent<ClimbMedalTooltipSource>(entity);
		}

		private ClimbSlotPresentation EnsureSlotPresentation(Entity entity)
		{
			var presentation = entity.GetComponent<ClimbSlotPresentation>();
			if (presentation == null)
			{
				presentation = new ClimbSlotPresentation();
				EntityManager.AddComponent(entity, presentation);
			}
			return presentation;
		}

		private Entity EnsureEntity(string name)
		{
			var entity = EntityManager.GetEntity(name);
			if (entity == null)
			{
				entity = EntityManager.CreateEntity(name);
				EntityManager.AddComponent(entity, new Transform());
				if (entity.GetComponent<OwnedByScene>() == null)
				{
					EntityManager.AddComponent(entity, new OwnedByScene { Scene = SceneId.Climb });
				}
			}
			return entity;
		}

		private void ConfigureParallax(Entity entity, ParallaxLayer settings)
		{
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

			var root = EntityManager.GetEntity(ClimbHeaderLayoutSystem.RootName);
			if (root == null) return;

			var parent = entity.GetComponent<ParentTransform>();
			if (parent == null)
			{
				EntityManager.AddComponent(entity, new ParentTransform { Parent = root });
			}
			else
			{
				parent.Parent = root;
			}
		}

		private void SetBounds(Entity entity, Rectangle rect, bool interactable, UIElementEventType eventType, int zOrder, bool hidden = false)
		{
			var transform = entity.GetComponent<Transform>();
			var parent = entity.GetComponent<ParentTransform>()?.Parent;
			bool usesLocalBounds = parent != null;
			if (transform != null)
			{
				if (usesLocalBounds)
				{
					Vector2 parentWorldPosition = TransformResolverService.ResolveWorldPosition(EntityManager, parent);
					transform.Position = new Vector2(rect.X, rect.Y) - parentWorldPosition;
				}
				else
				{
					transform.Position = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
				}
				transform.ZOrder = zOrder;
			}
			Rectangle uiBounds = usesLocalBounds
				? new Rectangle(0, 0, rect.Width, rect.Height)
				: rect;

			var preview = EntityManager.GetEntity(ClimbHeaderLayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
			var slot = entity.GetComponent<ClimbSlotPresentation>();
			bool blockedByPreview = preview?.IsActive == true
				&& slot != null
				&& !string.Equals(slot.SlotId, preview.SourceSlotId, StringComparison.OrdinalIgnoreCase)
				&& preview.WouldVanishSlotIds.Contains(slot.SlotId);

			var ui = entity.GetComponent<UIElement>();
			if (ui == null)
			{
				EntityManager.AddComponent(entity, new UIElement { Bounds = uiBounds, IsInteractable = interactable && !blockedByPreview, EventType = eventType, IsHidden = hidden });
			}
			else
			{
				ui.Bounds = uiBounds;
				ui.IsInteractable = interactable && !blockedByPreview;
				ui.EventType = eventType;
				ui.IsHidden = hidden;
			}
		}

		internal static ClimbColumnsLayout ComputeColumnsLayout(bool showEvents)
		{
			int top = ClimbColumnDisplaySystem.ColumnsTopValue;
			int maxWidth = ClimbColumnDisplaySystem.ColumnsMaxWidthValue;
			int gap = ClimbColumnDisplaySystem.ColumnsGapValue;
			int height = Game1.VirtualHeight - top - ColumnsBottomPaddingValue;
			int colW = ColumnWidthValue;
			int groupW = showEvents ? colW * 3 + gap * 2 : colW * 2 + gap;
			int x = (Game1.VirtualWidth - Math.Min(maxWidth, groupW)) / 2;
			if (showEvents && groupW <= maxWidth)
			{
				x = (Game1.VirtualWidth - groupW) / 2;
			}
			else if (!showEvents)
			{
				x = (Game1.VirtualWidth - groupW) / 2;
			}

			var shop = new Rectangle(x, top, colW, height);
			var encounter = new Rectangle(shop.Right + gap, top, colW, height);
			var events = new Rectangle(encounter.Right + gap, top, colW, height);
			int pad = ClimbColumnDisplaySystem.ColumnPaddingValue;
			return new ClimbColumnsLayout
			{
				Shop = shop,
				Encounter = encounter,
				Events = events,
				ShopInner = new Rectangle(shop.X + pad, shop.Y + pad, shop.Width - pad * 2, shop.Height - pad * 2),
				EncounterInner = new Rectangle(encounter.X + pad, encounter.Y + pad, encounter.Width - pad * 2, encounter.Height - pad * 2),
				EventInner = new Rectangle(events.X + pad, events.Y + pad, events.Width - pad * 2, events.Height - pad * 2),
			};
		}

		private static Rectangle ComputeShopSlotRect(Rectangle inner, int index)
		{
			return new Rectangle(inner.X, inner.Y + SlotListTopOffsetValue + index * (ShopSlotHeightValue + ClimbColumnDisplaySystem.SlotGapValue), inner.Width, ShopSlotHeightValue);
		}

		private static Rectangle ComputeEncounterSlotRect(Rectangle inner, int index)
		{
			int slotHeight = ClimbColumnDisplaySystem.ComputePortraitSlotHeight();
			return new Rectangle(inner.X, inner.Y + SlotListTopOffsetValue + index * (slotHeight + ClimbColumnDisplaySystem.SlotGapValue), inner.Width, slotHeight);
		}

		private static Rectangle ComputeEventSlotRect(Rectangle inner, int index)
		{
			int slotHeight = ClimbColumnDisplaySystem.ComputePortraitSlotHeight();
			return new Rectangle(inner.X, inner.Y + SlotListTopOffsetValue + index * (slotHeight + ClimbColumnDisplaySystem.SlotGapValue), inner.Width, slotHeight);
		}

		private static ClimbResourceSave Clone(ClimbResourceSave resources)
		{
			return new ClimbResourceSave
			{
				red = Math.Max(0, resources?.red ?? 0),
				white = Math.Max(0, resources?.white ?? 0),
				black = Math.Max(0, resources?.black ?? 0),
			};
		}
	}

	public struct ClimbColumnsLayout
	{
		public Rectangle Shop;
		public Rectangle Encounter;
		public Rectangle Events;
		public Rectangle ShopInner;
		public Rectangle EncounterInner;
		public Rectangle EventInner;
	}
}
