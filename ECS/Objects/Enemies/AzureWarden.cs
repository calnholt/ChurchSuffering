using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class AzureWarden : EnemyBase
{
    public AzureWarden()
    {
        Id = EnemyId.AzureWarden;
        Name = "Azure Warden";
        HP = 30;
        ClimbPool = ClimbEncounterPool.Late;
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
        return [EnemyAttackId.WardenSeal];
    }
}

public class WardenSeal : EnemyAttackBase
{
    public WardenSeal()
    {
        Id = EnemyAttackId.WardenSeal;
        Name = "Seal";
        Damage = 10;
        Text = "On reveal - Seal a random card from your hand.";

        OnAttackReveal = entityManager =>
        {
            EventManager.Publish(new ApplyCardApplicationEvent
            {
                Amount = 1,
                StacksPerCard = 2,
                Type = CardApplicationType.Sealed,
                Target = CardApplicationTarget.Hand,
            });
        };
    }
}
