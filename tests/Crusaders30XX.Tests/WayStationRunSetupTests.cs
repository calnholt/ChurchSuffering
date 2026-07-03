using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Xunit;

namespace Crusaders30XX.Tests;

public class WayStationRunSetupTests
{
	[Fact]
	public void Depart_starts_new_run_on_climb_without_queueing_battle()
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			var world = new World();
			ShowTransition transition = null;
			EventManager.Subscribe<ShowTransition>(evt => transition = evt);

			WayStationRunSetupService.Depart(world);

			Assert.True(SaveCache.IsRunActive());
			Assert.NotNull(transition);
			Assert.Equal(SceneId.Climb, transition.Scene);
			Assert.Empty(world.EntityManager.GetEntitiesWithComponent<QueuedEvents>());
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Theory]
	[InlineData(RunDifficulty.Easy, 25)]
	[InlineData(RunDifficulty.Normal, 22)]
	[InlineData(RunDifficulty.Hard, 20)]
	public void Depart_prepares_run_player_with_selected_difficulty_hp(
		RunDifficulty difficulty,
		int expectedMaxHp)
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			WayStationRunSetupSingleton.SelectedDifficulty = difficulty;
			var world = new World();

			WayStationRunSetupService.Depart(world);

			var player = world.EntityManager.GetEntity("Player");
			var hp = player?.GetComponent<HP>();
			Assert.NotNull(player);
			Assert.NotNull(hp);
			Assert.Equal(expectedMaxHp, hp.Max);
			Assert.Equal(expectedMaxHp, hp.Current);
			Assert.Same(world.EntityManager.GetEntity("Deck"), player.GetComponent<Player>().DeckEntity);
			Assert.Empty(world.EntityManager.GetEntitiesWithComponent<QueuedEvents>());
		}
		finally
		{
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
			EventManager.Clear();
		}
	}

	[Fact]
	public void EnsureRunPlayer_applies_selected_difficulty_hp_only_when_creating_player()
	{
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			SaveCache.StartNewRun();
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Normal;
			var world = new World();

			var player = RunPlayerService.EnsureRunPlayer(world);
			var hp = player.GetComponent<HP>();
			Assert.Equal(22, hp.Max);
			Assert.Equal(22, hp.Current);

			hp.Max = 27;
			hp.Current = 13;
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;

			Assert.Same(player, RunPlayerService.EnsureRunPlayer(world));
			Assert.Equal(27, hp.Max);
			Assert.Equal(13, hp.Current);
		}
		finally
		{
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
		}
	}

	[Fact]
	public void Weapon_starter_pools_only_reference_known_cards()
	{
		var swordPool = StartingDeckGeneratorService.GetSwordStarterCardPool();
		var daggerPool = StartingDeckGeneratorService.GetDaggerStarterCardPool();
		var hammerPool = StartingDeckGeneratorService.GetHammerStarterCardPool();
		var swordSingleCopyPool = StartingDeckGeneratorService.GetSwordSingleCopyStarterCardPool();
		var daggerSingleCopyPool = StartingDeckGeneratorService.GetDaggerSingleCopyStarterCardPool();
		var hammerSingleCopyPool = StartingDeckGeneratorService.GetHammerSingleCopyStarterCardPool();

		Assert.Equal(9, swordPool.Count);
		Assert.Equal(9, daggerPool.Count);
		Assert.Equal(9, hammerPool.Count);
		Assert.Contains("fervor", swordPool);
		Assert.Contains("seize", daggerPool);
		Assert.Contains("mantlet", hammerPool);
		Assert.DoesNotContain("exaltation", swordPool);
		Assert.DoesNotContain("razor_storm", daggerPool);
		Assert.DoesNotContain("unburdened_strike", hammerPool);
		foreach (var cardId in swordPool
			.Concat(daggerPool)
			.Concat(hammerPool)
			.Concat(swordSingleCopyPool)
			.Concat(daggerSingleCopyPool)
			.Concat(hammerSingleCopyPool))
		{
			Assert.NotNull(CardFactory.Create(cardId));
		}
	}

	[Fact]
	public void All_weapon_main_pools_include_shared_weapon_run_starter_cards()
	{
		foreach (var sharedCardId in StartingDeckGeneratorService.SharedWeaponRunStarterCardPool)
		{
			Assert.Contains(sharedCardId, StartingDeckGeneratorService.GetSwordStarterCardPool());
			Assert.Contains(sharedCardId, StartingDeckGeneratorService.GetDaggerStarterCardPool());
			Assert.Contains(sharedCardId, StartingDeckGeneratorService.GetHammerStarterCardPool());
		}
	}

	[Fact]
	public void Weapon_main_pools_do_not_overlap_single_copy_pools()
	{
		Assert.Empty(StartingDeckGeneratorService.GetSwordStarterCardPool()
			.Intersect(StartingDeckGeneratorService.GetSwordSingleCopyStarterCardPool(), System.StringComparer.OrdinalIgnoreCase));
		Assert.Empty(StartingDeckGeneratorService.GetDaggerStarterCardPool()
			.Intersect(StartingDeckGeneratorService.GetDaggerSingleCopyStarterCardPool(), System.StringComparer.OrdinalIgnoreCase));
		Assert.Empty(StartingDeckGeneratorService.GetHammerStarterCardPool()
			.Intersect(StartingDeckGeneratorService.GetHammerSingleCopyStarterCardPool(), System.StringComparer.OrdinalIgnoreCase));
	}

	[Theory]
	[InlineData(RunDifficulty.Easy, 25, 0.8f)]
	[InlineData(RunDifficulty.Normal, 22, 0.9f)]
	[InlineData(RunDifficulty.Hard, 20, 1.0f)]
	public void Difficulty_maps_to_player_hp_and_enemy_health_modifier(
		RunDifficulty difficulty,
		int expectedPlayerMaxHp,
		float expectedEnemyHealthModifier)
	{
		WayStationRunSetupSingleton.SelectedDifficulty = difficulty;

		Assert.Equal(expectedPlayerMaxHp, WayStationRunSetupSingleton.PlayerMaxHp);
		Assert.Equal(expectedEnemyHealthModifier, WayStationRunSetupSingleton.EnemyHealthModifier);
	}

	[Theory]
	[InlineData(RunDifficulty.Easy, 21)]
	[InlineData(RunDifficulty.Normal, 23)]
	[InlineData(RunDifficulty.Hard, 26)]
	public void Enemy_factory_scales_health_for_selected_run_difficulty(
		RunDifficulty difficulty,
		int expectedEnemyHp)
	{
		var world = PrepareWorldWithLoadout(Enumerable.Range(0, 20).Select(_ => "smite|White").ToList());

		WayStationRunSetupSingleton.SelectedDifficulty = difficulty;

		var enemyEntity = EntityFactory.CreateEnemyFromId(world, "skeleton", world.EntityManager);
		var enemy = enemyEntity.GetComponent<Enemy>();
		var hp = enemyEntity.GetComponent<HP>();

		Assert.Equal(expectedEnemyHp, enemy.MaxHealth);
		Assert.Equal(expectedEnemyHp, enemy.CurrentHealth);
		Assert.Equal(expectedEnemyHp, enemy.EnemyBase.MaxHealth);
		Assert.Equal(expectedEnemyHp, enemy.EnemyBase.CurrentHealth);
		Assert.Equal(expectedEnemyHp, hp.Max);
		Assert.Equal(expectedEnemyHp, hp.Current);
	}

	[Fact]
	public void Enemy_factory_adds_climb_time_weight_to_health()
	{
		var world = PrepareWorldWithLoadout(new List<string>
		{
			"smite|White",
			"fervor|Red",
			"reckoning|Black",
			"strike|Red",
		}, climbTime: 9);
		WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Hard;

		var enemyEntity = EntityFactory.CreateEnemyFromId(world, "skeleton", world.EntityManager);

		Assert.Equal(8, enemyEntity.GetComponent<Enemy>().MaxHealth);
	}

	[Fact]
	public void Enemy_factory_applies_st_clare_to_base_card_count_before_bonuses()
	{
		var world = PrepareWorldWithLoadout(
			new List<string>
			{
				"smite|White",
				"fervor|Red",
				"reckoning|Black",
				"strike|Red",
				"smite|White",
				"fervor|Red",
				"reckoning|Black",
				"strike|Red",
			},
			climbTime: 17);
		WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Hard;
		RunPlayerService.EnsureRunPlayer(world);
		RunMedalService.AcquireAndEquip(world.EntityManager, "st_clare");

		var enemyEntity = EntityFactory.CreateEnemyFromId(world, "skeleton", world.EntityManager);

		Assert.Equal(10, enemyEntity.GetComponent<Enemy>().MaxHealth);
	}

	[Fact]
	public void Diagnostic_enemy_hp_calculation_wyvern_easy_27cards_time8()
	{
		// Reproduce user report: Wyvern, Easy difficulty, ~27 cards, time ~8
		// Expected with fix: deckWeight=29, pre-mod=48, Easy(0.8)=38
		const int cardCount = 27;
		const int climbTime = 8;
		const int wyvernHp = 33;
		const float expectedModifier = 0.8f;   // Easy

		var world = PrepareWorldWithLoadout(
			Enumerable.Range(0, cardCount).Select(_ => "smite|White").ToList(),
			climbTime: climbTime);

		WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;

		float actualModifier = WayStationRunSetupSingleton.EnemyHealthModifier;
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		int loadoutCardCount = loadout.cards.Count;
		var climb = SaveCache.GetClimbState();
		int actualClimbTime = climb.time;

		Console.WriteLine($"=== DIAGNOSTIC: Enemy HP Calculation ===");
		Console.WriteLine($"SelectedDifficulty:       {WayStationRunSetupSingleton.SelectedDifficulty}");
		Console.WriteLine($"EnemyHealthModifier:       {actualModifier} (expected {expectedModifier})");
		Console.WriteLine($"Loadout card count:        {loadoutCardCount}");
		Console.WriteLine($"Climb time:                {actualClimbTime}");
		Console.WriteLine($"ShopRefreshInterval:       {ClimbRuleService.ShopRefreshInterval}");
		Console.WriteLine($"TimeBonusMultiplier:       {RunDeckService.EnemyHealthClimbTimeBonusMultiplier}");
		Console.WriteLine($"Wyvern HP (20-card base):  {wyvernHp}");

		var deckEntity = world.EntityManager.GetEntitiesWithComponent<Deck>().First();
		var deck = deckEntity.GetComponent<Deck>();
		float deckWeight = RunDeckService.CalculateEnemyHealthDeckWeight(
			world.EntityManager, deck.Cards.Count, 0);
		int timeBonus = (climbTime / ClimbRuleService.ShopRefreshInterval) * RunDeckService.EnemyHealthClimbTimeBonusMultiplier;
		int preModifierHp = (int)Math.Round(wyvernHp * deckWeight / EnemyBase.ReferenceDeckCardCount);
		int postModifierHp = (int)Math.Round(preModifierHp * actualModifier);

		Console.WriteLine($"Time bonus:                {timeBonus}");
		Console.WriteLine($"Deck weight:               {deckWeight} (cards + timeBonus)");
		Console.WriteLine($"Pre-modifier HP:           {preModifierHp} (= Round({wyvernHp} * {deckWeight} / {EnemyBase.ReferenceDeckCardCount}))");
		Console.WriteLine($"Post-modifier HP:          {postModifierHp} (= Round({preModifierHp} * {actualModifier}))");

		var enemyEntity = EntityFactory.CreateEnemyFromId(world, "wyvern", world.EntityManager);
		var enemy = enemyEntity.GetComponent<Enemy>();
		var hp = enemyEntity.GetComponent<HP>();

		Console.WriteLine($"--- Actual entity values ---");
		Console.WriteLine($"Enemy.MaxHealth:           {enemy.MaxHealth}");
		Console.WriteLine($"Enemy.CurrentHealth:       {enemy.CurrentHealth}");
		Console.WriteLine($"HP.Max:                    {hp.Max}");
		Console.WriteLine($"HP.Current:                {hp.Current}");
		Console.WriteLine($"================================");

		// Foundation: modifier must be Easy (0.8)
		Assert.Equal(expectedModifier, actualModifier);

		// Core assertion: post-modifier HP must match entity values
		Assert.Equal(postModifierHp, enemy.MaxHealth);
		Assert.Equal(postModifierHp, enemy.CurrentHealth);
		Assert.Equal(postModifierHp, hp.Max);
		Assert.Equal(postModifierHp, hp.Current);

		// With the fix: deckWeight=29, postMod=Round(48*0.8)=38
		// Without the fix (old %): deckWeight=27, postMod=Round(45*0.8)=36
		Assert.Equal(38, enemy.MaxHealth);
	}

	private static World PrepareWorldWithLoadout(
		IReadOnlyList<string> cardKeys,
		IReadOnlyList<string> tradedKeys = null,
		int climbTime = 0)
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		var unmatchedTradedKeys = (tradedKeys ?? Array.Empty<string>()).ToList();
		loadout.cards = cardKeys.Select((cardKey, index) =>
		{
			int tradedIndex = unmatchedTradedKeys.FindIndex(candidate =>
				string.Equals(candidate, cardKey, StringComparison.OrdinalIgnoreCase));
			bool countsAsTraded = tradedIndex >= 0;
			if (countsAsTraded) unmatchedTradedKeys.RemoveAt(tradedIndex);
			return new LoadoutCardEntry
			{
				entryId = $"test_card_{index}",
				cardKey = cardKey,
				isStarter = !countsAsTraded,
				countsAsTraded = countsAsTraded,
				restrictions = new List<string>(),
			};
		}).ToList();
		loadout.weaponId = "sword";
		loadout.medalIds = new List<string>();
		SaveCache.SaveLoadout(loadout);

		var climb = SaveCache.GetClimbState();
		climb.time = climbTime;
		SaveCache.SaveClimbState(climb);

		var world = new World();
		var deckEntity = world.CreateEntity("Deck");
		var deck = new Deck();
		world.AddComponent(deckEntity, deck);
		for (int i = 0; i < cardKeys.Count; i++)
		{
			deck.Cards.Add(world.CreateEntity($"DeckCard_{i}"));
		}

		return world;
	}
}
