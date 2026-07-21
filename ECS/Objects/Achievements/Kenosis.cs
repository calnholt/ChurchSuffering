using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Achievements;
using ChurchSuffering.ECS.Events;

namespace ChurchSuffering.ECS.Objects.Achievements
{
    /// <summary>
    /// Win a battle with no cards in your draw pile.
    /// </summary>
    public class Kenosis : AchievementBase
    {
        public Kenosis()
        {
            Id = "kenosis";
            Name = "Kenosis";
            Description = "Win a battle with no cards in your draw pile";
            Row = 5;
            Column = 2;
            StartsVisible = false;
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
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault().GetComponent<Deck>();
            if (deck == null) return;
            if (deck.DrawPile != null && deck.DrawPile.Count == 0)
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
