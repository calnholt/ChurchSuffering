using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Services
{
	public static class RunMapTreasureMedalPoolService
	{
		public static List<string> BuildEligibleMedalIds(EntityManager entityManager = null)
		{
			var excluded = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			EnsureLoadedLoadoutMedals(excluded);
			CollectEquippedMedalIds(entityManager, excluded);
			CollectShopMedalOffers(excluded);

			var pool = new List<string>();
			foreach (var medalId in MedalFactory.GetAllMedals().Keys)
			{
				var medalKey = medalId.ToKey();
				if (string.IsNullOrWhiteSpace(medalKey)) continue;
				if (excluded.Contains(medalKey)) continue;
				pool.Add(medalKey);
			}

			return pool;
		}

		public static string PickRandomMedal(Random rng, EntityManager entityManager = null)
		{
			if (rng == null) rng = Random.Shared;
			var pool = BuildEligibleMedalIds(entityManager);
			if (pool.Count == 0)
			{
				throw new InvalidOperationException(
					"[RunMapTreasureMedalPoolService] No eligible medals remain for Treasure Chest reward.");
			}

			return pool[rng.Next(pool.Count)];
		}

		private static void EnsureLoadedLoadoutMedals(HashSet<string> excluded)
		{
			try
			{
				var loadout = SaveCache.GetLoadout("loadout_1");
				if (loadout?.medalIds == null) return;
				foreach (var medalId in loadout.medalIds)
				{
					if (!string.IsNullOrWhiteSpace(medalId)) excluded.Add(medalId);
				}
			}
			catch { }
		}

		private static void CollectEquippedMedalIds(EntityManager entityManager, HashSet<string> excluded)
		{
			if (entityManager == null) return;

			var player = entityManager.GetEntity("Player");
			if (player == null) return;

			foreach (var entity in entityManager.GetEntitiesWithComponent<EquippedMedal>())
			{
				var equipped = entity.GetComponent<EquippedMedal>();
				if (equipped?.EquippedOwner != player || equipped.Medal == null) continue;
				if (!string.IsNullOrWhiteSpace(equipped.Medal.Id)) excluded.Add(equipped.Medal.Id);
			}
		}

		private static void CollectShopMedalOffers(HashSet<string> excluded)
		{
			foreach (var shop in SaveCache.GetRunMapShops())
			{
				if (shop?.items == null) continue;
				foreach (var item in shop.items)
				{
					if (item == null || item.isPurchased || !item.IsMedal) continue;
					if (!string.IsNullOrWhiteSpace(item.cardId)) excluded.Add(item.cardId);
				}
			}
		}
	}
}
