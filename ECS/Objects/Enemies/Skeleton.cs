using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.EnemyAttacks;

public class Skeleton : EnemyBase
{
  private int Armor = 1;

  public Skeleton()
  {
    Id = EnemyId.Skeleton;
    Name = "Skeleton";
    HP = 26;
    ClimbPool = ClimbEncounterPool.Throughout;

    OnStartOfBattle = (entityManager) =>
    {
      EventQueueBridge.EnqueueTriggerAction("Skeleton.OnStartOfBattle", () =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Armor, Delta = Armor });
      }, AppliedPassivesManagementSystem.Duration);
    };
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    return SkeletonAttackSelectionService.GetAttackIds(EnemyAttackId.BoneStrike);
  }
}

public class BoneStrike : EnemyAttackBase
{
  private int Scar = 1;
  public BoneStrike()
  {
    Id = EnemyAttackId.BoneStrike;
    Name = "Bone Strike";
    Damage = 2;
    AttackEffectRecipe = EnemySlashEffect();
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Scar, Scar, ConditionType.OnHit);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Scar, Delta = Scar });
    };
  }
}

public class Sweep : EnemyAttackBase
{
  private int Recoil = 1;
  public Sweep()
  {
    Id = EnemyAttackId.Sweep;
    Name = "Sweep";
    Damage = 4;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Recoil, Recoil);

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new ApplyRecoilEvent { Amount = Recoil });
    };
  }
}

public class Calcify : EnemyAttackBase
{
  private int Guard = 2;
  public Calcify()
  {
    Id = EnemyAttackId.Calcify;
    Name = "Calcify";
    Damage = 2;
    ConditionType = ConditionType.OnHit;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Guard, Guard, ConditionType);

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Guard, Delta = Guard });
    };
  }
}

public class SkullCrusher : EnemyAttackBase
{
  public SkullCrusher()
  {
    Id = EnemyAttackId.SkullCrusher;
    Name = "Skull Crusher";
    Damage = 9;
  }
}
