using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class ShieldOfFaith : CardBase
    {
        private int AegisGained = 8;

        private int AegisGainedUpgrade = 2;
        private List<string> CostUpgrade = ["White"];
        public ShieldOfFaith()
        {
            CardId = "shield_of_faith";
            Name = "Shield of Faith";
            Target = "Player";
            Cost = ["Any"];
            Text = $"Gain {GetAegisGained(IsUpgraded)} aegis.";
            VisualEffectRecipe = PlayerBuffEffect();
            Block = 3;
            IsFreeAction = true;
            Type = CardType.Prayer;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aegis, Delta = +GetAegisGained(IsUpgraded) });
            };

            OnUpgrade = (entityManager, card) =>
            {
                AegisGained += AegisGainedUpgrade;
                Cost = CostUpgrade;
                Text = $"Gain {GetAegisGained(IsUpgraded)} aegis.";
            };
        }

        private int GetAegisGained(bool isUpgraded)
        {
            return isUpgraded ? AegisGained + AegisGainedUpgrade : AegisGained;
        }
    }
}

