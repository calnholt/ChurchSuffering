using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class ClimbRuleServiceTests
{
	[Fact]
	public void Initial_state_has_time_resources_and_required_slots()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());

		Assert.Equal(0, state.time);
		Assert.Equal(1, state.resources.red);
		Assert.Equal(1, state.resources.white);
		Assert.Equal(1, state.resources.black);
		Assert.Equal(ClimbRuleService.ShopSlotCount, state.shopSlots.Count);
		Assert.Equal(ClimbRuleService.EncounterSlotCount, state.encounterSlots.Count);
		Assert.Equal(ClimbRuleService.EventSlotCount, state.eventSlots.Count);
		Assert.Equal(3, state.eventSlots.Count(slot => slot.kind == ClimbEventKind.Hazard));
		Assert.Equal(2, state.eventSlots.Count(slot => slot.kind == ClimbEventKind.Character));
		Assert.All(state.shopSlots.Where(s => s.kind != ClimbShopSlotKinds.Empty), slot => Assert.InRange(slot.timeCost, 0, 2));
		AssertShopSlotDisplayOrder(state);
		Assert.All(state.encounterSlots, slot =>
		{
			Assert.InRange(slot.timeCost, 1, 3);
			Assert.Equal(state.time, slot.generatedAtTime);
			Assert.InRange(slot.duration, ClimbRuleService.EncounterMinDuration, ClimbRuleService.EncounterMaxDuration);
			Assert.Contains(slot.enemyId, ClimbRuleService.GetClimbEncounterEnemyPool(), StringComparer.OrdinalIgnoreCase);
			Assert.Contains(slot.battleLocation, BattleLocationAssetService.ClimbEncounterLocations);
			Assert.NotEqual(BattleLocation.TheGate, slot.battleLocation);
			Assert.Equal(ClimbRuleService.GetEncounterMutationRestrictionName(slot.battleLocation), slot.cardMutationRestrictionName);
			Assert.False(string.IsNullOrWhiteSpace(slot.cardMutationDeckEntryId));
			Assert.False(string.IsNullOrWhiteSpace(slot.cardMutationCardKey));
		});
	}

	[Theory]
	[InlineData(BattleLocation.Desert, RunScopedStateService.RestrictionBrittle)]
	[InlineData(BattleLocation.Tundra, RunScopedStateService.RestrictionFrozen)]
	[InlineData(BattleLocation.Jungle, RunScopedStateService.RestrictionThorned)]
	[InlineData(BattleLocation.Volcano, RunScopedStateService.RestrictionScorched)]
	[InlineData(BattleLocation.Gothic, RunScopedStateService.RestrictionCursed)]
	public void Encounter_location_maps_to_card_mutation(BattleLocation location, string expectedRestriction)
	{
		Assert.Equal(expectedRestriction, ClimbRuleService.GetEncounterMutationRestrictionName(location));
	}

	[Fact]
	public void Encounter_mutation_target_filters_cards_that_already_have_status()
	{
		var loadout = TestLoadout();
		loadout.cards[0].restrictions.Add(RunScopedStateService.RestrictionBrittle);
		loadout.cards[1].restrictions.Add(RunScopedStateService.RestrictionBrittle);
		var state = new ClimbSaveState
		{
			time = 0,
			encounterSlots = new List<ClimbEncounterSlotSave>
			{
				new()
				{
					id = "encounter_a",
					enemyId = "skeleton",
					generatedAtTime = 0,
					duration = 5,
					timeCost = 1,
					battleLocation = BattleLocation.Desert,
				},
			},
		};

		Assert.True(ClimbRuleService.EnsureEncounterMutationTargets(state, 123, loadout));

		var slot = state.encounterSlots[0];
		Assert.Equal(RunScopedStateService.RestrictionBrittle, slot.cardMutationRestrictionName);
		Assert.Equal(loadout.cards[2].entryId, slot.cardMutationDeckEntryId);
		Assert.Equal(loadout.cards[2].cardKey, slot.cardMutationCardKey);
	}

	[Fact]
	public void Encounter_mutation_target_rerolls_when_time_advances()
	{
		bool sawChangedTarget = false;
		for (int seed = 1; seed <= 50 && !sawChangedTarget; seed++)
		{
			var loadout = TestLoadout();
			var state = ClimbRuleService.CreateInitialState(seed, loadout);
			var before = state.encounterSlots
				.Select(slot => $"{slot.id}|{slot.cardMutationDeckEntryId}|{slot.cardMutationCardKey}")
				.ToList();

			ClimbRuleService.AdvanceTimeAndUpdateSlots(state, seed, loadout, 1);

			var after = state.encounterSlots
				.Select(slot => $"{slot.id}|{slot.cardMutationDeckEntryId}|{slot.cardMutationCardKey}")
				.ToList();
			sawChangedTarget = !before.SequenceEqual(after);
			Assert.All(state.encounterSlots, slot =>
			{
				Assert.Equal(ClimbRuleService.GetEncounterMutationRestrictionName(slot.battleLocation), slot.cardMutationRestrictionName);
				Assert.False(string.IsNullOrWhiteSpace(slot.cardMutationDeckEntryId));
				Assert.False(string.IsNullOrWhiteSpace(slot.cardMutationCardKey));
			});
		}

		Assert.True(sawChangedTarget);
	}

	[Fact]
	public void Encounter_tooltip_preview_uses_current_restrictions_plus_new_restriction()
	{
		var entry = new LoadoutCardEntry
		{
			entryId = "entry_a",
			cardKey = "smite|White",
			restrictions = new List<string>
			{
				RunScopedStateService.RestrictionFrozen,
				RunScopedStateService.RestrictionScorched,
			},
		};

		var restrictions = ClimbRuleService.BuildPreviewRestrictionNames(
			entry,
			RunScopedStateService.RestrictionThorned);

		Assert.Contains(RunScopedStateService.RestrictionFrozen, restrictions);
		Assert.Contains(RunScopedStateService.RestrictionScorched, restrictions);
		Assert.Contains(RunScopedStateService.RestrictionThorned, restrictions);
		Assert.Equal(3, restrictions.Count);
	}

	[Fact]
	public void Generated_encounters_have_minimum_enemy_and_location_diversity()
	{
		for (int seed = 1; seed <= 500; seed++)
		{
			var state = ClimbRuleService.CreateInitialState(seed, TestLoadout());
			AssertEncounterSlotDiversity(state.encounterSlots);
		}
	}

	[Fact]
	public void Replenish_encounters_maintains_minimum_diversity()
	{
		var state = new ClimbSaveState
		{
			time = 0,
			encounterSlots = new List<ClimbEncounterSlotSave>
			{
				new()
				{
					id = "encounter_a",
					enemyId = "skeleton",
					generatedAtTime = 0,
					duration = 1,
					timeCost = 1,
					battleLocation = BattleLocation.Desert,
					rewardResources = new ClimbResourceSave { red = 1, white = 0, black = 0 },
				},
				new()
				{
					id = "encounter_b",
					enemyId = "skeleton",
					generatedAtTime = 0,
					duration = 1,
					timeCost = 1,
					battleLocation = BattleLocation.Desert,
					rewardResources = new ClimbResourceSave { red = 1, white = 0, black = 0 },
				},
				new()
				{
					id = "encounter_c",
					enemyId = "skeleton",
					generatedAtTime = 0,
					duration = 1,
					timeCost = 1,
					battleLocation = BattleLocation.Desert,
					rewardResources = new ClimbResourceSave { red = 1, white = 0, black = 0 },
				},
			},
		};

		Assert.True(ClimbRuleService.ReplenishEncounterSlots(state, 123, TestLoadout()));
		AssertEncounterSlotDiversity(state.encounterSlots);
	}

	[Fact]
	public void Generated_encounter_locations_are_uniform_rollable_locations_only()
	{
		var rolledLocations = Enumerable.Range(1, 200)
			.Select(seed => ClimbRuleService.CreateInitialState(seed, TestLoadout()))
			.SelectMany(state => state.encounterSlots)
			.Select(slot => slot.battleLocation)
			.ToHashSet();

		Assert.Contains(BattleLocation.Desert, rolledLocations);
		Assert.Contains(BattleLocation.Tundra, rolledLocations);
		Assert.Contains(BattleLocation.Jungle, rolledLocations);
		Assert.Contains(BattleLocation.Volcano, rolledLocations);
		Assert.DoesNotContain(BattleLocation.TheGate, rolledLocations);
		Assert.All(rolledLocations, location => Assert.Contains(location, BattleLocationAssetService.ClimbEncounterLocations));
	}

	[Fact]
	public void Encounter_expiration_uses_rolled_duration_boundary()
	{
		var slot = new ClimbEncounterSlotSave
		{
			id = "encounter",
			enemyId = "skeleton",
			generatedAtTime = 4,
			duration = 3,
			timeCost = 1,
		};

		Assert.False(ClimbRuleService.IsEncounterExpired(slot, 6));
		Assert.True(ClimbRuleService.IsEncounterExpired(slot, 7));

		slot.isFinal = true;
		Assert.False(ClimbRuleService.IsEncounterExpired(slot, ClimbRuleService.MaxTime));
	}

	[Fact]
	public void Replenish_encounters_rerolls_expired_normal_slots()
	{
		var state = new ClimbSaveState
		{
			time = 5,
			encounterSlots = new List<ClimbEncounterSlotSave>
			{
				new()
				{
					id = "encounter_a",
					enemyId = "skeleton",
					generatedAtTime = 0,
					duration = 2,
					timeCost = 1,
					rewardResources = new ClimbResourceSave { red = 1, white = 0, black = 0 },
				},
				new()
				{
					id = "encounter_b",
					enemyId = "demon",
					generatedAtTime = 5,
					duration = 5,
					timeCost = 1,
					rewardResources = new ClimbResourceSave { red = 0, white = 1, black = 0 },
				},
				new()
				{
					id = "encounter_c",
					enemyId = "thornreaver",
					generatedAtTime = 5,
					duration = 5,
					timeCost = 1,
					rewardResources = new ClimbResourceSave { red = 0, white = 0, black = 1 },
				},
			},
		};

		Assert.True(ClimbRuleService.ReplenishEncounterSlots(state, 123));

		var rerolled = state.encounterSlots.Single(slot => slot.id == "encounter_a");
		Assert.Equal(state.time, rerolled.generatedAtTime);
		Assert.InRange(rerolled.duration, ClimbRuleService.EncounterMinDuration, ClimbRuleService.EncounterMaxDuration);
		Assert.False(ClimbRuleService.IsEncounterExpired(rerolled, state.time));
		AssertEncounterSlotDiversity(state.encounterSlots);
	}

	[Fact]
	public void Event_slots_assign_complete_deterministic_schedule_in_exact_bands()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());
		var repeated = ClimbRuleService.CreateInitialState(123, TestLoadout());

		Assert.Equal(
			state.eventSlots.Select(ScheduleSnapshot),
			repeated.eventSlots.Select(ScheduleSnapshot));
		Assert.Equal(2, state.eventSlots
			.Where(slot => slot.kind == ClimbEventKind.Character)
			.Select(slot => slot.definitionId)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count());
		for (int index = 0; index < state.eventSlots.Count; index++)
		{
			var slot = state.eventSlots[index];
			var band = ClimbRuleService.GetEventAppearanceBand(index);
			Assert.Equal(ClimbEventStatus.Scheduled, slot.status);
			Assert.Equal(-1, slot.activatedAtTime);
			Assert.InRange(slot.scheduledAppearanceTime, band.Start, band.End);
			if (slot.kind == ClimbEventKind.Hazard)
			{
				Assert.Equal(0, slot.timeCost);
				Assert.InRange(slot.duration, 2, 4);
				Assert.InRange(ResourcePips(slot.rewardResources), 1, 2);
			}
			else
			{
				Assert.Equal(1, slot.timeCost);
				Assert.InRange(slot.duration, 3, 5);
				Assert.Equal(0, ResourcePips(slot.rewardResources));
			}
		}
		Assert.Equal((1, 6), ClimbRuleService.GetEventAppearanceBand(0));
		Assert.Equal((7, 12), ClimbRuleService.GetEventAppearanceBand(1));
		Assert.Equal((13, 19), ClimbRuleService.GetEventAppearanceBand(2));
		Assert.Equal((20, 25), ClimbRuleService.GetEventAppearanceBand(3));
		Assert.Equal((26, 32), ClimbRuleService.GetEventAppearanceBand(4));
	}

	[Fact]
	public void Generated_schedules_allow_repeated_hazards_and_vary_across_seeds()
	{
		var schedules = Enumerable.Range(1, 200)
			.Select(seed => ClimbRuleService.GenerateEventSchedule(seed))
			.ToList();

		Assert.Contains(schedules, schedule => schedule
			.Where(slot => slot.kind == ClimbEventKind.Hazard)
			.GroupBy(slot => slot.definitionId, StringComparer.OrdinalIgnoreCase)
			.Any(group => group.Count() > 1));
		Assert.True(schedules.Select(schedule => string.Join(";", schedule.Select(ScheduleSnapshot))).Distinct().Count() > 1);
		Assert.True(schedules.SelectMany(schedule => schedule).Select(slot => slot.duration).Distinct().Count() > 1);
		Assert.True(schedules.SelectMany(schedule => schedule).Select(slot => slot.scheduledAppearanceTime).Distinct().Count() > 1);
		Assert.True(schedules.SelectMany(schedule => schedule.Where(slot => slot.kind == ClimbEventKind.Hazard))
			.Select(slot => $"{slot.rewardResources.red},{slot.rewardResources.white},{slot.rewardResources.black}")
			.Distinct().Count() > 1);
	}

	[Fact]
	public void Time_clamps_at_final_encounter_boundary()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());

		int applied = ClimbRuleService.ApplyTime(state, 99);

		Assert.Equal(32, ClimbRuleService.MaxTime);
		Assert.Equal(ClimbRuleService.MaxTime, state.time);
		Assert.Equal(ClimbRuleService.MaxTime, applied);
		Assert.True(ClimbRuleService.HasPendingFinalEncounter(state));
	}

	[Fact]
	public void Shop_refresh_triggers_when_time_crosses_eight_time_boundaries()
	{
		Assert.False(ClimbRuleService.ShouldRefreshShopAtTime(0, 7));
		Assert.True(ClimbRuleService.ShouldRefreshShopAtTime(7, 8));
		Assert.True(ClimbRuleService.ShouldRefreshShopAtTime(7, 10));
		Assert.False(ClimbRuleService.ShouldRefreshShopAtTime(8, 9));
		Assert.True(ClimbRuleService.ShouldRefreshShopAtTime(15, 16));
		Assert.True(ClimbRuleService.ShouldRefreshShopAtTime(23, 24));
		Assert.False(ClimbRuleService.ShouldRefreshShopAtTime(24, 31));
		Assert.False(ClimbRuleService.ShouldRefreshShopAtTime(30, ClimbRuleService.MaxTime));
		Assert.False(ClimbRuleService.ShouldRefreshShopAtTime(32, 40));
	}

	[Fact]
	public void Event_lifecycle_activates_on_landing_and_expires_end_exclusive()
	{
		var state = new ClimbSaveState
		{
			time = 7,
			eventSlots = new List<ClimbEventSlotSave>
			{
				new()
				{
					id = "event",
					definitionId = "bleached_standard",
					scheduledAppearanceTime = 5,
					duration = 2,
					status = ClimbEventStatus.Scheduled,
				}
			}
		};

		Assert.True(ClimbRuleService.UpdateEventLifecycle(state));
		Assert.Equal(ClimbEventStatus.Active, state.eventSlots[0].status);
		Assert.Equal(7, state.eventSlots[0].activatedAtTime);
		Assert.True(ClimbRuleService.IsEventVisible(state.eventSlots[0], 8));

		state.time = 9;
		Assert.True(ClimbRuleService.UpdateEventLifecycle(state));
		Assert.Equal(ClimbEventStatus.Expired, state.eventSlots[0].status);
	}

	[Fact]
	public void Landing_activates_every_crossed_schedule_at_the_same_time()
	{
		var state = new ClimbSaveState
		{
			time = 12,
			eventSlots = new List<ClimbEventSlotSave>
			{
				new() { id = "one", definitionId = "bleached_standard", scheduledAppearanceTime = 4, duration = 2 },
				new() { id = "two", definitionId = "nun_counsel", scheduledAppearanceTime = 9, duration = 5 },
			},
		};

		Assert.True(ClimbRuleService.UpdateEventLifecycle(state));

		Assert.All(state.eventSlots, slot =>
		{
			Assert.Equal(ClimbEventStatus.Active, slot.status);
			Assert.Equal(12, slot.activatedAtTime);
		});
	}

	[Fact]
	public void Final_time_preempts_scheduled_and_active_events_but_not_pending()
	{
		var state = new ClimbSaveState
		{
			time = ClimbRuleService.MaxTime,
			eventSlots = new List<ClimbEventSlotSave>
			{
				new() { id = "scheduled", status = ClimbEventStatus.Scheduled, scheduledAppearanceTime = 30 },
				new() { id = "active", status = ClimbEventStatus.Active, activatedAtTime = 30, duration = 4 },
				new() { id = "pending", status = ClimbEventStatus.Pending, activatedAtTime = 30, duration = 4 },
			},
		};

		Assert.True(ClimbRuleService.UpdateEventLifecycle(state));

		Assert.Equal(ClimbEventStatus.Expired, state.eventSlots[0].status);
		Assert.Equal(ClimbEventStatus.Expired, state.eventSlots[1].status);
		Assert.Equal(ClimbEventStatus.Pending, state.eventSlots[2].status);
	}

	[Fact]
	public void Resource_math_spends_only_when_affordable_and_adds_rewards()
	{
		var resources = new ClimbResourceSave { red = 1, white = 1, black = 0 };

		Assert.False(ClimbRuleService.TrySpend(resources, new ClimbResourceSave { red = 2, white = 0, black = 0 }));
		Assert.True(ClimbRuleService.TrySpend(resources, new ClimbResourceSave { red = 1, white = 0, black = 0 }));
		Assert.Equal(0, resources.red);

		ClimbRuleService.AddResources(resources, new ClimbResourceSave { red = 2, white = 0, black = 3 });
		Assert.Equal(2, resources.red);
		Assert.Equal(1, resources.white);
		Assert.Equal(3, resources.black);
	}

	[Fact]
	public void Initial_shop_has_two_medal_slots()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());

		Assert.Equal(2, state.shopSlots.Count(s => s.kind == ClimbShopSlotKinds.Medal));
	}

	[Fact]
	public void Shop_refresh_rolls_two_distinct_medals()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());
		var medalIds = state.shopSlots
			.Where(s => s.kind == ClimbShopSlotKinds.Medal)
			.Select(s => s.itemId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.ToList();

		Assert.Equal(2, medalIds.Count);
		Assert.False(string.Equals(medalIds[0], medalIds[1], StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Refresh_shop_excludes_medals_and_equipment_once_shown()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());
		var initialMedalIds = state.shopSlots
			.Where(s => s.kind == ClimbShopSlotKinds.Medal)
			.Select(s => s.itemId)
			.Where(id => !string.IsNullOrWhiteSpace(id))
			.ToList();
		var firstEquipment = state.shopSlots.FirstOrDefault(s => s.kind == ClimbShopSlotKinds.Equipment)?.itemId;

		state.time = 8;
		ClimbRuleService.RefreshShopSlots(state, 123, TestLoadout());

		foreach (var medalId in initialMedalIds)
		{
			Assert.DoesNotContain(state.shopSlots, s => string.Equals(s.itemId, medalId, StringComparison.OrdinalIgnoreCase));
		}
		if (!string.IsNullOrWhiteSpace(firstEquipment))
		{
			Assert.DoesNotContain(state.shopSlots, s => string.Equals(s.itemId, firstEquipment, StringComparison.OrdinalIgnoreCase));
		}

		AssertShopSlotDisplayOrder(state);
	}

	[Fact]
	public void Shop_slots_use_fixed_display_order()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());
		AssertShopSlotDisplayOrder(state);

		state.time = 8;
		ClimbRuleService.RefreshShopSlots(state, 123, TestLoadout());
		AssertShopSlotDisplayOrder(state);
	}

	[Fact]
	public void Shop_costs_use_time_adjusted_item_specific_resource_weights()
	{
		AssertShopCost(ClimbShopSlotKinds.Upgrade, 0, expectedTotal: 3, expectedDominant: "red");
		AssertShopCost(ClimbShopSlotKinds.Upgrade, 1, expectedTotal: 2, expectedDominant: "red");
		AssertShopCost(ClimbShopSlotKinds.Upgrade, 2, expectedTotal: 1, expectedDominant: "red");

		AssertShopCost(ClimbShopSlotKinds.Replacement, 0, expectedTotal: 3, expectedDominant: "red");
		AssertShopCost(ClimbShopSlotKinds.Replacement, 1, expectedTotal: 2, expectedDominant: "red");
		AssertShopCost(ClimbShopSlotKinds.Replacement, 2, expectedTotal: 1, expectedDominant: "red");

		AssertShopCost(ClimbShopSlotKinds.Medal, 0, expectedTotal: 6, expectedDominant: "white");
		AssertShopCost(ClimbShopSlotKinds.Medal, 1, expectedTotal: 4, expectedDominant: "white");
		AssertShopCost(ClimbShopSlotKinds.Medal, 2, expectedTotal: 2, expectedDominant: "white");

		AssertShopCost(ClimbShopSlotKinds.Equipment, 0, expectedTotal: 7, expectedDominant: "black");
		AssertShopCost(ClimbShopSlotKinds.Equipment, 1, expectedTotal: 5, expectedDominant: "black");
		AssertShopCost(ClimbShopSlotKinds.Equipment, 2, expectedTotal: 3, expectedDominant: "black");
	}

	[Fact]
	public void Encounter_rewards_match_time_cost()
	{
		var state = ClimbRuleService.CreateInitialState(123, TestLoadout());

		Assert.All(state.encounterSlots, slot =>
		{
			Assert.Equal(slot.timeCost, ResourcePips(slot.rewardResources));
		});
	}

	[Theory]
	[InlineData(0, "Red")]
	[InlineData(24, "Red")]
	[InlineData(25, "White")]
	[InlineData(59, "White")]
	[InlineData(60, "Black")]
	[InlineData(99, "Black")]
	public void Generate_reward_uses_weighted_resource_colors(int roll, string expectedColor)
	{
		var color = ClimbRuleService.RollResourceColorForTests(roll);

		Assert.Equal(expectedColor, color.ToString());
	}

	[Fact]
	public void Climb_encounter_pool_excludes_banned_and_image_less_enemies()
	{
		var pool = ClimbRuleService.GetClimbEncounterEnemyPool();

		Assert.NotEmpty(pool);
		Assert.DoesNotContain("horde", pool);
		Assert.DoesNotContain("sand_corpse", pool);
		Assert.DoesNotContain("training_demon", pool);
		foreach (string enemyId in pool)
		{
			var enemy = EnemyFactory.Create(enemyId);
			Assert.NotNull(enemy);
			Assert.False(enemy.IsBoss);
			Assert.False(enemy.IsTutorialOnly);
			Assert.True(EnemyPortraitContent.HasPortrait(enemyId));
		}
	}

	[Fact]
	public void Climb_replacement_auto_upgrades_off_weapon_cards()
	{
		var autoUpgradeIds = StartingDeckGeneratorService.GetAutoUpgradeCardIds("sword");
		var loadout = new LoadoutDefinition
		{
			id = RunDeckService.PrimaryLoadoutId,
			cards = new List<LoadoutCardEntry>(),
			weaponId = "sword",
			medalIds = new List<string>(),
		};

		bool foundUpgradedOffWeapon = false;
		for (int i = 0; i < 50; i++)
		{
			string key = ClimbRuleService.RollReplacementIncomingCardKey(new Random(i), loadout);
			if (string.IsNullOrWhiteSpace(key)) continue;
			Assert.True(RunDeckService.TryParseCardKey(key, out var cardId, out _, out var isUpgraded));
			if (autoUpgradeIds.Contains(cardId))
			{
				Assert.True(isUpgraded,
					$"Off-weapon card '{cardId}' in climb shop should be auto-upgraded but was not: {key}");
				foundUpgradedOffWeapon = true;
			}
		}
		Assert.True(foundUpgradedOffWeapon,
			"Expected at least one off-weapon auto-upgraded card to appear in climb shop replacement within 50 iterations");
	}

	[Fact]
	public void Climb_replacement_does_not_auto_upgrade_starter_rarity_cards()
	{
		var loadout = new LoadoutDefinition
		{
			id = RunDeckService.PrimaryLoadoutId,
			cards = new List<LoadoutCardEntry>(),
			weaponId = "sword",
			medalIds = new List<string>(),
		};

		for (int i = 0; i < 50; i++)
		{
			string key = ClimbRuleService.RollReplacementIncomingCardKey(new Random(i), loadout);
			if (string.IsNullOrWhiteSpace(key)) continue;
			Assert.True(RunDeckService.TryParseCardKey(key, out var cardId, out _, out var isUpgraded));
			if (cardId == "smite" || cardId == "absolution" || cardId == "forge_strike"
				|| cardId == "reckoning" || cardId == "litany_of_wrath" || cardId == "courageous")
			{
				Assert.False(isUpgraded,
					$"Starter rarity card '{cardId}' in climb shop should NOT be auto-upgraded but was: {key}");
			}
		}
	}

	private static void AssertEncounterSlotDiversity(IReadOnlyList<ClimbEncounterSlotSave> slots)
	{
		var activeSlots = slots
			.Where(slot => slot != null && !slot.isFinal && !string.IsNullOrWhiteSpace(slot.enemyId))
			.ToList();
		if (activeSlots.Count < ClimbRuleService.EncounterSlotCount) return;

		Assert.True(activeSlots
			.Select(slot => slot.enemyId)
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Count() >= 2);
		Assert.True(activeSlots
			.Select(slot => slot.battleLocation)
			.Distinct()
			.Count() >= 2);
	}

	private static LoadoutDefinition TestLoadout()
	{
		return new LoadoutDefinition
		{
			id = RunDeckService.PrimaryLoadoutId,
			cards = new List<LoadoutCardEntry>
			{
				new() { entryId = "test_entry_0", cardKey = "smite|White", isStarter = true },
				new() { entryId = "test_entry_1", cardKey = "fervor|Red", isStarter = true },
				new() { entryId = "test_entry_2", cardKey = "reckoning|Black", isStarter = true },
			},
			weaponId = "sword",
			medalIds = new List<string>(),
		};
	}

	private static int ResourcePips(ClimbResourceSave resources)
	{
		return Math.Max(0, resources?.red ?? 0)
			+ Math.Max(0, resources?.white ?? 0)
			+ Math.Max(0, resources?.black ?? 0);
	}

	private static string ScheduleSnapshot(ClimbEventSlotSave slot)
	{
		return $"{slot.id}|{slot.definitionId}|{slot.kind}|{slot.hazardEffect}|{slot.characterReward}|{slot.scheduledAppearanceTime}|{slot.duration}|{slot.effectAmount}|{ResourcePips(slot.rewardResources)}";
	}

	private static void AssertShopSlotDisplayOrder(ClimbSaveState state)
	{
		Assert.Equal(ClimbRuleService.ShopSlotCount, state.shopSlots.Count);
		for (int i = 0; i < ClimbRuleService.ShopSlotCount; i++)
		{
			if (string.Equals(state.shopSlots[i].kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase))
				continue;
			Assert.Equal(ClimbShopSlotKinds.DisplayOrder[i], state.shopSlots[i].kind, ignoreCase: true);
		}
	}

	private static void AssertShopCost(string kind, int timeCost, int expectedTotal, string expectedDominant)
	{
		var cost = ClimbRuleService.GenerateShopCostForTests(kind, timeCost);

		Assert.Equal(expectedTotal, ResourcePips(cost));
		AssertDominant(cost, expectedDominant);
	}

	private static void AssertDominant(ClimbResourceSave cost, string expectedDominant)
	{
		if (string.Equals(expectedDominant, "red", StringComparison.OrdinalIgnoreCase))
		{
			Assert.True(cost.red > cost.white);
			Assert.True(cost.red > cost.black);
			return;
		}

		if (string.Equals(expectedDominant, "white", StringComparison.OrdinalIgnoreCase))
		{
			Assert.True(cost.white > cost.red);
			Assert.True(cost.white > cost.black);
			return;
		}

		Assert.True(cost.black > cost.red);
		Assert.True(cost.black > cost.white);
	}
}
