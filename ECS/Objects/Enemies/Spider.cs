using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Enemies;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Utils;
using static ChurchSuffering.ECS.Systems.MustBeBlockedSystem;

namespace ChurchSuffering.ECS.Objects.EnemyAttacks;

public class Spider : EnemyBase
{
  private int FearAmount = 2;
  public Spider()
  {
    Id = EnemyId.Spider;
    Name = "Spider";
    HP = 28;
    ClimbPool = ClimbEncounterPool.Late;

    OnStartOfBattle = (entityManager) =>
    {
      EventQueueBridge.EnqueueTriggerAction("Spider.OnStartOfBattle", () => {  
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Fear, Delta = FearAmount });
      }, AppliedPassivesManagementSystem.Duration);
    };
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    var random = Random.Shared.Next(0, 100);
    if (random <= 65)
    {
      return [EnemyAttackId.SuffocatingSilk];
    }
    return [EnemyAttackId.MandibleBreaker];
  }
}

public class SuffocatingSilk : EnemyAttackBase
{
  private int SlowAmount = 4;
  public SuffocatingSilk()
  {
    Id = EnemyAttackId.SuffocatingSilk;
    Name = "Suffocating Silk";
    Damage = 10;
    BlockRequiredToPreventEffect = Random.Shared.Next(0, 100) <= 50 ? 6 : 7;
    Text = $"{EnemyAttackTextHelper.GetBlockThresholdText(Damage - BlockRequiredToPreventEffect.Value, EnemyAttackTextHelper.GetText(EnemyAttackTextType.Slow, SlowAmount, ConditionType))}";

    OnDamageThresholdMet = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Slow, Delta = SlowAmount });
    };
  }
}

public class MandibleBreaker : EnemyAttackBase
{
  private int FearAmount = 1;
  public MandibleBreaker()
  {
    Id = EnemyAttackId.MandibleBreaker;
    Name = "Mandible Breaker";
    Damage = 10;
    ConditionType = ConditionType.OnBlockedByAtLeast1Card;
    Text = $"On attack - Intimidate 1 card.\n\n{EnemyAttackTextHelper.GetText(EnemyAttackTextType.Fear, FearAmount, ConditionType)}";

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new IntimidateEvent { Amount = 1 });
    };

    OnAttackHit = (entityManager) =>
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Fear, Delta = FearAmount });
    };
  }
}
