using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class EquipmentManagerSystem : Core.System
	{
		public EquipmentManagerSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<EquipmentActivateEvent>(OnEquipmentActivate);
			EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
			EventManager.Subscribe<EquipmentRemainingUsesChanged>(OnRemainingUsesChanged);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnEquipmentActivate(EquipmentActivateEvent e)
		{
			if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
			if (e.EquipmentEntity == null) return;
			LoggingService.Append("EquipmentManagerSystem.OnEquipmentActivate", new System.Text.Json.Nodes.JsonObject
			{
				["entityId"] = e.EquipmentEntity?.Id ?? -1
			});
			var equipment = e.EquipmentEntity.GetComponent<EquippedEquipment>();
			if (equipment == null) return;
			if (!equipment.Equipment.CanActivateDuringActionPhase || !equipment.Equipment.IsAvailable)
			{
				equipment.Equipment.CantActivateMessage();
				return;
			}
			if (!equipment.Equipment.CanActivate())
			{
				equipment.Equipment.CantActivateMessage();
				return;
			}
			if (equipment.Equipment.ActivationEffectRecipe != null)
			{
				EventQueue.EnqueueTrigger(new QueuedActivateEquipmentWithVisual(EntityManager, e.EquipmentEntity));
				return;
			}
			equipment.Equipment.OnActivate(EntityManager, e.EquipmentEntity);
			equipment.Equipment.MarkUsed();
			EventManager.Publish(new EquipmentAbilityTriggered { Equipment = e.EquipmentEntity, EquipmentId = equipment.Equipment.Id });
		}

		private void OnEnemyKilled(EnemyKilledEvent evt)
		{
			foreach (var entity in EntityManager.GetEntitiesWithComponent<EquippedEquipment>())
			{
				entity.GetComponent<EquippedEquipment>()?.Equipment?.RefreshForBattle();
			}
		}

		private void OnRemainingUsesChanged(EquipmentRemainingUsesChanged evt)
		{
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			if (loadout == null) return;
			if (!RunEquipmentService.SetSlotRemainingUses(loadout, evt.Slot, evt.RemainingUses)) return;
			SaveCache.SaveLoadout(loadout);
		}

	}
}
