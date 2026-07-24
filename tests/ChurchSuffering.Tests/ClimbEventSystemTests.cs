using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class ClimbEventSystemTests
{
	[Fact]
	public void Hazard_selection_is_binding_but_applies_nothing_until_confirm_and_confirm_is_idempotent()
	{
		EventManager.Clear();
		try
		{
			PrepareRun(
				new List<LoadoutCardEntry>
				{
					Entry("entry_a", "smite|White", RunScopedStateService.RestrictionBrittle),
				},
				Hazard("hazard", ClimbHazardEffectType.Frozen, rewardRed: 2));
			var world = ClimbWorld();
			_ = new ClimbEventSystem(world.EntityManager);
			ShowNarrativeEventOverlay shown = null;
			var resourceAnimations = new List<ClimbResourceAcquisitionAnimationRequested>();
			var mutationAnimations = new List<ClimbCardMutationAnimationRequested>();
			EventManager.Subscribe<ShowNarrativeEventOverlay>(evt => shown = evt);
			EventManager.Subscribe<ClimbResourceAcquisitionAnimationRequested>(evt => resourceAnimations.Add(evt));
			EventManager.Subscribe<ClimbCardMutationAnimationRequested>(evt => mutationAnimations.Add(evt));

			EventManager.Publish(new ClimbEventSlotSelectedEvent { SlotId = "hazard" });

			var pending = SaveCache.GetClimbState();
			Assert.Equal(1, pending.resources.red);
			Assert.Equal(ClimbEventStatus.Pending, pending.eventSlots.Single(slot => slot.id == "hazard").status);
			Assert.Equal(ClimbEventFlowPhase.HazardConfirmation, pending.pendingEvent.phase);
			Assert.DoesNotContain(RunScopedStateService.RestrictionFrozen,
				SaveCache.GetRunDeckEntryRestrictions(RunDeckService.PrimaryLoadoutId, "entry_a"));
			Assert.NotNull(shown?.Content);
			Assert.Contains("Effect: One random deck card becomes Frozen.", shown.Content.Body);
			Assert.Contains("Gain: 2 Red", shown.Content.Body);

			var first = new NarrativeModalChoiceRequested
			{
				ResolutionContextId = shown.ResolutionContextId,
				ChoiceIndex = 0,
			};
			EventManager.Publish(first);

			Assert.True(first.Handled);
			var resolved = SaveCache.GetClimbState();
			Assert.Equal(3, resolved.resources.red);
			Assert.Equal(ClimbEventStatus.Completed, resolved.eventSlots.Single(slot => slot.id == "hazard").status);
			Assert.Null(resolved.pendingEvent);
			Assert.Contains(RunScopedStateService.RestrictionFrozen,
				SaveCache.GetRunDeckEntryRestrictions(RunDeckService.PrimaryLoadoutId, "entry_a"));
			Assert.Single(resourceAnimations);
			Assert.Equal(2, resourceAnimations[0].Resources.red);
			Assert.Equal(0, resourceAnimations[0].Resources.white);
			Assert.Equal(0, resourceAnimations[0].Resources.black);
			Assert.Single(mutationAnimations);
			Assert.Equal("entry_a", mutationAnimations[0].DeckEntryId);
			Assert.Equal("smite|White", mutationAnimations[0].CardKey);
			Assert.Equal(RunScopedStateService.RestrictionFrozen, mutationAnimations[0].RestrictionName);
			Assert.Equal(
				new[] { RunScopedStateService.RestrictionBrittle },
				mutationAnimations[0].CurrentRestrictionNames);
			Assert.False(mutationAnimations[0].TransitionToBattleOnComplete);

			var repeated = new NarrativeModalChoiceRequested
			{
				ResolutionContextId = shown.ResolutionContextId,
				ChoiceIndex = 0,
			};
			EventManager.Publish(repeated);

			Assert.True(repeated.Handled);
			Assert.Equal(3, SaveCache.GetClimbState().resources.red);
			Assert.Single(resourceAnimations);
			Assert.Single(mutationAnimations);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Restriction_hazard_with_no_target_still_grants_resources()
	{
		PrepareRun(
			new List<LoadoutCardEntry>
			{
				Entry("entry_a", "smite|White", RunScopedStateService.RestrictionBrittle),
			},
			Hazard("hazard", ClimbHazardEffectType.Brittle, rewardRed: 1));
		Assert.True(SaveCache.TryBeginClimbEvent(
			"hazard", ClimbEventFlowPhase.HazardConfirmation, string.Empty, out _));

		Assert.True(SaveCache.TryResolveClimbHazard("hazard", out var result));

		Assert.True(result.Succeeded);
		Assert.Equal(string.Empty, result.RestrictedEntryId);
		Assert.Equal(2, SaveCache.GetClimbState().resources.red);
	}

	[Fact]
	public void Non_restriction_hazard_does_not_publish_mutation_animation()
	{
		EventManager.Clear();
		try
		{
			PrepareRun(
				new List<LoadoutCardEntry> { Entry("entry_a", "smite|White") },
				Hazard("hazard", ClimbHazardEffectType.Fear, effectAmount: 2, rewardRed: 1));
			var world = ClimbWorld();
			_ = new ClimbEventSystem(world.EntityManager);
			ShowNarrativeEventOverlay shown = null;
			var mutationAnimations = new List<ClimbCardMutationAnimationRequested>();
			EventManager.Subscribe<ShowNarrativeEventOverlay>(evt => shown = evt);
			EventManager.Subscribe<ClimbCardMutationAnimationRequested>(evt => mutationAnimations.Add(evt));

			EventManager.Publish(new ClimbEventSlotSelectedEvent { SlotId = "hazard" });
			EventManager.Publish(new NarrativeModalChoiceRequested
			{
				ResolutionContextId = shown.ResolutionContextId,
				ChoiceIndex = 0,
			});

			Assert.Empty(mutationAnimations);
			Assert.Equal(2, SaveCache.GetClimbState().nextBattlePenalty.fear);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Character_dialogue_and_summary_apply_reward_and_time_only_on_proceed()
	{
		EventManager.Clear();
		try
		{
			PrepareRun(
				new List<LoadoutCardEntry> { Entry("entry_a", "smite|White") },
				Character("character", "nun_counsel", ClimbCharacterRewardType.Temperance, 2),
				time: 10);
			var world = ClimbWorld();
			_ = new ClimbEventSystem(world.EntityManager);
			DialogueSequenceRequested dialogue = null;
			ShowNarrativeEventOverlay summary = null;
			EventManager.Subscribe<DialogueSequenceRequested>(evt => dialogue = evt);
			EventManager.Subscribe<ShowNarrativeEventOverlay>(evt => summary = evt);

			EventManager.Publish(new ClimbEventSlotSelectedEvent { SlotId = "character" });

			Assert.NotNull(dialogue);
			Assert.True(dialogue.BackgroundOnly);
			Assert.Equal(10, SaveCache.GetClimbState().time);
			Assert.Equal(0, SaveCache.GetClimbState().nextBattleBonus.temperance);

			EventManager.Publish(new DialogueSequenceCompleted
			{
				DefinitionId = dialogue.DefinitionId,
				SegmentId = dialogue.SegmentId,
				RequestId = dialogue.RequestId,
			});

			Assert.NotNull(summary?.Content);
			Assert.Equal("Proceed", summary.Content.ConfirmLabel);
			Assert.Equal(10, SaveCache.GetClimbState().time);

			var proceed = new NarrativeModalChoiceRequested
			{
				ResolutionContextId = summary.ResolutionContextId,
				ChoiceIndex = 0,
			};
			EventManager.Publish(proceed);

			var resolved = SaveCache.GetClimbState();
			Assert.True(proceed.Handled);
			Assert.Equal(11, resolved.time);
			Assert.Equal(2, resolved.nextBattleBonus.temperance);
			Assert.Equal(ClimbEventStatus.Completed, resolved.eventSlots.Single(slot => slot.id == "character").status);
			Assert.Null(resolved.pendingEvent);

			var repeatedProceed = new NarrativeModalChoiceRequested
			{
				ResolutionContextId = summary.ResolutionContextId,
				ChoiceIndex = 0,
			};
			EventManager.Publish(repeatedProceed);
			Assert.True(repeatedProceed.Handled);
			Assert.Equal(11, SaveCache.GetClimbState().time);
			Assert.Equal(2, SaveCache.GetClimbState().nextBattleBonus.temperance);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Smith_no_target_succeeds_and_charges_one_time()
	{
		PrepareRun(
			new List<LoadoutCardEntry> { Entry("entry_a", "smite|White|Upgraded") },
			Character("smith", "smith_forging", ClimbCharacterRewardType.RandomCardUpgrade, 0),
			time: 4);
		Assert.True(SaveCache.TryBeginClimbEvent(
			"smith", ClimbEventFlowPhase.CharacterDialogue, Guid.NewGuid().ToString("D"), out _));
		var pending = SaveCache.GetClimbState().pendingEvent;
		Assert.True(SaveCache.TrySetClimbCharacterSummaryPhase("smith", pending.dialogueRequestId));

		Assert.True(SaveCache.TryResolveClimbCharacter("smith", out var result));

		Assert.True(result.Succeeded);
		Assert.Equal(string.Empty, result.UpgradedEntryId);
		Assert.Equal(5, SaveCache.GetClimbState().time);
		Assert.Equal("smite|White|Upgraded", SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, "entry_a").cardKey);
	}

	[Fact]
	public void Smith_upgrade_preserves_entry_identity_and_restrictions_and_invokes_callback_once()
	{
		EventManager.Clear();
		try
		{
			PrepareRun(
				new List<LoadoutCardEntry>
				{
					Entry("entry_a", "smite|White", RunScopedStateService.RestrictionFrozen),
				},
				Character("smith", "smith_forging", ClimbCharacterRewardType.RandomCardUpgrade, 0));
			var world = ClimbWorld();
			_ = new ClimbEventSystem(world.EntityManager);
			DialogueSequenceRequested dialogue = null;
			ShowNarrativeEventOverlay summary = null;
			EventManager.Subscribe<DialogueSequenceRequested>(evt => dialogue = evt);
			EventManager.Subscribe<ShowNarrativeEventOverlay>(evt => summary = evt);
			CardUpgradeService.UpgradeConfirmedInvokeCountForTests = 0;

			EventManager.Publish(new ClimbEventSlotSelectedEvent { SlotId = "smith" });
			EventManager.Publish(new DialogueSequenceCompleted
			{
				DefinitionId = dialogue.DefinitionId,
				SegmentId = dialogue.SegmentId,
				RequestId = dialogue.RequestId,
			});
			var proceed = new NarrativeModalChoiceRequested
			{
				ResolutionContextId = summary.ResolutionContextId,
				ChoiceIndex = 0,
			};
			EventManager.Publish(proceed);

			var upgraded = SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, "entry_a");
			Assert.NotNull(upgraded);
			Assert.Equal("entry_a", upgraded.entryId);
			Assert.Equal("smite|White|Upgraded", upgraded.cardKey);
			Assert.Contains(RunScopedStateService.RestrictionFrozen, upgraded.restrictions);
			Assert.Equal(1, CardUpgradeService.UpgradeConfirmedInvokeCountForTests);

			EventManager.Publish(new NarrativeModalChoiceRequested
			{
				ResolutionContextId = summary.ResolutionContextId,
				ChoiceIndex = 0,
			});
			Assert.Equal(1, CardUpgradeService.UpgradeConfirmedInvokeCountForTests);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Repeated_hazards_stack_next_battle_penalties_and_run_long_passives()
	{
		var slots = new List<ClimbEventSlotSave>
		{
			Hazard("fear_a", ClimbHazardEffectType.Fear, effectAmount: 2),
			Hazard("fear_b", ClimbHazardEffectType.Fear, effectAmount: 3),
			Hazard("scar", ClimbHazardEffectType.Scar, effectAmount: 2),
		};
		PrepareRun(new List<LoadoutCardEntry> { Entry("entry_a", "smite|White") }, slots.ToArray());

		foreach (var slot in slots)
		{
			Assert.True(SaveCache.TryBeginClimbEvent(
				slot.id, ClimbEventFlowPhase.HazardConfirmation, string.Empty, out _));
			Assert.True(SaveCache.TryResolveClimbHazard(slot.id, out _));
		}

		Assert.Equal(5, SaveCache.GetClimbState().nextBattlePenalty.fear);
		Assert.Equal(2, SaveCache.GetRunLongPassivesSnapshot()[AppliedPassiveType.Scar.ToString()]);
	}

	[Fact]
	public void Climb_load_resumes_pending_character_dialogue_with_same_request()
	{
		EventManager.Clear();
		try
		{
			PrepareRun(
				new List<LoadoutCardEntry> { Entry("entry_a", "smite|White") },
				Character("character", "nun_counsel", ClimbCharacterRewardType.Temperance, 2));
			Guid requestId = Guid.NewGuid();
			Assert.True(SaveCache.TryBeginClimbEvent(
				"character", ClimbEventFlowPhase.CharacterDialogue, requestId.ToString("D"), out _));
			var world = ClimbWorld();
			_ = new ClimbEventSystem(world.EntityManager);
			DialogueSequenceRequested resumed = null;
			EventManager.Subscribe<DialogueSequenceRequested>(evt => resumed = evt);

			EventManager.Publish(new LoadSceneEvent { Scene = SceneId.Climb });

			Assert.NotNull(resumed);
			Assert.Equal(requestId, resumed.RequestId);
			Assert.True(resumed.BackgroundOnly);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Character_resolution_at_final_time_completes_itself_and_preempts_other_events()
	{
		var character = Character("character", "nun_counsel", ClimbCharacterRewardType.Temperance, 2);
		var lateHazard = Hazard("late", ClimbHazardEffectType.Fear, effectAmount: 2);
		lateHazard.status = ClimbEventStatus.Scheduled;
		lateHazard.activatedAtTime = -1;
		lateHazard.scheduledAppearanceTime = ClimbRuleService.BaseMaxTime;
		PrepareRun(
			new List<LoadoutCardEntry> { Entry("entry_a", "smite|White") },
			new[] { character, lateHazard },
			time: ClimbRuleService.BaseMaxTime - 1);
		Guid requestId = Guid.NewGuid();
		Assert.True(SaveCache.TryBeginClimbEvent(
			"character", ClimbEventFlowPhase.CharacterDialogue, requestId.ToString("D"), out _));
		Assert.True(SaveCache.TrySetClimbCharacterSummaryPhase("character", requestId.ToString("D")));

		Assert.True(SaveCache.TryResolveClimbCharacter("character", out var result));

		var climb = SaveCache.GetClimbState();
		Assert.True(result.ReachedFinalTime);
		Assert.Equal(ClimbRuleService.BaseMaxTime, climb.time);
		Assert.Equal(ClimbEventStatus.Completed, climb.eventSlots.Single(slot => slot.id == "character").status);
		Assert.Equal(ClimbEventStatus.Expired, climb.eventSlots.Single(slot => slot.id == "late").status);
	}

	[Fact]
	public void Climb_load_with_pending_encounter_reward_does_not_queue_final_encounter()
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();
			var climb = SaveCache.GetClimbState();
			climb.time = ClimbRuleService.BaseMaxTime;
			climb.pendingEncounterReward = new ClimbEncounterRewardSave
			{
				encounterSlotId = "encounter_a",
				pendingFinalEncounter = true,
				resources = new ClimbResourceSave(),
			};
			SaveCache.SaveClimbState(climb);

			var world = ClimbWorld();
			_ = new ClimbEventSystem(world.EntityManager);

			int transitionCount = 0;
			EventManager.Subscribe<ShowTransition>(_ => transitionCount++);

			EventManager.Publish(new LoadSceneEvent { Scene = SceneId.Climb });

			Assert.Equal(0, transitionCount);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Fact]
	public void Climb_load_at_final_time_without_pending_reward_queues_final_encounter()
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();
			var climb = SaveCache.GetClimbState();
			climb.time = ClimbRuleService.BaseMaxTime;
			climb.pendingEncounterReward = null;
			SaveCache.SaveClimbState(climb);

			var world = ClimbWorld();
			_ = new ClimbEventSystem(world.EntityManager);

			ShowTransition transition = null;
			EventManager.Subscribe<ShowTransition>(evt => transition = evt);

			EventManager.Publish(new LoadSceneEvent { Scene = SceneId.Climb });

			Assert.NotNull(transition);
			Assert.Equal(SceneId.Battle, transition.Scene);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static World ClimbWorld()
	{
		var world = new World();
		var scene = world.EntityManager.CreateEntity("Scene");
		world.EntityManager.AddComponent(scene, new SceneState { Current = SceneId.Climb });
		return world;
	}

	private static void PrepareRun(List<LoadoutCardEntry> entries, params ClimbEventSlotSave[] slots)
	{
		PrepareRun(entries, slots, time: 3);
	}

	private static void PrepareRun(List<LoadoutCardEntry> entries, ClimbEventSlotSave slot, int time)
	{
		PrepareRun(entries, new[] { slot }, time);
	}

	private static void PrepareRun(List<LoadoutCardEntry> entries, ClimbEventSlotSave[] slots, int time)
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = entries;
		SaveCache.SaveLoadout(loadout);
		var climb = SaveCache.GetClimbState();
		climb.time = time;
		climb.eventSlots = slots.ToList();
		foreach (var activeSlot in climb.eventSlots.Where(slot => slot.status == ClimbEventStatus.Active))
		{
			activeSlot.activatedAtTime = time;
		}
		for (int index = climb.eventSlots.Count; index < ClimbRuleService.EventSlotCount; index++)
		{
			climb.eventSlots.Add(new ClimbEventSlotSave
			{
				id = $"filler_{index}",
				definitionId = "bleached_standard",
				kind = ClimbEventKind.Hazard,
				status = ClimbEventStatus.Completed,
			});
		}
		climb.pendingEvent = null;
		climb.nextBattleBonus = new ClimbNextBattleBonusSave();
		climb.nextBattlePenalty = new ClimbNextBattlePenaltySave();
		SaveCache.SaveClimbState(climb);
	}

	private static LoadoutCardEntry Entry(string id, string key, params string[] restrictions)
	{
		return new LoadoutCardEntry
		{
			entryId = id,
			cardKey = key,
			restrictions = restrictions.ToList(),
		};
	}

	private static ClimbEventSlotSave Hazard(
		string id,
		ClimbHazardEffectType effect,
		int effectAmount = 1,
		int rewardRed = 0)
	{
		return new ClimbEventSlotSave
		{
			id = id,
			definitionId = effect switch
			{
				ClimbHazardEffectType.Frozen => "winter_reliquary",
				ClimbHazardEffectType.Brittle => "glass_psalm",
				ClimbHazardEffectType.Fear => "second_footsteps",
				ClimbHazardEffectType.Scar => "saint_of_old_wounds",
				_ => "bleached_standard",
			},
			kind = ClimbEventKind.Hazard,
			hazardEffect = effect,
			effectAmount = effectAmount,
			rewardResources = new ClimbResourceSave { red = rewardRed, white = 0, black = 0 },
			activatedAtTime = 3,
			duration = 4,
			status = ClimbEventStatus.Active,
		};
	}

	private static ClimbEventSlotSave Character(
		string id,
		string definitionId,
		ClimbCharacterRewardType reward,
		int amount)
	{
		return new ClimbEventSlotSave
		{
			id = id,
			definitionId = definitionId,
			kind = ClimbEventKind.Character,
			characterReward = reward,
			effectAmount = amount,
			timeCost = 1,
			activatedAtTime = 3,
			duration = 5,
			status = ClimbEventStatus.Active,
		};
	}
}
