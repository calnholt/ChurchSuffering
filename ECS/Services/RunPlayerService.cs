using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Services
{
	public static class RunPlayerService
	{
		public static Entity EnsureRunPlayer(World world)
		{
			var entityManager = world.EntityManager;
			var existing = entityManager.GetEntity("Player");
			if (existing != null && existing.IsActive && existing.HasComponent<Player>() && existing.HasComponent<DontDestroyOnLoad>())
			{
				RunScopedStateService.HydrateRunLongPassivesOntoPlayer(existing);
				return existing;
			}

			var player = EntityFactory.CreatePlayer(world);
			WayStationRunSetupService.ApplyPersistedPlayerHp(player);
			RunScopedStateService.HydrateRunLongPassivesOntoPlayer(player);
			return player;
		}

		public static void DestroyRunPlayer(EntityManager entityManager)
		{
			if (entityManager == null) return;
			var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null) return;

			var battleState = player.GetComponent<BattleStateInfo>();
			battleState?.RunTracking?.Clear();

			foreach (var e in entityManager.GetEntitiesWithComponent<EquippedEquipment>().ToList())
			{
				if (e.GetComponent<EquippedEquipment>()?.EquippedOwner == player)
				{
					entityManager.DestroyEntity(e.Id);
				}
			}

			foreach (var e in entityManager.GetEntitiesWithComponent<EquippedMedal>().ToList())
			{
				if (e.GetComponent<EquippedMedal>()?.EquippedOwner == player)
				{
					entityManager.DestroyEntity(e.Id);
				}
			}

			foreach (var e in entityManager.GetEntitiesWithComponent<Tribulation>().ToList())
			{
				if (e.GetComponent<Tribulation>()?.PlayerOwner == player)
				{
					entityManager.DestroyEntity(e.Id);
				}
			}

			string[] tooltipNames =
			{
				"UI_WeaponTooltip"
			};
			foreach (var name in tooltipNames)
			{
				entityManager.DestroyEntity(name);
			}

			entityManager.DestroyEntity(player.Id);
		}
	}
}
