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
