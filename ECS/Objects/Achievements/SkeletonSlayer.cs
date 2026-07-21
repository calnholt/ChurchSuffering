using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Kill 5 Skeletons.
    /// </summary>
    public class SkeletonSlayer : AchievementBase
    {
        private const int RequiredKills = 5;

        public SkeletonSlayer()
        {
            Id = "skeleton_slayer";
            Name = "Skeleton Slayer";
            Description = $"Defeat {RequiredKills} Skeletons";
            Row = 0;
            Column = 1;
            StartsVisible = false;
            TargetValue = RequiredKills;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            // Only care about damage to enemies
            if (evt.Enemy == null || !evt.Enemy.HasComponent<Enemy>()) return;
            var enemyBase = evt.Enemy.GetComponent<Enemy>().EnemyBase;
            if (enemyBase == null || enemyBase.Id != EnemyId.Skeleton) return;
            IncrementProgress();
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredKills)
            {
                Complete();
            }
        }
    }
}
