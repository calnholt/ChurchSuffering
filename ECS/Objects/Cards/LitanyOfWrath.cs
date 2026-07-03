using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class LitanyOfWrath : CardBase
    {
        private int AggressionGained = 3;
        private int AggressionGainedUpgrade = 5;
        private List<string> CostUpgrade = ["White"];
        public LitanyOfWrath()
        {
            CardId = "litany_of_wrath";
            Rarity = Rarity.Starter;
            Name = "Litany of Wrath";
            Target = "Player";
            Text = $"Gain {AggressionGained} aggression.";
            IsFreeAction = true;
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;

            OnPlay = (entityManager, card) =>
            {
                var player = entityManager.GetEntity("Player");
                EventManager.Publish(new ApplyPassiveEvent
                {
                    Target = player,
                    Type = AppliedPassiveType.Aggression,
                    Delta = AggressionGained
                });
            };

            OnUpgrade = (entityManager, card) =>
            {
                AggressionGained += AggressionGainedUpgrade;
                Cost = CostUpgrade;
                Text = $"Gain {AggressionGained} aggression.";
            };
        }
    }
}
