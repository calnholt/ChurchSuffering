using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Complete your first quest.
    /// </summary>
    public class FirstVictory : AchievementBase
    {
        public FirstVictory()
        {
            Id = "first_victory";
            Name = "First Victory";
            Description = "Complete your first battle";
            Row = 1;
            Column = 0;
            StartsVisible = true;
        }

        public override void RegisterListeners()
        {
            // ShowQuestRewardOverlay is published when a quest/battle is won
            EventManager.Subscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }

        private void OnQuestComplete(ShowQuestRewardOverlay evt)
        {
            // Battle/quest won - complete the achievement immediately
            Complete();
        }

        protected override void EvaluateCompletion()
        {
            // Not needed - we complete directly in the event handler
        }
    }
}