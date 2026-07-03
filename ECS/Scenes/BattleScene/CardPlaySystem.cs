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
        /// Counts distinct ways to satisfy cost requirements (order-independent multisets).
        /// Returns the count of distinct solutions, stopping early if count > 1 for optimization.
        /// </summary>
        private int CountDistinctSolutions(List<string> requiredCosts, List<Entity> handCards)
        {
            // Safety check: exclude Yellow cards
            var validCards = handCards.Where(e => 
            {
                var cd = e.GetComponent<CardData>();
                return cd != null && cd.Color != CardData.CardColor.Yellow;
            }).ToList();

            if (requiredCosts.Count == 0)
                return validCards.Count == 0 ? 1 : 0; // Empty requirement means no cards needed

            int count = 0;
            HashSet<string> seenSolutions = new HashSet<string>();

            // Recursive helper to count solutions
            void CountSolutionsRecursive(List<string> remainingCosts, List<Entity> availableCards, HashSet<Entity> usedCards, List<Entity> currentSolution)
            {
                // Early exit optimization: if we already found more than 1 solution, stop
                if (count > 1) return;

                if (remainingCosts.Count == 0)
                {
                    // Found a valid solution - create a canonical representation (sorted by entity ID)
                    var solutionKey = string.Join(",", currentSolution.OrderBy(e => e.Id).Select(e => e.Id.ToString()));
                    if (!seenSolutions.Contains(solutionKey))
                    {
                        seenSolutions.Add(solutionKey);
                        count++;
                    }
                    return;
                }

                string nextCost = remainingCosts[0];
                var remainingCostsAfter = remainingCosts.Skip(1).ToList();

                // Find all cards that can satisfy this cost requirement
                var candidates = availableCards.Where(card => 
                {
                    if (usedCards.Contains(card)) return false;
                    return CardColorQualificationService.IsEligibleForCost(card, nextCost);
                }).ToList();

                // Try each candidate for this requirement
                foreach (var candidate in candidates)
                {
                    var newUsedCards = new HashSet<Entity>(usedCards) { candidate };
                    var newSolution = new List<Entity>(currentSolution) { candidate };
                    CountSolutionsRecursive(remainingCostsAfter, availableCards, newUsedCards, newSolution);
                    
                    // Early exit optimization
                    if (count > 1) return;
                }
            }

            CountSolutionsRecursive(requiredCosts, validCards, new HashSet<Entity>(), new List<Entity>());
            return count;
        }

        /// <summary>
        /// Finds the first valid solution for satisfying cost requirements.
        /// Assumes count == 1, so returns the single solution.
        /// </summary>
        private List<Entity> FindFirstSolution(List<string> requiredCosts, List<Entity> handCards)
        {
            // Safety check: exclude Yellow cards
            var validCards = handCards.Where(e => 
            {
                var cd = e.GetComponent<CardData>();
                return cd != null && cd.Color != CardData.CardColor.Yellow;
            }).ToList();

            if (requiredCosts.Count == 0)
                return new List<Entity>();

            List<Entity> solution = null;

            // Recursive helper to find first solution
            bool FindSolutionRecursive(List<string> remainingCosts, List<Entity> availableCards, HashSet<Entity> usedCards, List<Entity> currentSolution)
            {
                if (remainingCosts.Count == 0)
                {
                    solution = new List<Entity>(currentSolution);
                    return true; // Found solution
                }

                string nextCost = remainingCosts[0];
                var remainingCostsAfter = remainingCosts.Skip(1).ToList();

                // Find all cards that can satisfy this cost requirement
                var candidates = availableCards.Where(card => 
                {
                    if (usedCards.Contains(card)) return false;
                    return CardColorQualificationService.IsEligibleForCost(card, nextCost);
                }).ToList();

                // Try each candidate for this requirement
                foreach (var candidate in candidates)
                {
                    var newUsedCards = new HashSet<Entity>(usedCards) { candidate };
                    var newSolution = new List<Entity>(currentSolution) { candidate };
                    if (FindSolutionRecursive(remainingCostsAfter, availableCards, newUsedCards, newSolution))
                    {
                        return true; // Found solution
                    }
                }

                return false; // No solution found
            }

            FindSolutionRecursive(requiredCosts, validCards, new HashSet<Entity>(), new List<Entity>());
            return solution ?? new List<Entity>();
        }

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

            // Only in Action phase
            var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault().GetComponent<PhaseState>();
            if (phase.Sub != SubPhase.Action)
            {
                LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "InvalidPhase",
                    ["phase"] = phase.Sub.ToString()
                });
                return;
            }

            var data = evt.Card.GetComponent<CardData>();
            if (data == null) return;

            // The entity's card is the initialized runtime definition. It includes
            // upgrade mutations and any state captured by its gameplay delegates.
            var card = data.Card;
            if (card == null || string.IsNullOrEmpty(card.CardId)) return;


            if (card.Type == CardType.Relic)
            {
                EventManager.Publish(new CantPlayCardMessage { Message = "Relics can only be discarded to pay for costs!" });
                return;
            }
            if (card.Type == CardType.Block)
            {
                EventManager.Publish(new CantPlayCardMessage { Message = "Block cards can only be used to block!" });
                return;
            }

            var pledge = evt.Card.GetComponent<Pledge>();
            if (pledge != null && !pledge.CanPlay)
            {
                EventManager.Publish(new CantPlayCardMessage { Message = "You can't play a card you pledged this turn!" });
                return;
            }

            if (evt.Card.HasComponent<Pledge>())
            {
                var appliedPassives = GetComponentHelper.GetAppliedPassives(EntityManager, "Player");
                if (appliedPassives != null
                    && appliedPassives.Passives.TryGetValue(AppliedPassiveType.Silenced, out int silencedStacks)
                    && silencedStacks > 0)
                {
                    EventManager.Publish(new CantPlayCardMessage { Message = "You cannot play pledged cards because you are silenced!" });
                    return;
                }
            }

            // Capture sealed state before playing (will be used after play resolves to apply HP cost)
            var sealedComp = evt.Card.GetComponent<Sealed>();
            int sealCount = sealedComp?.Seals ?? 0;

            // Weapons can only be played during Action phase (already gated) and cannot be used to pay costs of other cards
            bool isWeapon = card.IsWeapon;

            // Gate by Action Points unless the card is a free action
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            var ap = player?.GetComponent<ActionPoints>();
            bool isFree = card.IsFreeAction;
            if (!isFree)
            {
                int currentAp = ap?.Current ?? 0;
                if (currentAp <= 0)
                {
                    LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                    {
                        ["reason"] = "NoActionPoints",
                        ["cardId"] = card.CardId,
                        ["isWeapon"] = isWeapon,
                        ["isFree"] = isFree,
                        ["currentAp"] = currentAp
                    });
                    EventManager.Publish(new CantPlayCardMessage { Message = "Not enough action points!" });
                    return;
                }
            }

            // Evaluate any additional costs/requirements tied to the card id
            if (card.CanPlay(EntityManager, evt.Card) == false)
            {
                card.OnCantPlay?.Invoke(EntityManager, evt.Card);
                LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "CantPlay",
                    ["cardId"] = card.CardId
                });
                return;
            }

            // If costs are not yet paid, determine if payment is needed and either auto-resolve or open overlay
            if (!evt.CostsPaid)
            {
                var deckEntityForCost = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
                var deck = deckEntityForCost?.GetComponent<Deck>();
                var cardId = evt.Card.GetComponent<CardData>().Card.CardId;
                // gonna cloodge this in for now
                if (card.SpecialAction == "SelectOneCardFromHand")
                {
                    EventManager.Publish(new OpenPayCostOverlayEvent { CardToPlay = evt.Card, RequiredCosts = ["Any"], Type = PayCostOverlayType.SelectOneCard });
                    return;
                }
                var requiredCosts = VigorService.GetEffectiveCost(card, VigorService.GetPlayerVigorStacks(EntityManager));
                if (requiredCosts.Count > 0 && deck != null)
                {
                    // Build hand color multiset excluding the card being played
                    var handOthers = deck.Hand.Where(c => c != evt.Card).ToList();
                    // Exclude weapons and tokens from being considered as payment candidates
                    List<Entity> handNonWeapons = new List<Entity>();
                    foreach (var e in handOthers)
                    {
                        var paymentCard = e.GetComponent<CardData>()?.Card;
                        if (paymentCard == null || paymentCard.CanDiscardForCost)
                        {
                            handNonWeapons.Add(e);
                        }
                    }

                    // Exclude Yellow cards - they cannot be discarded/used to pay costs
                    handNonWeapons = handNonWeapons.Where(e => e.GetComponent<CardData>()?.Color != CardData.CardColor.Yellow).ToList();

                    // Exclude pledged cards - they cannot be used to pay costs
                    handNonWeapons = handNonWeapons.Where(e => e.GetComponent<Pledge>() == null).ToList();


                    // Helper to attempt greedy satisfaction of remaining requirements
                    bool CanSatisfy(List<string> req, List<Entity> candidates, out List<Entity> picks)
                    {
                        picks = new List<Entity>();
                        var remaining = new List<string>(req);
                        // Prefer matching specific colors first
                        foreach (var e in candidates)
                        {
                            if (remaining.Count == 0) break;
                            int idx = remaining.FindIndex(r =>
                                r != "Any" && CardColorQualificationService.IsEligibleForCost(e, r));
                            if (idx >= 0)
                            {
                                picks.Add(e);
                                remaining.RemoveAt(idx);
                            }
                        }
                        // Then satisfy Any with remaining cards
                        foreach (var e in candidates)
                        {
                            if (remaining.Count == 0) break;
                            if (!picks.Contains(e))
                            {
                                int idx = remaining.FindIndex(r => r == "Any");
                                if (idx >= 0)
                                {
                                    picks.Add(e);
                                    remaining.RemoveAt(idx);
                                }
                            }
                        }
                        return remaining.Count == 0;
                    }

                    bool canSatisfy = CanSatisfy(requiredCosts, handNonWeapons, out _);

                    if (!canSatisfy)
                    {
                        LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                        {
                            ["reason"] = "CannotSatisfyCost"
                        });
                        EventManager.Publish(new CantPlayCardMessage { Message = "Can't pay card's cost!" });
                        return;
                    }

                    // Count distinct ways to satisfy the cost (order-independent)
                    int solutionCount = CountDistinctSolutions(requiredCosts, handNonWeapons);

                    if (solutionCount == 0)
                    {
                        LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                        {
                            ["reason"] = "NoSolutionForCost"
                        });
                        EventManager.Publish(new CantPlayCardMessage { Message = "Can't pay card's cost!" });
                        return;
                    }
                    else if (solutionCount == 1)
                    {
                        // Exactly one way to satisfy cost - auto-pay
                        var solution = FindFirstSolution(requiredCosts, handNonWeapons);
                        LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                        {
                            ["reason"] = "AutoPayCost",
                            ["paymentCount"] = solution.Count
                        });
                        foreach (var c in solution)
                        {
                            EventManager.Publish(new CardDiscardedForCostEvent { Card = c });
                            EventManager.Publish(new CardMoveRequested { Card = c, Deck = deckEntityForCost, Destination = CardZoneType.DiscardPile, Reason = "AutoPayCost" });
                            var cardData = c.GetComponent<CardData>();
                            if (cardData != null && cardData.Card.OnDiscardedForCost != null)
                            {
                                cardData.Card.OnDiscardedForCost(EntityManager, c);
                            }
                        }
                        
                        // Populate payment cache so card effects can reference what was paid
                        var cache = EnsurePaymentCacheExists();
                        cache.CardPlayed = evt.Card;
                        cache.PaymentCards = new List<Entity>(solution);
                        cache.HasData = true;

                        ComponentLoggerService.LogComponent(cache, $"Auto-pay complete, {solution.Count} cards discarded");
                        
                        EventManager.Publish(new PlayCardRequested { Card = evt.Card, CostsPaid = true, PaymentCards = new List<Entity>(solution) });
                        return;
                    }
                    else
                    {
                        // Multiple ways to satisfy cost - show overlay for player choice
                        LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                        {
                            ["reason"] = "MultipleCostSolutions",
                            ["solutionCount"] = solutionCount
                        });
                        EventManager.Publish(new OpenPayCostOverlayEvent { CardToPlay = evt.Card, RequiredCosts = requiredCosts, Type = PayCostOverlayType.ColorDiscard });
                        return;
                    }
                }
            }

            if (card.VisualEffectRecipe != null)
            {
                var request = VisualEffectRequestFactory.ForCard(EntityManager, evt.Card, card.VisualEffectRecipe);
                if (request != null)
                {
                    EventManager.Publish(request);
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

            ResolveAcceptedCardPlay(evt.Card, evt.PaymentCards, sealCount, VigorService.GetPlayerVigorStacks(EntityManager));
        }

        private void ResolveAcceptedCardPlay(
            Entity cardEntity,
            List<Entity> paymentCards,
            int sealCount,
            int vigorStacksAtPlay)
        {
            if (cardEntity == null) return;
            var data = cardEntity.GetComponent<CardData>();
            var card = data?.Card;
            if (card == null || string.IsNullOrEmpty(card.CardId)) return;

            ComponentLoggerService.LogEntity(cardEntity, "Executing card OnPlay effect");
            AttachPlayStatContext(cardEntity, paymentCards);
            try
            {
                card.OnPlay?.Invoke(EntityManager, cardEntity);
            }
            finally
            {
                RemovePlayStatContext(cardEntity);
            }
            EventManager.Publish(new CardPlayedEvent { Card = cardEntity, VigorStacksAtPlay = vigorStacksAtPlay });
            bool isCurseCard = string.Equals(card.CardId, Curse.CardIdValue, StringComparison.OrdinalIgnoreCase);
            if (!isCurseCard && !GuidedTutorialService.IsActive(EntityManager))
                EventManager.Publish(new TrackingEvent { Type = card.CardId, Delta = 1 });

            // If the card was sealed, apply HP cost and remove the seal
            if (sealCount > 0)
            {
                var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                EventManager.Publish(new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = player,
                    Delta = -sealCount,
                    DamageType = ModifyTypeEnum.Effect,
                    IgnoresAegis = true
                });
                var sealedComp = cardEntity.GetComponent<Sealed>();
                if (sealedComp != null)
                    EntityManager.RemoveComponent<Sealed>(cardEntity);
                LoggingService.Append("CardPlaySystem.OnPlayCardRequested", new System.Text.Json.Nodes.JsonObject
                {
                    ["reason"] = "SealRemoved",
                    ["cardId"] = card.CardId,
                    ["sealCost"] = sealCount
                });
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
            bool isFree = card.IsFreeAction;
            if (!isFree)
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
