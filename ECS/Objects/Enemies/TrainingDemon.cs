using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Objects.Enemies;

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
