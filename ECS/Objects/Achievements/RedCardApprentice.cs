using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Play 100 red cards.
    /// </summary>
    public class RedCardApprentice : AchievementBase
    {
        private const int RequiredPlays = 100;

        public RedCardApprentice()
        {
            Id = "red_card_apprentice";
            Name = "Red Card Apprentice";
            Description = $"Play {RequiredPlays} red cards";
            Row = 2;
            Column = 1;
            StartsVisible = false;
            TargetValue = RequiredPlays;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<CardPlayedEvent>(OnCardPlayedEvent);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<CardPlayedEvent>(OnCardPlayedEvent);
        }

        private void OnCardPlayedEvent(CardPlayedEvent evt)
        {
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            // Check if it's a red card
            if (!CardColorQualificationService.QualifiesAs(evt.Card, CardData.CardColor.Red)) return;

            IncrementProgress();
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredPlays)
            {
                Complete();
            }
        }
    }
}
