using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Kill Gleeber in a single turn.
    /// </summary>
    public class OHKO : AchievementBase
    {
        private int StartOfBattleGleeberHp = 0;
        private bool IsStillEligible = true;

        public OHKO()
        {
            Id = "ohko";
            Name = "OHKO";
            Description = $"Defeat Gleeber in a single turn";
            Row = 5;
            Column = 5;
            StartsVisible = false;
            Points = 15;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<EnemyKilledEvent>(OnEnemyKilled);
            EventManager.Unsubscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhaseEvent);
        }

        private void OnChangeBattlePhaseEvent(ChangeBattlePhaseEvent evt)
        {
            if (!IsStillEligible) return;
            var enemy = EntityManager.GetEntity("Enemy");
            var id = enemy.GetComponent<Enemy>().EnemyBase.Id;
            // skip if not gleeber
            if (id != EnemyId.Gleeber)
            {
                IsStillEligible = false;
                return;
            }
            var enemyHp = EntityManager.GetEntity("Enemy").GetComponent<HP>();
            // set HP for validating
            if (evt.Current == SubPhase.StartBattle)
            {
                StartOfBattleGleeberHp = enemyHp.Current;
                return;
            }
            // check if dealt damage and still alive at end of turn
            if (evt.Current == SubPhase.PlayerEnd && enemyHp.Current < StartOfBattleGleeberHp && enemyHp.Current > 0)
            {
                IsStillEligible = false;
                return;
            }
        }

        private void OnEnemyKilled(EnemyKilledEvent evt)
        {
            if (!IsStillEligible) return;
            Complete();
        }

        protected override void EvaluateCompletion()
        {
        }
    }
}
