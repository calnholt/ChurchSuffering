using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Tutorials;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public sealed class GuidedTutorialDirectorSystem : Core.System
	{
		private bool _restartRequested;

		public GuidedTutorialDirectorSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChanged);
			EventManager.Subscribe<GuidedTutorialRestartRequested>(OnRestartRequested);
			EventManager.Subscribe<GuidedTutorialSkipRequested>(OnSkipRequested);
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();
		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnPhaseChanged(ChangeBattlePhaseEvent evt)
		{
			var state = GuidedTutorialService.GetState(EntityManager);
			if (state == null) return;

			if (evt.Current == SubPhase.EnemyStart)
			{
				var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
				int turn = phase?.TurnNumber ?? state.TurnWithinSection;
				if (phase?.Sub is SubPhase.PlayerEnd or SubPhase.Action or SubPhase.PlayerStart)
					turn++;

				int maxTurns = GuidedTutorialDefinitions.GetTurnCount(state.Section);
				if (turn <= maxTurns)
					GuidedTutorialService.BeginNextTurn(EntityManager, turn);
			}
			else if (evt.Current == SubPhase.EnemyAttack)
			{
				foreach (var card in EntityManager.GetEntitiesWithComponent<AssignedBlockCard>())
				{
					string id = card.GetComponent<CardData>()?.Card?.CardId;
					if (!string.IsNullOrEmpty(id))
						state.BlockedCardIdsThisTurn.Add(id);
				}
				state.ConfirmedAttackCountThisTurn++;
			}
		}

		private void OnSkipRequested(GuidedTutorialSkipRequested evt)
		{
			if (GuidedTutorialService.IsActive(EntityManager))
				GuidedTutorialService.Complete(EntityManager);
		}

		private void OnRestartRequested(GuidedTutorialRestartRequested evt)
		{
			var state = GuidedTutorialService.GetState(EntityManager);
			if (state == null || _restartRequested) return;

			_restartRequested = true;
			EventQueue.Clear();
			TimerScheduler.Clear();
			EventManager.Publish(new DeleteCachesEvent { Scene = SceneId.Battle });
			ResetPhaseState();
			ClearEnemyAttackState();
			BattleTransientStateCleanupService.ClearInteractionState(EntityManager);
			GuidedTutorialService.RestartSection(EntityManager);
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt.Scene == SceneId.Battle)
			{
				_restartRequested = false;
			}
		}

		private void ResetPhaseState()
		{
			var phase = EntityManager.GetEntitiesWithComponent<PhaseState>()
				.FirstOrDefault()?.GetComponent<PhaseState>();
			if (phase == null) return;

			phase.Main = MainPhase.StartBattle;
			phase.Sub = SubPhase.StartBattle;
			phase.TurnNumber = 1;
			phase.DefeatPresentationActive = false;
			phase.PendingBlockConfirm = false;
		}

		private void ClearEnemyAttackState()
		{
			foreach (var progress in EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>().ToList())
			{
				EntityManager.DestroyEntity(progress.Id);
			}

			foreach (var entity in EntityManager.GetEntitiesWithComponent<AttackIntent>())
			{
				entity.GetComponent<AttackIntent>()?.Planned.Clear();
				entity.GetComponent<NextTurnAttackIntent>()?.Planned.Clear();
			}
		}
	}
}
