using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Resurrect 4 cards in one turn.
    /// </summary>
    public class MassRevival : AchievementBase
    {
        private const int RequiredResurrects = 4;
        private int resurrectedThisTurn = 0;

        public MassRevival()
        {
            Id = "mass_revival";
            Name = "Mass Revival";
            Description = $"Resurrect {RequiredResurrects} cards in one turn";
            Row = 6;
            Column = 1;
            StartsVisible = false;
            TargetValue = RequiredResurrects;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<DrawRandomCardFromDiscardEvent>(OnDrawRandomCardFromDiscard);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Unsubscribe<DrawRandomCardFromDiscardEvent>(OnDrawRandomCardFromDiscard);
        }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current == SubPhase.PlayerStart)
            {
                resurrectedThisTurn = 0;
                SetProgress(0);
            }
        }

        private void OnDrawRandomCardFromDiscard(DrawRandomCardFromDiscardEvent evt)
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (evt == null || evt.Amount <= 0) return;

            resurrectedThisTurn += evt.Amount;
            SetProgress(resurrectedThisTurn);
        }

        protected override void EvaluateCompletion()
        {
            if (resurrectedThisTurn >= RequiredResurrects)
            {
                Complete();
            }
        }
    }
}
