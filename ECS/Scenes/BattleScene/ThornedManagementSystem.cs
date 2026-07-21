using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class ThornedManagementSystem : Core.System
	{
		public ThornedManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<CardDiscardedForCostEvent>(OnCardDiscardedForCost);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnCardDiscardedForCost(CardDiscardedForCostEvent evt)
		{
			if (evt.Card?.GetComponent<Thorned>() == null) return;

			var player = EntityManager.GetEntity("Player");
			if (player == null) return;

			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = player,
				Type = AppliedPassiveType.Scar,
				Delta = 1
			});
		}
	}
}
