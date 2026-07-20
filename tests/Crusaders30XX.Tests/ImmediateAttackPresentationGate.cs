using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Tests;

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
