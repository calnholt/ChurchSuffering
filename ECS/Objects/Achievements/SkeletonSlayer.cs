using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
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
