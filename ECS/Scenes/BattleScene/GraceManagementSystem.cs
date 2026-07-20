using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// After start-of-turn draw-up, if the player has Grace stacks, resurrect 1 and consume 1 Grace.
	/// </summary>
	public class GraceManagementSystem : Core.System
	{
		public GraceManagementSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<StartOfTurnDrawResolvedEvent>(OnStartOfTurnDrawResolved);
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnStartOfTurnDrawResolved(StartOfTurnDrawResolvedEvent evt)
		{
			var player = evt?.Player ?? EntityManager.GetEntity("Player");
			if (player == null) return;

			var passives = player.GetComponent<AppliedPassives>();
			if (passives?.Passives == null) return;
			if (!passives.Passives.TryGetValue(AppliedPassiveType.Grace, out int stacks) || stacks <= 0)
				return;

			EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = 1 });
			EventManager.Publish(new ApplyPassiveEvent
			{
				Target = player,
				Type = AppliedPassiveType.Grace,
				Delta = -1
			});
			EventManager.Publish(new PassiveTriggered
			{
				Owner = player,
				Type = AppliedPassiveType.Grace
			});
		}
	}
}
