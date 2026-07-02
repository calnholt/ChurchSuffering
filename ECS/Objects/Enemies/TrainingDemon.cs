using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Objects.Enemies;

public class TrainingDemon : EnemyBase
{
    public TrainingDemon(EnemyDifficulty difficulty = EnemyDifficulty.Easy) : base(difficulty)
    {
        Id = "training_demon";
        Name = "Training Demon";
        HP = 26;
    }

    public override IEnumerable<string> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
        return ["training_strike"];
    }
}

public class TrainingStrike : EnemyAttackBase
{
    public TrainingStrike()
    {
        Id = "training_strike";
        Name = "Training Strike";
        Damage = 9;
        AttackEffectRecipe = EnemySlashEffect();
    }
}
