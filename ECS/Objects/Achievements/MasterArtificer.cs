using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Upgrade 30 different cards across all runs.
    /// </summary>
    public class MasterArtificer : AchievementBase
    {
        private const int RequiredUniqueUpgrades = 30;

        public MasterArtificer()
        {
            Id = "master_artificer";
            Name = "Master Artificer";
            Description = $"Upgrade {RequiredUniqueUpgrades} different cards";
            Row = 6;
            Column = 4;
            StartsVisible = false;
            TargetValue = RequiredUniqueUpgrades;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<CardUpgradeConfirmedEvent>(OnCardUpgradeConfirmed);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<CardUpgradeConfirmedEvent>(OnCardUpgradeConfirmed);
        }

        private void OnCardUpgradeConfirmed(CardUpgradeConfirmedEvent evt)
        {
            if (string.IsNullOrWhiteSpace(evt?.CardId)) return;
            TrackUniqueCardId(evt.CardId);
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredUniqueUpgrades)
            {
                Complete();
            }
        }
    }
}
