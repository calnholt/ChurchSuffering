using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Complete 10 quests.
    /// </summary>
    public class QuestMaster : AchievementBase
    {
        private const int RequiredQuests = 10;

        public QuestMaster()
        {
            Id = "quest_master";
            Name = "Quest Master";
            Description = $"Complete {RequiredQuests} battles";
            Row = 4;
            Column = 2;
            StartsVisible = false;
            TargetValue = RequiredQuests;
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
            // Quest completed - increment counter
            IncrementProgress();
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredQuests)
            {
                Complete();
            }
        }
    }
}
