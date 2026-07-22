using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class SteelPrayer : CardBase
    {
        private int CourageAmount = 10;
        private int CourageAmountUpgrade = 2;
        private List<string> CostUpgrade = ["Any", "Any"];

        public SteelPrayer()
        {
            CardId = CardIds.SteelPrayer.ToKey();
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
