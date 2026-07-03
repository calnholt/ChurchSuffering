using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// When the player pledges a card, the enemy takes damage equal to its anathema stacks.
    /// Stack application and removal are handled by AppliedPassivesManagementSystem.
    /// </summary>
    public class AnathemaManagementSystem : Core.System
    {
        public AnathemaManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnPledgeAdded(PledgeAddedEvent evt)
        {
            if (evt.Card == null) return;

            var enemy = EntityManager.GetEntity("Enemy");
            if (enemy == null) return;

            var ap = enemy.GetComponent<AppliedPassives>();
            if (ap == null || ap.Passives == null || ap.Passives.Count == 0) return;
            if (!ap.Passives.TryGetValue(AppliedPassiveType.Anathema, out int stacks) || stacks <= 0) return;

            LoggingService.Append("AnathemaManagementSystem.OnPledgeAdded", new System.Text.Json.Nodes.JsonObject
            {
                ["stacks"] = stacks
            });
            EventManager.Publish(new ModifyHpRequestEvent
            {
                Source = enemy,
                Target = enemy,
                Delta = -stacks,
                DamageType = ModifyTypeEnum.Effect
            });
        }
    }
}
