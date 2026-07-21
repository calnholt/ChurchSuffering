using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class TestFightSetupServiceTests
{
	[Fact]
	public void PrepareWorld_builds_isolated_max_penance_run_setup()
	{
		TestFightRuntime.Configure(Options());
		try
		{
			var world = BuildWorld();

			TestFightSetupService.PrepareWorld(world);

			var player = world.EntityManager.GetEntity("Player");
			Assert.Equal("hammer", player.GetComponent<EquippedWeapon>().WeaponId);
			Assert.Equal("static_surge", player.GetComponent<EquippedTemperanceAbility>().AbilityId);
			Assert.Equal(20, player.GetComponent<HP>().Max);

			var deck = world.EntityManager.GetEntity("Deck").GetComponent<Deck>();
			Assert.Empty(deck.Cards);
			Assert.Same(world.EntityManager.GetEntity("Deck"), player.GetComponent<Player>().DeckEntity);

			var queued = world.EntityManager.GetEntity("QueuedEvents").GetComponent<QueuedEvents>();
			Assert.Single(queued.Events);
			Assert.Equal("skeleton", queued.Events[0].EventId);
		}
		finally
		{
			TestFightRuntime.Reset();
		}
	}

	[Fact]
	public void RegenerateDeck_replaces_all_cards_and_uses_a_new_seed()
	{
		int seed = 100;
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.UnlockAllCollectionItems();
		TestFightRuntime.Configure(Options());
		TestFightRuntime.SetDeckSeedProviderForTests(() => ++seed);
		EventManager.Clear();

		try
		{
			var world = BuildWorld();
			TestFightSetupService.PrepareWorld(world);
			_ = new AppliedPassivesManagementSystem(world.EntityManager);
			_ = new HpManagementSystem(world.EntityManager);
			var player = world.EntityManager.GetEntity("Player");
			player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Scar] = 3;
			player.GetComponent<HP>().Max = 17;
			player.GetComponent<HP>().Current = 4;

			TestFightSetupService.RegenerateDeck(world.EntityManager);
			var deck = world.EntityManager.GetEntity("Deck").GetComponent<Deck>();
			var firstIds = deck.Cards.Select(card => card.Id).ToHashSet();
			Assert.Equal(20, deck.Cards.Count);
			Assert.Equal(101, TestFightRuntime.LastDeckSeed);

			TestFightSetupService.RegenerateDeck(world.EntityManager);
			var secondIds = deck.Cards.Select(card => card.Id).ToHashSet();
			Assert.Equal(20, deck.Cards.Count);
			Assert.Equal(102, TestFightRuntime.LastDeckSeed);
			Assert.Empty(firstIds.Intersect(secondIds));
			Assert.All(deck.Cards, card => Assert.NotNull(card.GetComponent<RunDeckCard>()));
			Assert.Equal(12, deck.Cards.Count(card => card.GetComponent<CardData>().Card.IsStarter));
			Assert.Equal(8, deck.Cards.Count(HasReparationRestriction));
			Assert.Equal(
				deck.Cards.Count,
				deck.Cards.Select(card => card.GetComponent<RunDeckCard>().EntryId).Distinct().Count());

			var expectedLoadout = StartingDeckGeneratorService.BuildStartingLoadout("hammer", 102, "test_fight");
			ReparationService.Apply(
				expectedLoadout,
				"hammer",
				102,
				24,
				SaveCache.GetCollection(),
				8);
			var actualKeys = deck.Cards
				.Select(card => card.GetComponent<RunDeckCard>().CardKey)
				.OrderBy(key => key)
				.ToList();
			var expectedKeys = expectedLoadout.cards.Select(entry => entry.cardKey).OrderBy(key => key).ToList();
			Assert.Equal(expectedKeys, actualKeys);
			Assert.Empty(player.GetComponent<AppliedPassives>().Passives);
			Assert.Equal(20, player.GetComponent<HP>().Max);
			Assert.Equal(20, player.GetComponent<HP>().Current);
		}
		finally
		{
			EventManager.Clear();
			TestFightRuntime.Reset();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void RegenerateDeck_clears_accumulated_temperance()
	{
		EventManager.Clear();
		TestFightRuntime.Configure(Options());
		try
		{
			var world = BuildWorld();
			TestFightSetupService.PrepareWorld(world);
			_ = new TemperanceManagerSystem(world.EntityManager);
			var player = world.EntityManager.GetEntity("Player");
			player.GetComponent<Temperance>().Amount = 3;

			TestFightSetupService.RegenerateDeck(world.EntityManager);

			Assert.Equal(0, player.GetComponent<Temperance>().Amount);
		}
		finally
		{
			EventManager.Clear();
			TestFightRuntime.Reset();
		}
	}

	[Fact]
	public void ApplyEnemyHpDelta_updates_all_enemy_health_models()
	{
		TestFightRuntime.Configure(Options());
		try
		{
			var world = BuildWorld();
			var definition = new Crusaders30XX.ECS.Objects.EnemyAttacks.Skeleton
			{
				MaxHealth = 22,
				CurrentHealth = 22,
			};
			var enemyEntity = world.CreateEntity("Enemy");
			var enemy = new Enemy
			{
				MaxHealth = 22,
				CurrentHealth = 22,
				EnemyBase = definition,
			};
			var hp = new HP { Max = 22, Current = 22 };
			world.AddComponent(enemyEntity, enemy);
			world.AddComponent(enemyEntity, hp);

			TestFightSetupService.ApplyEnemyHpDelta(enemyEntity);
			TestFightRuntime.RecordVictory();
			TestFightSetupService.ApplyEnemyHpDelta(enemyEntity);

			Assert.Equal(23, hp.Max);
			Assert.Equal(23, hp.Current);
			Assert.Equal(23, enemy.MaxHealth);
			Assert.Equal(23, definition.MaxHealth);
		}
		finally
		{
			TestFightRuntime.Reset();
		}
	}

	[Theory]
	[InlineData(24, 2, "Max HP: 24, Delta: +2")]
	[InlineData(18, -3, "Max HP: 18, Delta: -3")]
	public void Hp_display_uses_signed_ascii_format(int maxHp, int delta, string expected)
	{
		Assert.Equal(expected, TestFightHpDisplaySystem.BuildText(maxHp, delta));
	}

	private static TestFightLaunchOptions Options()
	{
		return new TestFightLaunchOptions
		{
			WeaponId = "hammer",
			EnemyId = "skeleton",
			PenanceLevel = 24,
		};
	}

	private static bool HasReparationRestriction(Entity card)
	{
		return card.HasComponent<Thorned>()
			|| card.HasComponent<Scorched>()
			|| card.HasComponent<Cursed>()
			|| card.HasComponent<Frozen>()
			|| card.HasComponent<Brittle>();
	}

	private static World BuildWorld()
	{
		var world = new World();
		var scene = world.CreateEntity("SceneState");
		world.AddComponent(scene, new SceneState { Current = SceneId.TitleMenu });
		return world;
	}
}
