using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;

namespace ChurchSuffering.ECS.Objects.Enemies;

public class HaveNoMercy : EnemyAttackBase
{
  public HaveNoMercy()
  {
    Id = EnemyAttackId.HaveNoMercy;
    Name = "Have No Mercy";
    Damage = 3;
    ConditionType = ConditionType.OnBlockedByAtLeast1Card;

    OnAttackReveal = (entityManager) =>
    {
      EventManager.Publish(new MarkedForSpecificDiscardEvent { Amount = 1 });
      var markedCard = entityManager.GetEntitiesWithComponent<MarkedForSpecificDiscard>().FirstOrDefault().GetComponent<CardData>().Card.Name;
      if (markedCard != null)
      {
        Text = EnemyAttackTextHelper.GetText(EnemyAttackTextType.Custom, 0, ConditionType, 100, $"Discard {markedCard} from your hand.");
      }
    };
  }
}