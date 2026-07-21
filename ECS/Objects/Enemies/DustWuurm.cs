using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class DustWuurm : EnemyBase
{
  public DustWuurm()
  {
    Id = EnemyId.DustWuurm;
    Name = "Dust Wuurm";
    HP = 31;
    ClimbPool = ClimbEncounterPool.Early;

    OnStartOfBattle = (entityManager) =>
    {
      EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
      EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Rage, Delta = 1 });
    };
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    return [EnemyAttackId.DustStorm];
  }

  private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
  {
    if (evt.Current == SubPhase.EnemyStart)
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Power, Delta = 1 });
    }
  }

  public override void Dispose()
  {
    EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
    Console.WriteLine($"[DustWuurm] Unsubscribed from ChangeBattlePhaseEvent");
  }

}

public class DustStorm : EnemyAttackBase
{
  public DustStorm()
  {
    Id = EnemyAttackId.DustStorm;
    Name = "Dust Storm";
    Damage = 8;
    ConditionType = ConditionType.MustBeBlockedByAtLeast1Card;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, 1);
  }
}