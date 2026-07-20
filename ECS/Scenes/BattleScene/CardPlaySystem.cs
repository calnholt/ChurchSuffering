using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Data.Save;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Handles simple action-phase card plays from JSON definitions.
    /// Currently supports Strike-like damage with optional Courage threshold bonus.
    /// </summary>
    public class CardPlaySystem : Core.System
    {
        public CardPlaySystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<PlayCardRequested>(OnPlayCardRequested);
            EventManager.Subscribe<PayCostSatisfied>(OnPayCostSatisfied);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        /// <summary>
        /// Ensures the LastPaymentCache entity exists and returns it.
        /// </summary>
        private LastPaymentCache EnsurePaymentCacheExists()
        {
            var e = EntityManager.GetEntitiesWithComponent<LastPaymentCache>().FirstOrDefault();
            if (e == null)
            {
                e = EntityManager.CreateEntity("LastPaymentCache");
                EntityManager.AddComponent(e, new LastPaymentCache());
            }
            return e.GetComponent<LastPaymentCache>();
        }

        private void OnPlayCardRequested(PlayCardRequested evt)
        {
            if (evt?.Card == null) return;
            if (BattleInputGate.IsBattleInputFrozen(EntityManager)) return;
            if (!BattleInputGate.TryAllowTutorialAction(EntityManager, TutorialAction.PlayCard, evt.Card)) return;

            ComponentLoggerService.LogEntity(evt.Card, "PlayCardRequested received");

            var data = evt.Card.GetComponent<CardData>();
            if (data == null) return;

            // The entity's card is the initialized runtime definition. It includes
            // upgrade mutations and any state captured by its gameplay delegates.
            var card = data.Card;
            if (card == null || string.IsNullOrEmpty(card.CardId)) return;

            var phase = EntityManager.GetEntitiesWithComponent<PhaseState>()
                .FirstOrDefault()
                ?.GetComponent<PhaseState>();
            if (phase == null) return;
            var alternateProfile = AlternateCardPlayService.GetProfile(EntityManager, evt.Card, phase.Sub);
            var pledge = evt.Card.GetComponent<Pledge>();
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            var appliedPassives = GetComponentHelper.GetAppliedPassives(EntityManager, "Player");
            bool isSilenced = appliedPassives != null
                && appliedPassives.Passives.TryGetValue(AppliedPassiveType.Silenced, out int silencedStacks)
                && silencedStacks > 0;
            var deckEntityForCost = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntityForCost?.GetComponent<Deck>();
            var context = new CardPlayContext(
                evt.Card,
                card,
                phase.Sub,
                player?.GetComponent<ActionPoints>()?.Current ?? 0,
                VigorService.GetPlayerVigorStacks(EntityManager),
                evt.CostsPaid,
                pledge != null,
                pledge?.CanPlay ?? true,
                isSilenced,
                card.CanPlay?.Invoke(EntityManager, evt.Card) ?? true,
                alternateProfile,
                deck != null ? deck.Hand : Array.Empty<Entity>());
            var plan = CardPlayResolver.Resolve(context);

            if (!plan.IsPlayable)
            {
                HandleRejectedCardPlay(evt.Card, card, context, plan);
                return;
            }

            if (plan.PaymentDecision == CardPaymentDecision.SelectOneCard)
            {
                EventManager.Publish(new OpenPayCostOverlayEvent
                {
                    CardToPlay = evt.Card,
                    RequiredCosts = plan.RequiredCosts.ToList(),
                    Type = PayCostOverlayType.SelectOneCard,
                });
                return;
            }

            if (plan.PaymentDecision == CardPaymentDecision.AutoPay)
            {
                ResolveAutomaticPayment(evt.Card, plan.AutoPayment, deckEntityForCost);
                return;
            }

            if (plan.PaymentDecision == CardPaymentDecision.ChooseCostCards)
            {
                LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "MultipleCostSolutions",
                    ["solutionCount"] = 2
                });
                EventManager.Publish(new OpenPayCostOverlayEvent
                {
                    CardToPlay = evt.Card,
                    RequiredCosts = plan.RequiredCosts.ToList(),
                    Type = PayCostOverlayType.ColorDiscard,
                });
                return;
            }

            if (card.VisualEffectSequence != null)
            {
                var requests = VisualEffectRequestFactory.ForCardSequence(EntityManager, evt.Card, card.VisualEffectSequence);
                if (requests.Count > 0)
                {
                    foreach (var request in requests)
                    {
                        EventManager.Publish(request);
                    }
                }
                else
                {
                    LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                    {
                        ["reason"] = "VisualEffectRequestFailed",
                        ["cardId"] = card.CardId
                    });
                }
            }

            ResolveAcceptedCardPlay(evt.Card, evt.PaymentCards, context.VigorStacks, plan, alternateProfile);
        }

        private void HandleRejectedCardPlay(
            Entity cardEntity,
            CardBase card,
            CardPlayContext context,
            CardPlayPlan plan)
        {
            switch (plan.Rejection)
            {
                case CardPlayRejection.WrongPhase:
                    LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                    {
                        ["reason"] = "InvalidPhase",
                        ["phase"] = context.Phase.ToString()
                    });
                    break;
                case CardPlayRejection.IsRelic:
                    EventManager.Publish(new CantPlayCardMessage { Message = "Relics can only be discarded to pay for costs!" });
                    break;
                case CardPlayRejection.BlockWithoutAlternate:
                    EventManager.Publish(new CantPlayCardMessage { Message = "Block cards can only be used to block!" });
                    break;
                case CardPlayRejection.Pledged:
                    EventManager.Publish(new CantPlayCardMessage { Message = "You can't play a card you pledged this turn!" });
                    break;
                case CardPlayRejection.Silenced:
                    EventManager.Publish(new CantPlayCardMessage { Message = "You cannot play pledged cards because you are silenced!" });
                    break;
                case CardPlayRejection.NoActionPoints:
                    LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                    {
                        ["reason"] = "NoActionPoints",
                        ["cardId"] = card.CardId,
                        ["isWeapon"] = card.IsWeapon,
                        ["isFree"] = plan.IsFreeAction,
                        ["currentAp"] = context.ActionPoints
                    });
                    EventManager.Publish(new CantPlayCardMessage { Message = "Not enough action points!" });
                    break;
                case CardPlayRejection.CanPlayFalse:
                    card.OnCantPlay?.Invoke(EntityManager, cardEntity);
                    LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                    {
                        ["reason"] = "CantPlay",
                        ["cardId"] = card.CardId
                    });
                    break;
                case CardPlayRejection.CostUnsatisfiable:
                    LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                    {
                        ["reason"] = "CannotSatisfyCost"
                    });
                    EventManager.Publish(new CantPlayCardMessage
                    {
                        Message = DiscardCostMessageService.GetUnsatisfiableCostMessage(plan.RequiredCosts),
                    });
                    break;
            }
        }

        private void ResolveAutomaticPayment(
            Entity cardToPlay,
            IReadOnlyList<Entity> paymentCards,
            Entity deckEntity)
        {
            var solution = paymentCards.ToList();
            LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["reason"] = "AutoPayCost",
                ["paymentCount"] = solution.Count
            });
            foreach (var paymentCard in solution)
            {
                EventManager.Publish(new CardDiscardedForCostEvent { Card = paymentCard });
                EventManager.Publish(new CardMoveRequested
                {
                    Card = paymentCard,
                    Deck = deckEntity,
                    Destination = CardZoneType.DiscardPile,
                    Reason = "AutoPayCost",
                });
                paymentCard.GetComponent<CardData>()?.Card?.OnDiscardedForCost?.Invoke(EntityManager, paymentCard);
            }

            var cache = EnsurePaymentCacheExists();
            cache.CardPlayed = cardToPlay;
            cache.PaymentCards = new List<Entity>(solution);
            cache.HasData = true;
            ComponentLoggerService.LogComponent(cache, $"Auto-pay complete, {solution.Count} cards discarded");

            EventManager.Publish(new PlayCardRequested
            {
                Card = cardToPlay,
                CostsPaid = true,
                PaymentCards = new List<Entity>(solution),
            });
        }

        private void ResolveAcceptedCardPlay(
            Entity cardEntity,
            List<Entity> paymentCards,
            int vigorStacksAtPlay,
            CardPlayPlan plan,
            AlternateCardPlayProfile alternateProfile)
        {
            if (cardEntity == null) return;
            var data = cardEntity.GetComponent<CardData>();
            var card = data?.Card;
            if (card == null || string.IsNullOrEmpty(card.CardId)) return;

            ComponentLoggerService.LogEntity(cardEntity, "Executing card OnPlay effect");
            bool playedAsCurse = cardEntity.HasComponent<Cursed>()
                || string.Equals(card.CardId, Curse.CardIdValue, StringComparison.OrdinalIgnoreCase);
            AttachPlayStatContext(cardEntity, paymentCards);
            try
            {
                if (plan.Mode == CardPlayMode.AlternateAttack)
                {
                    var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                    var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
                    if (alternateProfile.SourceType == "Medal" && alternateProfile.SourceEntity != null)
                    {
                        EventManager.Publish(new MedalTriggered
                        {
                            MedalEntity = alternateProfile.SourceEntity,
                            MedalId = alternateProfile.SourceId,
                        });
                    }

                    EventManager.Publish(new ModifyHpRequestEvent
                    {
                        Source = player,
                        Target = enemy,
                        Delta = -alternateProfile.AttackDamage,
                        DamageType = ModifyTypeEnum.Attack,
                        AttackCard = cardEntity,
                    });
                }
                else
                {
                    card.OnPlay?.Invoke(EntityManager, cardEntity);
                }
            }
            finally
            {
                RemovePlayStatContext(cardEntity);
            }
            EventManager.Publish(new CardPlayedEvent
            {
                Card = cardEntity,
                VigorStacksAtPlay = vigorStacksAtPlay,
                PlayedAsCurse = playedAsCurse,
            });
            if (!playedAsCurse && !GuidedTutorialService.IsActive(EntityManager))
            {
                EventManager.Publish(new TrackingEvent { Type = card.CardId, Delta = 1 });
                if (card.Type == CardType.Prayer)
                    EventManager.Publish(new TrackingEvent { Type = TrackingTypeEnum.PrayersPlayed.ToString(), Delta = 1 });
            }

            // Remove Pledge if present when playing
            if (cardEntity.HasComponent<Pledge>())
            {
                EventManager.Publish(new RemovePledgeFromCardRequested { Card = cardEntity });
                LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "PledgeRemoved",
                    ["cardId"] = card.CardId
                });
            }

            // Move the played card to discard unless it's a weapon (weapons leave hand but do not go to discard)
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var destination = CardZoneType.DiscardPile;

            // Consume 1 AP if not a free action
            if (!plan.IsFreeAction)
            {
                EventManager.Publish(new ModifyActionPointsEvent { Delta = -1 });
                LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "APConsumed",
                    ["cardId"] = card.CardId
                });
            }

            if (deckEntity != null)
            {
                // Apply Frostbite when playing any Frozen card (including weapons)
                var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                if (cardEntity.GetComponent<Frozen>() != null)
                {
                    EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Frostbite, Delta = 1 });
                }

                bool isWeapon = card.IsWeapon;
                if (isWeapon)
                {
                    // Remove from hand without adding to discard/exhaust; stays out until re-added by phase rules
                    // CardZoneSystem will remove from lists when destination not specified; emulate by not re-adding
                    var deck = deckEntity.GetComponent<Deck>();
                    int handCountBeforeWeaponRemove = deck?.Hand.Count ?? 0;
                    deck?.Hand.Remove(cardEntity);
                    LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                    {
                        ["reason"] = "WeaponUsed",
                        ["cardId"] = card.CardId,
                        ["entityId"] = cardEntity.Id,
                        ["handCountBeforeMove"] = handCountBeforeWeaponRemove,
                        ["handCountAfterMove"] = deck?.Hand.Count ?? 0,
                        ["stillInHandAfterMove"] = deck?.Hand.Contains(cardEntity) ?? false,
                        ["card"] = HandStateLoggingService.BuildCardSnapshot(cardEntity)
                    });
                    EntityManager.DestroyEntity(cardEntity.Id);
                    return;
                }
                else {
                    if (cardEntity.GetComponent<MarkedForReturnToDeck>() != null)
                    {
                        destination = CardZoneType.DrawPile;
                        EventManager.Publish(new DeckShuffleEvent { Deck = deckEntity });
                        LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                        {
                            ["reason"] = "CardReturnedToDeck",
                            ["cardId"] = card.CardId
                        });
                        EntityManager.RemoveComponent<MarkedForReturnToDeck>(cardEntity);
                    }
                    else if (cardEntity.GetComponent<MarkedForBottomOfDrawPile>() != null)
                    {
                        destination = CardZoneType.DrawPile;
                        LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                        {
                            ["reason"] = "CardReturnedToBottomOfDeck",
                            ["cardId"] = card.CardId
                        });
                    }
                    if (cardEntity.GetComponent<MarkedForExhaust>() != null)
                    {
                        destination = CardZoneType.ExhaustPile;
                        LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                        {
                            ["reason"] = "CardExhausted",
                            ["cardId"] = card.CardId
                        });
                        EntityManager.RemoveComponent<MarkedForExhaust>(cardEntity);
                    }
                }
                var deckBeforeMove = deckEntity.GetComponent<Deck>();
                LoggingService.Append("CardPlaySystem.OnPlayCardRequested.moveCard", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "PlayCard",
                    ["cardId"] = card.CardId,
                    ["entityId"] = cardEntity.Id,
                    ["destination"] = destination.ToString(),
                    ["handCountBeforeMove"] = deckBeforeMove?.Hand.Count ?? 0,
                    ["stillInHandBeforeMove"] = deckBeforeMove?.Hand.Contains(cardEntity) ?? false,
                    ["card"] = HandStateLoggingService.BuildCardSnapshot(cardEntity)
                });
                EventManager.Publish(new CardMoveRequested { Card = cardEntity, Deck = deckEntity, Destination = destination, Reason = "PlayCard" });
            }
        }

        private void OnPayCostSatisfied(PayCostSatisfied evt)
        {
            if (evt?.CardToPlay == null) return;

            ComponentLoggerService.LogEntity(evt.CardToPlay, "PayCostSatisfied - proceeding to play");
            if (evt.PaymentCards != null)
            {
                LoggingService.Append("CardPlaySystem.OnPayCostSatisfied", new System.Text.Json.Nodes.JsonObject
                {
                    ["paymentCardCount"] = evt.PaymentCards.Count
                });
            }

            // Once costs are paid, proceed to resolve effect by re-publishing play with CostsPaid
            EventManager.Publish(new PlayCardRequested { Card = evt.CardToPlay, CostsPaid = true, PaymentCards = evt.PaymentCards });
        }

        private void AttachPlayStatContext(Entity card, List<Entity> paymentCards)
        {
            if (card == null) return;
            RemovePlayStatContext(card);
            EntityManager.AddComponent(card, new CardPlayStatContext
            {
                Owner = card,
                PaymentCards = paymentCards != null
                    ? new List<Entity>(paymentCards)
                    : new List<Entity>(),
            });
        }

        private void RemovePlayStatContext(Entity card)
        {
            if (card?.GetComponent<CardPlayStatContext>() == null) return;
            EntityManager.RemoveComponent<CardPlayStatContext>(card);
        }

        
    }
}
