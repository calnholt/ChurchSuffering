using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// After an enemy attack completes, remove the resolved planned attack and
	/// transition to Block if another planned attack remains, otherwise to Action.
	/// </summary>
	public class QueuedAdvanceToNextPlannedAttackEvent : EventQueue.IQueuedEvent
	{
		public string Name { get; }
		public object Payload { get; }
		public EventQueue.EventState State { get; set; } = EventQueue.EventState.Pending;

		private readonly EntityManager _entityManager;

		public QueuedAdvanceToNextPlannedAttackEvent(EntityManager entityManager)
		{
			_entityManager = entityManager;
			Name = "Rule.AdvanceToNextPlannedAttackIfAny";
			Payload = null;
		}

		public void StartResolving()
		{
			var enemy = _entityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			var intent = enemy?.GetComponent<AttackIntent>();
			if (intent != null && intent.Planned.Count > 0)
			{
				intent.Planned.RemoveAt(0);
			}

			var phase = _entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
			if (phase != null) phase.PendingBlockConfirm = false;

			BattleTransientStateCleanupService.ClearInteractionState(_entityManager);

			var hasNext = intent != null && intent.Planned != null && intent.Planned.Count > 0;
			if (hasNext)
			{
				intent.ActiveAttackSequence++;
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.PreBlock",
					new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock }
				));
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.Block",
					new ChangeBattlePhaseEvent { Current = SubPhase.Block }
				));
			}
			else
			{
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.EnemyEnd",
					new ChangeBattlePhaseEvent { Current = SubPhase.EnemyEnd }
				));
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.PlayerStart",
					new ChangeBattlePhaseEvent { Current = SubPhase.PlayerStart }
				));
				EventQueue.EnqueueRule(new EventQueueBridge.QueuedPublish<ChangeBattlePhaseEvent>(
					"Rule.ChangePhase.Action",
					new ChangeBattlePhaseEvent { Current = SubPhase.Action }
				));
			}

			State = EventQueue.EventState.Complete;
		}

		public void Update(float deltaSeconds) { }
	}
}


