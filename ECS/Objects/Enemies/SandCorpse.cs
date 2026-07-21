using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Utils;

namespace ChurchSuffering.ECS.Objects.EnemyAttacks;

public class SandCorpse : EnemyBase
{
  public SandCorpse()
  {
    Id = EnemyId.SandCorpse;
    Name = "Sand Corpse";
    IsTutorialOnly = true;
    HP = 16;
  }
  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    return ArrayUtils.Shuffled([EnemyAttackId.SandBlast, EnemyAttackId.SandStorm]);
  }
}
public class TutorialSandBlast : EnemyAttackBase
{
  public TutorialSandBlast()
  {
    Id = EnemyAttackId.TutorialSandBlast;
    Name = "Sand Blast";
    Damage = 4;
    GuardConversionChance = 0f;
  }
}

public class TutorialSandStorm : EnemyAttackBase
{
  public TutorialSandStorm()
  {
    Id = EnemyAttackId.TutorialSandStorm;
    Name = "Sand Storm";
    Damage = 3;
    GuardConversionChance = 0f;
  }
}
public class SandBlast : EnemyAttackBase
{
  public SandBlast()
  {
    Id = EnemyAttackId.SandBlast;
    Name = "Sand Blast";
    Damage = 4;
    AttackEffectRecipe = EnemyRockBlastEffect();
    GuardConversionChance = 0f;
  }
}

public class SandStorm : EnemyAttackBase
{
  public SandStorm()
  {
    Id = EnemyAttackId.SandStorm;
    Name = "Sand Storm";
    Damage = 3;
    GuardConversionChance = 0f;
  }
}
