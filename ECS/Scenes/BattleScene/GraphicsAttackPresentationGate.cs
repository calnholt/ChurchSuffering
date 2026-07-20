using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
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
