using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Objects.Enemies;

public class HaveNoMercy : EnemyAttackBase
{
  public HaveNoMercy()
  {
    Id = EnemyAttackId.HaveNoMercy;
    Name = "Have No Mercy";
    Damage = 5;
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