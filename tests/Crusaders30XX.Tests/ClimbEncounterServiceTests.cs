using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class ClimbEncounterServiceTests
{
	[Fact]
	public void Queue_encounter_creates_exactly_one_existing_queued_encounter()
	{
		EventManager.Clear();
		try
		{
			PrepareRunWithEncounter(timeCost: 3, battleLocation: BattleLocation.Tundra);
			var world = new World();
			int battleTransitions = 0;
			ClimbCardMutationAnimationRequested mutationRequest = null;
			EventManager.Subscribe<ShowTransition>(evt =>
			{
				if (evt.Scene == SceneId.Battle) battleTransitions++;
			});
			EventManager.Subscribe<ClimbCardMutationAnimationRequested>(evt => mutationRequest = evt);

			Assert.True(ClimbEncounterService.TryQueueEncounter(world.EntityManager, "encounter_a"));

			var queued = world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<QueuedEvents>();
			Assert.True(queued.IsClimbEncounter);
			Assert.Equal("encounter_a", queued.ClimbEncounterSlotId);
			Assert.Equal(BattleLocation.Tundra, queued.BattleLocation);
			Assert.Single(queued.Events);
			Assert.Equal("skeleton", queued.Events[0].EventId);
			Assert.Equal(0, battleTransitions);
			Assert.NotNull(mutationRequest);
			Assert.Equal(RunScopedStateService.RestrictionFrozen, mutationRequest.RestrictionName);
			Assert.True(mutationRequest.TransitionToBattleOnComplete);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Queue_encounter_transitions_immediately_when_no_mutation_target_exists()
	{
		EventManager.Clear();
		try
		{
			PrepareRunWithEncounter(timeCost: 3, allCardsAlreadyBrittle: true);
			var world = new World();
			int battleTransitions = 0;
			ClimbCardMutationAnimationRequested mutationRequest = null;
			EventManager.Subscribe<ShowTransition>(evt =>
			{
				if (evt.Scene == SceneId.Battle) battleTransitions++;
			});
			EventManager.Subscribe<ClimbCardMutationAnimationRequested>(evt => mutationRequest = evt);

			Assert.True(ClimbEncounterService.TryQueueEncounter(world.EntityManager, "encounter_a"));

			Assert.Equal(1, battleTransitions);
			Assert.Null(mutationRequest);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Complete_queued_encounter_advances_time_grants_resources_and_persists_reward()
	{
		EventManager.Clear();
		try
		{
			PrepareRunWithEncounter(timeCost: 3);
			var world = new World();
			Assert.True(ClimbEncounterService.TryQueueEncounter(world.EntityManager, "encounter_a"));
			world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<QueuedEvents>().CurrentIndex = 0;

			var result = ClimbEncounterService.CompleteQueuedEncounter(world.EntityManager);

			var climb = SaveCache.GetClimbState();
			Assert.True(result.Completed);
			Assert.Equal(3, climb.time);
			Assert.Equal(2, climb.resources.red);
			Assert.Equal(1, climb.resources.white);
			Assert.False(climb.encounterSlots.Single(s => s.id == "encounter_a").isCompleted);
			Assert.NotEqual("skeleton", climb.encounterSlots.Single(s => s.id == "encounter_a").enemyId);
			Assert.NotNull(climb.pendingEncounterReward);
			Assert.Equal("encounter_a", climb.pendingEncounterReward.encounterSlotId);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Complete_resources_only_encounter_persists_resources_without_deck_offer()
	{
		EventManager.Clear();
		try
		{
			PrepareRunWithEncounter(timeCost: 3);
			var world = new World();
			Assert.True(ClimbEncounterService.TryQueueEncounter(world.EntityManager, "encounter_a"));
			world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<QueuedEvents>().CurrentIndex = 0;

			var result = ClimbEncounterService.CompleteQueuedEncounter(world.EntityManager);
			var climb = SaveCache.GetClimbState();

			Assert.True(result.Completed);
			Assert.Null(result.DeckRewardOffer);
			Assert.Null(SaveCache.GetPendingDeckRewardOffer());
			Assert.NotNull(climb.pendingEncounterReward);
			Assert.Null(climb.pendingEncounterReward.deckRewardOffer);
			Assert.Equal(1, climb.pendingEncounterReward.resources.red);
			Assert.Equal(0, climb.pendingEncounterReward.resources.white);
			Assert.Equal(0, climb.pendingEncounterReward.resources.black);
			Assert.True(RewardModalDisplaySystem.HasClimbResourceReward(climb.pendingEncounterReward.resources));
			Assert.Equal("+1 RED", RewardModalDisplaySystem.BuildClimbResourceRewardText(climb.pendingEncounterReward.resources));
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Resolve_pending_reward_clears_save_and_queues_final_encounter_when_time_is_capped()
	{
		EventManager.Clear();
		try
		{
			PrepareRunWithEncounter(timeCost: 1);
			var climb = SaveCache.GetClimbState();
			climb.time = ClimbRuleService.MaxTime - 1;
			climb.encounterSlots.Single(s => s.id == "encounter_a").rewardResources = new ClimbResourceSave { red = 0, white = 1, black = 1 };
			SaveCache.SaveClimbState(climb);

			var world = new World();
			Assert.True(ClimbEncounterService.TryQueueEncounter(world.EntityManager, "encounter_a"));
			world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<QueuedEvents>().CurrentIndex = 0;
			var result = ClimbEncounterService.CompleteQueuedEncounter(world.EntityManager);
			Assert.True(result.PendingFinalEncounter);

			ShowTransition transition = null;
			EventManager.Subscribe<ShowTransition>(evt => transition = evt);

			Assert.True(ClimbEncounterService.ResolvePendingEncounterReward(world.EntityManager));

			var after = SaveCache.GetClimbState();
			var queued = world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<QueuedEvents>();
			Assert.Null(after.pendingEncounterReward);
			Assert.True(queued.IsClimbEncounter);
			Assert.Equal("final", queued.ClimbEncounterSlotId);
			Assert.Equal(BattleLocation.TheGate, queued.BattleLocation);
			Assert.Single(queued.Events);
			Assert.Equal("fallen_shepherd", queued.Events[0].EventId);
			var pending = world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<PendingQuestDialog>();
			Assert.NotNull(pending);
			Assert.Equal("fallen_shepherd", pending.DialogId);
			Assert.Equal("intro", pending.SegmentId);
			Assert.NotEqual(System.Guid.Empty, pending.RequestId);
			Assert.True(pending.WillShowDialog);
			Assert.NotNull(transition);
			Assert.Equal(SceneId.Battle, transition.Scene);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Completing_ordinary_encounter_rerolls_that_slot_before_final_time()
	{
		EventManager.Clear();
		try
		{
			PrepareRunWithEncounter(timeCost: 1);
			var world = new World();

			CompleteEncounter(world, "encounter_a");

			var climb = SaveCache.GetClimbState();
			Assert.Equal(ClimbRuleService.EncounterSlotCount, climb.encounterSlots.Count);
			var rerolled = climb.encounterSlots.Single(slot => slot.id == "encounter_a");
			Assert.False(rerolled.isCompleted);
			Assert.False(rerolled.isFinal);
			Assert.False(string.IsNullOrWhiteSpace(rerolled.enemyId));
			Assert.NotEqual("skeleton", rerolled.enemyId);
			Assert.InRange(rerolled.timeCost, 1, 3);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Replenishing_encounters_at_max_time_leaves_existing_slots_unchanged()
	{
		var climb = new ClimbSaveState
		{
			time = ClimbRuleService.MaxTime,
			encounterSlots = new List<ClimbEncounterSlotSave>
			{
				new() { id = "encounter_a", enemyId = "skeleton", isCompleted = true },
				new() { id = "encounter_b", enemyId = "demon", isCompleted = true },
				new() { id = "encounter_c", enemyId = "skeleton", isCompleted = true },
			}
		};

		Assert.False(ClimbRuleService.ReplenishEncounterSlots(climb, 123));

		Assert.Equal(ClimbRuleService.EncounterSlotCount, climb.encounterSlots.Count);
		Assert.All(climb.encounterSlots, slot => Assert.False(slot.isFinal));
		Assert.All(climb.encounterSlots, slot => Assert.True(slot.isCompleted));
		Assert.DoesNotContain(climb.encounterSlots, slot =>
			string.Equals(slot.enemyId, "fallen_shepherd", System.StringComparison.OrdinalIgnoreCase));
	}

	private static void PrepareRunWithEncounter(
		int timeCost,
		BattleLocation battleLocation = BattleLocation.Desert,
		bool allCardsAlreadyBrittle = false)
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = new List<LoadoutCardEntry>
		{
			new()
			{
				entryId = "test_entry_0",
				cardKey = "smite|White",
				isStarter = true,
				restrictions = allCardsAlreadyBrittle ? new List<string> { RunScopedStateService.RestrictionBrittle } : new List<string>(),
			},
			new()
			{
				entryId = "test_entry_1",
				cardKey = "fervor|Red",
				isStarter = true,
				restrictions = allCardsAlreadyBrittle ? new List<string> { RunScopedStateService.RestrictionBrittle } : new List<string>(),
			},
			new()
			{
				entryId = "test_entry_2",
				cardKey = "reckoning|Black",
				isStarter = true,
				restrictions = allCardsAlreadyBrittle ? new List<string> { RunScopedStateService.RestrictionBrittle } : new List<string>(),
			},
		};
		loadout.weaponId = "sword";
		loadout.medalIds = new List<string>();
		SaveCache.SaveLoadout(loadout);

		var climb = SaveCache.GetClimbState();
		climb.time = 0;
		climb.resources = new ClimbResourceSave { red = 1, white = 1, black = 1 };
		climb.encounterSlots = new List<ClimbEncounterSlotSave>
		{
			new()
			{
				id = "encounter_a",
				enemyId = "skeleton",
				generatedAtTime = 0,
				duration = 5,
				timeCost = timeCost,
				battleLocation = battleLocation,
				rewardResources = new ClimbResourceSave { red = 1, white = 0, black = 0 },
				hasDeckReward = false,
			},
			new() { id = "encounter_b", enemyId = "demon", generatedAtTime = 0, duration = 5, timeCost = 3, battleLocation = BattleLocation.Jungle, hasDeckReward = false },
			new() { id = "encounter_c", enemyId = "skeleton", generatedAtTime = 0, duration = 5, timeCost = 3, battleLocation = BattleLocation.Desert, hasDeckReward = false },
		};
		SaveCache.SaveClimbState(climb);
	}

	private static void CompleteEncounter(World world, string encounterSlotId)
	{
		Assert.True(ClimbEncounterService.TryQueueEncounter(world.EntityManager, encounterSlotId));
		world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<QueuedEvents>().CurrentIndex = 0;
		var result = ClimbEncounterService.CompleteQueuedEncounter(world.EntityManager);
		Assert.True(result.Completed);
	}
}
