using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Enemies;

public sealed class Blighttongue : EnemyBase
{
    public Blighttongue()
    {
        Id = EnemyId.Blighttongue;
        Name = "Blighttongue";
        HP = 28;
        ClimbPool = ClimbEncounterPool.Early;
        OnStartOfBattle = entityManager =>
            EventQueueBridge.EnqueueTriggerAction("Blighttongue.OnStartOfBattle", () =>
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = entityManager.GetEntity("Player"),
                    Type = AppliedPassiveType.Poison,
                    Delta = 3,
                }), AppliedPassivesManagementSystem.Duration);
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber) =>
        Random.Shared.Next(2) == 0 ? [EnemyAttackId.VenomLash] : [EnemyAttackId.ToxicDeluge];
}

public sealed class VenomLash : EnemyAttackBase
{
    public VenomLash()
    {
        Id = EnemyAttackId.VenomLash;
        Name = "Venom Lash";
        Damage = 9;
        ConditionType = ConditionType.OnBlockedByAtLeast1Card;
        Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Poison, 3, ConditionType);
        OnAttackHit = entityManager => EventManager.Publish(new ApplyPassiveEvent
        {
            Target = entityManager.GetEntity("Player"),
            Type = AppliedPassiveType.Poison,
            Delta = 3,
        });
    }
}

public sealed class ToxicDeluge : EnemyAttackBase
{
    public ToxicDeluge()
    {
        Id = EnemyAttackId.ToxicDeluge;
        Name = "Toxic Deluge";
        Damage = 10;
        BlockRequiredToPreventEffect = Random.Shared.Next(2) == 0 ? 6 : 7;
        Text = EnemyAttackTextHelper.GetBlockThresholdText(
            Damage - BlockRequiredToPreventEffect.Value,
            EnemyAttackTextHelper.GetText(EnemyAttackTextType.Poison, 2));
        OnDamageThresholdMet = entityManager => EventManager.Publish(new ApplyPassiveEvent
        {
            Target = entityManager.GetEntity("Player"),
            Type = AppliedPassiveType.Poison,
            Delta = 2,
        });
    }
}
