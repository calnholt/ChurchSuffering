using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Enemies
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