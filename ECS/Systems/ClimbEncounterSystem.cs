using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class ClimbEncounterSystem : Core.System
	{
		public ClimbEncounterSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ClimbEncounterSlotSelectedEvent>(OnEncounterSlotSelected);
		}

		private void OnEncounterSlotSelected(ClimbEncounterSlotSelectedEvent evt)
		{
			if (evt == null || string.IsNullOrWhiteSpace(evt.SlotId)) return;
			if (!IsClimbScene() || HasBlockingInputContext()) return;
			ClimbEncounterService.TryQueueEncounter(EntityManager, evt.SlotId);
		}

		private bool HasBlockingInputContext()
		{
			return EntityManager.GetEntitiesWithComponent<InputContext>()
				.Select(entity => entity.GetComponent<InputContext>())
				.Any(context => context?.IsActive == true
					&& !context.IsDiagnostic
					&& !string.Equals(context.Id, InputContextIds.Gameplay, System.StringComparison.Ordinal));
		}

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}
	}
}
