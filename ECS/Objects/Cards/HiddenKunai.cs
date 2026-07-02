using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Objects.Cards
{
    public class HiddenKunai : CardBase
    {
        private int KunaiAmount = 1;
        public HiddenKunai()
        {
            CardId = "hidden_kunai";
            Name = "Hidden Kunai";
            Text = $"Add {KunaiAmount} Kunai to your hand.";
            Block = 3;
            Type = CardType.Block;

            OnBlock = (entityManager, card) =>
            {
                var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                for (int i = 0; i < KunaiAmount; i++)
                {
                    var kunai = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White, false, i + 1, null, false, false, IsUpgraded);
                    EventManager.Publish(new CardMoveRequested { Card = kunai, Deck = deckEntity, Destination = CardZoneType.Hand, Reason = "HiddenKunai" });
                }
            };

            OnUpgrade = (entityManager, card) =>
            {
                Text = $"Add {KunaiAmount} Kunai+ to your hand.";
            };
        }
    }
}
