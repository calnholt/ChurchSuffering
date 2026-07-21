using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
	public interface IEnemyAttackResolver
	{
		void ResolveCurrentAttack();
	}

	public interface IAttackPresentationGate
	{
		EventQueue.IQueuedEvent CreateDiscardStep(EntityManager entityManager);
		EventQueue.IQueuedEvent CreateAbsorbWait();
		IReadOnlyList<EventQueue.IQueuedEvent> BuildImpactSteps(
			EntityManager entityManager,
			Entity enemy,
			EnemyAttackBase attack,
			int attackSequence);
	}

	/// <summary>
	/// Assembles the complete queued rule sequence for the current enemy attack.
	/// Presentation-specific waits and impact timing are supplied by IAttackPresentationGate.
	/// </summary>
	public sealed class EnemyAttackResolver : IEnemyAttackResolver
	{
		private readonly EntityManager _entityManager;
		private readonly IAttackPresentationGate _presentationGate;

		public EnemyAttackResolver(EntityManager entityManager, IAttackPresentationGate presentationGate)
		{
			_entityManager = entityManager ?? throw new ArgumentNullException(nameof(entityManager));
			_presentationGate = presentationGate ?? throw new ArgumentNullException(nameof(presentationGate));
		}

		public void ResolveCurrentAttack()
		{
			EnemyAttackFlowService.TryGetCurrentEnemyAttack(
				_entityManager,
				out var enemy,
				out var intent,
				out var planned);
			planned?.AttackDefinition?.OnBlocksConfirmed?.Invoke(_entityManager);

			EventManager.Publish(new ChangeBattlePhaseEvent
			{
				Current = SubPhase.EnemyAttack,
				Previous = SubPhase.Block
			});

			EventQueue.EnqueueRule(_presentationGate.CreateDiscardStep(_entityManager));
			EventQueue.EnqueueRule(new QueuedResolveAttackEvent());
			EventQueue.EnqueueRule(_presentationGate.CreateAbsorbWait());

			foreach (var step in _presentationGate.BuildImpactSteps(
				_entityManager,
				enemy,
				planned?.AttackDefinition,
				intent?.ActiveAttackSequence ?? -1))
			{
				EventQueue.EnqueueRule(step);
			}

			EventQueue.EnqueueRule(new QueuedAdvanceToNextPlannedAttackEvent(_entityManager));
		}
	}
}
