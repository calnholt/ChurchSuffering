using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

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

			Depart(world, StartingWeapon.Sword, 0);

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
	[InlineData(0, 25)]
	[InlineData(12, 22)]
	[InlineData(24, 20)]
	public void Depart_prepares_run_player_with_penance_hp(int penanceLevel, int expectedMaxHp)
	{
		EventManager.Clear();
		try
		{
			SaveCache.DeleteSaveFilesIfPresent();
			var world = new World();

			Depart(world, StartingWeapon.Sword, penanceLevel);

			var player = world.EntityManager.GetEntity("Player");
			var hp = player?.GetComponent<HP>();
			Assert.NotNull(player);
			Assert.NotNull(hp);
			Assert.Equal(expectedMaxHp, hp.Max);
			Assert.Equal(expectedMaxHp, hp.Current);
			Assert.Equal(penanceLevel, SaveCache.GetClimbState().penanceLevel);
			Assert.Same(world.EntityManager.GetEntity("Deck"), player.GetComponent<Player>().DeckEntity);
		}
		finally
		{
			EventManager.Clear();
		}
	}

	[Theory]
	[InlineData(StartingWeapon.Sword, "sword")]
	[InlineData(StartingWeapon.Dagger, "dagger")]
	[InlineData(StartingWeapon.Hammer, "hammer")]
	public void Depart_persists_starting_weapon(StartingWeapon weapon, string expectedWeaponId)
	{
		SaveCache.DeleteSaveFilesIfPresent();
		Depart(new World(), weapon, 0);
		Assert.Equal(expectedWeaponId, SaveCache.GetClimbState().startingWeaponId);
	}

	[Fact]
	public void EnsureRunPlayer_uses_persisted_penance_only_when_creating_player()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		SaveCache.ConfigurePrimaryRunSetup("sword", "fervent_prayer", 12);
		var world = new World();

		var player = RunPlayerService.EnsureRunPlayer(world);
		var hp = player.GetComponent<HP>();
		Assert.Equal(22, hp.Max);
		Assert.Equal(22, hp.Current);

		hp.Max = 27;
		hp.Current = 13;

		Assert.Same(player, RunPlayerService.EnsureRunPlayer(world));
		Assert.Equal(27, hp.Max);
		Assert.Equal(13, hp.Current);
	}

	[Theory]
	[InlineData(0, 18)]
	[InlineData(12, 22)]
	[InlineData(24, 26)]
	public void Enemy_factory_scales_health_for_penance(int penanceLevel, int expectedEnemyHp)
	{
		var world = PrepareWorldWithLoadout(
			Enumerable.Range(0, 20).Select(_ => "smite|White").ToList(),
			penanceLevel);

		var enemyEntity = EntityFactory.CreateEnemyFromId(world, "skeleton", world.EntityManager);
		var enemy = enemyEntity.GetComponent<Enemy>();
		var hp = enemyEntity.GetComponent<HP>();

		Assert.Equal(expectedEnemyHp, enemy.MaxHealth);
		Assert.Equal(expectedEnemyHp, enemy.EnemyBase.MaxHealth);
		Assert.Equal(expectedEnemyHp, hp.Max);
	}

	[Fact]
	public void Enemy_factory_climb_time_bonus_keeps_base_eight_interval()
	{
		var world = PrepareWorldWithLoadout(
			Enumerable.Range(0, 20).Select(_ => "smite|White").ToList(),
			penanceLevel: 10,
			climbTime: 8);

		var enemy = EntityFactory.CreateEnemyFromId(world, "skeleton", world.EntityManager);

		// Penance 10 has two Mortification stacks: Round(26 * 0.80) = 21.
		// Climb time still uses the fixed eight-hour cadence: Round(21 * 1.10) = 23.
		Assert.Equal(23, enemy.GetComponent<Enemy>().MaxHealth);
		Assert.Equal(9, ClimbRuleService.GetShopRefreshInterval(SaveCache.GetClimbState()));
	}

	private static World PrepareWorldWithLoadout(
		IReadOnlyList<string> cardKeys,
		int penanceLevel,
		int climbTime = 0)
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = cardKeys.Select((cardKey, index) => new LoadoutCardEntry
		{
			entryId = $"test_card_{index}",
			cardKey = cardKey,
			isStarter = true,
			restrictions = new List<string>(),
		}).ToList();
		loadout.weaponId = "sword";
		loadout.medalIds = new List<string>();
		SaveCache.SaveLoadout(loadout);

		var climb = SaveCache.GetClimbState();
		climb.penanceLevel = penanceLevel;
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

	private static void Depart(World world, StartingWeapon weapon, int penanceLevel)
	{
		var setup = WayStationRunSetupService.GetRunSetup(world.EntityManager);
		setup.SelectedWeapon = weapon;
		setup.SelectedPenanceLevel = penanceLevel;
		WayStationRunSetupService.Depart(world);
	}
}
