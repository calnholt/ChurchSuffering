using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class BrittleManagementSystem : Core.System
	{
		public BrittleManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnCardBlocked(CardBlockedEvent evt)
		{
			if (evt.Card?.GetComponent<Brittle>() == null) return;

			if (!EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var progress)) return;
			if (progress.PlayedCards != 1) return;

			EventManager.Publish(new MillCardEvent());
		}
	}
}
