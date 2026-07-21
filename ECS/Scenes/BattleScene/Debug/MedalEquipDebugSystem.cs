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
	[DebugTab("Medal Equip")]
	public sealed class MedalEquipDebugSystem : Core.System
	{
		public MedalEquipDebugSystem(EntityManager entityManager) : base(entityManager) { }

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		[DebugActionList("Equip Medal")]
		public IEnumerable<DebugNamedAction> EquipMedals()
		{
			return MedalFactory.GetAllMedals()
				.OrderBy(pair => pair.Value.Name)
				.Select(pair => CreateEquipAction(pair.Key, pair.Value.Name));
		}

		private DebugNamedAction CreateEquipAction(MedalId medalId, string displayName)
		{
			string key = medalId.ToKey();
			return new DebugNamedAction
			{
				Label = $"{displayName} [{key}]",
				IsEnabled = !RunMedalService.IsEquippedOnPlayer(EntityManager, key),
				Invoke = () => RunMedalService.AcquireAndEquipPersisted(EntityManager, key),
			};
		}
	}
}
