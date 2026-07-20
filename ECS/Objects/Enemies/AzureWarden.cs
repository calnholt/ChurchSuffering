using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Objects.Enemies;

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
