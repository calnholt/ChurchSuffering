using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Achievements;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Objects.Achievements
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
