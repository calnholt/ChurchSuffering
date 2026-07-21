using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Have frostbite damage trigger twice during the quest and still complete quest.
    /// </summary>
    public class FrozenButUnbroken : AchievementBase
    {
        private const int RequiredFrostbiteTriggers = 2;
        private int frostbiteTriggersThisQuest = 0;

        public FrozenButUnbroken()
        {
            Id = "frozen_but_unbroken";
            Name = "Frozen But Unbroken";
            Description = "Have frostbite damage trigger twice during a battle and win the battle.";
            Row = 4;
            Column = 0;
            StartsVisible = false;
            TargetValue = RequiredFrostbiteTriggers;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<QuestSelected>(OnQuestSelected);
            EventManager.Subscribe<FrostbiteTriggered>(OnFrostbiteTriggered);
            EventManager.Subscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<QuestSelected>(OnQuestSelected);
            EventManager.Unsubscribe<FrostbiteTriggered>(OnFrostbiteTriggered);
            EventManager.Unsubscribe<ShowQuestRewardOverlay>(OnQuestComplete);
        }

        private void OnQuestSelected(QuestSelected evt)
        {
            // Reset counter when a new quest starts
            frostbiteTriggersThisQuest = 0;
            SetProgress(0);
        }

        private void OnFrostbiteTriggered(FrostbiteTriggered evt)
        {
            // Only count frostbite triggers on the player
            if (evt?.Target == null) return;
            
            var player = EntityManager.GetEntity("Player");
            if (player == null || evt.Target != player) return;

            frostbiteTriggersThisQuest++;
            SetProgress(frostbiteTriggersThisQuest);
        }

        private void OnQuestComplete(ShowQuestRewardOverlay evt)
        {
            // Check if frostbite triggered at least 2 times during the quest
            if (frostbiteTriggersThisQuest >= RequiredFrostbiteTriggers)
            {
                Complete();
            }
        }

        protected override void EvaluateCompletion()
        {
            // Completion is checked in OnQuestComplete instead
        }
    }
}
