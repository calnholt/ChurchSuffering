using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class EnemyFactoryTests
{
	[Fact]
	public void Melee_skeleton_variants_share_base_stats_and_throughout_pool()
	{
		EnemyId[] meleeSkeletons =
		{
			EnemyId.Skeleton,
			EnemyId.FireSkeleton,
			EnemyId.EarthSkeleton,
			EnemyId.FrostSkeleton,
			EnemyId.CursedSkeleton,
		};

		foreach (EnemyId id in meleeSkeletons)
		{
			var enemy = EnemyFactory.Create(id);
			Assert.NotNull(enemy);
			Assert.Equal(26, enemy.HP);
			Assert.Equal(ClimbEncounterPool.Throughout, enemy.ClimbPool);
			Assert.True(EnemyPortraitContent.HasPortrait(id));
		}

		Assert.Equal(ClimbEncounterPool.Early, EnemyFactory.Create(EnemyId.SkeletalArcher).ClimbPool);
	}

	[Fact]
	public void Fallen_shepherd_is_registered_as_a_boss()
	{
		var enemy = EnemyFactory.Create(EnemyId.FallenShepherd);
		var allEnemies = EnemyFactory.GetAllEnemies();

		var shepherd = Assert.IsType<FallenShepherd>(enemy);
		Assert.True(shepherd.IsBoss);
		Assert.IsType<FallenShepherd>(allEnemies[EnemyId.FallenShepherd]);
	}

	[Fact]
	public void Climb_encounter_pool_contains_only_registered_non_boss_enemies()
	{
		var pool = EnemyPortraitContent.GetClimbEncounterEnemyPool();

		Assert.NotEmpty(pool);
		Assert.DoesNotContain("fallen_shepherd", pool);
		Assert.DoesNotContain("horde", pool);
		Assert.DoesNotContain("sand_corpse", pool);
		Assert.DoesNotContain("training_demon", pool);
		Assert.False(EnemyPortraitContent.HasPortrait("training_demon"));
		Assert.True(EnemyFactory.IsRegistered("training_demon"));
		Assert.True(EnemyFactory.Create("horde").IsTutorialOnly);
		Assert.True(EnemyFactory.Create("sand_corpse").IsTutorialOnly);
		foreach (string enemyId in pool)
		{
			var enemy = EnemyFactory.Create(enemyId);
			Assert.NotNull(enemy);
			Assert.False(enemy.IsBoss, $"{enemyId} is marked as a boss");
			Assert.NotEqual(ClimbEncounterPool.None, enemy.ClimbPool);
			Assert.True(EnemyPortraitContent.HasPortrait(enemyId));
		}
	}

	[Fact]
	public void Fallen_shepherd_spawns_with_phase_one_arsenal()
	{
		var world = CreateWorldWithDeck();

		var enemyEntity = EntityFactory.CreateEnemyFromId(
			world,
			"fallen_shepherd",
			world.EntityManager);

		var enemy = enemyEntity.GetComponent<Enemy>();
		var arsenal = enemyEntity.GetComponent<EnemyArsenal>();
		Assert.Equal(EnemyId.FallenShepherd, enemy.Id);
		Assert.IsType<FallenShepherd>(enemy.EnemyBase);
		Assert.Equal(new[] { EnemyAttackId.FallenShepherdPhase1 }, arsenal.AttackIds);
	}

	[Fact]
	public void Unknown_enemy_id_throws_a_descriptive_exception()
	{
		var world = new World();

		var exception = Assert.Throws<InvalidOperationException>(() =>
			EntityFactory.CreateEnemyFromId(world, "missing_enemy", world.EntityManager));

		Assert.Contains("missing_enemy", exception.Message, StringComparison.Ordinal);
	}

	private static World CreateWorldWithDeck()
	{
		var world = new World();
		var deckEntity = world.CreateEntity("Deck");
		var deck = new Deck();
		world.AddComponent(deckEntity, deck);
		for (int i = 0; i < 20; i++)
		{
			deck.Cards.Add(world.CreateEntity($"DeckCard_{i}"));
		}
		return world;
	}
}
