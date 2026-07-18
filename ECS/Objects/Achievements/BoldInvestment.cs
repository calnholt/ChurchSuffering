using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
{
    /// <summary>
    /// Spend 10 or more courage in a single battle.
    /// </summary>
    public class BoldInvestment : AchievementBase
    {
        private const int RequiredCourageSpent = 10;
        private int courageSpentThisBattle = 0;

        public BoldInvestment()
        {
            Id = "bold_investment";
            Name = "Bold Investment";
            Description = "Spend 10 or more courage in a single battle";
            Row = 3;
            Column = 0;
            StartsVisible = false;
        }

        public override void RegisterListeners()
        {
            EventManager.Subscribe<StartBattleRequested>(OnBattleStart);
            EventManager.Subscribe<ModifyCourageRequestEvent>(OnCourageModified);
        }

        public override void UnregisterListeners()
        {
            EventManager.Unsubscribe<StartBattleRequested>(OnBattleStart);
            EventManager.Unsubscribe<ModifyCourageRequestEvent>(OnCourageModified);
        }

        private void OnBattleStart(StartBattleRequested evt)
        {
            // Reset counter when a new battle starts
            courageSpentThisBattle = 0;
            SetProgress(0);
        }

        private void OnCourageModified(ModifyCourageRequestEvent evt)
        {
            // Only track spending (negative delta with Spent type)
            if (evt.Delta < 0 && evt.Type == ModifyCourageType.Spent)
            {
                courageSpentThisBattle += Math.Abs(evt.Delta);
                SetProgress(courageSpentThisBattle);
            }
        }

        protected override void EvaluateCompletion()
        {
            if (courageSpentThisBattle >= RequiredCourageSpent)
            {
                Complete();
            }
        }
    }
}
