using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using static ChurchSuffering.ECS.Components.CardData;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StPeter : MedalBase
    {
        public StPeter()
        {
            Id = "st_peter";
            Name = "St. Peter the Apostle";
            MaxCount = 3;
            Text = $"Each time you block with {MaxCount} black cards this quest, resurrect 1.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EventManager.Subscribe<CardBlockedEvent>(OnCardBlockedEvent);
            EntityManager = entityManager;
            MedalEntity = medalEntity;
        }

        private void OnCardBlockedEvent(CardBlockedEvent evt)
        {
            if (CardColorQualificationService.QualifiesAs(evt.Card, CardColor.Black))
            {
                CurrentCount++;
                if (CurrentCount >= MaxCount)
                {
                    CurrentCount = 0;
                    EmitActivateEvent();
                }
            }
        }

        public override void Activate()
        {
            Console.WriteLine($"[StPeter] Activate: Drawing 1 card");
            EventManager.Publish(new DrawRandomCardFromDiscardEvent { Amount = 1 });
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<CardBlockedEvent>(OnCardBlockedEvent);
        }
    }
}
