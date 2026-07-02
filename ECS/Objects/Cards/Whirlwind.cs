using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class Whirlwind : CardBase
    {
        private int NumOfHits = 2;
        private int NumOfHitsUpgrade = 1;
        private List<string> CostUpgrade = ["Red"];
        public Whirlwind()
        {
            CardId = "whirlwind";
            Rarity = Rarity.Common;
            Name = "Whirlwind";
            Target = "Enemy";
            Text = $"Attacks {GetNumOfHits(IsUpgraded)} times.";
            Cost = ["Any"];
            VisualEffectRecipe = PlayerAttackEffect();
            Damage = 3;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var time = 0.5f;
                var numOfHits = GetNumOfHits(IsUpgraded);
                EventManager.Publish(new EndTurnDisplayEvent { ShowButton = false });
                TimerScheduler.Schedule(time * numOfHits, () => {
                    EventManager.Publish(new EndTurnDisplayEvent { ShowButton = true });
                });
                for (int j = 0; j < numOfHits; j++)
                {
                    TimerScheduler.Schedule(time + (j * time), () => {
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
                Text = $"Attacks {GetNumOfHits(IsUpgraded)} times.";
            };
        }
        private int GetNumOfHits(bool isUpgraded)
        {
            return isUpgraded ? NumOfHits + NumOfHitsUpgrade : NumOfHits;
        }
    }
}

