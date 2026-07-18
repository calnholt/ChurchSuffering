using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework.Graphics;
using static Crusaders30XX.ECS.Components.CardData;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Objects.Enemies;

public class Thornreaver : EnemyBase
{
  public Thornreaver()
  {
    Id = EnemyId.Thornreaver;
    Name = "Thornreaver";
    HP = 34;
    ClimbPool = ClimbEncounterPool.Early;
  }

  public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
  {
    return [EnemyAttackId.SawtoothRend];
  }

}
public class SawtoothRend : EnemyAttackBase
{
  private int Bleed = 2;
  private CardData.CardColor? Color;
  public SawtoothRend()
  {
    Id = EnemyAttackId.SawtoothRend;
    Name = "Sawtooth Rend";
    Damage = 9;
    ConditionType = ConditionType.None;

    OnAttackReveal = (entityManager) =>
    {
      Color = PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager);
      Text = Color.HasValue
        ? $"Gain {Bleed} bleed for each {Color.Value.ToString().ToLower()} card that blocks this."
        : $"Gain {Bleed} bleed for each card of the selected color that blocks this. No color is selected.";
    };

    OnBlockProcessed = (entityManager, card) =>
    {
      if (Color.HasValue && CardColorQualificationService.QualifiesAs(card, Color.Value))
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Bleed, Delta = Bleed });
      }
    };
  }
}
