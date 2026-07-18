using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Gain 500 aegis total.
    /// </summary>
    public class Archangel : AchievementBase
    {
        private const int RequiredAegis = 500;

        public Archangel()
        {
            Id = "archangel";
            Name = "Archangel";
            Description = $"Gain {RequiredAegis} aegis";
            Row = 4;
            Column = 1;
            StartsVisible = false;
            TargetValue = RequiredAegis;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<ApplyPassiveEvent>(OnApplyPassive);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<ApplyPassiveEvent>(OnApplyPassive);
        }

        private void OnApplyPassive(ApplyPassiveEvent evt)
        {
            // Only count aegis gained on the player
            if (evt == null || evt.Target == null) return;
            
            var player = EntityManager.GetEntity("Player");
            if (player == null || evt.Target != player) return;

            // Only count aegis passives
            if (evt.Type != AppliedPassiveType.Aegis) return;

            // Only count positive deltas (gaining aegis, not losing it)
            if (evt.Delta > 0)
            {
                IncrementProgress(evt.Delta);
            }
        }

        protected override void EvaluateCompletion()
        {
            if (GetProgress() >= RequiredAegis)
            {
                Complete();
            }
        }
    }
}
