using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Equipment;

namespace Crusaders30XX.ECS.Services
{
	public static class RunMapEquipmentPoolService
	{
		public static List<string> BuildEligibleEquipmentIds(
			LoadoutDefinition loadout,
			IReadOnlyList<RunMapShop> shops,
			bool excludeShopOffers,
			IReadOnlyCollection<EquipmentSlot> excludedSlots = null)
		{
			var excludedIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			CollectEquippedEquipmentIds(loadout, excludedIds);
			if (excludeShopOffers)
			{
				CollectShopEquipmentOffers(shops, excludedIds, unpurchasedOnly: true);
			}

			var excludedSlotSet = excludedSlots != null
				? new HashSet<EquipmentSlot>(excludedSlots)
				: new HashSet<EquipmentSlot>();
			if (excludeShopOffers)
			{
				foreach (var slot in CollectShopEquipmentSlots(shops))
				{
					excludedSlotSet.Add(slot);
				}
			}

			var pool = new List<string>();
			foreach (var equipmentId in EquipmentFactory.GetAllEquipment().Keys)
			{
				var equipmentKey = equipmentId.ToKey();
				if (string.IsNullOrWhiteSpace(equipmentKey)) continue;
				if (excludedIds.Contains(equipmentKey)) continue;

				var equipment = EquipmentFactory.Create(equipmentId);
				if (equipment == null) continue;
				if (excludedSlotSet.Contains(equipment.Slot)) continue;

				pool.Add(equipmentKey);
			}

			return pool;
		}

		public static HashSet<EquipmentSlot> CollectExcludedSlots(
			LoadoutDefinition loadout,
			IReadOnlyList<RunMapShop> shops,
			bool excludeShopOffers)
		{
			var slots = new HashSet<EquipmentSlot>();
			CollectEquippedEquipmentSlots(loadout, slots);
			if (excludeShopOffers)
			{
				foreach (var slot in CollectShopEquipmentSlots(shops))
				{
					slots.Add(slot);
				}
			}

			return slots;
		}

		public static string PickRandomEquipment(
			Random rng,
			LoadoutDefinition loadout,
			IReadOnlyList<RunMapShop> shops,
			bool excludeShopOffers,
			IReadOnlyCollection<EquipmentSlot> excludedSlots = null)
		{
			if (rng == null) rng = Random.Shared;
			var pool = BuildEligibleEquipmentIds(loadout, shops, excludeShopOffers, excludedSlots);
			if (pool.Count == 0)
			{
				throw new InvalidOperationException(
					"[RunMapEquipmentPoolService] No eligible equipment remains for run-map reward.");
			}

			return pool[rng.Next(pool.Count)];
		}

		public static void ApplyEquipmentToLoadout(LoadoutDefinition loadout, string equipmentId)
		{
			if (loadout == null || string.IsNullOrWhiteSpace(equipmentId)) return;

			EquipmentBase equipment = EquipmentFactory.Create(equipmentId);
			if (equipment == null) return;

			switch (equipment.Slot)
			{
				case EquipmentSlot.Chest:
					loadout.chestId = equipmentId;
					break;
				case EquipmentSlot.Legs:
					loadout.legsId = equipmentId;
					break;
				case EquipmentSlot.Arms:
					loadout.armsId = equipmentId;
					break;
				case EquipmentSlot.Head:
					loadout.headId = equipmentId;
					break;
			}
		}

		public static List<string> BuildShopOfferPool(LoadoutDefinition loadout)
		{
			return BuildEligibleEquipmentIds(loadout, shops: null, excludeShopOffers: false);
		}

		private static void CollectEquippedEquipmentIds(LoadoutDefinition loadout, HashSet<string> excluded)
		{
			if (loadout == null) return;
			AddIfPresent(excluded, loadout.headId);
			AddIfPresent(excluded, loadout.chestId);
			AddIfPresent(excluded, loadout.armsId);
			AddIfPresent(excluded, loadout.legsId);
		}

		private static void CollectEquippedEquipmentSlots(LoadoutDefinition loadout, HashSet<EquipmentSlot> slots)
		{
			if (loadout == null) return;
			TryAddSlot(slots, loadout.headId);
			TryAddSlot(slots, loadout.chestId);
			TryAddSlot(slots, loadout.armsId);
			TryAddSlot(slots, loadout.legsId);
		}

		private static void CollectShopEquipmentOffers(
			IReadOnlyList<RunMapShop> shops,
			HashSet<string> excluded,
			bool unpurchasedOnly)
		{
			if (shops == null) return;
			foreach (var shop in shops)
			{
				if (shop?.items == null) continue;
				foreach (var item in shop.items)
				{
					if (item == null || !item.IsEquipment) continue;
					if (unpurchasedOnly && item.isPurchased) continue;
					if (!string.IsNullOrWhiteSpace(item.cardId)) excluded.Add(item.cardId);
				}
			}
		}

		private static IEnumerable<EquipmentSlot> CollectShopEquipmentSlots(IReadOnlyList<RunMapShop> shops)
		{
			if (shops == null) yield break;
			foreach (var shop in shops)
			{
				if (shop?.items == null) continue;
				foreach (var item in shop.items)
				{
					if (item == null || !item.IsEquipment) continue;
					if (string.IsNullOrWhiteSpace(item.cardId)) continue;
					var equipment = EquipmentFactory.Create(item.cardId);
					if (equipment != null) yield return equipment.Slot;
				}
			}
		}

		private static void AddIfPresent(HashSet<string> excluded, string equipmentId)
		{
			if (!string.IsNullOrWhiteSpace(equipmentId)) excluded.Add(equipmentId);
		}

		private static void TryAddSlot(HashSet<EquipmentSlot> slots, string equipmentId)
		{
			if (string.IsNullOrWhiteSpace(equipmentId)) return;
			var equipment = EquipmentFactory.Create(equipmentId);
			if (equipment != null) slots.Add(equipment.Slot);
		}
	}
}
