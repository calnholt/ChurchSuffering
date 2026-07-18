using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Enemies
{
  public class Berserker : EnemyBase
  {
    private int WoundedAmount = 1;
    private int ShackledAmount = 5;
    public Berserker()
    {
      Id = EnemyId.Berserker;
      Name = "Berserker";
      HP = 31;
      ClimbPool = ClimbEncounterPool.Early;

      OnStartOfBattle = (entityManager) =>
      {
        EventQueueBridge.EnqueueTriggerAction("Berserker.OnStartOfBattle", () => {  
          EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Enemy"), Type = AppliedPassiveType.Wounded, Delta = WoundedAmount });
        }, AppliedPassivesManagementSystem.Duration);
        EventQueueBridge.EnqueueTriggerAction("Berserker.OnStartOfBattle", () => {  
          EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Shackled, Delta = ShackledAmount });
        }, AppliedPassivesManagementSystem.Duration);
      };
    }
    public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
    {
      return [EnemyAttackId.Rage];
    }
  }
}

public class Rage : EnemyAttackBase
{
  public Rage()
  {
    Id = EnemyAttackId.Rage;
    Name = "Rage";
    Damage = 9;
  }
}