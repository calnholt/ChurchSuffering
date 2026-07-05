using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
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
