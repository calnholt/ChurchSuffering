using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Enemies
{
    public class Wyvern : EnemyBase
    {
        public Wyvern()
        {
            Id = EnemyId.Wyvern;
            Name = "Wyvern";
            HP = 28;
            ClimbPool = ClimbEncounterPool.Late;

            OnStartOfBattle = (entityManager) =>
            {
                EventQueueBridge.EnqueueTriggerAction("Wyvern.OnStartOfBattle", () =>
                {
                    EventManager.Publish(new ApplyPassiveEvent
                    {
                        Target = entityManager.GetEntity("Enemy"),
                        Type = AppliedPassiveType.Plunder,
                        Delta = 1
                    });
                }, AppliedPassivesManagementSystem.Duration);
            };
        }

        public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
        {
            if (turnNumber % 2 == 0)
                return [EnemyAttackId.WyvernThreat];
            return [EnemyAttackId.WyvernStrike];
        }
    }
}

public class WyvernStrike : EnemyAttackBase
{
    public WyvernStrike()
    {
        Id = EnemyAttackId.WyvernStrike;
        Name = "Talon Swipe";
        Damage = 10;
    }
}

public class WyvernThreat : EnemyAttackBase
{
    public WyvernThreat()
    {
        Id = EnemyAttackId.WyvernThreat;
        Name = "Rend & Ruin";
        Damage = 10;
        ConditionType = ConditionType.OnBlockedByAtLeast2Cards;
        Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Custom, conditionType: ConditionType, customText: "Discards the plundered card.");

        OnAttackHit = (entityManager) =>
        {
            EventManager.Publish(new PlunderForceDiscardEvent());
        };
    }
}
