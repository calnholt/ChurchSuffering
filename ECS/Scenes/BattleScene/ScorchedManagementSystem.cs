using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class ScorchedManagementSystem : Core.System
	{
		public ScorchedManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnPledgeAdded(PledgeAddedEvent evt)
		{
			if (evt.Card?.GetComponent<Scorched>() == null) return;

			var player = EntityManager.GetEntity("Player");
			if (player == null) return;

			EventManager.Publish(new ModifyHpRequestEvent
			{
				Source = player,
				Target = player,
				Delta = -1,
				DamageType = ModifyTypeEnum.Effect
			});
		}
	}
}
