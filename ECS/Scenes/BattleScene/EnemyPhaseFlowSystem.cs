using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Dialog;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.Diagnostics;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	public class EnemyPhaseFlowSystem : Core.System
	{
		private Guid _pendingRequestId;
		private Entity _pendingEnemy;
		private bool _pendingFinalPhase;
		private object _pendingDefeatHandle;

		public EnemyPhaseFlowSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<EnemyPhaseLethalEvent>(OnEnemyPhaseLethal);
			EventManager.Subscribe<DialogueSequenceCompleted>(OnDialogueCompleted);
			EventManager.Subscribe<DeleteCachesEvent>(_ => ClearAllPending());
			EventManager.Subscribe<StartBattleRequested>(_ => ClearAllPending());
		}

		protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnEnemyPhaseLethal(EnemyPhaseLethalEvent evt)
		{
			var enemyBase = evt?.Enemy?.GetComponent<Enemy>()?.EnemyBase;
			if (enemyBase == null
				|| enemyBase.Phases <= 1
				|| _pendingRequestId != Guid.Empty
				|| _pendingDefeatHandle != null)
			{
				return;
			}

			EventQueue.Clear();
			SetBattleInputFrozen(true);
			_pendingEnemy = evt.Enemy;
			_pendingFinalPhase = enemyBase.CurrentPhase >= enemyBase.Phases;
			string segmentId = _pendingFinalPhase
				? "victory"
				: $"phase_{enemyBase.CurrentPhase}_end";

			if (!HasDialogueSegment(enemyBase.Id.ToKey(), segmentId))
			{
				ContinueFlow();
				return;
			}

			_pendingRequestId = Guid.NewGuid();
			EventManager.Publish(new DialogueSequenceRequested
			{
				DefinitionId = enemyBase.Id.ToKey(),
				SegmentId = segmentId,
				RequestId = _pendingRequestId,
			});
		}

		private void OnDialogueCompleted(DialogueSequenceCompleted evt)
		{
			if (evt == null || evt.RequestId == Guid.Empty || evt.RequestId != _pendingRequestId) return;
			ContinueFlow();
		}

		private void ContinueFlow()
		{
			var enemy = _pendingEnemy;
			bool finalPhase = _pendingFinalPhase;
			ClearPending();

			if (enemy == null) return;
			if (finalPhase)
			{
				_pendingDefeatHandle = TimerScheduler.Schedule(0.1f, () =>
				{
					_pendingDefeatHandle = null;
					EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });
				});
				return;
			}

			if (!EnemyPhaseResetService.TryResetForNextPhase(EntityManager, enemy))
			{
				SetBattleInputFrozen(false);
				return;
			}
			SetBattleInputFrozen(false);
			EventManager.Publish(new EnemyPhaseResetEvent
			{
				Enemy = enemy,
				CurrentPhase = enemy.GetComponent<Enemy>()?.EnemyBase?.CurrentPhase ?? 1,
			});

			EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
				"Rule.ChangePhase.EnemyStart",
				new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart }));
			EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
				"Rule.ChangePhase.PreBlock",
				new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock }));
			EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
				"Rule.ChangePhase.Block",
				new ChangeBattlePhaseEvent { Current = SubPhase.Block }));
		}

		private static bool HasDialogueSegment(string definitionId, string segmentId)
		{
			if (TestFightRuntime.IsActive) return false;
			return DialogDefinitionCache.TryGet(definitionId, out var definition)
				&& definition?.ResolveSegment(segmentId)?.Count > 0;
		}

		private void ClearPending()
		{
			_pendingRequestId = Guid.Empty;
			_pendingEnemy = null;
			_pendingFinalPhase = false;
		}

		private void ClearAllPending()
		{
			ClearPending();
			if (_pendingDefeatHandle != null)
			{
				TimerScheduler.Cancel(_pendingDefeatHandle);
				_pendingDefeatHandle = null;
			}
			SetBattleInputFrozen(false);
		}

		private void SetBattleInputFrozen(bool frozen)
		{
			var phase = EntityManager.GetEntitiesWithComponent<PhaseState>()
				.FirstOrDefault()?.GetComponent<PhaseState>();
			if (phase != null) phase.DefeatPresentationActive = frozen;
		}
	}
}
