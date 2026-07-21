using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Medals
{
    public class StJude : MedalBase
    {
        private Entity _armedCard;

        public StJude()
        {
            Id = "st_jude";
            Name = "St. Jude";
            MaxCount = 4;
            Text = "The fourth Kunai you add to your hand is upgraded.";
        }

        public override void Initialize(EntityManager entityManager, Entity medalEntity)
        {
            EntityManager = entityManager;
            MedalEntity = medalEntity;
            EventManager.Subscribe<CardMoved>(OnCardMoved);
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private void OnCardMoved(CardMoved evt)
        {
            if (evt?.Card == null || evt.To != CardZoneType.Hand) return;

            var card = evt.Card.GetComponent<CardData>()?.Card;
            if (card == null || card.CardId != "kunai") return;

            CurrentCount++;
            if (CurrentCount >= MaxCount)
            {
                CurrentCount = 0;
                _armedCard = evt.Card;
                EmitActivateEvent();
            }
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current != SubPhase.StartBattle) return;
            CurrentCount = 0;
            _armedCard = null;
        }

        public override void Activate()
        {
            var card = _armedCard?.GetComponent<CardData>()?.Card;
            if (card != null && !card.IsUpgraded)
            {
                card.IsUpgraded = true;
                card.OnUpgrade?.Invoke(EntityManager, _armedCard);
            }
            _armedCard = null;
        }

        public override void Dispose()
        {
            EventManager.Unsubscribe<CardMoved>(OnCardMoved);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }
    }
}
