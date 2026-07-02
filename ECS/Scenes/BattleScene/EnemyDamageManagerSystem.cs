using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Applies incoming damage to the player: consumes assigned block, then publishes ModifyHpRequestEvent.
    /// Listens to ApplyEffect(Damage) events.
    /// </summary>
    public class EnemyDamageManagerSystem : Core.System
    {
        public EnemyDamageManagerSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ApplyEffect>(OnApplyEffect);
            EventManager.Subscribe<EnemyAttackImpactNow>(OnImpactNow);
            LoggingService.Append("EnemyDamageManagerSystem.ctor", new System.Text.Json.Nodes.JsonObject { ["message"] = "subscribed to ApplyEffect, EnemyAttackImpactNow" });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnApplyEffect(ApplyEffect e)
        {
            if ((e.EffectType ?? string.Empty) != "Damage") return;
            LoggingService.Append("EnemyDamageManagerSystem.OnApplyEffect", new System.Text.Json.Nodes.JsonObject { ["effectType"] = e.EffectType, ["amount"] = e.Amount, ["percentage"] = e.Percentage });
            if (e.Percentage != 100 && Random.Shared.Next(0, 100) > e.Percentage) return;
            int amt = Math.Max(0, e.Amount);
            _pendingDamage += amt;
        }

        private int _pendingDamage;

        private void OnImpactNow(EnemyAttackImpactNow e)
        {
            LoggingService.Append("EnemyDamageManagerSystem.OnImpactNow", new System.Text.Json.Nodes.JsonObject { ["pendingDamage"] = _pendingDamage });
            int baseDamage = _pendingDamage;
            _pendingDamage = 0;

            int assignedBlock = 0;
            EnemyAttackFlowService.TryGetCurrentProgress(EntityManager, out var prog);
            if (prog != null) assignedBlock = prog.AssignedBlockTotal;

            bool willHit = baseDamage > assignedBlock;

            EventManager.Publish(new ResolvingEnemyDamageEvent
            {
                BaseDamage = baseDamage,
                AssignedBlock = assignedBlock,
                WillHit = willHit
            });

            int extraDamage = _pendingDamage;
            _pendingDamage = 0;
            int totalDamage = baseDamage + extraDamage;
            int damageAfterBlock = totalDamage;
            int finalDamage = totalDamage;
            bool wasHit = false;

            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            if (player != null && totalDamage > 0)
            {
                int useAssigned = Math.Min(assignedBlock, totalDamage);
                damageAfterBlock -= useAssigned;
                finalDamage = damageAfterBlock;

                if (damageAfterBlock > 0)
                {
                    var enemy = EntityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
                    bool ignoresAegis = prog?.IgnoresAegis ?? false;
                    int effectiveAegis = ignoresAegis ? 0 : prog?.AegisTotal ?? 0;
                    finalDamage = Math.Max(0, damageAfterBlock - effectiveAegis);
                    wasHit = finalDamage > 0;
                    LoggingService.Append("EnemyDamageManagerSystem.OnImpactNow.modifyHp", new System.Text.Json.Nodes.JsonObject { ["finalDamage"] = finalDamage, ["aegisTotal"] = prog?.AegisTotal, ["ignoresAegis"] = ignoresAegis, ["wasHit"] = wasHit });
                    EventManager.Publish(new ModifyHpRequestEvent
                    {
                        Source = enemy,
                        Target = player,
                        Delta = -damageAfterBlock,
                        IgnoresAegis = ignoresAegis
                    });
                }
            }

            EventManager.Publish(new EnemyDamageAppliedEvent
            {
                FinalDamage = finalDamage,
                TotalDamage = totalDamage,
                WasHit = wasHit
            });
        }
    }
}
