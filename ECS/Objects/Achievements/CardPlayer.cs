using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Play 50 cards.
    /// </summary>
    public class CardPlayer : AchievementBase
    {
        private const int RequiredPlays = 50;

        public CardPlayer()
        {
            Id = "card_player";
            Name = "Card Player";
            Description = $"Play {RequiredPlays} cards";
            Row = 2;
            Column = 0;
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
