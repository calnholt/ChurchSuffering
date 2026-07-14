using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Objects.Enemies;

public sealed class HexBailiff : EnemyBase
{
    public HexBailiff()
    {
        Id = EnemyId.HexBailiff;
        Name = "Hex Bailiff";
        HP = 32;
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber) =>
        [EnemyAttackId.WritOfMalice];
}

public sealed class WritOfMalice : EnemyAttackBase
{
    public WritOfMalice()
    {
        Id = EnemyAttackId.WritOfMalice;
        Name = "Writ of Malice";
        Damage = 10;
        Text = "On reveal - A random card in your hand becomes Hex.";
        OnAttackReveal = _ => EventManager.Publish(new ApplyCardApplicationEvent
        {
            Amount = 1,
            Type = CardApplicationType.Hex,
            Target = CardApplicationTarget.Hand,
        });
    }
}
