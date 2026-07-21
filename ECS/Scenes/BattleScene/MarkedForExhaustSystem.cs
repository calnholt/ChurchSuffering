using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Exhausts cards marked for exhaust when battle ends before play exhaust or PlayerEnd can run.
    /// </summary>
    public class MarkedForExhaustSystem : Core.System
    {
        public MarkedForExhaustSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<BeginDefeatPresentationEvent>(OnBeginDefeatPresentation);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, Microsoft.Xna.Framework.GameTime gameTime) { }

        private void OnBeginDefeatPresentation(BeginDefeatPresentationEvent evt)
        {
            if (evt?.IsPreview == true) return;

            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck == null) return;

            var toExhaust = CollectCardsToExhaustAtBattleEnd(deck);
            if (toExhaust.Count == 0) return;

            foreach (var card in toExhaust)
            {
                ClearExhaustMarkers(card);
                RunDeckService.ExhaustRunCard(EntityManager, card);
                LoggingService.Append("MarkedForExhaustSystem.OnBeginDefeatPresentation", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "BattleEndExhaust",
                    ["entityId"] = card.Id,
                    ["cardId"] = card.GetComponent<CardData>()?.Card?.CardId ?? "unknown"
                });
            }
        }

        private List<Entity> CollectCardsToExhaustAtBattleEnd(Deck deck)
        {
            var seen = new HashSet<Entity>();
            var result = new List<Entity>();

            foreach (var card in EntityManager.GetEntitiesWithComponent<MarkedForExhaust>().ToList())
            {
                if (card != null && card.IsActive && seen.Add(card))
                {
                    result.Add(card);
                }
            }

            foreach (var card in deck.Hand.ToList())
            {
                if (card == null || !card.IsActive || !seen.Add(card)) continue;

                var animZone = card.GetComponent<AnimatingHandToZone>();
                if (animZone?.Destination == CardZoneType.ExhaustPile)
                {
                    result.Add(card);
                    continue;
                }

                var cardData = card.GetComponent<CardData>();
                if (cardData?.Card?.ExhaustsOnEndTurn == true)
                {
                    result.Add(card);
                }
            }

            return result;
        }

        private void ClearExhaustMarkers(Entity card)
        {
            if (card.HasComponent<MarkedForExhaust>())
            {
                EntityManager.RemoveComponent<MarkedForExhaust>(card);
            }
            if (card.HasComponent<AnimatingHandToZone>())
            {
                EntityManager.RemoveComponent<AnimatingHandToZone>(card);
            }
            if (card.HasComponent<AnimatingHandToDiscard>())
            {
                EntityManager.RemoveComponent<AnimatingHandToDiscard>(card);
            }
        }
    }
}
