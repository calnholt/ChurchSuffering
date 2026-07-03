using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Dialog;
using Crusaders30XX.ECS.Data.VisualEffects;
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
		private string _pendingDefinitionId = string.Empty;
		private string _pendingSegmentId = string.Empty;
		private bool _pendingDialogueRequested;
		private Guid _pendingDamagePresentationId;
		private readonly HashSet<Guid> _pendingVisualEffectIds = new();
		private readonly HashSet<(Guid PresentationId, BattlePresentationKind Kind)> _activePresentationWaits = new();
		private readonly HashSet<(Guid PresentationId, BattlePresentationKind Kind)> _pendingPresentationWaits = new();

		public EnemyPhaseFlowSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<EnemyPhaseLethalEvent>(OnEnemyPhaseLethal);
			EventManager.Subscribe<DialogueSequenceCompleted>(OnDialogueCompleted);
			EventManager.Subscribe<VisualEffectCompleted>(OnVisualEffectCompleted);
			EventManager.Subscribe<BattlePresentationStarted>(OnBattlePresentationStarted);
			EventManager.Subscribe<BattlePresentationCompleted>(OnBattlePresentationCompleted);
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
			_pendingDamagePresentationId = evt.DamagePresentationId;
			string segmentId = _pendingFinalPhase
				? "victory"
				: $"phase_{enemyBase.CurrentPhase}_end";

			if (!HasDialogueSegment(enemyBase.Id.ToKey(), segmentId))
			{
				ContinueFlow();
				return;
			}

			_pendingRequestId = Guid.NewGuid();
			_pendingDefinitionId = enemyBase.Id.ToKey();
			_pendingSegmentId = segmentId;
			PreparePresentationBarrier(evt.Enemy, evt.DamagePresentationId);
			TryRequestPendingDialogue();
		}

		private void OnDialogueCompleted(DialogueSequenceCompleted evt)
		{
			if (evt == null
				|| !_pendingDialogueRequested
				|| evt.RequestId == Guid.Empty
				|| evt.RequestId != _pendingRequestId) return;
			ContinueFlow();
		}

		private void OnVisualEffectCompleted(VisualEffectCompleted evt)
		{
			if (evt == null || evt.RequestId == Guid.Empty || evt.IsPreview) return;
			if (_pendingVisualEffectIds.Remove(evt.RequestId))
			{
				TryRequestPendingDialogue();
			}
		}

		private void OnBattlePresentationStarted(BattlePresentationStarted evt)
		{
			if (evt == null || evt.PresentationId == Guid.Empty) return;
			var key = (evt.PresentationId, evt.Kind);
			_activePresentationWaits.Add(key);
			if (!_pendingDialogueRequested
				&& _pendingRequestId != Guid.Empty
				&& evt.PresentationId == _pendingDamagePresentationId
				&& evt.Target == _pendingEnemy)
			{
				_pendingPresentationWaits.Add(key);
			}
		}

		private void OnBattlePresentationCompleted(BattlePresentationCompleted evt)
		{
			if (evt == null || evt.PresentationId == Guid.Empty) return;
			var key = (evt.PresentationId, evt.Kind);
			_activePresentationWaits.Remove(key);
			if (_pendingPresentationWaits.Remove(key))
			{
				TryRequestPendingDialogue();
			}
		}

		private void PreparePresentationBarrier(Entity enemy, Guid damagePresentationId)
		{
			_pendingVisualEffectIds.Clear();
			_pendingPresentationWaits.Clear();

			foreach (var entity in EntityManager.GetEntitiesWithComponent<ActiveVisualEffect>())
			{
				var active = entity.GetComponent<ActiveVisualEffect>();
				if (active == null
					|| active.IsPreview
					|| active.CompletionPublished
					|| active.RequestId == Guid.Empty
					|| !InvolvesPhaseTransitionActors(active, enemy))
				{
					continue;
				}
				_pendingVisualEffectIds.Add(active.RequestId);
			}

			if (damagePresentationId == Guid.Empty) return;
			foreach (var key in _activePresentationWaits)
			{
				if (key.PresentationId == damagePresentationId)
				{
					_pendingPresentationWaits.Add(key);
				}
			}
		}

		private bool InvolvesPhaseTransitionActors(ActiveVisualEffect active, Entity enemy)
		{
			if (active == null) return false;
			if (active.Source == enemy || active.Target == enemy) return true;
			if (active.Source?.GetComponent<Player>() != null || active.Target?.GetComponent<Player>() != null)
			{
				return active.SourceKind == VisualEffectSourceKind.Card
					|| active.SourceKind == VisualEffectSourceKind.Equipment
					|| active.SourceKind == VisualEffectSourceKind.Medal
					|| active.SourceKind == VisualEffectSourceKind.EnemyAttack;
			}
			return false;
		}

		private void TryRequestPendingDialogue()
		{
			if (_pendingRequestId == Guid.Empty || _pendingDialogueRequested) return;
			if (_pendingVisualEffectIds.Count > 0 || _pendingPresentationWaits.Count > 0) return;

			_pendingDialogueRequested = true;
			EventManager.Publish(new DialogueSequenceRequested
			{
				DefinitionId = _pendingDefinitionId,
				SegmentId = _pendingSegmentId,
				RequestId = _pendingRequestId,
			});
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
			_pendingDefinitionId = string.Empty;
			_pendingSegmentId = string.Empty;
			_pendingDialogueRequested = false;
			_pendingDamagePresentationId = Guid.Empty;
			_pendingVisualEffectIds.Clear();
			_pendingPresentationWaits.Clear();
		}

		private void ClearAllPending()
		{
			ClearPending();
			if (_pendingDefeatHandle != null)
			{
				TimerScheduler.Cancel(_pendingDefeatHandle);
				_pendingDefeatHandle = null;
			}
			_activePresentationWaits.Clear();
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
