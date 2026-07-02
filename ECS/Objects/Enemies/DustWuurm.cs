using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Objects.Enemies;

public class DustWuurm : EnemyBase
{
  public DustWuurm()
  {
    Id = EnemyId.DustWuurm;
    Name = "Dust Wuurm";
    HP = 31;

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