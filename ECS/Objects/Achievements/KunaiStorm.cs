using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Play 6 Kunai in a single turn.
    /// </summary>
    public class KunaiStorm : AchievementBase
    {
        private const int RequiredKunai = 6;
        private int kunaiPlayedThisTurn = 0;

        public KunaiStorm()
        {
            Id = "kunai_storm";
            Name = "Kunai Storm";
            Description = "Play 6 Kunai in a single turn";
            Row = 3;
            Column = 2;
            StartsVisible = false;
            TargetValue = RequiredKunai;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Unsubscribe<CardPlayedEvent>(OnCardPlayed);
        }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            // Reset counter when player turn starts
            if (evt.Current == SubPhase.PlayerStart)
            {
                kunaiPlayedThisTurn = 0;
                SetProgress(0);
            }
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (evt?.Card == null) return;
            
            var cardData = evt.Card.GetComponent<CardData>();
            if (cardData == null || cardData.Card == null) return;
            
            // Only count Kunai cards
            if (cardData.Card.CardId == "kunai")
            {
                kunaiPlayedThisTurn++;
                SetProgress(kunaiPlayedThisTurn);
            }
        }

        protected override void EvaluateCompletion()
        {
            if (kunaiPlayedThisTurn >= RequiredKunai)
            {
                Complete();
            }
        }
    }
}
