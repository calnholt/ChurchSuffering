using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Dialog;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class WayStationDialogueTests : IDisposable
{
	public WayStationDialogueTests()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
	}

	public void Dispose()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
	}

	[Fact]
	public void Planner_auto_returns_keeper_intro_only_until_seen()
	{
		var meta = new WayStationMetaSave();

		var intro = WayStationDialoguePlanner.TryGetAutoDialogue(meta);

		Assert.NotNull(intro);
		Assert.Equal(WayStationDialogueCatalog.KeeperDefinitionId, intro.DefinitionId);
		Assert.Equal(WayStationDialogueCatalog.KeeperIntroSegmentId, intro.SegmentId);

		meta.completedDialogueSegments[WayStationDialogueCatalog.KeeperCharacterId] =
		[
			WayStationDialogueCatalog.KeeperIntroSegmentId,
		];

		Assert.Null(WayStationDialoguePlanner.TryGetAutoDialogue(meta));
	}

	[Fact]
	public void Planner_gates_keeper_early_return_to_failed_or_abandoned_return()
	{
		var meta = MetaWithSeen(
			WayStationDialogueCatalog.KeeperCharacterId,
			WayStationDialogueCatalog.KeeperIntroSegmentId);

		var failed = WayStationDialoguePlanner.TryGetKeeperPoiDialogue(
			meta,
			WayStationArrivalKind.ReturnedFromFailedClimb);
		var abandoned = WayStationDialoguePlanner.TryGetKeeperPoiDialogue(
			meta,
			WayStationArrivalKind.ReturnedFromAbandonedClimb);
		var completed = WayStationDialoguePlanner.TryGetKeeperPoiDialogue(
			meta,
			WayStationArrivalKind.ReturnedFromCompletedClimb);

		Assert.NotNull(failed);
		Assert.Equal(WayStationDialogueCatalog.KeeperEarlyReturnSegmentId, failed.SegmentId);
		Assert.NotNull(abandoned);
		Assert.Equal(WayStationDialogueCatalog.KeeperEarlyReturnSegmentId, abandoned.SegmentId);
		Assert.Null(completed);
	}

	[Fact]
	public void Planner_rook_tutorial_appears_for_first_three_climb_windows()
	{
		for (int climbAttempts = 0; climbAttempts < 3; climbAttempts++)
		{
			var meta = new WayStationMetaSave { climbAttempts = climbAttempts };

			var rook = WayStationDialoguePlanner.TryGetRookTutorialDialogue(meta);

			Assert.NotNull(rook);
			Assert.Equal(WayStationDialoguePlanner.RookTutorialOfferId, rook.OfferId);
			Assert.Equal(WayStationDialogueCatalog.RookCharacterId, rook.CharacterId);
			Assert.Equal(WayStationDialogueCatalog.RookDefinitionId, rook.DefinitionId);
			Assert.Equal(WayStationDialogueCatalog.RookTutorialSegment1Id, rook.SegmentId);
		}
	}

	[Fact]
	public void Planner_rook_tutorial_uses_first_unseen_segment()
	{
		var meta = new WayStationMetaSave { climbAttempts = 2 };
		meta.completedDialogueSegments[WayStationDialogueCatalog.RookCharacterId] =
		[
			WayStationDialogueCatalog.RookTutorialSegment1Id,
			WayStationDialogueCatalog.RookTutorialSegment2Id,
		];

		var rook = WayStationDialoguePlanner.TryGetRookTutorialDialogue(meta);

		Assert.NotNull(rook);
		Assert.Equal(WayStationDialogueCatalog.RookTutorialSegment3Id, rook.SegmentId);
	}

	[Fact]
	public void Planner_rook_tutorial_stops_after_third_climb_window()
	{
		var meta = new WayStationMetaSave { climbAttempts = 3 };

		Assert.Null(WayStationDialoguePlanner.TryGetRookTutorialDialogue(meta));
	}

	[Fact]
	public void Planner_npc_requires_pending_offer_and_skips_exhausted_characters()
	{
		var meta = new WayStationMetaSave();
		meta.completedDialogueSegments[WayStationDialogueCatalog.EliasCharacterId] =
			WayStationDialogueCatalog.GetOrderedSegments(WayStationDialogueCatalog.EliasCharacterId).ToList();
		meta.completedDialogueSegments[WayStationDialogueCatalog.OldConfessorCharacterId] =
			WayStationDialogueCatalog.GetOrderedSegments(WayStationDialogueCatalog.OldConfessorCharacterId).ToList();

		Assert.Null(WayStationDialoguePlanner.TryGetNpcDialogue(meta, new Random(0)));

		meta.pendingNpcDialogueOffer = true;
		var npc = WayStationDialoguePlanner.TryGetNpcDialogue(meta, new Random(0));

		Assert.NotNull(npc);
		Assert.Equal(WayStationDialogueCatalog.MaraCharacterId, npc.CharacterId);
		Assert.Equal("dialogue_1", npc.SegmentId);

		meta.completedDialogueSegments[WayStationDialogueCatalog.MaraCharacterId] =
			WayStationDialogueCatalog.GetOrderedSegments(WayStationDialogueCatalog.MaraCharacterId).ToList();
		Assert.Null(WayStationDialoguePlanner.TryGetNpcDialogue(meta, new Random(0)));
	}

	[Fact]
	public void Planner_rook_random_npc_dialogue_starts_after_tutorial_window()
	{
		var meta = new WayStationMetaSave
		{
			climbAttempts = 2,
			pendingNpcDialogueOffer = true,
		};
		meta.completedDialogueSegments[WayStationDialogueCatalog.EliasCharacterId] =
			WayStationDialogueCatalog.GetOrderedSegments(WayStationDialogueCatalog.EliasCharacterId).ToList();
		meta.completedDialogueSegments[WayStationDialogueCatalog.OldConfessorCharacterId] =
			WayStationDialogueCatalog.GetOrderedSegments(WayStationDialogueCatalog.OldConfessorCharacterId).ToList();
		meta.completedDialogueSegments[WayStationDialogueCatalog.MaraCharacterId] =
			WayStationDialogueCatalog.GetOrderedSegments(WayStationDialogueCatalog.MaraCharacterId).ToList();

		Assert.Null(WayStationDialoguePlanner.TryGetNpcDialogue(meta, new Random(0)));

		meta.climbAttempts = 3;
		var npc = WayStationDialoguePlanner.TryGetNpcDialogue(meta, new Random(0));

		Assert.NotNull(npc);
		Assert.Equal(WayStationDialogueCatalog.RookCharacterId, npc.CharacterId);
		Assert.Equal("dialogue_1", npc.SegmentId);
	}

	[Fact]
	public void Save_waystation_meta_survives_run_start_and_inactive_reset()
	{
		SaveCache.MarkWayStationDialogueSegmentSeen(
			WayStationDialogueCatalog.KeeperCharacterId,
			WayStationDialogueCatalog.KeeperIntroSegmentId);
		SaveCache.SaveWayStationVisit(new WayStationVisitSave
		{
			initialized = true,
			offers =
			[
				new WayStationDialogueOfferSave
				{
					offerId = WayStationDialoguePlanner.NpcOfferId,
					characterId = WayStationDialogueCatalog.MaraCharacterId,
					definitionId = WayStationDialogueCatalog.MaraDefinitionId,
					segmentId = "dialogue_1",
					screenX = 300,
					screenY = 400,
					visible = true,
				},
			],
		});

		SaveCache.StartWayStationClimbAttempt();
		var afterStart = SaveCache.GetWayStationMeta();
		Assert.Equal(1, afterStart.climbAttempts);
		Assert.True(WayStationDialoguePlanner.HasSeen(
			afterStart,
			WayStationDialogueCatalog.KeeperCharacterId,
			WayStationDialogueCatalog.KeeperIntroSegmentId));
		Assert.False(afterStart.currentVisit.initialized);
		Assert.False(afterStart.pendingNpcDialogueOffer);

		SaveCache.RecordWayStationClimbCompletion();
		SaveCache.MarkRunInactive();
		var afterInactive = SaveCache.GetWayStationMeta();
		Assert.Equal(1, afterInactive.climbAttempts);
		Assert.Equal(1, afterInactive.climbCompletions);
		Assert.True(afterInactive.pendingNpcDialogueOffer);
		Assert.True(WayStationDialoguePlanner.HasSeen(
			afterInactive,
			WayStationDialogueCatalog.KeeperCharacterId,
			WayStationDialogueCatalog.KeeperIntroSegmentId));
	}

	[Fact]
	public void Save_visit_round_trips_offer_state_by_value()
	{
		var visit = new WayStationVisitSave
		{
			initialized = true,
			offers =
			[
				new WayStationDialogueOfferSave
				{
					offerId = WayStationDialoguePlanner.KeeperOfferId,
					characterId = WayStationDialogueCatalog.KeeperCharacterId,
					definitionId = WayStationDialogueCatalog.KeeperDefinitionId,
					segmentId = WayStationDialogueCatalog.KeeperEarlyReturnSegmentId,
					screenX = 100,
					screenY = 200,
					visible = true,
				},
			],
		};

		SaveCache.SaveWayStationVisit(visit);
		visit.offers[0].visible = false;

		var restored = SaveCache.GetWayStationVisit();

		Assert.True(restored.initialized);
		Assert.True(restored.offers.Single().visible);
		Assert.Equal(100, restored.offers.Single().screenX);
	}

	[Fact]
	public void Failed_returns_below_half_fill_npc_dialogue_counter_to_three()
	{
		for (int i = 1; i <= 3; i++)
		{
			StartAttemptAtClimbTime(ClimbRuleService.MaxTime / 2 - 1);
			SaveCache.RecordWayStationClimbReturn(WayStationArrivalKind.ReturnedFromFailedClimb);
			var meta = SaveCache.GetWayStationMeta();
			Assert.Equal(i, meta.deferredNpcDialogueCounter);
			Assert.Equal(i == 3, meta.pendingNpcDialogueOffer);
		}
	}

	[Fact]
	public void Failed_return_at_half_time_triggers_npc_dialogue_and_queued_offer_resets_counter()
	{
		StartAttemptAtClimbTime(ClimbRuleService.MaxTime / 2 - 1);
		SaveCache.RecordWayStationClimbReturn(WayStationArrivalKind.ReturnedFromFailedClimb);
		Assert.Equal(1, SaveCache.GetWayStationMeta().deferredNpcDialogueCounter);

		StartAttemptAtClimbTime(ClimbRuleService.MaxTime / 2);
		SaveCache.RecordWayStationClimbReturn(WayStationArrivalKind.ReturnedFromFailedClimb);
		var triggered = SaveCache.GetWayStationMeta();
		Assert.True(triggered.pendingNpcDialogueOffer);
		Assert.Equal(1, triggered.deferredNpcDialogueCounter);

		SaveCache.MarkWayStationNpcDialogueOfferQueued();
		var reset = SaveCache.GetWayStationMeta();
		Assert.False(reset.pendingNpcDialogueOffer);
		Assert.Equal(0, reset.deferredNpcDialogueCounter);
	}

	[Fact]
	public void Completed_climb_triggers_npc_dialogue_and_increments_completion()
	{
		StartAttemptAtClimbTime(0);

		SaveCache.RecordWayStationClimbReturn(WayStationArrivalKind.ReturnedFromCompletedClimb);

		var meta = SaveCache.GetWayStationMeta();
		Assert.True(meta.pendingNpcDialogueOffer);
		Assert.Equal(1, meta.climbCompletions);
	}

	[Fact]
	public void Abandoned_climb_does_not_increment_or_trigger_npc_dialogue_counter()
	{
		StartAttemptAtClimbTime(ClimbRuleService.MaxTime / 2 - 1);
		SaveCache.RecordWayStationClimbReturn(WayStationArrivalKind.ReturnedFromFailedClimb);

		StartAttemptAtClimbTime(ClimbRuleService.MaxTime);
		SaveCache.RecordWayStationClimbReturn(WayStationArrivalKind.ReturnedFromAbandonedClimb);

		var meta = SaveCache.GetWayStationMeta();
		Assert.Equal(1, meta.deferredNpcDialogueCounter);
		Assert.False(meta.pendingNpcDialogueOffer);
	}

	[Fact]
	public void Catalog_contains_waystation_dialogue_and_ascii_text()
	{
		Assert.True(DialogCatalog.TryGet(WayStationDialogueCatalog.KeeperDefinitionId, out var keeper));
		Assert.Equal(13, keeper.ResolveSegment(WayStationDialogueCatalog.KeeperIntroSegmentId).Count);
		Assert.Equal(9, keeper.ResolveSegment(WayStationDialogueCatalog.KeeperEarlyReturnSegmentId).Count);

		Assert.True(DialogCatalog.TryGet(WayStationDialogueCatalog.EliasDefinitionId, out var elias));
		Assert.Equal(10, elias.ResolveSegment("dialogue_1").Count);
		Assert.True(DialogCatalog.TryGet(WayStationDialogueCatalog.OldConfessorDefinitionId, out var oldConfessor));
		Assert.Equal(9, oldConfessor.ResolveSegment("dialogue_1").Count);
		Assert.True(DialogCatalog.TryGet(WayStationDialogueCatalog.MaraDefinitionId, out var mara));
		Assert.Equal(8, mara.ResolveSegment("dialogue_4").Count);
		Assert.True(DialogCatalog.TryGet(WayStationDialogueCatalog.RookDefinitionId, out var rook));
		Assert.Equal(4, rook.ResolveSegment(WayStationDialogueCatalog.RookTutorialSegment1Id).Count);
		Assert.Equal(3, rook.ResolveSegment(WayStationDialogueCatalog.RookTutorialSegment2Id).Count);
		Assert.Equal(4, rook.ResolveSegment(WayStationDialogueCatalog.RookTutorialSegment3Id).Count);
		Assert.Equal(10, rook.ResolveSegment("dialogue_1").Count);

		foreach (var definition in new[] { keeper, elias, oldConfessor, mara, rook })
		{
			foreach (var segment in definition.segments.Values)
			{
				foreach (var line in segment)
				{
					Assert.All(line.actor, character => Assert.InRange((int)character, 0, 127));
					Assert.All(line.message, character => Assert.InRange((int)character, 0, 127));
				}
			}
		}
	}

	[Theory]
	[InlineData("Keeper", "waystation/the-keeper")]
	[InlineData("Elias", "waystation/elias")]
	[InlineData("Old Confessor", "waystation/old-confessor")]
	[InlineData("Mara", "waystation/mara")]
	[InlineData("Rook", "waystation/rook")]
	public void Portraits_resolve_for_waystation_actors(string actor, string asset)
	{
		Assert.Equal(asset, DialogDisplaySystem.ResolvePortraitAssetName(actor));
	}

	[Fact]
	public void Dialogue_poi_delegate_publishes_selected_offer()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("dialogue_poi");
		entityManager.AddComponent(entity, new UIElement { EventType = UIElementEventType.WayStationDialoguePoiSelect });
		entityManager.AddComponent(entity, new WayStationDialoguePoiAction { OfferId = WayStationDialoguePlanner.NpcOfferId });
		string selected = string.Empty;
		EventManager.Subscribe<WayStationDialoguePoiSelectedEvent>(evt => selected = evt.OfferId);

		UIElementEventDelegateService.HandleEvent(UIElementEventType.WayStationDialoguePoiSelect, entity, entityManager);

		Assert.Equal(WayStationDialoguePlanner.NpcOfferId, selected);
	}

	[Fact]
	public void Abandon_delegate_publishes_abandon_run_end_cause()
	{
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("abandon");
		RunEndSequenceRequested request = null;
		EventManager.Subscribe<RunEndSequenceRequested>(evt => request = evt);

		UIElementEventDelegateService.HandleEvent(UIElementEventType.AbandonQuest, entity, entityManager);

		Assert.NotNull(request);
		Assert.Equal(RunEndCause.Abandon, request.Cause);
	}

	private static WayStationMetaSave MetaWithSeen(string characterId, params string[] segmentIds)
	{
		return new WayStationMetaSave
		{
			completedDialogueSegments =
			{
				[characterId] = segmentIds.ToList(),
			},
		};
	}

	private static void StartAttemptAtClimbTime(int climbTime)
	{
		SaveCache.StartWayStationClimbAttempt();
		var climb = SaveCache.GetClimbState();
		climb.time = ClimbRuleService.ClampTime(climbTime);
		SaveCache.SaveClimbState(climb);
	}
}
