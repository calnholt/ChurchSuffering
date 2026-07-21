using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Utils;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class Sorcerer : EnemyBase
{
  public Sorcerer()
  {
    Id = EnemyId.Sorcerer;
    Name = "Sorcerer";
    HP = 25;
    ClimbPool = ClimbEncounterPool.Late;

    OnStartOfBattle = (entityManager) =>
    {
      // EventQueueBridge.EnqueueTriggerAction("Sorcerer.OnCreate", () =>
      // {
      //   EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Intimidated, Delta = 1 });

      // }, AppliedPassivesManagementSystem.Duration);
      EventQueueBridge.EnqueueTriggerAction("Sorcerer.OnCreate", () =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.MindFog, Delta = 1 });
      }, AppliedPassivesManagementSystem.Duration);
    };
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    return ArrayUtils.TakeRandomWithReplacement(new List<EnemyAttackId> { EnemyAttackId.StrangeForce }, 1);
  }

  public override void Dispose()
  {
    Console.WriteLine($"[Sorcerer] Dispose");
  }
}

public class StrangeForce : EnemyAttackBase
{
  private int DrawCount = 1;
  private int IntimidateAmount = 1;

  public StrangeForce()
  {
    Id = EnemyAttackId.StrangeForce;
    Name = "Strange Force";
    Damage = 11;
    ConditionType = ConditionType.OnBlockedByAtLeast2DifferentColors;
    Text = $"On attack - Draw {DrawCount} {(DrawCount == 1 ? "card" : "cards")}, intimidate {IntimidateAmount}.\n\n{EnemyAttackTextHelper.GetConditionText(ConditionType)}Mill 1.";

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new RequestDrawCardsEvent { Count = DrawCount });
      EventManager.Publish(new IntimidateEvent { Amount = IntimidateAmount });
    };

    OnAttackHit = (entityManager) =>
    {
      EventQueueBridge.EnqueueTriggerAction("StrangeForce.OnAttackHit.Mill", () =>
      {
        EventManager.Publish(new MillCardEvent { });
      }, 0.5f);
    };
  }
}
