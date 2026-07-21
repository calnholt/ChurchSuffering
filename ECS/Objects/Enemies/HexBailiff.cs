using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Objects.Enemies;

public sealed class HexBailiff : EnemyBase
{
    public HexBailiff()
    {
        Id = EnemyId.HexBailiff;
        Name = "Hex Bailiff";
        HP = 32;
        ClimbPool = ClimbEncounterPool.Late;
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
