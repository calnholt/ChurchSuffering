using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
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
            Points = 20;
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