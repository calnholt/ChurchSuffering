using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Objects.Enemies;

/// <summary>
/// Sniper enemy with the Marksman passive.
/// A fragile ranged enemy that marks cards in the player's hand,
/// forcing difficult decisions about how to handle the marked card.
/// </summary>
public class Sniper : EnemyBase
{
    public Sniper()
    {
        Id = EnemyId.Sniper;
        Name = "Sniper";
        HP = 53;

        OnStartOfBattle = (entityManager) =>
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = entityManager.GetEntity("Enemy"),
                Type = AppliedPassiveType.Marksman,
                Delta = 1
            });
        };
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
        return new[] { EnemyAttackId.SniperShot };
    }

    public override void Dispose()
    {
        // No event subscriptions to clean up - MarkManagementSystem handles the logic
    }
}

/// <summary>
/// Sniper's basic attack - a single precise shot.
/// </summary>
public class SniperShot : EnemyAttackBase
{
    public SniperShot()
    {
        Id = EnemyAttackId.SniperShot;
        Name = "Sniper Shot";
        Damage = 10;
    }
}
