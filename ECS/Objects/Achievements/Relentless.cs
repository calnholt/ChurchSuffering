using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Play 8 cards in one turn.
    /// </summary>
    public class Relentless : AchievementBase
    {
        private const int RequiredPlays = 8;
        private int cardsPlayedThisTurn = 0;

        public Relentless()
        {
            Id = "relentless";
            Name = "Relentless";
            Description = $"Play {RequiredPlays} cards in one turn";
            Row = 6;
            Column = 2;
            StartsVisible = false;
            TargetValue = RequiredPlays;
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
            if (evt.Current == SubPhase.PlayerStart)
            {
                cardsPlayedThisTurn = 0;
                SetProgress(0);
            }
        }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (evt?.Card == null) return;

            cardsPlayedThisTurn++;
            SetProgress(cardsPlayedThisTurn);
        }

        protected override void EvaluateCompletion()
        {
            if (cardsPlayedThisTurn >= RequiredPlays)
            {
                Complete();
            }
        }
    }
}
