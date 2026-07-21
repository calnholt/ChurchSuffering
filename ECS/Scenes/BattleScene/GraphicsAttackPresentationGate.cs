using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.VisualEffects;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Production attack presentation gate. It preserves the animated discard,
	/// absorb wait, and gameplay-driving visual impact behavior used in battle.
	/// </summary>
	public sealed class GraphicsAttackPresentationGate : IAttackPresentationGate
	{
		public EventQueue.IQueuedEvent CreateDiscardStep(EntityManager entityManager)
		{
			return new QueuedDiscardAssignedBlocksEvent(entityManager);
		}

		public EventQueue.IQueuedEvent CreateAbsorbWait()
		{
			return new QueuedWaitAbsorbEvent();
		}

		public IReadOnlyList<EventQueue.IQueuedEvent> BuildImpactSteps(
			EntityManager entityManager,
			Entity enemy,
			EnemyAttackBase attack,
			int attackSequence)
		{
			var requests = attack == null
				? Array.Empty<VisualEffectRequested>()
				: VisualEffectRequestFactory.ForEnemyAttackSequence(
					entityManager,
					enemy,
					attack,
					attack.AttackEffectSequence);
			var drivingRequest = requests.SingleOrDefault(request => request.DrivesGameplayImpact);
			if (drivingRequest != null)
			{
				var steps = new List<EventQueue.IQueuedEvent>(requests.Count + 1);
				steps.AddRange(requests.Select(request => new QueuedStartVisualEffect(request)));
				steps.Add(new QueuedWaitVisualEffectImpact(drivingRequest.RequestId));
				return steps;
			}

			LoggingService.Append("EnemyAttackDisplaySystem.ExecuteConfirm", new JsonObject
			{
				["reason"] = "VisualEffectRequestFailed",
				["attackSequence"] = attackSequence,
				["attackId"] = attack?.Id.ToKey() ?? string.Empty
			});
			return
			[
				new EventQueueBridge.QueuedPublish<EnemyAttackImpactNow>(
					"Rule.EnemyAttackImpactNow.Emergency",
					new EnemyAttackImpactNow())
			];
		}
	}
}
