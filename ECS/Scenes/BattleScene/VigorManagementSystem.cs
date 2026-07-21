using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Consumes Vigor stacks when the player plays a non-weapon card with a discard cost.
    /// Cost reduction is applied earlier in CardPlaySystem via VigorService.
    /// </summary>
    public class VigorManagementSystem : Core.System
    {
        public VigorManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<CardPlayedEvent>(OnCardPlayed);
        }

        protected override IEnumerable<Entity> GetRelevantEntities() => Array.Empty<Entity>();

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnCardPlayed(CardPlayedEvent evt)
        {
            if (evt?.Card == null) return;

            var cardData = evt.Card.GetComponent<CardData>();
            var card = cardData?.Card;
            if (card == null || card.IsWeapon) return;

            int costCount = card.Cost?.Count ?? 0;
            if (costCount == 0) return;

            var player = EntityManager.GetEntity("Player");
            if (player == null) return;

            int consumed = VigorService.GetWaivedPipCount(card, evt.VigorStacksAtPlay);
            if (consumed <= 0) return;
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = player,
                Type = AppliedPassiveType.Vigor,
                Delta = -consumed
            });
        }
    }
}
