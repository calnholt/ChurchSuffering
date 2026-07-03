using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class SteadfastResolve : CardBase
    {
        private int VigorGained = 1;

        private int VigorGainedUpgrade = 3;
        private List<string> CostUpgrade = ["Any", "Any"];

        public SteadfastResolve()
        {
            CardId = "steadfast_resolve";
            Rarity = Rarity.Common;
            Name = "Steadfast Resolve";
            Target = "Player";
            Text = $"Gain {GetVigorGained(IsUpgraded)} vigor.";
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;
            IsFreeAction = false;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Vigor,
                    Delta = GetVigorGained(IsUpgraded)
                });
            };
            OnUpgrade = (entityManager, card) =>
            {
                IsFreeAction = true;
                Cost = CostUpgrade;
                Text = $"Gain {GetVigorGained(IsUpgraded)} vigor.";
            };
        }
        private int GetVigorGained(bool isUpgraded)
        {
            return isUpgraded ? VigorGained + VigorGainedUpgrade : VigorGained;
        }
    }
}
