using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StAnthonyOfPadua : MedalBase
    {
        private const int ShuffleAmount = 4;

        public StAnthonyOfPadua()
        {
            Id = "st_anthony_of_padua";
            Name = "St. Anthony of Padua";
            MaxCount = 1;
            Text = $"The first time each battle you try to draw and your deck is empty, shuffle {ShuffleAmount} random cards from your discard pile back into your deck.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<DrawPileEmptyEvent>(OnDrawPileEmpty);
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

        private void OnDrawPileEmpty(DrawPileEmptyEvent evt)
        {
            if (CurrentCount <= 0) return;
            if (!HasEligibleDiscardCard(evt?.Deck)) return;

            CurrentCount = 0;
            EventManager.Publish(new ShuffleRandomCardsFromDiscardToDrawPileEvent
            {
                Deck = evt.Deck,
                Amount = ShuffleAmount
            });
            EmitActivateEvent();
        }

        public override void Activate()
        {
            // The rescue must happen synchronously during the failed draw attempt.
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<DrawPileEmptyEvent>(OnDrawPileEmpty);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private static bool HasEligibleDiscardCard(Entity deckEntity)
        {
            var deck = deckEntity?.GetComponent<Deck>();
            return deck?.DiscardPile?.Any(card => card.GetComponent<CardData>()?.Card?.IsWeapon != true) == true;
        }
    }
}
