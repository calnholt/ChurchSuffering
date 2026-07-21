using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Have 5 cursed cards in your deck.
    /// </summary>
    public class HexedHoard : AchievementBase
    {
        private const int RequiredCursedCards = 5;

        public HexedHoard()
        {
            Id = "hexed_hoard";
            Name = "Hexed Hoard";
            Description = $"Have {RequiredCursedCards} cursed cards in your deck";
            Row = 6;
            Column = 0;
            StartsVisible = false;
            TargetValue = RequiredCursedCards;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<StartBattleRequested>(OnDeckCompositionCheck);
            EventManager.Subscribe<ApplyCardApplicationEvent>(OnApplyCardApplication);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<StartBattleRequested>(OnDeckCompositionCheck);
            EventManager.Unsubscribe<ApplyCardApplicationEvent>(OnApplyCardApplication);
        }

        private void OnDeckCompositionCheck(StartBattleRequested evt)
        {
            EvaluateDeckComposition();
        }

        private void OnApplyCardApplication(ApplyCardApplicationEvent evt)
        {
            if (evt?.Type != CardApplicationType.Cursed) return;
            EvaluateDeckComposition();
        }

        private void EvaluateDeckComposition()
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            SetProgress(RunDeckCompositionService.CountCursedCardsInLoadout());
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredCursedCards)
            {
                Complete();
            }
        }
    }
}
