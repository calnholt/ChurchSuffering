using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards;

public class RazorStorm : CardBase
{
  public RazorStorm()
  {
    CardId = "razor_storm";
    Rarity = Rarity.Uncommon;
    Name = "Razor Storm";
    Target = "Enemy";
    MultiHitCount = 2;
    FirstHitDelaySeconds = 0.5f;
    HitIntervalSeconds = 0.5f;
    Text = $"Attacks {MultiHitCount} times.";
    VisualEffectRecipe = PlayerAttackEffect();
    Damage = 1;
    Block = 2;
    IsFreeAction = true;

    OnPlay = (entityManager, card) =>
    {
      EventManager.Publish(new EndTurnDisplayEvent { ShowButton = false });
      float finalHitTime = FirstHitDelaySeconds + (MultiHitCount - 1) * HitIntervalSeconds;
      TimerScheduler.Schedule(finalHitTime, () =>
      {
        EventManager.Publish(new EndTurnDisplayEvent { ShowButton = true });
      });
      for (int hitIndex = 0; hitIndex < MultiHitCount; hitIndex++)
      {
        TimerScheduler.Schedule(FirstHitDelaySeconds + hitIndex * HitIntervalSeconds, () =>
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
      MultiHitCount = 3;
      Text = $"Attacks {MultiHitCount} times.";
    };
  }
}
