using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StRita : MedalBase
    {
        private const int ResurrectAmount = 2;

        public StRita()
        {
            Id = "st_rita";
            Name = "St. Rita of Cascia";
            Text = $"Whenever you play a Curse card, resurrect {ResurrectAmount}.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed);
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            if (evt?.Card == null) return;
            var cardData = evt.Card.GetComponent<CardData>();
            if (!string.Equals(cardData?.Card?.CardId, Curse.CardIdValue, StringComparison.OrdinalIgnoreCase)) return;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = ResurrectAmount });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        }
    }
}
