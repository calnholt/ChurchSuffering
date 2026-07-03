using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Manages pledging during the Action phase (Arsenal zone mechanic).
    /// Players may pledge one eligible hand card per action phase via right-click, space, or gamepad X.
    /// </summary>
    public class PledgeManagementSystem : Core.System
    {
        public PledgeManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnPhaseChanged);
            EventManager.Subscribe<PledgeCardRequested>(OnPledgeCardRequested);
            EventManager.Subscribe<ApplyPledgeToCardRequested>(OnApplyPledgeToCardRequested);
            EventManager.Subscribe<PledgeRandomCardFromDiscardRequested>(OnPledgeRandomCardFromDiscardRequested);
            EventManager.Subscribe<RemovePledgeFromCardRequested>(OnRemovePledgeFromCardRequested);
            EventManager.Subscribe<StartBattleRequested>(_ => ClearAllPledges());
            EventManager.Subscribe<EnemyPhaseResetEvent>(_ => ClearAllPledges());
            EventManager.Subscribe<CardMoved>(OnCardMoved);
            EventManager.Subscribe<DiscardAllCardsEvent>(OnDiscardAllCards, priority: 10);
            EventManager.Subscribe<RedrawHandEvent>(OnRedrawHand, priority: 10);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnPledgeCardRequested(PledgeCardRequested evt)
        {
            if (evt?.Card == null) return;
            TryPledge(evt.Card);
        }

        private void OnApplyPledgeToCardRequested(ApplyPledgeToCardRequested evt)
        {
            if (evt?.Card == null) return;
            AddPledgeToCard(evt.Card, evt.MarkPledgedThisActionPhase);
        }

        private void OnPledgeRandomCardFromDiscardRequested(PledgeRandomCardFromDiscardRequested evt)
        {
            if (PledgeService.HasPledgedCardInHand(EntityManager)) return;

            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null || deck.DiscardPile.Count == 0) return;

            var card = deck.DiscardPile[Random.Shared.Next(deck.DiscardPile.Count)];
            EventManager.Publish(new CardMoveRequested
            {
                Card = card,
                Deck = deckEntity,
                Destination = CardZoneType.Hand,
                Reason = "PledgeRandomCardFromDiscard"
            });
            AddPledgeToCard(card, true);
        }

        private void OnRemovePledgeFromCardRequested(RemovePledgeFromCardRequested evt)
        {
            if (evt?.Card == null) return;
            RemovePledgeFromCard(evt.Card);
        }

        private void OnCardMoved(CardMoved evt)
        {
            if (evt?.Card == null) return;
            if (evt.From != CardZoneType.Hand) return;
            if (evt.To == CardZoneType.Hand || evt.To == CardZoneType.HandStaged) return;
            RemovePledgeFromCard(evt.Card);
        }

        private void OnDiscardAllCards(DiscardAllCardsEvent evt)
        {
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck?.Hand == null) return;
            foreach (var card in deck.Hand.ToList())
                RemovePledgeFromCard(card);
        }

        private void OnRedrawHand(RedrawHandEvent evt)
        {
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck?.Hand == null) return;
            foreach (var card in deck.Hand.ToList())
                RemovePledgeFromCard(card);
        }

        private void OnPhaseChanged(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current != SubPhase.Action) return;

            SetPledgedThisActionPhase(false);

            // Unlock pledges from prior turns. Previous is rarely set on ChangeBattlePhaseEvent,
            // so we unlock at Action start (Shadow still sees !CanPlay at PlayerEnd entry).
            foreach (var card in EntityManager.GetEntitiesWithComponent<Pledge>())
            {
                var pledge = card.GetComponent<Pledge>();
                if (pledge != null)
                    pledge.CanPlay = true;
            }
            LogPledgeHandSnapshot("ActionPhaseUnlock", null);
        }

        public void TryPledge(Entity card)
        {
            if (StateSingleton.PreventClicking || StateSingleton.IsTutorialActive) return;
            if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
            if (!BattleInputGate.TryAllowTutorialAction(EntityManager, TutorialAction.PledgeCard, card)) return;

            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck?.Hand == null || !deck.Hand.Contains(card)) return;

            var cardEligibility = PledgeAvailabilityService.EvaluateCard(card);
            if (!cardEligibility.IsEligible)
            {
                if (!string.IsNullOrEmpty(cardEligibility.RejectionMessage))
                {
                    EventManager.Publish(new CantPlayCardMessage { Message = cardEligibility.RejectionMessage });
                }
                return;
            }

            var availability = PledgeAvailabilityService.Evaluate(EntityManager);
            if (!availability.IsAvailable)
            {
                if (availability.Failure == PledgeAvailabilityFailure.AlreadyPledgedThisActionPhase)
                {
                    EventManager.Publish(new CantPlayCardMessage { Message = "You can only pledge one card per action phase!" });
                }
                else if (availability.Failure == PledgeAvailabilityFailure.CardAlreadyPledged)
                {
                    EventManager.Publish(new CantPlayCardMessage { Message = "You already have a pledged card in hand!" });
                }
                return;
            }

            AddPledgeToCard(card, true);
        }

        private void AddPledgeToCard(Entity card, bool markPledgedThisActionPhase)
        {
            if (card == null) return;
            if (card.GetComponent<Pledge>() != null) return;

            EntityManager.AddComponent(card, new Pledge { Owner = card, CanPlay = false });
            EventManager.Publish(new PledgeAddedEvent { Card = card });
            var cardData = card.GetComponent<CardData>();
            cardData?.Card?.OnPledged?.Invoke(EntityManager, card);

            if (markPledgedThisActionPhase)
                SetPledgedThisActionPhase(true);

            LoggingService.Append("PledgeManagementSystem.AddPledgeToCard", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = card.Id,
                ["cardId"] = card.GetComponent<CardData>()?.Card.CardId ?? "unknown",
                ["card"] = HandStateLoggingService.BuildCardSnapshot(card)
            });
            LogPledgeHandSnapshot("AddPledgeToCard", card);
        }

        private void RemovePledgeFromCard(Entity card)
        {
            if (card == null || card.GetComponent<Pledge>() == null) return;

            EntityManager.RemoveComponent<Pledge>(card);
            LoggingService.Append("PledgeManagementSystem.RemovePledgeFromCard", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = card.Id,
                ["cardId"] = card.GetComponent<CardData>()?.Card.CardId ?? "unknown",
                ["card"] = HandStateLoggingService.BuildCardSnapshot(card)
            });
            LogPledgeHandSnapshot("RemovePledgeFromCard", card);
        }

        private void ClearAllPledges()
        {
            SetPledgedThisActionPhase(false);

            foreach (var card in EntityManager.GetEntitiesWithComponent<Pledge>().ToList())
                RemovePledgeFromCard(card);

            foreach (var card in EntityManager.GetEntitiesWithComponent<PledgePreview>().ToList())
                EntityManager.RemoveComponent<PledgePreview>(card);
        }

        private void SetPledgedThisActionPhase(bool value)
        {
            var phaseEntity = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault();
            if (phaseEntity == null) return;

            var state = phaseEntity.GetComponent<PledgeAvailabilityState>();
            if (state == null)
            {
                state = new PledgeAvailabilityState { Owner = phaseEntity };
                EntityManager.AddComponent(phaseEntity, state);
            }

            state.PledgedThisActionPhase = value;
        }

        private void LogPledgeHandSnapshot(string reason, Entity card)
        {
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck == null) return;

            var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>()?.Sub;
            var snapshot = HandStateLoggingService.BuildHandSnapshot(deck, reason, phase);
            snapshot["pledgedEntityId"] = card?.Id ?? -1;
            snapshot["pledgedCardId"] = card?.GetComponent<CardData>()?.Card?.CardId ?? "none";
            LoggingService.Append("PledgeManagementSystem.HandSnapshot", snapshot);
        }
    }
}
