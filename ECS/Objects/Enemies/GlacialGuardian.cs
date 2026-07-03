using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Utils;
using static Crusaders30XX.ECS.Systems.MustBeBlockedSystem;

namespace Crusaders30XX.ECS.Objects.Enemies;

public class GlacialGuardian : EnemyBase
{
  public GlacialGuardian()
  {
    Id = EnemyId.GlacialGuardian;
    Name = "Glacial Guardian";
    HP = 24;

    OnStartOfBattle = (entityManager) =>
    {
      EventManager.Subscribe<CardMoved>(OnCardMoved, priority: 10);
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

  private void OnCardMoved(CardMoved evt)
  {
    if ((evt.To == CardZoneType.DiscardPile || evt.To == CardZoneType.ExhaustPile) && evt.From == CardZoneType.AssignedBlock && evt.Card.GetComponent<Frozen>() != null)
    {
      EventManager.Publish(new ApplyPassiveEvent { Target = EntityManager.GetEntity("Player"), Type = AppliedPassiveType.Scar, Delta = 1 });
    }
  }

  public override void Dispose()
  {
    EventManager.Unsubscribe<CardMoved>(OnCardMoved);
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
