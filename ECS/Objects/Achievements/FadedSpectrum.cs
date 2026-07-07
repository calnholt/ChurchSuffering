using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Complete a climb with an entire color absent from your deck.
    /// </summary>
    public class FadedSpectrum : AchievementBase
    {
        public FadedSpectrum()
        {
            Id = "faded_spectrum";
            Name = "Faded Spectrum";
            Description = "Complete a climb with an entire color absent from your deck";
            Row = 6;
            Column = 3;
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
            if (GuidedTutorialService.IsActive(EntityManager)) return;
            if (evt.Enemy == null || !evt.Enemy.HasComponent<Enemy>()) return;

            var enemyComponent = evt.Enemy.GetComponent<Enemy>();
            if (enemyComponent?.EnemyBase == null || enemyComponent.EnemyBase.Id != EnemyId.FallenShepherd)
            {
                return;
            }

            if (RunDeckCompositionService.HasEliminatedColor())
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
