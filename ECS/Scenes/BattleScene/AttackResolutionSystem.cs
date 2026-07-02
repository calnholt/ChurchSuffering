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
			if (string.IsNullOrEmpty(e.ContextId)) return;
			LoggingService.Append("AttackResolutionSystem.OnResolveAttack", new System.Text.Json.Nodes.JsonObject
			{
				["contextId"] = e.ContextId
			});
			// Find the planned attack by context
			var enemy = GetRelevantEntities().FirstOrDefault(en => en.GetComponent<AttackIntent>().Planned.Any(pa => pa.ContextId == e.ContextId));
			if (enemy == null) return;
			var intent = enemy.GetComponent<AttackIntent>();
			var pa = intent.Planned.FirstOrDefault(x => x.ContextId == e.ContextId);
			if (pa == null) return;

			var def = pa.AttackDefinition;
			if (def == null) return;

			var progress = EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
				.FirstOrDefault(ent => ent.GetComponent<EnemyAttackProgress>()?.ContextId == pa.ContextId)
				?.GetComponent<EnemyAttackProgress>();

			bool blocked = ConditionService.Evaluate(def.ConditionType, EntityManager, progress);

			// If a special effect like GlassCannon has already fully prevented this attack
			// (preview shows 0 damage with the condition met), treat it as blocked and
			// avoid publishing any base damage.
			bool fullyPreventedBySpecial = progress != null && progress.FullyPreventedBySpecial;

			pa.WasBlocked = blocked;

			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			var source = enemy;
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

			// Defer not-blocked effects (and final resolution signal) until the attack impact occurs.
			// IMPORTANT: we intentionally reuse the original blocked result captured at resolution time
			// so that changes to Aegis or other prevention between resolution and impact do not cause
			// effectsOnNotBlocked (like bonus damage) to misfire.
			bool blockedAtResolution = blocked;

			Action<ResolvingEnemyDamageEvent> onResolving = null;
			Action<EnemyDamageAppliedEvent> onApplied = null;

			onResolving = (evt) =>
			{
				if (evt.ContextId != pa.ContextId) return;

				// Check if the attack lands:
				// 1. Not blocked by special condition (blockedAtResolution)
				// 2. Not blocked by gameplay mitigation (Block/Aegis) if it deals damage
			};

			onApplied = (evt) =>
			{
				if (evt.ContextId != pa.ContextId) return;

				var impactProgress = EntityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
					.FirstOrDefault(ent => ent.GetComponent<EnemyAttackProgress>()?.ContextId == pa.ContextId)
					?.GetComponent<EnemyAttackProgress>();

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
					EventManager.Publish(new OnEnemyAttackHitEvent {} );
					EventManager.Publish(new TrackingEvent { Type = def.Id.ToKey(), Delta = 1 });
				}
				EventManager.Unsubscribe(onResolving);
				EventManager.Unsubscribe(onApplied);
				EventManager.Publish(new AttackResolved { ContextId = pa.ContextId, WasConditionMet = blockedAtResolution });
			};

			

			EventManager.Subscribe(onResolving);
			EventManager.Subscribe(onApplied);
		}

	}
}
