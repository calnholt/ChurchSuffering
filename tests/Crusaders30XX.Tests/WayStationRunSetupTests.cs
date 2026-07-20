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
			Assert.Equal(difficulty, SaveCache.GetClimbState().difficulty);
			Assert.Same(world.EntityManager.GetEntity("Deck"), player.GetComponent<Player>().DeckEntity);
			Assert.Empty(world.EntityManager.GetEntitiesWithComponent<QueuedEvents>());
		}
		finally
		{
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
			EventManager.Clear();
		}
	}

	[Theory]
	[InlineData(StartingWeapon.Sword, "sword")]
	[InlineData(StartingWeapon.Dagger, "dagger")]
	[InlineData(StartingWeapon.Hammer, "hammer")]
	public void Depart_persists_the_starting_weapon_for_climb_completion(
		StartingWeapon weapon,
		string expectedWeaponId)
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			WayStationRunSetupSingleton.SelectedWeapon = weapon;

			WayStationRunSetupService.Depart(new World());

			Assert.Equal(expectedWeaponId, SaveCache.GetClimbState().startingWeaponId);
		}
		finally
		{
			WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Sword;
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
	[InlineData(RunDifficulty.Easy, 25, 0.7f)]
	[InlineData(RunDifficulty.Normal, 22, 0.85f)]
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
	[InlineData(RunDifficulty.Easy, 18)]
	[InlineData(RunDifficulty.Normal, 22)]
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
	public void Enemy_factory_adds_climb_time_percent_bonus_after_difficulty()
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

		// Skeleton HP 26, Hard (1.0), time 9 → 1 interval → Round(26 * 1.1) = 29
		Assert.Equal(29, enemyEntity.GetComponent<Enemy>().MaxHealth);
	}

	[Fact]
	public void Enemy_factory_climb_time_bonus_ignores_deck_size()
	{
		const int climbTime = 8;
		// Wyvern 28, Easy 0.7 → Round(28*0.7)=20; time 8 → 1 interval → Round(20*1.1)=22
		const int expectedHp = 22;

		var smallDeckWorld = PrepareWorldWithLoadout(
			Enumerable.Range(0, 4).Select(_ => "smite|White").ToList(),
			climbTime: climbTime);
		WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
		int smallDeckHp = EntityFactory.CreateEnemyFromId(smallDeckWorld, "wyvern", smallDeckWorld.EntityManager)
			.GetComponent<Enemy>().MaxHealth;

		var largeDeckWorld = PrepareWorldWithLoadout(
			Enumerable.Range(0, 27).Select(_ => "smite|White").ToList(),
			climbTime: climbTime);
		WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
		int largeDeckHp = EntityFactory.CreateEnemyFromId(largeDeckWorld, "wyvern", largeDeckWorld.EntityManager)
			.GetComponent<Enemy>().MaxHealth;

		Assert.Equal(expectedHp, smallDeckHp);
		Assert.Equal(expectedHp, largeDeckHp);
		Assert.Equal(smallDeckHp, largeDeckHp);
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
