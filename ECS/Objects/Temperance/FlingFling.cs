using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Temperance
{
    public class FlingFling : TemperanceBase
    {
        public FlingFling()
        {
            Id = "fling_fling";
            Name = "Fling Fling";
            Target = "Player";
            Text = "Add 2 Kunai cards to your hand.";
            Threshold = 3;
        }

        public override void Activate(EntityManager entityManager)
        {
            var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            for (int i = 0; i < 2; i++)
            {
                var kunai = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White, false, i);
                EventManager.Publish(new CardMoveRequested { Card = kunai, Deck = deckEntity, Destination = CardZoneType.Hand, Reason = "FlingFling" });
            }
        }
    }
}
