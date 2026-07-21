using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Enemies
{
    public class CinderboltDemon : EnemyBase
    {
      private bool UsedInsidiousBolt = false;
        public CinderboltDemon()
        {
            Id = EnemyId.CinderboltDemon;
            Name = "Cinderbolt Demon";
            HP = 30;
            ClimbPool = ClimbEncounterPool.Late;
        }

        public override IEnumerable<EnemyAttackId> GetAttackIds(EntityManager entityManager, int turnNumber)
        {
          var random = Random.Shared.Next(0, 100);
          if (!UsedInsidiousBolt && (turnNumber == 3 && random < 50 || turnNumber > 3))
          {
            UsedInsidiousBolt = true;
            return [EnemyAttackId.InsidiousBolt];
          }
          return [EnemyAttackId.Cinderbolt];
        }
    }
}

public class Cinderbolt : EnemyAttackBase
{
  private int Burn = 1;
  private bool AppliedBurn = false;
  private CardData.CardColor? Color;
    public Cinderbolt()
    {
        Id = EnemyAttackId.Cinderbolt;
        Name = "Cinderbolt";
        Damage = 10;
        OnAttackReveal = (entityManager) =>
        {
          Color = PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager);
          Text = Color.HasValue
            ? $"Gain {Burn} burn if at least one {Color.Value.ToString().ToLower()} card blocks this."
            : $"Gain {Burn} burn if a card of the selected color blocks this. No color is selected.";
        };

        OnBlockProcessed = (entityManager, card) =>
        {
		  if (Color.HasValue && CardColorQualificationService.QualifiesAs(card, Color.Value) && !AppliedBurn)
          {
            EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Burn, Delta = Burn });
            AppliedBurn = true;
          }
        };
    }
}

public class InsidiousBolt : EnemyAttackBase
{
  private int Scar = 2;
  private bool AppliedScar = false;
  private CardData.CardColor? Color;
  public InsidiousBolt()
  {
    Id = EnemyAttackId.InsidiousBolt;
    Name = "Insidious Bolt";
    Damage = 10;

    OnAttackReveal = (entityManager) =>
    {
      Color = PlayerHandColorService.GetRandomCardColorInPlayerHand(entityManager);
      Text = Color.HasValue
        ? $"Gain {Scar} scar if at least one {Color.Value.ToString().ToLower()} card blocks this."
        : $"Gain {Scar} scar if a card of the selected color blocks this. No color is selected.";
    };

    OnBlockProcessed = (entityManager, card) =>
    {
	  if (Color.HasValue && CardColorQualificationService.QualifiesAs(card, Color.Value) && !AppliedScar)
      {
        EventManager.Publish(new ApplyPassiveEvent { Target = entityManager.GetEntity("Player"), Type = AppliedPassiveType.Scar, Delta = Scar });
        AppliedScar = true;
      }
    };
  }
}
