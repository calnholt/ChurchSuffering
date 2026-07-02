using System;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Resolves planned attacks by evaluating conditions and publishing ApplyEffect events
	/// for either on-hit or on-blocked outcomes. Emits AttackResolved at the end.
	/// </summary>
	public class AttackResolutionSystem : Core.System
	{
		public AttackResolutionSystem(EntityManager entityManager) : base(entityManager)
		{
			EventManager.Subscribe<ResolveAttack>(OnResolveAttack);
			LoggingService.Append("AttackResolutionSystem.ctor", new System.Text.Json.Nodes.JsonObject { ["message"] = "subscribed to ResolveAttack" });
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<AttackIntent>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnResolveAttack(ResolveAttack e)
		{
			if (!EnemyAttackFlowService.TryGetCurrentEnemyAttack(EntityManager, out var enemy, out var intent, out var pa))
				return;

			int capturedSequence = intent.ActiveAttackSequence;

			LoggingService.Append("AttackResolutionSystem.OnResolveAttack", new System.Text.Json.Nodes.JsonObject
			{
				["attackSequence"] = capturedSequence
			});

			var def = pa.AttackDefinition;
			if (def == null) return;

			EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var progress);

			bool blocked = ConditionService.Evaluate(def.ConditionType, EntityManager, progress);
			bool fullyPreventedBySpecial = progress != null && progress.FullyPreventedBySpecial;

			pa.WasBlocked = blocked;

			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			LoggingService.Append("AttackResolutionSystem.OnResolveAttack", new System.Text.Json.Nodes.JsonObject { ["attackId"] = pa.AttackId.ToKey(), ["damage"] = def.Damage, ["isBlocked"] = blocked, ["fullyPreventedBySpecial"] = fullyPreventedBySpecial });

			if (def.Damage > 0 && !fullyPreventedBySpecial)
			{
				EventManager.Publish(new ApplyEffect
				{
					EffectType = "Damage",
					Amount = def.Damage,
					Source = enemy,
					Target = player,
					attackId = !blocked ? pa.AttackId.ToKey() : null,
					Percentage = 100
				});
			}

			bool blockedAtResolution = blocked;

			Action<ResolvingEnemyDamageEvent> onResolving = null;
			Action<EnemyDamageAppliedEvent> onApplied = null;

			onResolving = (evt) =>
			{
				if (EnemyAttackFlowService.GetActiveAttackSequence(EntityManager) != capturedSequence) return;
			};

			onApplied = (evt) =>
			{
				if (EnemyAttackFlowService.GetActiveAttackSequence(EntityManager) != capturedSequence) return;

				EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var impactProgress);

				if (ConditionService.ShouldTriggerNotBlockedEffect(
					def.ConditionType,
					EntityManager,
					impactProgress,
					blockedAtResolution,
					evt.WasHit,
					def.Damage))
				{
					def.OnAttackHit?.Invoke(EntityManager);
				}
				if (def.BlockRequiredToPreventEffect is int blockRequired &&
					impactProgress?.FullyPreventedBySpecial != true)
				{
					int assignedBlock = impactProgress?.AssignedBlockTotal ?? 0;
					if (assignedBlock < blockRequired && evt.FinalDamage > 0)
					{
						def.OnDamageThresholdMet?.Invoke(EntityManager);
					}
				}
				if (evt.WasHit)
				{
					EventManager.Publish(new OnEnemyAttackHitEvent { });
					EventManager.Publish(new TrackingEvent { Type = def.Id.ToKey(), Delta = 1 });
				}
				EventManager.Unsubscribe(onResolving);
				EventManager.Unsubscribe(onApplied);
				EventManager.Publish(new AttackResolved { WasConditionMet = blockedAtResolution });
			};

			EventManager.Subscribe(onResolving);
			EventManager.Subscribe(onApplied);
		}
	}
}
