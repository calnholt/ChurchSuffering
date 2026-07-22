using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Data.Ids;
using CardIds = ChurchSuffering.ECS.Data.Ids.CardId;

namespace ChurchSuffering.ECS.Objects.Cards
{
    public class HiddenKunai : CardBase
    {
        private int KunaiAmount = 1;
        public HiddenKunai()
        {
            CardId = CardIds.HiddenKunai.ToKey();
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
