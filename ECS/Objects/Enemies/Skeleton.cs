using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Objects.EnemyAttacks;

public class Skeleton : EnemyBase
{
  private int Armor = 1;

  public Skeleton(EnemyDifficulty difficulty = EnemyDifficulty.Easy) : base(difficulty)
  {
    Id = "skeleton";
    Name = "Skeleton";
    HP = 26;
    Armor += (int)difficulty;

    OnStartOfBattle = (entityManager) =>
    {
      EventQueueBridge.EnqueueTriggerAction("Skeleton.OnStartOfBattle", () =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Armor, Delta = Armor });
      }, AppliedPassivesManagementSystem.Duration);
    };
  }

  public override IEnumerable<string> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    int random = Random.Shared.Next(0, 100);
    var linkers = new List<string> { "bone_strike", "sweep", "calcify" };
    if (random <= 65)
    {
      var selected = ArrayUtils.TakeRandomWithReplacement(linkers, 3);
      var sweepCount = selected.Count(x => x == "sweep");
      while (sweepCount > 2)
      {
        selected = ArrayUtils.TakeRandomWithReplacement(linkers, 3);
        sweepCount = selected.Count(x => x == "sweep");
      }
      int haveNoMercy = Random.Shared.Next(0, 100);
      if (haveNoMercy <= 5)
      {
        var selected2 = ArrayUtils.TakeRandomWithReplacement(linkers, 2);
        selected2 = selected2.Append("have_no_mercy");
        selected = ArrayUtils.Shuffled(selected2);
      }
      return selected;
    }
    return ["skull_crusher"];
  }
}

public class BoneStrike : EnemyAttackBase
{
  private int Scar = 1;
  public BoneStrike()
  {
    Id = "bone_strike";
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
    Id = "sweep";
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
    Id = "calcify";
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
    Id = "skull_crusher";
    Name = "Skull Crusher";
    Damage = 9;
  }
}
