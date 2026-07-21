using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class Whirlwind : CardBase
    {
        private List<string> CostUpgrade = ["Red"];
        public Whirlwind()
        {
            CardId = "whirlwind";
            Rarity = Rarity.Common;
            Name = "Whirlwind";
            Target = "Enemy";
            MultiHitCount = 2;
            FirstHitDelaySeconds = 0.5f;
            HitIntervalSeconds = 0.5f;
            Text = $"Attacks {MultiHitCount} times.";
            Cost = ["Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 3;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new EndTurnDisplayEvent { ShowButton = false });
                float finalHitTime = FirstHitDelaySeconds + (MultiHitCount - 1) * HitIntervalSeconds;
                TimerScheduler.Schedule(finalHitTime, () => {
                    EventManager.Publish(new EndTurnDisplayEvent { ShowButton = true });
                });
                for (int hitIndex = 0; hitIndex < MultiHitCount; hitIndex++)
                {
                    TimerScheduler.Schedule(FirstHitDelaySeconds + hitIndex * HitIntervalSeconds, () => {
                        EventManager.Publish(new ModifyHpRequestEvent { 
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
                Cost = CostUpgrade;
                MultiHitCount = 3;
                Text = $"Attacks {MultiHitCount} times.";
            };
        }
    }
}
