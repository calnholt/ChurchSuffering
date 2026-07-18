using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Kill 10 enemies (any type).
    /// </summary>
    public class Slayer : AchievementBase
    {
        private const int RequiredKills = 10;

        public Slayer()
        {
            Id = "slayer";
            Name = "Slayer";
            Description = $"Defeat {RequiredKills} enemies";
            Row = 0;
            Column = 0;
            StartsVisible = false; // This is a starter achievement
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