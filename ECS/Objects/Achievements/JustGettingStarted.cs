using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Win 10 battles.
    /// </summary>
    public class JustGettingStarted : AchievementBase
    {
        private const int RequiredWins = 10;

        public JustGettingStarted()
        {
            Id = "just_getting_started";
            Name = "Just Getting Started";
            Description = "Win 10 battles";
            Row = 1;
            Column = 1;
            StartsVisible = false;
            TargetValue = RequiredWins;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }

        private void OnQuestComplete(ShowQuestRewardOverlay evt)
        {
            IncrementProgress();
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredWins)
            {
                Complete();
            }
        }
    }
}
