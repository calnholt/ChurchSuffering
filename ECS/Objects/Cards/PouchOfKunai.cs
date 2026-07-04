using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using CardIds = Crusaders30XX.ECS.Data.Ids.CardId;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class PouchOfKunai : CardBase
    {
        private int MinKunai = 2;
        private int MaxKunai = 4;

        private int MinKunaiUpgrade = 1;
        public PouchOfKunai()
        {
            CardId = "pouch_of_kunai";
            Name = "Pouch of Kunai";
            Target = "Player";
            Text = $"Put {GetMinKunai(IsUpgraded)} to {GetMaxKunai(IsUpgraded)} Kunai cards in your hand.";
            Cost = ["Black"];
            VisualEffectRecipe = PlayerBuffEffect();
            Type = CardType.Prayer;
            Block = 3;
            IsFreeAction = true;
            CardTooltip = GameIdExtensions.ToKey(CardIds.Kunai);

            OnPlay = (entityManager, card) =>
            {
                var count = Random.Shared.Next(GetMinKunai(IsUpgraded), GetMaxKunai(IsUpgraded) + 1);
                var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                for (int j = 0; j < count; j++)
                {
                    var kunai = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White, false, j + 1);
                    EventManager.Publish(new CardMoveRequested { Card = kunai, Deck = deckEntity, Destination = CardZoneType.Hand, Reason = "PouchOfKunai" });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Put {GetMinKunai(IsUpgraded)} to {GetMaxKunai(IsUpgraded)} Kunai+ cards in your hand.";
            };
        }

        private int GetMinKunai(bool isUpgraded)
        {
            return isUpgraded ? MinKunai + MinKunaiUpgrade : MinKunai;
        }

        private int GetMaxKunai(bool isUpgraded)
        {
            return MaxKunai;
        }
    }
}
