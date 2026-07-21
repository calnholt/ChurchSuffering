using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CollectionProgressionTests : IDisposable
{
	public CollectionProgressionTests()
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
	public void New_collection_contains_the_configured_core_pools()
	{
		var collection = SaveCache.GetCollection();
		Assert.Contains("consecrate", collection.cardIds);
		Assert.Contains("dowse_with_holy_water", collection.cardIds);
		Assert.Contains("strike", collection.cardIds);
		Assert.Contains(StartingDeckGeneratorService.DefaultStarterCardPool, id => collection.cardIds.Contains(id));
		Assert.Equal(9, collection.medalIds.Count);
		Assert.Equal(12, collection.equipmentIds.Count);
	}

	[Fact]
	public void Collection_persists_when_a_run_starts_and_ends()
	{
		var collection = SaveCache.GetCollection();
		collection.cardIds.Add("absolution");
		collection.pendingClimbPoints = 7;
		SaveCache.SaveCollection(collection);

		SaveCache.StartNewRun();
		SaveCache.MarkRunInactive();
		SaveCache.Reload();

		var restored = SaveCache.GetCollection();
		Assert.Contains("absolution", restored.cardIds);
		Assert.Equal(7, restored.pendingClimbPoints);
	}

	[Theory]
	[InlineData(0, 0, 0, 5)]
	[InlineData(4, 0, 4, 5)]
	[InlineData(5, 1, 0, 10)]
	[InlineData(14, 1, 9, 10)]
	[InlineData(15, 2, 0, 10)]
	[InlineData(104, 10, 9, 10)]
	[InlineData(105, 11, 0, 20)]
	[InlineData(124, 11, 19, 20)]
	[InlineData(125, 12, 0, 20)]
	public void Level_state_uses_five_ten_then_twenty_point_thresholds(int total, int level, int inLevel, int required)
	{
		var actual = CollectionProgressionRules.GetLevelState(total);
		Assert.Equal(level, actual.Level);
		Assert.Equal(inLevel, actual.PointsInLevel);
		Assert.Equal(required, actual.PointsRequired);
	}

	[Fact]
	public void Pack_generation_uses_only_unowned_items_and_reserves_all_three_rewards()
	{
		var collection = new PlayerCollectionSave();
		var pack = CollectionProgressionRules.CreatePack(collection, new Random(1234));

		Assert.NotNull(pack);
		Assert.Equal(3, pack.rewards.Count);
		Assert.Equal(3, pack.rewards.Select(reward => reward.kind + ":" + reward.id).Distinct(StringComparer.OrdinalIgnoreCase).Count());
		foreach (var reward in pack.rewards)
		{
			switch (reward.kind)
			{
				case "card": Assert.Contains(reward.id, collection.cardIds); break;
				case "medal": Assert.Contains(reward.id, collection.medalIds); break;
				case "equipment": Assert.Contains(reward.id, collection.equipmentIds); break;
			}
		}
	}

	[Fact]
	public void Fully_owned_collection_processes_level_without_creating_an_empty_pack()
	{
		var collection = new PlayerCollectionSave
		{
			cardIds = CardFactory.GetAllCards().Values
				.Where(card => card.CanAddToLoadout && !card.IsWeapon && !card.IsToken)
				.Select(card => card.CardId).ToList(),
			medalIds = MedalFactory.GetAllMedals().Keys.Select(id => id.ToKey()).ToList(),
			equipmentIds = EquipmentFactory.GetAllEquipment().Keys.Select(id => id.ToKey()).ToList(),
			totalPoints = 5,
		};

		CollectionProgressionRules.ReconcileEarnedPacks(collection, new Random(1));

		Assert.Equal(1, collection.processedRewardLevels);
		Assert.Empty(collection.pendingBoosterPacks);
	}

	[Theory]
	[InlineData(0, false, false, 0)]
	[InlineData(8, false, false, 1)]
	[InlineData(16, false, false, 4)]
	[InlineData(24, false, false, 9)]
	[InlineData(32, true, false, 12)]
	[InlineData(32, true, true, 0)]
	public void Climb_point_banking_uses_refreshes_and_final_boss(int time, bool completed, bool abandoned, int expected)
	{
		Assert.Equal(expected, CollectionProgressionRules.CalculateClimbPoints(time, completed, abandoned));
	}

	[Theory]
	[InlineData(8, false, 0)]
	[InlineData(9, false, 1)]
	[InlineData(18, false, 4)]
	[InlineData(27, false, 9)]
	[InlineData(36, true, 12)]
	public void Pilgrimage_climb_points_follow_nine_pip_shop_refreshes(int time, bool completed, int expected)
	{
		Assert.Equal(expected, CollectionProgressionRules.CalculateClimbPoints(
			time,
			completed,
			abandoned: false,
			shopRefreshInterval: 9));
	}

	[Fact]
	public void Collection_system_persists_pilgrimage_refresh_interval_with_award()
	{
		_ = new CollectionProgressionSystem(new EntityManager());

		EventManager.Publish(new ClimbEndedEvent
		{
			TimeReached = 8,
			ShopRefreshInterval = 9,
			CompletedFinalBoss = false,
			Abandoned = false,
		});

		var collection = SaveCache.GetCollection();
		Assert.Equal(0, collection.pendingClimbPoints);
		Assert.Equal(9, collection.pendingClimbPointAward.shopRefreshInterval);
	}

	[Theory]
	[InlineData(0, false, false, 0)]
	[InlineData(12, false, false, 1)]
	[InlineData(20, false, false, 4)]
	[InlineData(26, false, false, 9)]
	[InlineData(32, true, false, 12)]
	[InlineData(18, false, true, 0)]
	public void Collection_system_persists_each_climb_award_presentation(
		int time,
		bool completed,
		bool abandoned,
		int expectedPoints)
	{
		_ = new CollectionProgressionSystem(new EntityManager());

		EventManager.Publish(new ClimbEndedEvent
		{
			TimeReached = time,
			CompletedFinalBoss = completed,
			Abandoned = abandoned,
		});

		var collection = SaveCache.GetCollection();
		Assert.Equal(expectedPoints, collection.pendingClimbPoints);
		Assert.NotNull(collection.pendingClimbPointAward);
		Assert.Equal(time, collection.pendingClimbPointAward.timeReached);
		Assert.Equal(completed, collection.pendingClimbPointAward.completedFinalBoss);
		Assert.Equal(abandoned, collection.pendingClimbPointAward.abandoned);
		Assert.Equal(expectedPoints, collection.pendingClimbPointAward.pointsAwarded);
	}

	[Fact]
	public void Authoritative_overlay_dismissal_clears_only_presentation_payload()
	{
		_ = new CollectionProgressionSystem(new EntityManager());
		EventManager.Publish(new ClimbEndedEvent
		{
			TimeReached = 20,
			CompletedFinalBoss = false,
			Abandoned = false,
		});

		EventManager.Publish(new ClimbPointsAwardOverlayDismissedEvent { WasAuthoritative = true });

		var collection = SaveCache.GetCollection();
		Assert.Equal(4, collection.pendingClimbPoints);
		Assert.Null(collection.pendingClimbPointAward);
	}

	[Fact]
	public void Debug_overlay_dismissal_does_not_clear_authoritative_payload()
	{
		_ = new CollectionProgressionSystem(new EntityManager());
		EventManager.Publish(new ClimbEndedEvent
		{
			TimeReached = 12,
			CompletedFinalBoss = false,
			Abandoned = false,
		});

		EventManager.Publish(new ClimbPointsAwardOverlayDismissedEvent { WasAuthoritative = false });

		Assert.NotNull(SaveCache.GetCollection().pendingClimbPointAward);
	}

	[Theory]
	[InlineData(0, 5)]
	[InlineData(4, 5)]
	[InlineData(5, 15)]
	[InlineData(14, 15)]
	[InlineData(104, 105)]
	[InlineData(105, 125)]
	[InlineData(124, 125)]
	[InlineData(125, 145)]
	public void Next_level_threshold_uses_five_ten_then_twenty_point_costs(int total, int expected)
	{
		Assert.Equal(expected, CollectionProgressionRules.GetNextLevelThresholdTotal(total));
	}

	[Fact]
	public void Collection_system_claims_banked_climb_points()
	{
		var manager = new EntityManager();
		var scene = manager.CreateEntity("SceneState");
		manager.AddComponent(scene, new SceneState { Current = SceneId.Achievement });
		_ = new CollectionProgressionSystem(manager);
		var collection = SaveCache.GetCollection();
		collection.cardIds = new List<string>(collection.cardIds);
		collection.pendingClimbPoints = 5;
		collection.totalPoints = 0;
		collection.processedRewardLevels = 0;
		collection.pendingBoosterPacks.Clear();
		SaveCache.SaveCollection(collection);

		EventManager.Publish(new ClimbPointsSegmentAwardedEvent
		{
			NewTotalPoints = 5,
			TriggeredLevelComplete = true,
		});

		var claimed = SaveCache.GetCollection();
		Assert.Equal(5, claimed.totalPoints);
		Assert.Equal(0, claimed.pendingClimbPoints);
		Assert.Equal(1, claimed.processedRewardLevels);
		Assert.Single(claimed.pendingBoosterPacks);
	}

	[Fact]
	public void Collection_system_awards_partial_climb_points_without_booster()
	{
		var manager = new EntityManager();
		var scene = manager.CreateEntity("SceneState");
		manager.AddComponent(scene, new SceneState { Current = SceneId.Achievement });
		_ = new CollectionProgressionSystem(manager);
		var collection = SaveCache.GetCollection();
		collection.cardIds = new List<string>(collection.cardIds);
		collection.pendingClimbPoints = 8;
		collection.totalPoints = 0;
		collection.processedRewardLevels = 0;
		collection.pendingBoosterPacks.Clear();
		SaveCache.SaveCollection(collection);

		EventManager.Publish(new ClimbPointsSegmentAwardedEvent
		{
			NewTotalPoints = 5,
			TriggeredLevelComplete = true,
		});
		EventManager.Publish(new ClimbPointsSegmentAwardedEvent
		{
			NewTotalPoints = 8,
			TriggeredLevelComplete = false,
		});

		var claimed = SaveCache.GetCollection();
		Assert.Equal(8, claimed.totalPoints);
		Assert.Equal(0, claimed.pendingClimbPoints);
		Assert.Equal(1, claimed.processedRewardLevels);
		Assert.Single(claimed.pendingBoosterPacks);
	}

	[Fact]
	public void Climb_shop_and_deck_rewards_only_use_collection_content()
	{
		var collection = SaveCache.GetCollection();
		collection.cardIds = new List<string> { "consecrate", "crusade", "strike", "zealous_vow" };
		collection.medalIds = new List<string> { "st_luke" };
		collection.equipmentIds = new List<string> { "scarlet_vest" };
		SaveCache.SaveCollection(collection);

		var loadout = SaveCache.GetLoadout("loadout_1");
		var climb = new ClimbSaveState
		{
			resources = new ClimbResourceSave(),
			shopSlots = new List<ClimbShopSlotSave>(),
			encounterSlots = new List<ClimbEncounterSlotSave>(),
			eventSlots = new List<ClimbEventSlotSave>(),
			shownMedalIds = new List<string>(),
			shownEquipmentIds = new List<string>(),
		};
		ClimbRuleService.RefreshShopSlots(climb, 987, loadout);
		Assert.All(climb.shopSlots.Where(slot => slot.kind == ClimbShopSlotKinds.Medal), slot => Assert.Equal("st_luke", slot.itemId));
		Assert.All(climb.shopSlots.Where(slot => slot.kind == ClimbShopSlotKinds.Equipment), slot => Assert.Equal("scarlet_vest", slot.itemId));

		var offer = QuestCardRewardService.GenerateDeckRewardOffer(
			new[] { "strike|White", "crusade|Red", "consecrate|Black" },
			"sword",
			restrictToCollection: true);
		Assert.Contains(offer.options, option => option.kind == DeckRewardOfferKinds.Exchange);
		Assert.All(offer.options.Where(option => option.kind == DeckRewardOfferKinds.Exchange), option =>
		{
			Assert.True(RunDeckService.TryParseCardKey(option.incomingCardKey, out var id, out _));
			Assert.Contains(id, collection.cardIds, StringComparer.OrdinalIgnoreCase);
		});
	}
}
