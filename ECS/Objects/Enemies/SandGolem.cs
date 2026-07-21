using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using static ChurchSuffering.ECS.Systems.MustBeBlockedSystem;

namespace ChurchSuffering.ECS.Objects.EnemyAttacks;

public class SandGolem : EnemyBase
{
  public SandGolem()
  {
    Id = EnemyId.SandGolem;
    Name = "Sand Golem";
    HP = 30;
    ClimbPool = ClimbEncounterPool.Early;
  }
  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    return turnNumber % 2 == 1 ? [EnemyAttackId.SandPound] : [EnemyAttackId.SandSlam];
  }
}

public class SandPound : EnemyAttackBase
{
  private int Threshold = 1;
  public SandPound()
  {
    Id = EnemyAttackId.SandPound;
    Name = "Sand Pound";
    Damage = 7;
    AttackEffectRecipe = EnemyRockBlastEffect();
    ConditionType = ConditionType.MustBeBlockedByExactly1Card;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedExactly, Threshold);

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new MustBeBlockedEvent { Threshold = Threshold, Type = MustBeBlockedByType.Exactly });
    };
  }
}

public class SandSlam : EnemyAttackBase
{
  private int Threshold = 2;
  public SandSlam()
  {
    Id = EnemyAttackId.SandSlam;
    Name = "Sand Slam";
    Damage = 10;
    ConditionType = ConditionType.MustBeBlockedByExactly2Cards;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedExactly, Threshold);

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new MustBeBlockedEvent { Threshold = Threshold, Type = MustBeBlockedByType.Exactly });
    };
  }
}
