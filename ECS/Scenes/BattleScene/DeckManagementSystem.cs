using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.Diagnostics;
using Microsoft.Xna.Framework;
using System.Collections.Generic;
using System.Linq;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// System for managing deck operations like shuffling, drawing, and discarding
    /// </summary>
    public class DeckManagementSystem : Core.System
    {
        public DeckManagementSystem(EntityManager entityManager) : base(entityManager)
        {
            // Subscribe to deck management events
            EventManager.Subscribe<DeckShuffleDrawEvent>(OnDeckShuffleDrawEvent);
            EventManager.Subscribe<RequestDrawCardsEvent>(OnRequestDrawCards);
            EventManager.Subscribe<RedrawHandEvent>(OnRedrawHandEvent);
            EventManager.Subscribe<DeckShuffleEvent>(OnDeckShuffleEvent);
            EventManager.Subscribe<ResetDeckEvent>(OnResetDeckEvent);
            EventManager.Subscribe<RemoveTopCardFromDrawPileRequested>(OnRemoveTopCardFromDrawPileRequested);
            EventManager.Subscribe<DiscardAllCardsEvent>(OnDiscardAllCards);
            EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
            EventManager.Subscribe<RemoveRandomCardEvent>(OnRemoveRandomCard);
            EventManager.Subscribe<DrawRandomCardFromDiscardEvent>(OnDrawRandomCardFromDiscard);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<Deck>();
        }

        private void OnRequestDrawCards(RequestDrawCardsEvent evt)
        {
            // Find the first deck and draw count cards
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return;

            int drawn = DrawCards(deck, Math.Max(1, evt.Count));

            LoggingService.Append("DeckManagementSystem.OnRequestDrawCards", new System.Text.Json.Nodes.JsonObject
            {
                ["requestedCount"] = evt.Count,
                ["drawnCount"] = drawn,
                ["handCount"] = deck.Hand.Count,
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardCount"] = deck.DiscardPile.Count
            });
        }

        private void OnRedrawHandEvent(RedrawHandEvent evt)
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return;

            // Move current hand to discard, then reshuffle, then draw
            // Move current hand to discard and reset their transforms, so re-drawn cards animate from spawn
            foreach (var c in deck.Hand)
            {
                var t = c.GetComponent<Transform>();
                if (t != null)
                {
                    t.Position = Vector2.Zero;
                    t.Rotation = 0f;
                }
            }
            deck.DiscardPile.AddRange(deck.Hand);
            deck.Hand.Clear();
            ShuffleDrawPile(deck);
            DrawCards(deck, evt.DrawCount);

            EventManager.Publish(new CardsDrawnEvent
            {
                Deck = deckEntity,
                DrawnCards = deck.Hand.ToList()
            });

            LoggingService.Append("DeckManagementSystem.OnRedrawHandEvent", new System.Text.Json.Nodes.JsonObject
            {
                ["drawCount"] = evt.DrawCount,
                ["handCount"] = deck.Hand.Count,
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardCount"] = deck.DiscardPile.Count
            });
        }
        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        /// <summary>
        /// Shuffles the draw pile
        /// </summary>
        public void ShuffleDrawPile(Deck deck)
        {
            var random = new System.Random();
            var cards = deck.DrawPile.ToList();

            for (int i = cards.Count - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                var temp = cards[i];
                cards[i] = cards[j];
                cards[j] = temp;
            }

            deck.DrawPile.Clear();
            deck.DrawPile.AddRange(cards);

            LoggingService.Append("DeckManagementSystem.ShuffleDrawPile", new System.Text.Json.Nodes.JsonObject
            {
                ["shuffledCount"] = cards.Count,
                ["drawPileCount"] = deck.DrawPile.Count
            });
        }

        /// <summary>
        /// Draws a card from the draw pile to the hand
        /// </summary>
        public bool DrawCard(Deck deck)
        {
            if (deck.DrawPile.Count == 0)
            {
                LoggingService.Append("DeckManagementSystem.DrawCard", new System.Text.Json.Nodes.JsonObject
                {
                    ["result"] = "failed",
                    ["reason"] = "drawPileEmpty",
                    ["handCount"] = deck.Hand.Count,
                    ["drawPileCount"] = 0,
                    ["discardPileCount"] = deck.DiscardPile.Count
                });
                return false; // No cards to draw
            }
            if (deck.DrawPile.Count > 0)
            {
                var card = deck.DrawPile[0];
                deck.DrawPile.RemoveAt(0);
                CardTransientStateService.ClearHandVisibilityFilters(EntityManager, card);
                CardTransientStateService.ClearAssignedBlockHotKey(EntityManager, card);
                if (card.GetComponent<CardData>()?.Card?.IsWeapon == true)
                {
                    deck.DiscardPile.Add(card);
                    LoggingService.Append("DeckManagementSystem.DrawCard", new System.Text.Json.Nodes.JsonObject
                    {
                        ["result"] = "skipped",
                        ["reason"] = "weaponCardInDrawPile",
                        ["cardId"] = card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                        ["handCount"] = deck.Hand.Count,
                        ["drawPileCount"] = deck.DrawPile.Count
                    });
                    return DrawCard(deck);
                }
                PromoteCardToHand(deck, card);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Moves up to amount random cards from discard pile to hand.
        /// Returns the number of cards actually moved.
        /// </summary>
        public int DrawRandomCardsFromDiscard(Deck deck, int amount)
        {
            if (deck == null || amount <= 0 || deck.DiscardPile.Count == 0) return 0;

            int toDraw = Math.Min(amount, deck.DiscardPile.Count);
            var picked = deck.DiscardPile
                .OrderBy(_ => Guid.NewGuid())
                .Take(toDraw)
                .ToList();

            int movedCount = 0;
            foreach (var card in picked)
            {
                if (!deck.DiscardPile.Remove(card)) continue;
                if (card.GetComponent<CardData>()?.Card?.IsWeapon == true)
                {
                    deck.DiscardPile.Add(card);
                    continue;
                }
                PromoteCardToHand(deck, card);
                movedCount++;
            }

            if (movedCount > 0)
            {
                EventManager.Publish(new CardsDrawnEvent
                {
                    Deck = deck.Owner,
                    DrawnCards = deck.Hand.ToList()
                });
            }

            LoggingService.Append("DeckManagementSystem.DrawRandomCardsFromDiscard", new System.Text.Json.Nodes.JsonObject
            {
                ["requestedAmount"] = amount,
                ["movedCount"] = movedCount,
                ["handCount"] = deck.Hand.Count,
                ["discardPileCount"] = deck.DiscardPile.Count
            });

            return movedCount;
        }

        private void PromoteCardToHand(Deck deck, Entity card)
        {
            CardTransientStateService.ClearHandVisibilityFilters(EntityManager, card);
            CardTransientStateService.ClearAssignedBlockHotKey(EntityManager, card);

            var transform = card.GetComponent<Transform>();
            if (transform != null)
            {
                var cvs = CardGeometryService.GetSettings(EntityManager);
                float cardW = cvs?.CardWidth ?? CardGeometrySettings.DefaultWidth;
                var spawn = new Vector2(Game1.VirtualWidth + (cardW * 1.5f), (float)Game1.VirtualHeight);
                transform.Position = spawn;
                transform.Rotation = 0f;
                var tween = card.GetComponent<PositionTween>();
                if (tween != null)
                {
                    tween.Current = spawn;
                    tween.Target = spawn;
                    tween.Initialized = true;
                }
            }
            deck.Hand.Add(card);
            EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.DrawCard, Volume = 0.5f });
            var ui = card.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.SuppressCount = 0;
                ui.IsInteractable = true;
                ui.IsHovered = false;
                ui.IsClicked = false;
                ui.EventType = UIElementEventType.CardClicked;
            }

            LoggingService.Append("DeckManagementSystem.PromoteCardToHand", new System.Text.Json.Nodes.JsonObject
            {
                ["result"] = "success",
                ["cardId"] = card?.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["entityId"] = card?.Id ?? -1,
                ["handCount"] = deck.Hand.Count,
                ["visibleHandCount"] = HandStateLoggingService.CountVisibleHand(deck.Hand),
                ["effectiveDrawHandCount"] = HandStateLoggingService.CountEffectiveDrawHand(deck.Hand),
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardPileCount"] = deck.DiscardPile.Count,
                ["spawnX"] = transform?.Position.X ?? 0,
                ["spawnY"] = transform?.Position.Y ?? 0,
                ["hasPositionTween"] = card?.HasComponent<PositionTween>() ?? false,
                ["card"] = HandStateLoggingService.BuildCardSnapshot(card)
            });
        }

        /// <summary>
        /// Discards a card from hand to discard pile
        /// </summary>
        public void DiscardCard(Deck deck, Entity card)
        {
            if (deck.Hand.Contains(card))
            {
                deck.Hand.Remove(card);
                deck.DiscardPile.Add(card);

                LoggingService.Append("DeckManagementSystem.DiscardCard", new System.Text.Json.Nodes.JsonObject
                {
                    ["cardId"] = card?.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                    ["handCount"] = deck.Hand.Count,
                    ["drawPileCount"] = deck.DrawPile.Count,
                    ["discardPileCount"] = deck.DiscardPile.Count
                });
            }
        }

        /// <summary>
        /// Draws multiple cards from the deck to the hand
        /// </summary>
        public int DrawCards(Deck deck, int count)
        {
            int drawnCount = 0;

            for (int i = 0; i < count; i++)
            {
                if (DrawCard(deck))
                {
                    drawnCount++;
                }
                else
                {
                    break; // No more cards to draw
                }
            }
            // Optionally publish CardsDrawnEvent reflecting current hand for UI updates
            EventManager.Publish(new CardsDrawnEvent
            {
                Deck = deck.Owner,
                DrawnCards = deck.Hand.ToList()
            });
            // Log for each card currently in hand as "drawn" (assuming these were newly drawn)
            // Alternatively, accumulate drawn cards in DrawCards and log only those - but current method logs what we know
            foreach (var card in deck.Hand.TakeLast(drawnCount))
            {
                LoggingService.Append("DeckManagementSystem.DrawCard", new System.Text.Json.Nodes.JsonObject
                {
                    ["result"] = "success",
                    ["cardId"] = card?.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                    ["entityId"] = card?.Id ?? -1,
                    ["handCount"] = deck.Hand.Count,
                    ["drawPileCount"] = deck.DrawPile.Count,
                    ["discardPileCount"] = deck.DiscardPile.Count
                });
            }
            LoggingService.Append("DeckManagementSystem.DrawCards", new System.Text.Json.Nodes.JsonObject
            {
                ["requestedCount"] = count,
                ["drawnCount"] = drawnCount,
                ["handCount"] = deck.Hand.Count,
                ["visibleHandCount"] = HandStateLoggingService.CountVisibleHand(deck.Hand),
                ["effectiveDrawHandCount"] = HandStateLoggingService.CountEffectiveDrawHand(deck.Hand),
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardPileCount"] = deck.DiscardPile.Count,
            });
 
            return drawnCount;
        }

        /// <summary>
        /// Event handler for deck shuffle event
        /// </summary>
        private void OnDeckShuffleEvent(DeckShuffleEvent evt)
        {
            // Support null Deck in event by defaulting to the first deck entity
            var deckEntity = evt.Deck ?? EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;

            ShuffleDrawPile(deck);

            LoggingService.Append("DeckManagementSystem.OnDeckShuffleEvent", new System.Text.Json.Nodes.JsonObject
            {
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardCount"] = deck.DiscardPile.Count
            });
        }

        /// <summary>
        /// Event handler for deck shuffle and draw events
        /// </summary>
        private void OnDeckShuffleDrawEvent(DeckShuffleDrawEvent evt)
        {
            var deck = evt.Deck.GetComponent<Deck>();
            if (deck != null)
            {
                var drawnCards = ShuffleAndDraw(deck, evt.DrawCount);

                // Publish event for cards drawn
                EventManager.Publish(new CardsDrawnEvent
                {
                    Deck = evt.Deck,
                    DrawnCards = deck.Hand.ToList()
                });

                LoggingService.Append("DeckManagementSystem.OnDeckShuffleDrawEvent", new System.Text.Json.Nodes.JsonObject
                {
                    ["requestedDrawCount"] = evt.DrawCount,
                    ["actualDrawn"] = drawnCards,
                    ["handCount"] = deck.Hand.Count
                });
            }
        }

        /// <summary>
        /// Shuffles the deck and draws the specified number of cards
        /// </summary>
        public int ShuffleAndDraw(Deck deck, int drawCount)
        {
            // First, move all cards from hand and discard pile back to draw pile
            deck.DrawPile.AddRange(deck.Hand);
            deck.DrawPile.AddRange(deck.DiscardPile);
            deck.Hand.Clear();
            deck.DiscardPile.Clear();

            // Shuffle the draw pile
            ShuffleDrawPile(deck);

            // Draw the specified number of cards
            int drawn = DrawCards(deck, drawCount);

            LoggingService.Append("DeckManagementSystem.ShuffleAndDraw", new System.Text.Json.Nodes.JsonObject
            {
                ["requestedDrawCount"] = drawCount,
                ["actualDrawn"] = drawn,
                ["handCount"] = deck.Hand.Count,
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardPileCount"] = deck.DiscardPile.Count
            });

            return drawn;
        }

        /// <summary>
        /// Moves all non-weapon cards from Hand and Discard back into the Draw pile and shuffles.
        /// Battle-scoped weapon entities are destroyed here so they never persist into the draw pile.
        /// </summary>
        private void ResetDeckExcludingWeapon(Deck deck)
        {
            if (deck == null) return;

            int weaponsDestroyed = DestroyBattleWeaponCards(deck);

            // Collect surviving cards from all zones (draw, hand, discard)
            var allCards = new List<Entity>();
            allCards.AddRange(deck.DrawPile);
            allCards.AddRange(deck.Hand);
            allCards.AddRange(deck.DiscardPile);

            var toReturn = new List<Entity>();
            var seen = new HashSet<Entity>();

            foreach (var c in allCards)
            {
                CardTransientStateService.ClearAssignedBlockHotKey(EntityManager, c);

                // Reset UI/transform state for ALL cards so they are hidden and reset
                var ui = c.GetComponent<UIElement>();
                if (ui != null)
                {
                    ui.IsInteractable = false;
                    ui.IsHovered = false;
                    ui.IsClicked = false;
                    ui.EventType = UIElementEventType.None;
                    // Move bounds off-screen and shrink to avoid accidental hit-tests
                    ui.Bounds = new Rectangle(-1000, -1000, 1, 1);
                }

                var t = c.GetComponent<Transform>();
                if (t != null)
                {
                    t.Position = Vector2.Zero;
                    t.Rotation = 0f;
                    t.Scale = Vector2.One;
                }

                if (c.GetComponent<CardData>()?.Card?.IsWeapon == true) continue;

                if (seen.Add(c))
                {
                    toReturn.Add(c);
                }
            }

            // Cards listed in deck.Cards but not in any zone (e.g. after location hub)
            if (deck.Cards != null)
            {
                foreach (var c in deck.Cards)
                {
                    if (c == null) continue;
                    CardTransientStateService.ClearAssignedBlockHotKey(EntityManager, c);
                    if (c.GetComponent<CardData>()?.Card?.IsWeapon == true) continue;
                    if (seen.Contains(c)) continue;
                    var ui = c.GetComponent<UIElement>();
                    if (ui != null)
                    {
                        ui.IsInteractable = false;
                        ui.IsHovered = false;
                        ui.IsClicked = false;
                        ui.EventType = UIElementEventType.None;
                        ui.Bounds = new Rectangle(-1000, -1000, 1, 1);
                    }
                    var t = c.GetComponent<Transform>();
                    if (t != null)
                    {
                        t.Position = Vector2.Zero;
                        t.Rotation = 0f;
                        t.Scale = Vector2.One;
                    }
                    if (seen.Add(c))
                    {
                        toReturn.Add(c);
                    }
                }
            }

            // Clear original zones entirely (removes weapon from hand/discard as well)
            deck.DrawPile.Clear();
            deck.Hand.Clear();
            deck.DiscardPile.Clear();

            // Add non-weapon cards to draw pile
            if (toReturn.Count > 0)
            {
                deck.DrawPile.AddRange(toReturn);
            }

            // Shuffle draw pile after returning
            ShuffleDrawPile(deck);

            LoggingService.Append("DeckManagementSystem.ResetDeckExcludingWeapon", new System.Text.Json.Nodes.JsonObject
            {
                ["totalCardsCollected"] = allCards.Count,
                ["cardsReturnedToDraw"] = toReturn.Count,
                ["weaponsDestroyed"] = weaponsDestroyed,
                ["handCount"] = deck.Hand.Count,
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardPileCount"] = deck.DiscardPile.Count
            });
        }

        /// <summary>
        /// Destroys every weapon card entity from deck zones and deck.Cards. Clears EquippedWeapon.SpawnedEntity.
        /// Weapons are re-spawned per battle by WeaponManagementSystem; they must not shuffle with the run deck.
        /// </summary>
        private int DestroyBattleWeaponCards(Deck deck)
        {
            var toDestroy = new HashSet<Entity>();
            var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
            var equipped = player?.GetComponent<EquippedWeapon>();

            void Consider(Entity card)
            {
                if (card == null || !card.IsActive) return;
                if (card.GetComponent<CardData>()?.Card?.IsWeapon == true)
                {
                    toDestroy.Add(card);
                }
            }

            if (equipped?.SpawnedEntity != null)
            {
                toDestroy.Add(equipped.SpawnedEntity);
            }

            foreach (var card in deck.DrawPile) Consider(card);
            foreach (var card in deck.Hand) Consider(card);
            foreach (var card in deck.DiscardPile) Consider(card);
            foreach (var card in deck.ExhaustPile) Consider(card);
            if (deck.Cards != null)
            {
                foreach (var card in deck.Cards) Consider(card);
            }

            foreach (var card in toDestroy)
            {
                deck.DrawPile.Remove(card);
                deck.Hand.Remove(card);
                deck.DiscardPile.Remove(card);
                deck.ExhaustPile.Remove(card);
                deck.Cards?.Remove(card);
                if (card.IsActive)
                {
                    EntityManager.DestroyEntity(card.Id);
                }
            }

            if (equipped != null)
            {
                equipped.SpawnedEntity = null;
            }

            return toDestroy.Count;
        }

        private void OnLoadScene(LoadSceneEvent evt)
        {
            if (evt.Scene == SceneId.Battle) return;
            DeactivateAllRunDeckCardPresentation();
        }

        private void DeactivateAllRunDeckCardPresentation()
        {
            var deck = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault()?.GetComponent<Deck>();
            if (deck == null) return;

            var cards = new HashSet<Entity>();
            cards.UnionWith(deck.DrawPile);
            cards.UnionWith(deck.Hand);
            cards.UnionWith(deck.DiscardPile);
            cards.UnionWith(deck.ExhaustPile);
            foreach (var c in deck.Cards)
            {
                if (c != null) cards.Add(c);
            }

            foreach (var card in cards)
            {
                if (card != null && card.IsActive)
                {
                    ResetCard(card);
                }
            }
        }

        private void ResetCard(Entity card)
        {
            var ui = card.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.IsInteractable = false;
                ui.IsHovered = false;
                ui.IsClicked = false;
                ui.EventType = UIElementEventType.None;
                // Move bounds off-screen and shrink to avoid accidental hit-tests
                ui.Bounds = new Rectangle(-1000, -1000, 1, 1);
            }

            var t = card.GetComponent<Transform>();
            if (t != null)
            {
                t.Position = Vector2.Zero;
                t.Rotation = 0f;
                t.Scale = Vector2.One;
            }
        }

        /// <summary>
        /// Event handler for ResetDeckEvent
        /// </summary>
        private void OnResetDeckEvent(ResetDeckEvent evt)
        {
            var deckEntity = evt.Deck ?? EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;

            ResetDeckExcludingWeapon(deck);

            LoggingService.Append("DeckManagementSystem.OnResetDeckEvent", new System.Text.Json.Nodes.JsonObject
            {
                ["handCount"] = deck.Hand.Count,
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardCount"] = deck.DiscardPile.Count
            });
        }

        /// <summary>
        /// Handles coordinated removal of the top draw card for mill animations.
        /// Does not insert the card into another zone; responder will handle placement later.
        /// </summary>
        private void OnRemoveTopCardFromDrawPileRequested(RemoveTopCardFromDrawPileRequested evt)
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null || deck.DrawPile.Count == 0) return;

            var card = deck.DrawPile[0];
            deck.DrawPile.RemoveAt(0);
            // Ensure UI of this card is non-interactive while in limbo
            var ui = card.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.IsInteractable = false;
                ui.IsHovered = false;
                ui.IsClicked = false;
                ui.EventType = UIElementEventType.None;
            }
            // Publish response for animation kickoff
            EventManager.Publish(new TopCardRemovedForMillEvent
            {
                Deck = deckEntity,
                Card = card
            });

            LoggingService.Append("DeckManagementSystem.OnRemoveTopCardFromDrawPileRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = card?.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["remainingDrawPile"] = deck.DrawPile.Count
            });
        }

        private void OnDiscardAllCards(DiscardAllCardsEvent evt)
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return;
            int discardedCount = deck.Hand.Count;
            foreach (var c in deck.Hand)
            {
                ResetCard(c);
            }
            deck.DiscardPile.AddRange(deck.Hand);
            deck.Hand.Clear();

            LoggingService.Append("DeckManagementSystem.OnDiscardAllCards", new System.Text.Json.Nodes.JsonObject
            {
                ["discardedCount"] = discardedCount,
                ["handCount"] = deck.Hand.Count,
                ["drawPileCount"] = deck.DrawPile.Count,
                ["discardPileCount"] = deck.DiscardPile.Count
            });
        }

        private void OnDrawRandomCardFromDiscard(DrawRandomCardFromDiscardEvent evt)
        {
            if (evt == null || evt.Amount <= 0) return;

            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            if (deckEntity == null) return;
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return;

            DrawRandomCardsFromDiscard(deck, evt.Amount);
        }

        private void OnRemoveRandomCard(RemoveRandomCardEvent evt)
        {
            if (evt == null || evt.Amount <= 0) return;

            RunDeckService.EnsureRunDeck(EntityManager);

            var candidates = EntityManager
                .GetEntitiesWithComponent<RunDeckCard>()
                .Where(e => e.IsActive && e.GetComponent<CardData>()?.Card?.IsStarter == true)
                .OrderBy(_ => Guid.NewGuid())
                .Take(evt.Amount)
                .ToList();

            foreach (var card in candidates)
            {
				RunDeckService.ExhaustRunCard(EntityManager, card);
            }

            LoggingService.Append("DeckManagementSystem.OnRemoveRandomCard", new System.Text.Json.Nodes.JsonObject
            {
                ["requestedAmount"] = evt.Amount,
                ["removedCount"] = candidates.Count
            });
        }
    }
}
