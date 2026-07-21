using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Systems;
using ChurchSuffering.ECS.Utils;
using static ChurchSuffering.ECS.Systems.MustBeBlockedSystem;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class GlacialGuardian : EnemyBase
{
  public GlacialGuardian()
  {
    Id = EnemyId.GlacialGuardian;
    Name = "Glacial Guardian";
    HP = 30;
    ClimbPool = ClimbEncounterPool.Late;

    OnStartOfBattle = (entityManager) =>
    {
      EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked, priority: 10);
      EventQueueBridge.EnqueueTriggerAction("GlacialGuardian.OnCreate", () =>
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Windchill, Delta = 1 });
      }, AppliedPassivesManagementSystem.Duration);
    };
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    return ArrayUtils.TakeRandomWithoutReplacement(new List<EnemyAttackId> { EnemyAttackId.GlacialStrike, EnemyAttackId.GlacialBlast }, 1);
  }

  private void OnCardBlocked(CardBlockedEvent evt)
  {
    if (evt.Card?.GetComponent<Frozen>() != null)
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Scar, Delta = 1 });
    }
  }

  public override void Dispose()
  {
    EventManager.Unsubscribe<CardBlockedEvent>(OnCardBlocked);
    base.Dispose();
  }
}

public class GlacialStrike : EnemyAttackBase
{
  private int Threshold = 1;
  public GlacialStrike()
  {
    Id = EnemyAttackId.GlacialStrike;
    Name = "Glacial Strike";
    Damage = 8;
    ConditionType = ConditionType.MustBeBlockedByAtLeast1Card;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, Threshold);

    OnAttackReveal = (entityManager) =>
{
  EventManager.Publish(new MustBeBlockedEvent { Threshold = Threshold, Type = MustBeBlockedByType.AtLeast });
};
  }
}

public class GlacialBlast : EnemyAttackBase
{
  private int Threshold = 2;
  public GlacialBlast()
  {
    Id = EnemyAttackId.GlacialBlast;
    Name = "Glacial Blast";
    Damage = 11;
    ConditionType = ConditionType.MustBeBlockedByAtLeast2Cards;
    Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.MustBeBlockedByAtLeast, Threshold);

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new MustBeBlockedEvent { Threshold = Threshold, Type = MustBeBlockedByType.AtLeast });
    };
  }
}
