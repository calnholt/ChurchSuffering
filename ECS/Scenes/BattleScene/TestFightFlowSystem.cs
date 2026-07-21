using System;
using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	public class TestFightFlowSystem : Core.System
	{
		private bool _restartRequested;

		public TestFightFlowSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<PlayerDied>(OnPlayerDied, priority: 100);
			EventManager.Subscribe<DeleteCachesEvent>(_ => _restartRequested = false);
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnPlayerDied(PlayerDied evt)
		{
			if (!TestFightRuntime.IsActive || _restartRequested) return;

			_restartRequested = true;
			EventQueue.Clear();
			TestFightRuntime.RecordDefeat();
			TestFightSetupService.ResetEncounterQueue(EntityManager);
			EventManager.Publish(new ShowTransition { Scene = SceneId.Battle });
		}
	}
}
