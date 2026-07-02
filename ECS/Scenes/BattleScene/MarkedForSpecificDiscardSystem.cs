using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Manages MarkedForSpecificDiscard components for the current enemy attack.
    /// </summary>
    public class MarkedForSpecificDiscardSystem : Core.System
    {
        private readonly System.Random _random = new System.Random();
        public MarkedForSpecificDiscardSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<MarkedForSpecificDiscardEvent>(OnMarkedForSpecificDiscard);
            EventManager.Subscribe<DiscardMarkedForSpecificDiscardEvent>(OnDiscardMarkedForSpecificDiscard);
            EventManager.Subscribe<AttackResolved>(OnAttackResolved);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<AttackIntent>();
        }

        protected override void UpdateEntity(Entity entity, Microsoft.Xna.Framework.GameTime gameTime)
        {
        }

        private void OnMarkedForSpecificDiscard(MarkedForSpecificDiscardEvent evt)
        {
            TryPreselectSpecificDiscards(evt);
        }

        private void TryPreselectSpecificDiscards(MarkedForSpecificDiscardEvent evt)
        {
            var attackDef = GetComponentHelper.GetPlannedAttack(EntityManager);
            if (attackDef == null) return;
            if (evt.Amount <= 0) return;
            var candidates = GetComponentHelper.GetHandOfCards(EntityManager);
            int pick = System.Math.Min(evt.Amount, candidates.Count);
            LoggingService.Append("MarkedForSpecificDiscardSystem.TryPreselectSpecificDiscards", new System.Text.Json.Nodes.JsonObject { ["pickCount"] = pick, ["candidateCount"] = candidates.Count });
            if (pick <= 0) return;
            var selected = candidates.OrderBy(_ => _random.Next()).Take(pick).ToList();
            foreach (var card in selected)
            {
                EntityManager.AddComponent(card, new MarkedForSpecificDiscard
                {
                    Owner = card,
                });
            }
        }

        private void OnDiscardMarkedForSpecificDiscard(DiscardMarkedForSpecificDiscardEvent evt)
        {
            var entities = GetMarkedCards().ToList();
            if (entities.Count == 0) return;

            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity?.GetComponent<Deck>() == null) return;

            foreach (var entity in entities)
            {
                EntityManager.RemoveComponent<MarkedForSpecificDiscard>(entity);
                EventManager.Publish(new CardMoveRequested
                {
                    Card = entity,
                    Deck = deckEntity,
                    Destination = CardZoneType.DiscardPile,
                    Reason = "DiscardSpecificCard",
                });
            }
        }

        private void OnAttackResolved(AttackResolved evt)
        {
            var entities = GetMarkedCards().ToList();
            if (entities.Count == 0) return;
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;
            foreach (var e in entities)
            {
                EntityManager.RemoveComponent<MarkedForSpecificDiscard>(e);
                if (evt.WasConditionMet)
                {
                    continue;
                }
                EventManager.Publish(new CardMoveRequested { Card = e, Deck = deckEntity, Destination = CardZoneType.DiscardPile, Reason = "DiscardSpecificCard" });
            }
        }

        private IEnumerable<Entity> GetMarkedCards()
        {
            return EntityManager.GetEntitiesWithComponent<MarkedForSpecificDiscard>();
        }
    }
}
