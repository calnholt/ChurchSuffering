using System;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Services
{
	public static class TestFightSetupService
	{
		public static void PrepareWorld(World world)
		{
			if (world == null || !TestFightRuntime.IsActive) return;

			ApplyRunDifficulty(TestFightRuntime.Options.Difficulty);

			var deckEntity = world.EntityManager.GetEntity("Deck");
			if (deckEntity == null)
			{
				deckEntity = world.CreateEntity("Deck");
				world.AddComponent(deckEntity, new Deck());
				world.AddComponent(deckEntity, new DontDestroyOnLoad());
			}

			var player = world.EntityManager.GetEntity("Player");
			if (player == null)
			{
				player = EntityFactory.CreatePlayer(world, loadoutOverride: BuildLoadout());
			}

			player.GetComponent<Player>().DeckEntity = deckEntity;
			var hp = player.GetComponent<HP>();
			if (hp != null)
			{
				hp.Max = WayStationRunSetupSingleton.PlayerMaxHp;
				hp.UnscarredMax = hp.Max;
				hp.Current = hp.Max;
			}

			var queuedEntity = world.EntityManager.GetEntity("QueuedEvents");
			if (queuedEntity == null)
			{
				queuedEntity = world.CreateEntity("QueuedEvents");
				world.AddComponent(queuedEntity, new DontDestroyOnLoad());
				world.AddComponent(queuedEntity, new QueuedEvents());
			}

			var queued = queuedEntity.GetComponent<QueuedEvents>();
			queued.Events.Clear();
			queued.Events.Add(new QueuedEvent
			{
				EventId = TestFightRuntime.Options.EnemyId,
				EventType = QueuedEventType.Enemy,
			});
			queued.CurrentIndex = -1;
			queued.LocationId = string.Empty;
			queued.QuestIndex = 0;
		}

		public static void RegenerateDeck(EntityManager entityManager)
		{
			if (entityManager == null || !TestFightRuntime.IsActive) return;

			var deckEntity = entityManager.GetEntity("Deck");
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck == null)
			{
				throw new InvalidOperationException("Cannot start test fight: test deck is missing.");
			}

			ResetPlayerState(entityManager);

			int seed = TestFightRuntime.BeginBattle();
			var loadout = StartingDeckGeneratorService.BuildStartingLoadout(
				TestFightRuntime.Options.WeaponId,
				seed,
				"test_fight");
			RunDeckService.ReplaceDeckFromLoadout(entityManager, loadout);
		}

		public static void ApplyEnemyHpDelta(Entity enemyEntity)
		{
			if (!TestFightRuntime.IsActive || enemyEntity == null) return;

			var enemy = enemyEntity.GetComponent<Enemy>();
			var hp = enemyEntity.GetComponent<HP>();
			var definition = enemy?.EnemyBase;
			if (enemy == null || hp == null || definition == null) return;

			int baselineMax = hp.Max;
			int adjustedMax = TestFightRuntime.ApplyHpDelta(baselineMax);
			int adjustedCurrent = Math.Clamp(hp.Current + TestFightRuntime.HpDelta, 1, adjustedMax);

			hp.Max = adjustedMax;
			hp.Current = adjustedCurrent;
			enemy.MaxHealth = adjustedMax;
			enemy.CurrentHealth = adjustedCurrent;
			definition.MaxHealth = adjustedMax;
			definition.CurrentHealth = adjustedCurrent;
		}

		public static void ResetEncounterQueue(EntityManager entityManager)
		{
			var queued = entityManager?.GetEntity("QueuedEvents")?.GetComponent<QueuedEvents>();
			if (queued != null)
			{
				queued.CurrentIndex = -1;
			}
		}

		private static void ResetPlayerState(EntityManager entityManager)
		{
			var player = entityManager.GetEntity("Player");
			if (player == null) return;

			var passives = player.GetComponent<AppliedPassives>()?.Passives;
			if (passives != null)
			{
				foreach (var passive in passives.Keys.ToList())
				{
					EventManager.Publish(new RemovePassive
					{
						Owner = player,
						Type = passive,
					});
				}
			}

			var battleState = player.GetComponent<BattleStateInfo>();
			battleState?.BattleTracking?.Clear();
			battleState?.TurnTracking?.Clear();
			battleState?.PhaseTracking?.Clear();

			EventManager.Publish(new SetTemperanceEvent { Amount = 0 });

			var hp = player.GetComponent<HP>();
			if (hp != null)
			{
				hp.Max = WayStationRunSetupSingleton.PlayerMaxHp;
				hp.UnscarredMax = hp.Max;
				hp.Current = hp.Max;
			}
		}

		private static LoadoutDefinition BuildLoadout()
		{
			return StartingDeckGeneratorService.BuildStartingLoadout(
				TestFightRuntime.Options.WeaponId,
				seed: 0,
				loadoutId: "test_fight");
		}

		private static void ApplyRunDifficulty(RunDifficulty difficulty)
		{
			WayStationRunSetupSingleton.SelectedDifficulty = difficulty;
			WayStationRunSetupSingleton.SelectedWeapon = TestFightRuntime.Options.WeaponId switch
			{
				"dagger" => StartingWeapon.Dagger,
				"hammer" => StartingWeapon.Hammer,
				_ => StartingWeapon.Sword,
			};
		}
	}
}
