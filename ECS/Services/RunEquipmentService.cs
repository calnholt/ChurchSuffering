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
			ApplySavedRemainingUses(equipment, SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId));
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

			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId)
				?? new LoadoutDefinition { id = RunDeckService.PrimaryLoadoutId };
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
					loadout.chestRemainingUses = equipment.MaxUses;
					break;
				case EquipmentSlot.Legs:
					loadout.legsId = equipmentId;
					loadout.legsRemainingUses = equipment.MaxUses;
					break;
				case EquipmentSlot.Arms:
					loadout.armsId = equipmentId;
					loadout.armsRemainingUses = equipment.MaxUses;
					break;
				case EquipmentSlot.Head:
					loadout.headId = equipmentId;
					loadout.headRemainingUses = equipment.MaxUses;
					break;
			}
		}

		public static void PersistRemainingUses(EquipmentBase equipment)
		{
			if (equipment == null) return;

			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			if (loadout == null) return;

			if (!SetSlotRemainingUses(loadout, equipment.Slot, equipment.RemainingUses)) return;
			SaveCache.SaveLoadout(loadout);
		}

		public static void ApplySavedRemainingUses(EquipmentBase equipment, LoadoutDefinition loadout)
		{
			if (equipment == null || loadout == null) return;
			int? saved = GetSlotRemainingUses(loadout, equipment.Slot);
			if (!saved.HasValue) return;
			equipment.SetRemainingUses(saved.Value);
		}

		public static int? GetSlotRemainingUses(LoadoutDefinition loadout, EquipmentSlot slot)
		{
			if (loadout == null) return null;
			return slot switch
			{
				EquipmentSlot.Chest => loadout.chestRemainingUses,
				EquipmentSlot.Legs => loadout.legsRemainingUses,
				EquipmentSlot.Arms => loadout.armsRemainingUses,
				EquipmentSlot.Head => loadout.headRemainingUses,
				_ => null,
			};
		}

		public static bool SetSlotRemainingUses(LoadoutDefinition loadout, EquipmentSlot slot, int remainingUses)
		{
			if (loadout == null) return false;
			switch (slot)
			{
				case EquipmentSlot.Chest:
					loadout.chestRemainingUses = remainingUses;
					return true;
				case EquipmentSlot.Legs:
					loadout.legsRemainingUses = remainingUses;
					return true;
				case EquipmentSlot.Arms:
					loadout.armsRemainingUses = remainingUses;
					return true;
				case EquipmentSlot.Head:
					loadout.headRemainingUses = remainingUses;
					return true;
				default:
					return false;
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
