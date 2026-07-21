using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.Tests;

internal sealed class ImmediateAttackPresentationGate : IAttackPresentationGate
{
	public EventQueue.IQueuedEvent CreateDiscardStep(EntityManager entityManager)
	{
		return new EventQueueBridge.TriggerActionThenWait(
			"Rule.DiscardAssignedBlocks.Immediate",
			() => QueuedDiscardAssignedBlocksEvent.ResolveImmediately(
				entityManager,
				QueuedDiscardAssignedBlocksEvent.ShouldDiscardSpentBlocks(entityManager)),
			0f);
	}

	public EventQueue.IQueuedEvent CreateAbsorbWait()
	{
		return new EventQueueBridge.TriggerActionThenWait(
			"Rule.WaitAbsorb.Immediate",
			() => { },
			0f);
	}

	public IReadOnlyList<EventQueue.IQueuedEvent> BuildImpactSteps(
		EntityManager entityManager,
		Entity enemy,
		EnemyAttackBase attack,
		int attackSequence)
	{
		return
		[
			new EventQueueBridge.QueuedPublish<EnemyAttackImpactNow>(
				"Rule.EnemyAttackImpactNow.Immediate",
				new EnemyAttackImpactNow())
		];
	}
}
