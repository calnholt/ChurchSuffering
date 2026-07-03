using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Defeat Berserker and have no shackles.
    /// </summary>
    public class Unshackled : AchievementBase
    {
        public Unshackled()
        {
            Id = "unshackled";
            Name = "Unshackled";
            Description = "Defeat Berserker and have no shackles";
            Row = 5;
            Column = 0;
            StartsVisible = false;
            Points = 30;
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
            // Check if the killed enemy is a Berserker
            if (evt.Enemy == null || !evt.Enemy.HasComponent<Enemy>()) return;
            
            var enemyComponent = evt.Enemy.GetComponent<Enemy>();
            if (enemyComponent?.EnemyBase == null || enemyComponent.EnemyBase.Id != EnemyId.Berserker)
            {
                return;
            }

            // Check if player has no shackles
            var player = EntityManager.GetEntity("Player");
            if (player == null) return;

            var passives = player.GetComponent<AppliedPassives>();
            if (passives == null || passives.Passives == null) 
            {
                // No passives = no shackles, complete the achievement
                Complete();
                return;
            }

            // Check if player has no Shackled passive stacks
            if (!passives.Passives.TryGetValue(AppliedPassiveType.Shackled, out int shackledStacks) || shackledStacks <= 0)
            {
                Complete();
            }
        }

        protected override void EvaluateCompletion()
        {
            // Completion is checked directly in the event handler
        }
    }
}
