using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Equipment;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Services
{
	public static class RunEquipmentService
	{
		public static bool IsEquippedOnPlayer(EntityManager entityManager, string equipmentId)
		{
			if (entityManager == null || string.IsNullOrWhiteSpace(equipmentId)) return false;

			return entityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Any(entity => string.Equals(
					entity.GetComponent<EquippedEquipment>()?.Equipment?.Id,
					equipmentId,
					StringComparison.OrdinalIgnoreCase));
		}

		public static Entity EquipOnPlayer(EntityManager entityManager, string equipmentId)
		{
			if (entityManager == null || string.IsNullOrWhiteSpace(equipmentId)) return null;

			var equipment = EquipmentFactory.Create(equipmentId);
			if (equipment == null) return null;

			var player = entityManager.GetEntity("Player");
			if (player == null || !player.IsActive) return null;

			DestroyEquippedInSlot(entityManager, player, equipment.Slot);

			var equipmentEntity = entityManager.CreateEntity($"Equip_{equipment.Slot}_{Guid.NewGuid():N}");
			equipment.Initialize(entityManager, equipmentEntity);
			entityManager.AddComponent(equipmentEntity, new Transform { Position = Vector2.Zero, ZOrder = 10001 });
			entityManager.AddComponent(equipmentEntity, new UIElement { IsInteractable = true });
			entityManager.AddComponent(equipmentEntity, new EquippedEquipment
			{
				EquippedOwner = player,
				Equipment = equipment,
			});
			entityManager.AddComponent(equipmentEntity, ParallaxLayer.GetUIParallaxLayer());
			if (!equipmentEntity.HasComponent<DontDestroyOnLoad>())
			{
				entityManager.AddComponent(equipmentEntity, new DontDestroyOnLoad());
			}

			return equipmentEntity;
		}

		public static Entity AcquireAndEquip(EntityManager entityManager, string equipmentId)
		{
			if (string.IsNullOrWhiteSpace(equipmentId)) return null;

			var loadout = SaveCache.GetLoadout("loadout_1") ?? new LoadoutDefinition { id = "loadout_1" };
			ApplyEquipmentToLoadout(loadout, equipmentId);
			SaveCache.SaveLoadout(loadout);
			return EquipOnPlayer(entityManager, equipmentId);
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

		private static void DestroyEquippedInSlot(EntityManager entityManager, Entity player, EquipmentSlot slot)
		{
			foreach (var entity in entityManager.GetEntitiesWithComponent<EquippedEquipment>().ToList())
			{
				var equipped = entity.GetComponent<EquippedEquipment>();
				if (equipped?.EquippedOwner != player || equipped.Equipment == null) continue;
				if (equipped.Equipment.Slot != slot) continue;
				entityManager.DestroyEntity(entity.Id);
			}
		}
	}
}
