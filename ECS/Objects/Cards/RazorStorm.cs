using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards;

public class RazorStorm : CardBase
{
  private int NumOfHits = 2;

  private int NumOfHitsUpgrade = 1;
  public RazorStorm()
  {
    CardId = "razor_storm";
    Rarity = Rarity.Uncommon;
    Name = "Razor Storm";
    Target = "Enemy";
    Text = $"Attacks {GetNumOfHits(IsUpgraded)} times.";
    VisualEffectRecipe = PlayerAttackEffect();
    Damage = 1;
    Block = 2;
    IsFreeAction = true;

    OnPlay = (entityManager, card) =>
    {
      var time = 0.5f;
      var numOfHits = GetNumOfHits(IsUpgraded);
      EventManager.Publish(new EndTurnDisplayEvent { ShowButton = false });
      TimerScheduler.Schedule(time * numOfHits, () =>
      {
        EventManager.Publish(new EndTurnDisplayEvent { ShowButton = true });
      });
      for (int j = 0; j < numOfHits; j++)
      {
        TimerScheduler.Schedule(time + (j * time), () =>
        {
          EventManager.Publish(new ModifyHpRequestEvent
          {
            Source = entityManager.GetEntity("Player"),
            Target = entityManager.GetEntity("Enemy"),
            Delta = -GetDerivedDamage(entityManager, card),
            AttackCard = card,

            DamageType = ModifyTypeEnum.Attack
          });
        });
      }
    };

    OnUpgrade = (entityManager, card) =>
    {
      Text = $"Attacks {GetNumOfHits(IsUpgraded)} times.";
    };
  }

  private int GetNumOfHits(bool isUpgraded)
  {
    return isUpgraded ? NumOfHits + NumOfHitsUpgrade : NumOfHits;
  }
}
