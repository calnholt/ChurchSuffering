using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class TrainingDemon : EnemyBase
{
    public TrainingDemon()
    {
        Id = EnemyId.TrainingDemon;
        Name = "Training Demon";
        HP = 26;
    }

    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
        return [EnemyAttackId.TrainingStrike];
    }
}

public class TrainingStrike : EnemyAttackBase
{
    public TrainingStrike()
    {
        Id = EnemyAttackId.TrainingStrike;
        Name = "Training Strike";
        Damage = 9;
        AttackEffectRecipe = EnemySlashEffect();
    }
}
