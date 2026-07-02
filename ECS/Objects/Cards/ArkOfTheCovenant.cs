using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class ArkOfTheCovenant : CardBase
    {
        private int HealAmount = 2;
        private int HealAmountUpgrade = 1;
        public ArkOfTheCovenant()
        {
            CardId = "ark_of_the_covenant";
            Name = "Ark of the Covenant";
            Target = "Player";
            Text = $"When this card is discarded to pay for a card cost, heal {HealAmount} HP.";
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Relic;
            Block = 1;

            OnDiscardedForCost = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ModifyHpRequestEvent { 
                    Source = player, 
                    Target = player, 
                    Delta = +HealAmount, 
                    DamageType = ModifyTypeEnum.Heal 
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                HealAmount += HealAmountUpgrade;
                Text = $"When this card is discarded to pay for a card cost, heal {HealAmount} HP.";
            };
        }
    }
}
