using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StJoanOfArc : MedalBase
    {
        public StJoanOfArc()
        {
            Id = "st_joan_of_arc";
            Name = "St. Joan of Arc";
            Text = "Whenever you attack with your weapon, gain 1 might.";
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
            if (cardData?.Card?.IsWeapon != true) return;
            EmitActivateEvent();
        }

        public override void Activate()
        {
            var player = EntityManager.GetEntity("Player");
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = player,
                Type = AppliedPassiveType.Might,
                Delta = 1
            });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        }
    }
}
