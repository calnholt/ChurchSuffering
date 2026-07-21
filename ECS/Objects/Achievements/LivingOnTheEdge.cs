using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Win 20 battles with 1HP remaining.
    /// </summary>
    public class LivingOnTheEdge : AchievementBase
    {
        private const int RequiredBattles = 20;

        public LivingOnTheEdge()
        {
            Id = "living_on_the_edge";
            Name = "Living on the Edge";
            Description = "Win 20 battles with 1HP remaining";
            Row = 3;
            Column = 1;
            StartsVisible = false;
            TargetValue = RequiredBattles;
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
            // Check if player has exactly 1 HP when battle is won
            var player = EntityManager.GetEntitiesWithComponent<Player>()
                .FirstOrDefault(e => e.HasComponent<HP>());
            
            if (player == null) return;
            
            var hp = player.GetComponent<HP>();
            if (hp == null) return;
            
            // Only count if player has exactly 1 HP remaining
            if (hp.Current == 1)
            {
                IncrementProgress();
            }
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredBattles)
            {
                Complete();
            }
        }
    }
}
