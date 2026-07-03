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
	public void Fallen_shepherd_is_registered_as_a_boss()
	{
		var enemy = EnemyFactory.Create(EnemyId.FallenShepherd);
		var allEnemies = EnemyFactory.GetAllEnemies();

		var shepherd = Assert.IsType<FallenShepherd>(enemy);
		Assert.True(shepherd.IsBoss);
		Assert.IsType<FallenShepherd>(allEnemies[EnemyId.FallenShepherd]);
	}

	[Fact]
	public void Normal_run_map_pool_contains_only_registered_non_boss_enemies()
	{
		var pool = EnemyPortraitContent.GetRunMapEnemyPool();

		Assert.NotEmpty(pool);
		Assert.DoesNotContain("fallen_shepherd", pool);
		Assert.DoesNotContain("gleeber", pool);
		Assert.DoesNotContain("sand_corpse", pool);
		Assert.DoesNotContain("training_demon", pool);
		Assert.False(EnemyPortraitContent.HasPortrait("training_demon"));
		Assert.True(EnemyFactory.IsRegistered("training_demon"));
		Assert.True(EnemyFactory.Create("gleeber").IsTutorialOnly);
		Assert.True(EnemyFactory.Create("sand_corpse").IsTutorialOnly);
		foreach (string enemyId in pool)
		{
			var enemy = EnemyFactory.Create(enemyId);
			Assert.NotNull(enemy);
			Assert.False(enemy.IsBoss, $"{enemyId} is marked as a boss");
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
