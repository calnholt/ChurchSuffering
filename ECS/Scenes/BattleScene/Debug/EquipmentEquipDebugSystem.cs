using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Equipment Equip")]
	public sealed class EquipmentEquipDebugSystem : Core.System
	{
		public EquipmentEquipDebugSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		[DebugActionList("Equip Equipment")]
		public IEnumerable<DebugNamedAction> EquipEquipment()
		{
			return EquipmentFactory.GetAllEquipment()
				.OrderBy(pair => pair.Value.Name)
				.Select(pair => CreateEquipAction(pair.Key, pair.Value.Name));
		}

		private DebugNamedAction CreateEquipAction(EquipmentId equipmentId, string displayName)
		{
			string key = equipmentId.ToKey();
			return new DebugNamedAction
			{
				Label = $"{displayName} [{key}]",
				IsEnabled = !RunEquipmentService.IsEquippedOnPlayer(EntityManager, key),
				Invoke = () => RunEquipmentService.AcquireAndEquip(EntityManager, key),
			};
		}
	}
}
