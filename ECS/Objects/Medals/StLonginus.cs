using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using static Crusaders30XX.ECS.Components.CardData;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StLonginus : MedalBase
    {
        public StLonginus()
        {
            Id = "st_longinus";
            Name = "St. Longinus";
            Text = "Whenever you pledge a thorned card, add a Kunai to your hand.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
        }

        private void OnPledgeAdded(PledgeAddedEvent evt)
        {
            if (evt?.Card?.GetComponent<Thorned>() == null) return;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var kunai = EntityFactory.CreateCardFromDefinition(EntityManager, "kunai", CardColor.White, false, 1);
            EventManager.Publish(new CardMoveRequested
            {
                Card = kunai,
                Deck = deckEntity,
                Destination = CardZoneType.Hand,
                Reason = Id
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<PledgeAddedEvent>(OnPledgeAdded);
        }
    }
}
