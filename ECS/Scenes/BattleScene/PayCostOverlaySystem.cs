using System;
using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Objects.Cards;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Renders and manages the overlay used to pay color costs by discarding cards.
    /// </summary>
    [DebugTab("Pay Cost Overlay")] 
    public class PayCostOverlaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font = FontSingleton.TitleFont;
        private readonly Texture2D _pixel;
        private CardGeometrySettings _cardSettings;

        [DebugEditable(DisplayName = "Fade In (s)", Step = 0.05f, Min = 0.01f, Max = 1.0f)]
        public float FadeInDurationSec { get; set; } = 0.25f;

        [DebugEditable(DisplayName = "Overlay Alpha", Step = 0.05f, Min = 0.1f, Max = 1.0f)]
        public float OverlayAlpha { get; set; } = 0.5f; // 0..1

        [DebugEditable(DisplayName = "Staged Scale", Step = 0.05f, Min = 0.5f, Max = 2.0f)]
        public float StagedCardScale { get; set; } = 1.1f;
        
        [DebugEditable(DisplayName = "Staged Move (s)", Step = 0.05f, Min = 0.01f, Max = 1.0f)]
        public float StagedMoveDurationSec { get; set; } = 0.25f;
        [DebugEditable(DisplayName = "Staged Return (s)", Step = 0.05f, Min = 0.01f, Max = 1.0f)]
        public float StagedReturnDurationSec { get; set; } = 0.1f;

        [DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.5f, Max = 2.0f)]
        public float TextScale { get; set; } = 0.25f;

        [DebugEditable(DisplayName = "Text Offset X", Step = 1f, Min = -1000f, Max = 1000f)]
        public float TextOffsetX { get; set; } = 0f;

        [DebugEditable(DisplayName = "Text Offset Y", Step = 1f, Min = -1000f, Max = 1000f)]
        public float TextOffsetY { get; set; } = -389f;

        [DebugEditable(DisplayName = "Card Offset X", Step = 1f, Min = -2000f, Max = 2000f)]
        public float CardOffsetX { get; set; } = 0f;

        [DebugEditable(DisplayName = "Card Offset Y", Step = 1f, Min = -2000f, Max = 2000f)]
        public float CardOffsetY { get; set; } = -160f;

        // Selected-cards row settings
        [DebugEditable(DisplayName = "Selected Scale", Step = 0.05f, Min = 0.3f, Max = 1.5f)]
        public float SelectedCardScale { get; set; } = 0.5f;

        [DebugEditable(DisplayName = "Selected Move (s)", Step = 0.05f, Min = 0.01f, Max = 1.0f)]
        public float SelectedMoveDurationSec { get; set; } = 0.25f;

        [DebugEditable(DisplayName = "Selected Return (s)", Step = 0.05f, Min = 0.01f, Max = 1.0f)]
        public float SelectedReturnDurationSec { get; set; } = 0.12f;

        [DebugEditable(DisplayName = "Selected Spacing", Step = 2f, Min = 40f, Max = 400f)]
        public float SelectedSpacingPx { get; set; } = 140f;

        [DebugEditable(DisplayName = "Selected Offset X", Step = 2f, Min = -1000f, Max = 2000f)]
        public float SelectedOffsetX { get; set; } = 180f;

        [DebugEditable(DisplayName = "Selected Offset Y", Step = 2f, Min = -1000f, Max = 1000f)]
        public float SelectedOffsetY { get; set; } = 0f;

        // Cancel button settings
        [DebugEditable(DisplayName = "Cancel Btn Offset X", Step = 1f, Min = -500f, Max = 500f)]
        public float CancelButtonOffsetX { get; set; } = 0f;

        [DebugEditable(DisplayName = "Cancel Btn Offset Y", Step = 1f, Min = -500f, Max = 500f)]
        public float CancelButtonOffsetY { get; set; } = 60f;

        public PayCostOverlaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            EventManager.Subscribe<OpenPayCostOverlayEvent>(OnOpen);
            EventManager.Subscribe<ClosePayCostOverlayEvent>(_ => Close());
            EventManager.Subscribe<PayCostCandidateClicked>(OnCandidateClicked);
            EventManager.Subscribe<PayCostCancelRequested>(OnCancel);
            
            // Clear payment cache on next card play or end of turn
            EventManager.Subscribe<PlayCardRequested>(evt => 
            {
                if (!evt.CostsPaid) ClearPaymentCache();
            });
            EventManager.Subscribe<ChangeBattlePhaseEvent>(evt =>
            {
                if (evt.Current == SubPhase.PlayerEnd)
                    ClearPaymentCache();
            });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<PayCostOverlayState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var state = entity.GetComponent<PayCostOverlayState>();
            if (state == null) return;
            EnsureInputContext(entity, state);
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (state.IsOpen)
            {
                state.OpenElapsedSeconds += dt;
                state.StagedMoveElapsedSeconds += dt;
            }
            if (state.IsReturning)
            {
                state.ReturnElapsedSeconds += dt;
                // Reinsert staged card once its return tween completes
                if (state.ReturnElapsedSeconds >= StagedReturnDurationSec && state.CardToPlay != null)
                {
                    var t = state.CardToPlay.GetComponent<Transform>();
                    if (t != null)
                    {
                        t.Position = state.StagedStartPos;
                        t.Rotation = 0f;
                        t.ZOrder = 0;
                    }
                    EventManager.Publish(new CardMoveRequested
                    {
                        Card = state.CardToPlay,
                        Destination = CardZoneType.Hand,
                        InsertIndex = state.OriginalHandIndex,
                        Reason = "PayCostCancelReturn"
                    });
                    state.CardToPlay = null;
                }
            }

            // Step tweens for selected-for-payment cards
            if (state.IsOpen || state.IsReturning)
            {
                // Make a copy in case list changes during iteration
                var selectedSnapshot = state.SelectedCards.ToList();
                foreach (var c in selectedSnapshot)
                {
                    var sel = c.GetComponent<SelectedForPayment>();
                    if (sel == null) continue;
                    if (!sel.IsReturning)
                    {
                        sel.MoveElapsed += dt;
                    }
                    else
                    {
                        sel.ReturnElapsed += dt;
                        if (sel.ReturnElapsed >= Math.Max(0.001f, sel.ReturnDuration))
                        {
                            // Move back to hand at original index
                            EventManager.Publish(new CardMoveRequested
                            {
                                Card = c,
                                Destination = CardZoneType.Hand,
                                InsertIndex = sel.OriginalHandIndex,
                                Reason = "PayCostUnselectReturn"
                            });
                            // Remove from selected list
                            state.SelectedCards.Remove(c);
                        }
                    }

                    // Update UI bounds to current tweened rect so input hit-test matches visuals
                    GetCurrentSelectedPosScale(sel, out var posNow, out var scaleNow);
                    var uiNow = c.GetComponent<UIElement>();
                    if (uiNow != null)
                    {
                        uiNow.Bounds = GetCardVisualRectScaled(posNow, scaleNow);
                    }
                }
            }

            // If we're returning, close overlay only when staged and all selected are done
            if (state.IsReturning)
            {
                bool stagedDone = state.CardToPlay == null || state.ReturnElapsedSeconds >= StagedReturnDurationSec;
                bool anyReturning = state.SelectedCards.Any(c => c.GetComponent<SelectedForPayment>()?.IsReturning == true);
                if (stagedDone && !anyReturning)
                {
                    // Fully close overlay and clear flags
                    ClearAllFilteredFromHandMarkers();
                    state.IsReturning = false;
                    state.IsOpen = false;
                    state.RequiredCosts.Clear();
                    state.SelectedCards.Clear();
                    state.OpenElapsedSeconds = 0f;
                    state.StagedMoveElapsedSeconds = 0f;
                    state.ReturnElapsedSeconds = 0f;
                    state.OriginalHandIndex = -1;
                    RemovePayCostMemberships();
                }
            }

            // Update cancel button bounds during Update phase (before input processing)
            if (state.IsOpen && !state.IsReturning)
            {
                int w = Game1.VirtualWidth;
                var btnRect = new Rectangle(
                    (int)(w - 28 - 24 + CancelButtonOffsetX), 
                    (int)(24 + CancelButtonOffsetY), 
                    28, 
                    28);
                var cancel = EntityManager.GetEntitiesWithComponent<PayCostCancelButton>().FirstOrDefault();
                if (cancel != null)
                {
                    var ui = cancel.GetComponent<UIElement>();
                    if (ui != null)
                    {
                        ui.Bounds = btnRect;
                    }
                }
            }
        }

        private void EnsureStateEntityExists()
        {
            var e = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
            if (e == null)
            {
                e = EntityManager.CreateEntity("PayCostOverlay");
                EntityManager.AddComponent(e, new PayCostOverlayState { IsOpen = false });
                EntityManager.AddComponent(e, new Transform
                {
                    Position = Vector2.Zero,
                    ZOrder = 19000,
                });
            }
            // Ensure a cancel button entity exists (bounds updated in Draw)
            var cancel = EntityManager.GetEntitiesWithComponent<PayCostCancelButton>().FirstOrDefault();
            if (cancel == null)
            {
                cancel = EntityManager.CreateEntity("PayCostOverlay_Cancel");
                EntityManager.AddComponent(cancel, new Transform { Position = Vector2.Zero, ZOrder = 20000 });
                EntityManager.AddComponent(cancel, new UIElement { Bounds = new Rectangle(0, 0, 1, 1), IsInteractable = true, Tooltip = "Cancel", LayerType = UILayerType.Overlay, EventType = UIElementEventType.PayCostCancel });
                EntityManager.AddComponent(cancel, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Below });
                EntityManager.AddComponent(cancel, new PayCostCancelButton());
                InputContextService.EnsureMember(
                    EntityManager,
                    cancel,
                    "overlay.pay-cost");
            }
        }

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

        private void ClearPaymentCache()
        {
            var cache = EntityManager.GetEntitiesWithComponent<LastPaymentCache>().FirstOrDefault()?.GetComponent<LastPaymentCache>();
            if (cache != null)
            {
                cache.CardPlayed = null;
                cache.PaymentCards.Clear();
                cache.HasData = false;
            }
        }

        private void OnOpen(OpenPayCostOverlayEvent evt)
        {
            if (evt == null || evt.CardToPlay == null) return;
            EnsureStateEntityExists();
            var e = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
            var state = e.GetComponent<PayCostOverlayState>();
            state.IsOpen = true;
            state.CardToPlay = evt.CardToPlay;
            state.RequiredCosts = (evt.RequiredCosts ?? new List<string>()).ToList();
            state.SelectedCards.Clear();
            state.ConsumedCostByCardId.Clear();
            state.OpenElapsedSeconds = 0f;
            state.Type = evt.Type;

            LoggingService.Append("PayCostOverlaySystem.OnOpen", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = state.CardToPlay.Id,
                ["cardId"] = state.CardToPlay.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["type"] = state.Type.ToString(),
                ["requiredCosts"] = string.Join(",", state.RequiredCosts),
                ["card"] = HandStateLoggingService.BuildCardSnapshot(state.CardToPlay)
            });

            // Stage the card: record original index and move to HandStaged zone
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck != null)
            {
                state.OriginalHandIndex = deck.Hand.IndexOf(state.CardToPlay);
                EventManager.Publish(new CardMoveRequested { Card = state.CardToPlay, Deck = deckEntity, Destination = CardZoneType.HandStaged, Reason = "PayCostStage" });
            }
            // Disable staged card interactions and set its z-order high
            var stagedUI = state.CardToPlay.GetComponent<UIElement>();
            if (stagedUI != null)
            {
                stagedUI.IsInteractable = false;
                stagedUI.IsHovered = false;
                stagedUI.IsClicked = false;
            }
            var stagedT = state.CardToPlay.GetComponent<Transform>();
            if (stagedT != null)
            {
                // Record start for tween and elevate Z
                state.StagedStartPos = stagedT.Position;
                state.StagedStartRotation = stagedT.Rotation;
                state.StagedMoveElapsedSeconds = 0f;
                stagedT.ZOrder = 30000;
            }

            // Mark the staged card as filtered so it doesn't appear in the hand fan
            // (weapons stay in deck.Hand even after HandStaged move, so this is necessary)
            AddFilteredFromHand(state.CardToPlay, "staged");

            // Update interactability of remaining hand cards to only those viable to pay costs
            UpdateInteractablesForRemainingCosts(state);
        }

        private void Close()
        {
            var e = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
            if (e == null) return;
            var state = e.GetComponent<PayCostOverlayState>();
            if (state != null)
            {
                // If the play was canceled (overlay is closing without PayCostSatisfied), restore staged card to hand
                bool restoring = state.CardToPlay != null && state.RequiredCosts.Count > 0; // if not satisfied
                LoggingService.Append("PayCostOverlaySystem.Close", new System.Text.Json.Nodes.JsonObject
                {
                    ["restoring"] = restoring,
                    ["selectedCount"] = state.SelectedCards.Count,
                    ["remainingCostCount"] = state.RequiredCosts.Count,
                    ["cardToPlayEntityId"] = state.CardToPlay?.Id ?? -1,
                    ["cardToPlayCardId"] = state.CardToPlay?.GetComponent<CardData>()?.Card?.CardId ?? "unknown"
                });
                // Defer reinsertion until the return tween completes
                // Restore interactability for all hand cards
                RestoreHandInteractables();

                if (restoring)
                {
                    // Begin return tween for staged card from center back to original hand index
                    state.IsReturning = true;
                    state.ReturnElapsedSeconds = 0f;
                    // Keep overlay open briefly to animate the card back
                    state.IsOpen = true;
                    // Also return all selected cards
                    foreach (var c in state.SelectedCards.ToList())
                    {
                        var sel = c.GetComponent<SelectedForPayment>();
                        if (sel != null)
                        {
                            // Capture current position/scale as start of return
                            GetCurrentSelectedPosScale(sel, out var curPos, out var curScale);
                            sel.StartPos = curPos;
                            sel.StartScale = curScale;
                            sel.IsReturning = true;
                            sel.ReturnElapsed = 0f;
                            sel.ReturnDuration = SelectedReturnDurationSec;
                            // Avoid interacting while returning
                            var ui = c.GetComponent<UIElement>();
                            if (ui != null) ui.IsInteractable = false;
                        }
                    }
                }
                else
                {
                    // Successful pay: do not return to hand; close immediately
                    ClearAllFilteredFromHandMarkers();
                    state.IsReturning = false;
                    state.ReturnElapsedSeconds = 0f;
                    state.IsOpen = false;
                    state.CardToPlay = null;
                    state.RequiredCosts.Clear();
                    state.SelectedCards.Clear();
                    state.OpenElapsedSeconds = 0f;
                    state.StagedMoveElapsedSeconds = 0f;
                    state.OriginalHandIndex = -1;
                    RemovePayCostMemberships();
                }
            }
        }

        private void OnCancel(PayCostCancelRequested _)
        {
            var e = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
            if (e == null) return;
            var state = e.GetComponent<PayCostOverlayState>();
            if (state == null) return;

            // Clear filtered markers so cards reappear while return animation plays
            ClearAllFilteredFromHandMarkers();

            // Restore overall hand interactability
            RestoreHandInteractables();

            // Begin return tween for staged card (if present)
            state.IsReturning = true;
            state.ReturnElapsedSeconds = 0f;
            state.IsOpen = true;

            // Mark all selected as returning from their current tweened position/scale
            foreach (var c in state.SelectedCards)
            {
                var sel = c.GetComponent<SelectedForPayment>();
                if (sel == null) continue;
                GetCurrentSelectedPosScale(sel, out var curPos, out var curScale);
                sel.StartPos = curPos;
                sel.StartScale = curScale;
                sel.IsReturning = true;
                sel.ReturnElapsed = 0f;
                sel.ReturnDuration = SelectedReturnDurationSec;
                var ui = c.GetComponent<UIElement>();
                if (ui != null) ui.IsInteractable = false;
            }
            EntityManager.DestroyEntity("PayCostOverlay_Cancel");
        }

        private static bool TryConsumeCostForCard(List<string> remainingCosts, Entity candidate, out int consumedIndex)
        {
            consumedIndex = -1;
            // Prefer consuming a specific matching color first
            for (int i = 0; i < remainingCosts.Count; i++)
            {
                string c = remainingCosts[i];
                if (c != "Any" && CardColorQualificationService.IsEligibleForCost(candidate, c))
                {
                    consumedIndex = i;
                    return true;
                }
            }
            // Otherwise consume an Any slot if available
            for (int i = 0; i < remainingCosts.Count; i++)
            {
                if (remainingCosts[i] == "Any")
                {
                    consumedIndex = i;
                    return true;
                }
            }
            return false;
        }

        private void OnCandidateClicked(PayCostCandidateClicked evt)
        {
            var stateEntity = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
            if (stateEntity == null) return;
            var state = stateEntity.GetComponent<PayCostOverlayState>();
            if (state == null || !state.IsOpen || evt?.Card == null) return;

            // Only allow selecting from current hand or toggling from selected list; never the card being played
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;
            if (evt.Card == state.CardToPlay) return;

            if (!CardPlayResolver.IsEligiblePaymentCard(evt.Card)) return;

            var alreadySelected = evt.Card.GetComponent<SelectedForPayment>() != null || state.SelectedCards.Contains(evt.Card);

            if (alreadySelected)
            {
                LoggingService.Append("PayCostOverlaySystem.CardUnselected", new System.Text.Json.Nodes.JsonObject
                {
                    ["entityId"] = evt.Card.Id,
                    ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                    ["requiredCostCountBefore"] = state.RequiredCosts.Count,
                    ["selectedCountBefore"] = state.SelectedCards.Count,
                    ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
                });
                // Unselect: return requirement and animate back
                if (state.ConsumedCostByCardId.TryGetValue(evt.Card.Id, out var consumed))
                {
                    state.RequiredCosts.Add(consumed);
                    state.ConsumedCostByCardId.Remove(evt.Card.Id);
                }
                var sel = evt.Card.GetComponent<SelectedForPayment>();
                if (sel != null)
                {
                    GetCurrentSelectedPosScale(sel, out var curPos, out var curScale);
                    sel.StartPos = curPos;
                    sel.StartScale = curScale;
                    sel.IsReturning = true;
                    sel.ReturnElapsed = 0f;
                    sel.ReturnDuration = SelectedReturnDurationSec;
                    // Set non-interactable during return
                    var ui = evt.Card.GetComponent<UIElement>();
                    if (ui != null) { ui.IsInteractable = false; ui.IsHovered = false; ui.IsClicked = false; }
                }
                RetargetSelectedLayout(state);
                UpdateInteractablesForRemainingCosts(state);
                return;
            }

            // Selecting from hand
            if (!BattleInputGate.TryAllowTutorialAction(EntityManager, TutorialAction.PayCost, evt.Card)) return;
            if (!deck.Hand.Contains(evt.Card)) return;

            if (TryConsumeCostForCard(state.RequiredCosts, evt.Card, out int idx))
            {
                // Remember consumed requirement and remove from remaining
                var consumed = state.RequiredCosts[idx];
                state.RequiredCosts.RemoveAt(idx);
                state.ConsumedCostByCardId[evt.Card.Id] = consumed;

                // Record original index before zone move
                int originalIndex = deck.Hand.IndexOf(evt.Card);

                // Move card into selected zone (makes it interactable)
                EventManager.Publish(new CardMoveRequested { Card = evt.Card, Deck = deckEntity, Destination = CardZoneType.CostSelected, Reason = "PayCostSelect" });

                // Initialize tween state
                var t = evt.Card.GetComponent<Transform>();
                var sel = evt.Card.GetComponent<SelectedForPayment>() ?? new SelectedForPayment();
                sel.OriginalHandIndex = originalIndex;
                sel.OriginalHandPos = t?.Position ?? Vector2.Zero;
                sel.StartPos = t?.Position ?? Vector2.Zero;
                sel.StartScale = 1f;
                sel.MoveElapsed = 0f;
                sel.MoveDuration = SelectedMoveDurationSec;
                sel.IsReturning = false;
                sel.ReturnElapsed = 0f;
                sel.ReturnDuration = SelectedReturnDurationSec;
                if (evt.Card.GetComponent<SelectedForPayment>() == null)
                {
                    EntityManager.AddComponent(evt.Card, sel);
                }

                state.SelectedCards.Add(evt.Card);
                LoggingService.Append("PayCostOverlaySystem.CardSelected", new System.Text.Json.Nodes.JsonObject
                {
                    ["entityId"] = evt.Card.Id,
                    ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                    ["consumedCost"] = consumed,
                    ["originalHandIndex"] = originalIndex,
                    ["remainingCostCount"] = state.RequiredCosts.Count,
                    ["selectedCount"] = state.SelectedCards.Count,
                    ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
                });

                // Recompute targets and interactivity for remaining hand
                RetargetSelectedLayout(state);
                UpdateInteractablesForRemainingCosts(state);

                // If all requirements are satisfied, resolve immediately
                if (state.RequiredCosts.Count == 0)
                {
                    if (state.Type == PayCostOverlayType.ColorDiscard)
                    {
                        foreach (var c in state.SelectedCards.ToList())
                        {
                            EventManager.Publish(new CardDiscardedForCostEvent { Card = c });
                            EventManager.Publish(new CardMoveRequested { Card = c, Deck = deckEntity, Destination = CardZoneType.DiscardPile, Reason = "PayCost" });
                            var card = c.GetComponent<CardData>().Card;
                            if (card != null && card.OnDiscardedForCost != null)
                            {
                                card.OnDiscardedForCost(EntityManager, c);
                            }
                        }
                    }
                    else if (state.Type == PayCostOverlayType.SelectOneCard)
                    {
                        // Restore the selected card back to hand (no discard)
                        foreach (var c in state.SelectedCards.ToList())
                        {
                            var sfp = c.GetComponent<SelectedForPayment>();
                            int idxIns = sfp?.OriginalHandIndex ?? -1;
                            EventManager.Publish(new CardMoveRequested { Card = c, Deck = deckEntity, Destination = CardZoneType.Hand, Reason = "PayCostSelectOneReturn", InsertIndex = idxIns });
                        }
                    }

                    // Cache the payment data before publishing the event
                    var cache = EnsurePaymentCacheExists();
                    cache.CardPlayed = state.CardToPlay;
                    cache.PaymentCards = new List<Entity>(state.SelectedCards);
                    cache.HasData = true;

                    EventManager.Publish(new PayCostSatisfied { CardToPlay = state.CardToPlay, PaymentCards = new List<Entity>(state.SelectedCards) });
                    EntityManager.DestroyEntity("PayCostOverlay_Cancel");
                    Close();
                }
            }
        }

        private string BuildCostPhrase(IEnumerable<string> costs)
        {
            var list = costs.ToList();
            if (list.Count == 0) return "";
            var groups = list
                .GroupBy(c => c)
                .Select(g => g.Count() > 1 ? $"{g.Count()} {g.Key}" : g.Key)
                .ToList();
            return string.Join(", ", groups);
        }

        public void DrawBackdrop()
        {
            var stateEntity = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
            if (stateEntity == null) return;
            var state = stateEntity.GetComponent<PayCostOverlayState>();
            if (state == null || !state.IsOpen || state.IsReturning) return;

            int w = Game1.VirtualWidth;
            int h = Game1.VirtualHeight;

            float t = MathHelper.Clamp(state.OpenElapsedSeconds / FadeInDurationSec, 0f, 1f);
            // EaseOutQuad
            float eased = 1f - (1f - t) * (1f - t);
            float alphaF = MathHelper.Clamp(eased * OverlayAlpha, 0f, 1f);

            // Full-screen dim overlay only
            _spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, h), new Color(0f, 0f, 0f, alphaF));
        }

        public void DrawForeground()
        {
            var stateEntity = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
            if (stateEntity == null) return;
            var state = stateEntity.GetComponent<PayCostOverlayState>();
            if (state == null || !(state.IsOpen || state.IsReturning)) return;

            int w = Game1.VirtualWidth;
            int h = Game1.VirtualHeight;

            float t = MathHelper.Clamp(state.OpenElapsedSeconds / FadeInDurationSec, 0f, 1f);
            float eased = 1f - (1f - t) * (1f - t);
            float alphaF = MathHelper.Clamp(eased * OverlayAlpha, 0f, 1f);

            // If returning, skip overlay text/buttons and only draw the staged card tweening back
            var cd = state.CardToPlay?.GetComponent<CardData>();
            string cardName = ResolveCardName(cd);
            var cardDef = cd?.Card;
            var effectiveCosts = cardDef != null
                ? VigorService.GetEffectiveCost(cardDef, VigorService.GetPlayerVigorStacks(EntityManager))
                : GetDefinitionCosts(state.CardToPlay);
            string costText = BuildCostPhrase(effectiveCosts);
            string line = "";
            switch(state.Type)
            {
                case PayCostOverlayType.ColorDiscard:
                {
                    line = $"Discard {costText} to pay for {cardName}";
                    break;
                }
                case PayCostOverlayType.SelectOneCard:
                {
                    line = $"Select one card from your hand for {cardName}";
                    break;
                }
            }
            var size = _font.MeasureString(line);
            if (!state.IsReturning)
            {
                var textPos = new Vector2(w / 2f - (size.X * TextScale) / 2f + TextOffsetX, h / 2f - (size.Y * TextScale) / 2f + TextOffsetY);
                _spriteBatch.DrawString(_font, line, textPos, Color.White, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
            }

            // Staged card above the dim
            var centerPos = new Vector2(w / 2f + CardOffsetX, h / 2f - (size.Y * TextScale) + CardOffsetY);
            if (state.CardToPlay != null)
            {
                Vector2 pos;
                float rot;
                if (!state.IsReturning)
                {
                    // Tween from hand to center (ease-out)
                    Vector2 start = state.StagedStartPos;
                    Vector2 target = centerPos;
                    float tm = MathHelper.Clamp(state.StagedMoveElapsedSeconds / Math.Max(0.001f, StagedMoveDurationSec), 0f, 1f);
                    float ease = 1f - (1f - tm) * (1f - tm);
                    pos = start + (target - start) * ease;
                    // Rotate from starting rotation to 0 while moving to center
                    float startRot = state.StagedStartRotation;
                    rot = MathHelper.Lerp(startRot, 0f, ease);
                }
                else
                {
                    // Tween back from center to original position (ease-in)
                    Vector2 start = centerPos;
                    Vector2 target = state.StagedStartPos;
                    float tm = MathHelper.Clamp(state.ReturnElapsedSeconds / Math.Max(0.001f, StagedReturnDurationSec), 0f, 1f);
                    float easeIn = tm * tm; // ease-in
                    pos = start + (target - start) * easeIn;
                    // Rotate back from 0 to original rotation while returning
                    float startRot = 0f;
                    rot = MathHelper.Lerp(startRot, state.StagedStartRotation, easeIn);
                }
                // Temporarily override rotation for draw by mutating transform, then restore
                var tcmp = state.CardToPlay.GetComponent<Transform>();
                if (tcmp != null) tcmp.Rotation = rot;
                EventManager.Publish(new CardRenderScaledRotatedEvent { Card = state.CardToPlay, Position = pos, Scale = StagedCardScale });
            }

            // Draw selected-for-payment row to the right of staged card
            DrawSelectedRow(state, centerPos);

            if (!state.IsReturning)
            {
                // Cancel button (top-right) - bounds are set in UpdateEntity
                var btnRect = new Rectangle(
                    (int)(w - 28 - 24 + CancelButtonOffsetX), 
                    (int)(24 + CancelButtonOffsetY), 
                    28, 
                    28);
                float btnAlpha = Math.Min(1f, alphaF + 0.2f);
                _spriteBatch.Draw(_pixel, btnRect, new Color(70f / 255f, 70f / 255f, 70f / 255f, btnAlpha));
                DrawBorder(btnRect, Color.White, 2);
                var xSize = _font.MeasureString("X") * 0.15f;
                _spriteBatch.DrawString(_font, "X", new Vector2(btnRect.Center.X - xSize.X / 2f, btnRect.Center.Y - xSize.Y / 2f), Color.White, 0f, Vector2.Zero, 0.15f, SpriteEffects.None, 0f);
            }
        }

        private void DrawSelectedRow(PayCostOverlayState state, Vector2 stagedCenter)
        {
            if (state == null) return;
            var anchor = new Vector2(stagedCenter.X + SelectedOffsetX, stagedCenter.Y + SelectedOffsetY);
            for (int i = 0; i < state.SelectedCards.Count; i++)
            {
                var c = state.SelectedCards[i];
                var sel = c.GetComponent<SelectedForPayment>();
                if (sel == null) continue;

                // Compute desired target for current index
                var target = new Vector2(anchor.X + i * SelectedSpacingPx, anchor.Y);
                // If the target changed, retarget this card smoothly
                if (!ApproximatelyEqual(sel.TargetPos, target))
                {
                    // Capture current interpolated state as new start
                    GetCurrentSelectedPosScale(sel, out var curPos, out var curScale);
                    sel.StartPos = curPos;
                    sel.StartScale = curScale;
                    sel.MoveElapsed = 0f;
                    sel.MoveDuration = SelectedMoveDurationSec;
                    sel.TargetPos = target;
                    sel.TargetScale = SelectedCardScale;
                }

                Vector2 drawPos;
                float drawScale;
                if (!sel.IsReturning)
                {
                    float tm = MathHelper.Clamp(sel.MoveElapsed / Math.Max(0.001f, sel.MoveDuration), 0f, 1f);
                    float ease = 1f - (1f - tm) * (1f - tm);
                    drawPos = sel.StartPos + (sel.TargetPos - sel.StartPos) * ease;
                    drawScale = MathHelper.Lerp(sel.StartScale, sel.TargetScale, ease);
                }
                else
                {
                    float tm = MathHelper.Clamp(sel.ReturnElapsed / Math.Max(0.001f, sel.ReturnDuration), 0f, 1f);
                    float easeIn = tm * tm;
                    drawPos = sel.StartPos + (sel.OriginalHandPos - sel.StartPos) * easeIn;
                    drawScale = MathHelper.Lerp(sel.StartScale, 1f, easeIn);
                }

                EventManager.Publish(new CardRenderScaledRotatedEvent { Card = c, Position = drawPos, Scale = drawScale });

                // Keep bounds synced during draw as well (harmless if already set in Update)
                var ui = c.GetComponent<UIElement>();
                if (ui != null)
                {
                    ui.Bounds = GetCardVisualRectScaled(drawPos, drawScale);
                }
            }
        }

        private void RetargetSelectedLayout(PayCostOverlayState state)
        {
            if (state == null) return;
            int w = Game1.VirtualWidth;
            int h = Game1.VirtualHeight;
            // Mirror the staged center used in DrawForeground
            // Use font height to keep consistent anchor even before DrawForeground
            string line = "";
            var cd = state.CardToPlay?.GetComponent<CardData>();
            string cardName = ResolveCardName(cd);
            var cardDef = cd?.Card;
            var effectiveCosts = cardDef != null
                ? VigorService.GetEffectiveCost(cardDef, VigorService.GetPlayerVigorStacks(EntityManager))
                : GetDefinitionCosts(state.CardToPlay);
            string costText = BuildCostPhrase(effectiveCosts);
            switch(state.Type)
            {
                case PayCostOverlayType.ColorDiscard: line = $"Discard {costText} to pay for {cardName}"; break;
                case PayCostOverlayType.SelectOneCard: line = $"Select one card from your hand for {cardName}"; break;
            }
            var size = _font.MeasureString(line);
            var stagedCenter = new Vector2(w / 2f + CardOffsetX, h / 2f - (size.Y * TextScale) + CardOffsetY);
            var anchor = new Vector2(stagedCenter.X + SelectedOffsetX, stagedCenter.Y + SelectedOffsetY);
            for (int i = 0; i < state.SelectedCards.Count; i++)
            {
                var c = state.SelectedCards[i];
                var sel = c.GetComponent<SelectedForPayment>();
                if (sel == null) continue;
                // Compute current interpolated state and retarget
                GetCurrentSelectedPosScale(sel, out var curPos, out var curScale);
                sel.StartPos = curPos;
                sel.StartScale = curScale;
                sel.MoveElapsed = 0f;
                sel.MoveDuration = SelectedMoveDurationSec;
                sel.TargetPos = new Vector2(anchor.X + i * SelectedSpacingPx, anchor.Y);
                sel.TargetScale = SelectedCardScale;
            }
        }

        private static void GetCurrentSelectedPosScale(SelectedForPayment sel, out Vector2 pos, out float scale)
        {
            if (sel == null)
            {
                pos = Vector2.Zero; scale = 1f; return;
            }
            if (!sel.IsReturning)
            {
                float tm = MathHelper.Clamp(sel.MoveElapsed / Math.Max(0.001f, sel.MoveDuration), 0f, 1f);
                float ease = 1f - (1f - tm) * (1f - tm);
                pos = sel.StartPos + (sel.TargetPos - sel.StartPos) * ease;
                scale = MathHelper.Lerp(sel.StartScale, sel.TargetScale, ease);
            }
            else
            {
                float tm = MathHelper.Clamp(sel.ReturnElapsed / Math.Max(0.001f, sel.ReturnDuration), 0f, 1f);
                float easeIn = tm * tm;
                pos = sel.StartPos + (sel.OriginalHandPos - sel.StartPos) * easeIn;
                scale = MathHelper.Lerp(sel.StartScale, 1f, easeIn);
            }
        }

        private static bool ApproximatelyEqual(Vector2 a, Vector2 b)
        {
            return Math.Abs(a.X - b.X) < 0.001f && Math.Abs(a.Y - b.Y) < 0.001f;
        }

        private Rectangle GetCardVisualRectScaled(Vector2 position, float scale)
        {
            _cardSettings ??= CardGeometryService.GetSettings(EntityManager);
            return CardGeometryService.GetVisualRect(_cardSettings, position, scale);
        }

        private void RestoreHandInteractables()
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;
            foreach (var c in deck.Hand)
            {
                var ui = c.GetComponent<UIElement>();
                if (ui != null)
                {
                    ui.IsInteractable = true;
                    ui.IsHovered = false;
                    ui.IsClicked = false;
                }
            }
        }

        private void ClearAllFilteredFromHandMarkers()
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            var payState = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault()?.GetComponent<PayCostOverlayState>();
            var candidates = new HashSet<Entity>();

            void AddCandidate(Entity card)
            {
                if (card != null) candidates.Add(card);
            }

            if (deck != null)
            {
                foreach (var c in deck.Hand) AddCandidate(c);
                foreach (var c in deck.DrawPile) AddCandidate(c);
                foreach (var c in deck.DiscardPile) AddCandidate(c);
                foreach (var c in deck.ExhaustPile) AddCandidate(c);
                foreach (var c in deck.Cards) AddCandidate(c);
            }

            AddCandidate(payState?.CardToPlay);
            if (payState?.SelectedCards != null)
            {
                foreach (var c in payState.SelectedCards) AddCandidate(c);
            }

            foreach (var c in candidates)
            {
                if (c.HasComponent<FilteredFromHand>())
                    RemoveFilteredFromHand(c, "clearAll");
            }
        }

        private static bool IsCardViableForCosts(Entity card, List<string> remainingCosts)
        {
            if (!CardPlayResolver.IsEligiblePaymentCard(card)) return false;

            // Card is viable if it can satisfy at least one remaining requirement
            foreach (var c in remainingCosts)
            {
                if (CardColorQualificationService.IsEligibleForCost(card, c)) return true;
            }
            return false;
        }

        private void UpdateInteractablesForRemainingCosts(PayCostOverlayState state)
        {
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;
            foreach (var c in deck.Hand)
            {
                if (c == state.CardToPlay) continue;
                var ui = c.GetComponent<UIElement>();
                if (ui == null) continue;
                InputContextService.EnsureMember(
                    EntityManager,
                    c,
                    "overlay.pay-cost");
                bool viable = IsCardViableForCosts(c, state.RequiredCosts);
                ui.IsInteractable = viable;
                if (!viable)
                {
                    ui.IsHovered = false;
                    ui.IsClicked = false;
                    AddFilteredFromHand(c, "nonViableCost");
                }
                else
                {
                    if (c.HasComponent<FilteredFromHand>())
                        RemoveFilteredFromHand(c, "viableCost");
                }
            }
        }

        private void AddFilteredFromHand(Entity card, string reason)
        {
            if (card == null || card.HasComponent<FilteredFromHand>()) return;

            EntityManager.AddComponent(card, new FilteredFromHand());
            LoggingService.Append("PayCostOverlaySystem.FilteredFromHand", new System.Text.Json.Nodes.JsonObject
            {
                ["action"] = "add",
                ["reason"] = reason,
                ["entityId"] = card.Id,
                ["cardId"] = card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["card"] = HandStateLoggingService.BuildCardSnapshot(card)
            });
        }

        private void RemoveFilteredFromHand(Entity card, string reason)
        {
            if (card == null || !card.HasComponent<FilteredFromHand>()) return;

            EntityManager.RemoveComponent<FilteredFromHand>(card);
            LoggingService.Append("PayCostOverlaySystem.FilteredFromHand", new System.Text.Json.Nodes.JsonObject
            {
                ["action"] = "remove",
                ["reason"] = reason,
                ["entityId"] = card.Id,
                ["cardId"] = card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["card"] = HandStateLoggingService.BuildCardSnapshot(card)
            });
        }

        private List<string> GetDefinitionCosts(Entity card)
        {
            if (card == null) return new List<string>();
            var data = card.GetComponent<CardData>();
            if (data == null) return new List<string>();

            // Lookup JSON definition to read cost array
            string id = data.Card.CardId ?? string.Empty;
            if (string.IsNullOrEmpty(id)) return new List<string>();
            return data.Card.Cost.ToList();
        }

        private static string ResolveCardName(CardData cd)
        {
            if (cd == null) return "Card";
            try
            {
                string id = cd.Card.CardId ?? string.Empty;
                return cd.Card.Name ?? cd.Card.CardId ?? "Card";
            }
            catch { }
            return cd.Card.Name ?? cd.Card.CardId ?? "Card";
        }

        private void EnsureInputContext(Entity entity, PayCostOverlayState state)
        {
            if (entity.GetComponent<UIElement>() != null)
            {
                EntityManager.RemoveComponent<UIElement>(entity);
            }

            bool active = state.IsOpen || state.IsReturning;
            InputContextService.EnsureContext(
                EntityManager,
                entity,
                "overlay.pay-cost",
                600,
                active);
        }

        private void RemovePayCostMemberships()
        {
            foreach (Entity entity in EntityManager
                .GetEntitiesWithComponent<InputContextMember>()
                .Where(entity => entity.GetComponent<CardData>() != null)
                .ToList())
            {
                InputContextService.RemoveMember(
                    EntityManager,
                    entity,
                    "overlay.pay-cost");
            }
        }

        private void DrawBorder(Rectangle rect, Color color, int thickness)
        {
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }
    }
}
