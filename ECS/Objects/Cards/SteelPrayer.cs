using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class SteelPrayer : CardBase
    {
        private int CourageAmount = 10;
        private int CourageAmountUpgrade = 2;
        private List<string> CostUpgrade = ["Any", "Any"];

        public SteelPrayer()
        {
            CardId = "steel_prayer";
            Name = "Steel Prayer";
            Target = "Player";
            Text = $"Gain {GetCourageAmount(IsUpgraded)} courage.";
            Cost = ["Red", "Any"];
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 2;
            IsFreeAction = true;

            OnPlay = (entityManager, card) =>
            {
                EventManager.Publish(new ModifyCourageRequestEvent
                {
                    Delta = GetCourageAmount(IsUpgraded),
                    Type = ModifyCourageType.Gain
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                Cost = CostUpgrade;
                Text = $"Gain {GetCourageAmount(IsUpgraded)} courage.";
            };
        }

        private int GetCourageAmount(bool isUpgraded)
        {
            return isUpgraded ? CourageAmount + CourageAmountUpgrade : CourageAmount;
        }
    }
}
