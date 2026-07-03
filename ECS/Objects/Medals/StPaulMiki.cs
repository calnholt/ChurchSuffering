using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using static Crusaders30XX.ECS.Components.CardData;

namespace Crusaders30XX.ECS.Objects.Medals
{
    public class StPaulMiki : MedalBase
    {
        public StPaulMiki()
        {
            Id = "st_paul_miki";
            Name = "St. Paul Miki";
            MaxCount = 1;
            Text = "The first time you block with a black card in a battle, add a Kunai to your hand.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<CardBlockedEvent>(OnCardBlockedEvent);
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        public override void OnAcquire()
        {
            CurrentCount = MaxCount;
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current != SubPhase.StartBattle) return;
            CurrentCount = MaxCount;
        }

        private void OnCardBlockedEvent(CardBlockedEvent evt)
        {
            if (CurrentCount <= 0) return;
            if (!CardColorQualificationService.QualifiesAs(evt.Card, CardColor.Black)) return;
            CurrentCount = 0;
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
            EventManager.Unsubscribe<CardBlockedEvent>(OnCardBlockedEvent);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
