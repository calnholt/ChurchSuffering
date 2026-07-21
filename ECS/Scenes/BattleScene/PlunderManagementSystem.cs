using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Handles the Plunder mechanic for enemies like Wyvern.
    /// During preblock, steals a random card from the player's deck.
    /// The player can rescue the card by dealing enough damage to the enemy.
    /// </summary>
    public class PlunderManagementSystem : Core.System
    {
        private static readonly Random _random = new Random();
        private readonly GraphicsDevice _graphicsDevice;

        // Pending animation state
        private Entity _pendingCard;
        private int _pendingThreshold;

        public PlunderManagementSystem(EntityManager entityManager, GraphicsDevice graphicsDevice) : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<ModifyHpEvent>(OnModifyHp);
            EventManager.Subscribe<PlunderTriggerEvent>(OnPlunderTrigger);
            EventManager.Subscribe<PlunderSnatchAnimationCompleted>(OnAnimationCompleted);
            EventManager.Subscribe<PlunderForceDiscardEvent>(OnPlunderForceDiscard);
            EventManager.Subscribe<ResetDeckEvent>(_ => ClearAllPlunderState());
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current == SubPhase.PreBlock)
            {
                var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
                if (enemy == null) return;

                var ap = enemy.GetComponent<AppliedPassives>();
                if (ap == null || !ap.Passives.ContainsKey(AppliedPassiveType.Plunder)) return;

                // Trigger plunder
                EventManager.Publish(new PlunderTriggerEvent { Enemy = enemy });
            }
            else if (evt.Current == SubPhase.PlayerStart)
            {
                // Reset damage tracking at start of player turn
                ResetDamageTracking();
            }
        }

        private void OnPlunderTrigger(PlunderTriggerEvent evt)
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;

            var deck = deckEntity.GetComponent<Deck>();
            if (deck?.DrawPile == null || deck.DrawPile.Count == 0)
            {
                LoggingService.Append("PlunderManagementSystem.OnPlunderTrigger", new System.Text.Json.Nodes.JsonObject { ["message"] = "no cards in deck to plunder" });
                return;
            }

            // If there's already a plundered card, discard it first
            var existingPlundered = EntityManager.GetEntitiesWithComponent<Plundered>().FirstOrDefault();
            if (existingPlundered != null)
            {
                DiscardPlunderedCard(existingPlundered, deckEntity);
            }

            // Get a random card from the draw pile (excluding weapon cards)
            var eligibleCards = deck.DrawPile
                .Where(c => c.GetComponent<Plundered>() == null)
                .Where(c => (c.GetComponent<CardData>()?.Card.IsWeapon ?? false) == false)
                .ToList();

            if (eligibleCards.Count == 0)
            {
                LoggingService.Append("PlunderManagementSystem.OnPlunderTrigger", new System.Text.Json.Nodes.JsonObject { ["message"] = "no eligible cards to plunder" });
                return;
            }

            var cardToPlunder = eligibleCards[_random.Next(eligibleCards.Count)];
            var threshold = _random.Next(4, 9); // 4 to 8 inclusive

            // Remove from draw pile (but don't add to any zone - it's "held" by the wyvern)
            deck.DrawPile.Remove(cardToPlunder);

            var cardData = cardToPlunder.GetComponent<CardData>();
            LoggingService.Append("PlunderManagementSystem.OnPlunderTrigger.plundered", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card.CardId ?? "unknown", ["threshold"] = threshold });

            // Calculate animation positions - get draw pile position from UI entity
            var startPos = ResolveDrawPileAnchor();

            // Get enemy position for target
            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            var enemyTransform = enemy?.GetComponent<Transform>();
            var enemyPos = enemyTransform?.Position ?? new Vector2(400, 300);
            var targetPos = new Vector2(enemyPos.X + 180, enemyPos.Y - 20); // Match PlunderDisplaySystem offsets

            // Store pending state for when animation completes
            _pendingCard = cardToPlunder;
            _pendingThreshold = threshold;

            // Queue animation through event queue so game waits for it
            EventQueueBridge.EnqueueTriggerAction("PlunderManagementSystem.SnatchAnimation", () =>
            {
                EventManager.Publish(new PlunderSnatchAnimationRequested
                {
                    Card = cardToPlunder,
                    StartPos = startPos,
                    TargetPos = targetPos,
                    DamageThreshold = threshold
                });
            }, 0f);
        }

        private void OnAnimationCompleted(PlunderSnatchAnimationCompleted evt)
        {
            if (evt.Card == null) return;

            // Remove flight component
            if (evt.Card.HasComponent<PlunderSnatchFlight>())
            {
                EntityManager.RemoveComponent<PlunderSnatchFlight>(evt.Card);
            }

            // Add Plundered component now that animation is done
            if (!evt.Card.HasComponent<Plundered>())
            {
                EntityManager.AddComponent(evt.Card, new Plundered
                {
                    Owner = evt.Card,
                    DamageThreshold = evt.DamageThreshold,
                    DamageDealt = 0
                });

                // Add HP component for PlunderDisplaySystem to render the gauge (starts full, counts down)
                EntityManager.AddComponent(evt.Card, new HP
                {
                    Owner = evt.Card,
                    Current = evt.DamageThreshold,
                    Max = evt.DamageThreshold
                });
            }

            var cardData = evt.Card.GetComponent<CardData>();
            LoggingService.Append("PlunderManagementSystem.OnAnimationCompleted", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card.CardId ?? "unknown" });

            // Now publish the PlunderCardEvent to notify other systems
            EventManager.Publish(new PlunderCardEvent { Card = evt.Card, DamageThreshold = evt.DamageThreshold });

            // Clear pending state
            _pendingCard = null;
            _pendingThreshold = 0;
        }

        private void OnPlunderForceDiscard(PlunderForceDiscardEvent evt)
        {
            var plunderedCard = EntityManager.GetEntitiesWithComponent<Plundered>().FirstOrDefault();
            if (plunderedCard == null) return;

            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;

            DiscardPlunderedCard(plunderedCard, deckEntity);
        }

        private void OnModifyHp(ModifyHpEvent evt)
        {
            // Only track damage dealt to enemy (negative delta = damage)
            if (evt.Delta >= 0) return;

            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            if (enemy == null || evt.Target != enemy) return;

            // Check if there's a plundered card
            var plunderedCard = EntityManager.GetEntitiesWithComponent<Plundered>().FirstOrDefault();
            if (plunderedCard == null) return;

            var plundered = plunderedCard.GetComponent<Plundered>();
            if (plundered == null) return;

            // Add damage dealt
            int damageDealt = Math.Abs(evt.Delta);
            plundered.DamageDealt += damageDealt;

            // Sync HP component for PlunderDisplaySystem (count down)
            var hp = plunderedCard.GetComponent<HP>();
            if (hp != null)
            {
                hp.Current = plundered.DamageThreshold - plundered.DamageDealt;
            }

            var cardData = plunderedCard.GetComponent<CardData>();
            LoggingService.Append("PlunderManagementSystem.OnModifyHp", new System.Text.Json.Nodes.JsonObject { ["damageDealt"] = damageDealt, ["total"] = $"{plundered.DamageDealt}/{plundered.DamageThreshold}" });

            // Check if threshold reached
            if (plundered.DamageDealt >= plundered.DamageThreshold)
            {
                RescueCard(plunderedCard);
            }
        }

        private void RescueCard(Entity card)
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;

            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return;

            // Remove Plundered, HP, and HPBarOverride components
            EntityManager.RemoveComponent<Plundered>(card);
            EntityManager.RemoveComponent<HP>(card);
            EntityManager.RemoveComponent<HPBarOverride>(card);

            // Add to player's hand and restore interactability
            deck.Hand.Add(card);
            var ui = card.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.SuppressCount = 0;
                ui.IsInteractable = true;
                ui.IsHovered = false;
                ui.IsClicked = false;
                ui.EventType = UIElementEventType.CardClicked;
            }

            var cardData = card.GetComponent<CardData>();
            LoggingService.Append("PlunderManagementSystem.RescueCard", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card.CardId ?? "unknown" });

            EventManager.Publish(new PlunderRescueEvent { Card = card });
        }

        private void DiscardPlunderedCard(Entity card, Entity deckEntity)
        {
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return;

            // Calculate animation start position (current plundered card display position)
            var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
            var enemyTransform = enemy?.GetComponent<Transform>();
            var enemyPos = enemyTransform?.Position ?? new Vector2(400, 300);
            var startPos = new Vector2(enemyPos.X + 180, enemyPos.Y - 20);

            // Remove Plundered, HP, and HPBarOverride components
            EntityManager.RemoveComponent<Plundered>(card);
            EntityManager.RemoveComponent<HP>(card);
            EntityManager.RemoveComponent<HPBarOverride>(card);

            // Set card position for animation start
            var transform = card.GetComponent<Transform>();
            if (transform != null)
            {
                transform.Position = startPos;
            }

            // Add animating marker component
            if (!card.HasComponent<AnimatingHandToZone>())
            {
                EntityManager.AddComponent(card, new AnimatingHandToZone { Destination = CardZoneType.DiscardPile });
            }

            var cardData = card.GetComponent<CardData>();
            LoggingService.Append("PlunderManagementSystem.DiscardPlunderedCard", new System.Text.Json.Nodes.JsonObject { ["cardId"] = cardData?.Card.CardId ?? "unknown" });

            // Queue animation - reuse CardMoveDisplaySystem
            EventQueueBridge.EnqueueTriggerAction("PlunderManagementSystem.DiscardAnimation", () =>
            {
                EventManager.Publish(new PlayCardToDiscardAnimationRequested
                {
                    Card = card,
                    Deck = deckEntity,
                    ContextId = "plunder_discard"
                });
            }, 0f);
        }

        private void ResetDamageTracking()
        {
            var plunderedCard = EntityManager.GetEntitiesWithComponent<Plundered>().FirstOrDefault();
            if (plunderedCard == null) return;

            var plundered = plunderedCard.GetComponent<Plundered>();
            if (plundered != null)
            {
                plundered.DamageDealt = 0;

                // Reset HP.Current back to Max so the bar is full again
                var hp = plunderedCard.GetComponent<HP>();
                if (hp != null)
                {
                    hp.Current = hp.Max;
                }

                LoggingService.Append("PlunderManagementSystem.ResetDamageTracking", new System.Text.Json.Nodes.JsonObject { ["message"] = "damage tracking reset for new turn" });
            }
        }

        /// <summary>
        /// Strips battle-scoped plunder state so cards do not carry Plundered into the next battle.
        /// Called on <see cref="ResetDeckEvent"/> (published from InitBattle only).
        /// </summary>
        private void ClearAllPlunderState()
        {
            _pendingCard = null;
            _pendingThreshold = 0;

            var cards = new HashSet<Entity>();
            foreach (var c in EntityManager.GetEntitiesWithComponent<Plundered>())
            {
                cards.Add(c);
            }
            foreach (var c in EntityManager.GetEntitiesWithComponent<PlunderSnatchFlight>())
            {
                cards.Add(c);
            }
            foreach (var c in EntityManager.GetEntitiesWithComponent<PlunderRescueFlight>())
            {
                cards.Add(c);
            }

            foreach (var card in cards)
            {
                if (card == null || !card.IsActive) continue;
                StripPlunderStateFromCard(card);
            }

            if (cards.Count > 0)
            {
                LoggingService.Append("PlunderManagementSystem.ClearAllPlunderState", new System.Text.Json.Nodes.JsonObject
                {
                    ["cardsCleared"] = cards.Count
                });
            }
        }

        private void StripPlunderStateFromCard(Entity card)
        {
            if (card.HasComponent<Plundered>())
            {
                EntityManager.RemoveComponent<Plundered>(card);
            }
            if (card.HasComponent<HP>())
            {
                EntityManager.RemoveComponent<HP>(card);
            }
            if (card.HasComponent<HPBarOverride>())
            {
                EntityManager.RemoveComponent<HPBarOverride>(card);
            }
            if (card.HasComponent<PlunderSnatchFlight>())
            {
                EntityManager.RemoveComponent<PlunderSnatchFlight>(card);
            }
            if (card.HasComponent<PlunderRescueFlight>())
            {
                EntityManager.RemoveComponent<PlunderRescueFlight>(card);
            }
            if (card.HasComponent<AnimatingHandToZone>())
            {
                EntityManager.RemoveComponent<AnimatingHandToZone>(card);
            }

            var ui = card.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.SuppressCount = 0;
                ui.IsInteractable = false;
                ui.IsHovered = false;
                ui.IsClicked = false;
                ui.EventType = UIElementEventType.None;
            }

            var cardData = card.GetComponent<CardData>();
            LoggingService.Append("PlunderManagementSystem.StripPlunderStateFromCard", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = cardData?.Card.CardId ?? "unknown"
            });
        }

        private Vector2 ResolveDrawPileAnchor()
        {
            var root = EntityManager.GetEntity("UI_DrawPileRoot");
            var tr = root?.GetComponent<Transform>();
            if (tr != null) return tr.Position;
            return new Vector2(Game1.VirtualWidth - 60, Game1.VirtualHeight - 60);
        }
    }
}
