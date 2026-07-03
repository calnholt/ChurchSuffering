using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using static Crusaders30XX.ECS.Systems.MustBeBlockedSystem;

namespace Crusaders30XX.ECS.Objects.EnemyAttacks;

public class SandGolem : EnemyBase
{
  public SandGolem()
  {
    Id = EnemyId.SandGolem;
    Name = "Sand Golem";
    HP = 30;
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
