using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Handles recoil effects from enemy attacks.
    /// On ApplyRecoilEvent: adds Recoil to a random unmarked hand card (if all are marked, does nothing).
    /// On CardBlockedEvent: removes Recoil from the blocking card (safe exit, no penalty).
    /// On AttackResolved: for each remaining Recoil card, deals Stacks damage to the player, then removes component.
    /// </summary>
    public class RecoilManagementSystem : Core.System
    {
        private readonly Random _random = new Random();

        public RecoilManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ApplyRecoilEvent>(OnApplyRecoil);
            EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked);
            EventManager.Subscribe<AttackResolved>(OnAttackResolved);
            EventManager.Subscribe<BeginDefeatPresentationEvent>(OnBeginDefeatPresentation);
            EventManager.Subscribe<EnemyPhaseResetEvent>(_ => RemoveAllRecoilWithoutPenalty());
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnApplyRecoil(ApplyRecoilEvent evt)
        {
            // Each event targets a different card — cards already marked are excluded.
            // If all hand cards are already marked, this event does nothing.
            var available = GetComponentHelper.GetHandOfCards(EntityManager)
                ?.Where(c => c.GetComponent<Recoil>() == null)
                .ToList();
            if (available == null || available.Count == 0) return;

            var card = available.OrderBy(_ => _random.Next()).First();
            EntityManager.AddComponent(card, new Recoil { Owner = card, Stacks = evt.Amount });
            LoggingService.Append("RecoilManagementSystem.OnApplyRecoil", new System.Text.Json.Nodes.JsonObject { ["cardId"] = card.GetComponent<CardData>().Card.CardId, ["amount"] = evt.Amount });
        }

        private void OnCardBlocked(CardBlockedEvent evt)
        {
            var card = evt.Card;
            if (card == null) return;
            var recoil = card.GetComponent<Recoil>();
            if (recoil == null) return;

            // Card was used to block — safe exit, no damage
            EntityManager.RemoveComponent<Recoil>(card);
            LoggingService.Append("RecoilManagementSystem.OnCardBlocked", new System.Text.Json.Nodes.JsonObject { ["cardId"] = card.GetComponent<CardData>().Card.CardId, ["message"] = "recoil removed, no penalty" });
        }

        private void OnAttackResolved(AttackResolved evt)
        {
            var recoilCards = EntityManager.GetEntitiesWithComponent<Recoil>().ToList();
            if (recoilCards.Count == 0) return;

            var playerEntity = EntityManager.GetEntity("Player");
            foreach (var card in recoilCards)
            {
                var recoil = card.GetComponent<Recoil>();
                if (recoil == null) continue;

                LoggingService.Append("RecoilManagementSystem.OnAttackResolved", new System.Text.Json.Nodes.JsonObject { ["cardId"] = card.GetComponent<CardData>().Card.CardId, ["damage"] = recoil.Stacks });
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Target = playerEntity,
                    Source = EntityManager.GetEntity("Enemy"),
                    Delta = -recoil.Stacks,
                    DamageType = ModifyTypeEnum.Attack
                });
                EntityManager.RemoveComponent<Recoil>(card);
            }
        }

        private void OnBeginDefeatPresentation(BeginDefeatPresentationEvent evt)
        {
            if (evt?.IsPreview == true) return;
            RemoveAllRecoilWithoutPenalty();
        }

        private void RemoveAllRecoilWithoutPenalty()
        {
            var recoilCards = EntityManager.GetEntitiesWithComponent<Recoil>().ToList();
            foreach (var card in recoilCards)
            {
                LoggingService.Append("RecoilManagementSystem.RemoveAllRecoilWithoutPenalty", new System.Text.Json.Nodes.JsonObject { ["cardId"] = card.GetComponent<CardData>()?.Card?.CardId ?? "unknown" });
                EntityManager.RemoveComponent<Recoil>(card);
            }
        }
    }
}
