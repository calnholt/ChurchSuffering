using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Enemies
{
    public class FireSkeleton : EnemyBase
    {
        private int Armor = 1;

        public FireSkeleton()
        {
            Id = EnemyId.FireSkeleton;
            Name = "Fire Skeleton";
            HP = 26;
            ClimbPool = ClimbEncounterPool.Throughout;

            OnStartOfBattle = (entityManager) =>
            {
                EventQueueBridge.EnqueueTriggerAction("FireSkeleton.OnStartOfBattle", () =>
                {
                    EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Armor, Delta = Armor });
                }, AppliedPassivesManagementSystem.Duration);
            };
        }

        public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
        {
            return SkeletonAttackSelectionService.GetAttackIds(EnemyAttackId.SearingStrike);
        }
    }

    public class SearingStrike : EnemyAttackBase
    {
        private int Bleed = 1;

        public SearingStrike()
        {
            Id = EnemyAttackId.SearingStrike;
            Name = "Searing Strike";
            Damage = 2;
            ConditionType = ConditionType.OnHit;
            Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Custom, 0, ConditionType, 100, $"Gain {Bleed} bleed.");

            OnAttackHit = (entityManager) =>
            {
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = entityManager.GetEntity("Player"),
                    Type = AppliedPassiveType.Bleed,
                    Delta = Bleed,
                });
            };
        }
    }
}
