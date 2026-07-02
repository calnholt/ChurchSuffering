using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public class EquipmentManagerSystem : Core.System
	{
		public EquipmentManagerSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<EquipmentActivateEvent>(OnEquipmentActivate);
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
			if (!equipment.Equipment.CanActivateDuringActionPhase || !equipment.Equipment.HasUses)
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
			EventManager.Publish(new EquipmentAbilityTriggered { Equipment = e.EquipmentEntity, EquipmentId = equipment.Equipment.Id });
		}

	}
}
