using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class ArkOfTheCovenant : CardBase
    {
        private int HealAmount = 2;
        private int HealAmountUpgrade = 1;
        public ArkOfTheCovenant()
        {
            CardId = CardIds.ArkOfTheCovenant.ToKey();
            Name = "Ark of the Covenant";
            Target = "Player";
            Text = $"When this card is discarded to pay for a card cost, heal {HealAmount} HP.";
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Block;
            Block = 3;

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
