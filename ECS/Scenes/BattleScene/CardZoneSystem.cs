using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Single authority for moving cards between zones (hand, discard, draw, exhaust, assigned block).
    /// Other systems should publish CardMoveRequested events instead of mutating Deck lists directly.
    /// </summary>
    public class CardZoneSystem : Core.System
    {
        public CardZoneSystem(EntityManager entityManager) : base(entityManager)
        {
            EventManager.Subscribe<CardMoveRequested>(OnCardMoveRequested);
            EventManager.Subscribe<CardBlockedEvent>(OnCardBlocked);
            EventManager.Subscribe<ChangeBattlePhaseEvent>(OnChangeBattlePhase);
            EventManager.Subscribe<CardMoveFinalizeRequested>(OnCardMoveFinalizeRequested);
            EventManager.Subscribe<BeginDefeatPresentationEvent>(OnBeginDefeatPresentation);
			EventManager.Subscribe<ReserveAssignedBlockReturnRequested>(OnReserveAssignedBlockReturnRequested);
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		private void OnReserveAssignedBlockReturnRequested(ReserveAssignedBlockReturnRequested evt)
		{
			if (evt?.Card == null || evt.Card.GetComponent<AssignedBlockCard>()?.IsEquipment != false) return;
			var deck = (evt.Deck ?? EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault())?.GetComponent<Deck>();
			if (deck == null || deck.Hand.Contains(evt.Card)) return;
			deck.Hand.Add(evt.Card);
		}

        private void OnCardBlocked(CardBlockedEvent evt)
        {
            var attackDef = GetComponentHelper.GetPlannedAttack(EntityManager);
            if (attackDef != null && attackDef.OnBlockProcessed != null)
            {
                attackDef.OnBlockProcessed(EntityManager, evt.Card);
            }
        }

        private void OnChangeBattlePhase(ChangeBattlePhaseEvent evt)
        {
            if (evt.Current != SubPhase.PlayerEnd) return;
            var deckEntity = EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;
            foreach (var card in deck.Hand)
            {
                var cardObj = CardFactory.Create(card.GetComponent<CardData>().Card.CardId);
                if (cardObj?.ExhaustsOnEndTurn ?? false)
                {
                    EventQueueBridge.EnqueueTriggerAction("CardZoneSystem.OnChangeBattlePhase.EndTurnExhaust", () =>
                    {
                        EventManager.Publish(new CardMoveRequested { Card = card, Deck = deckEntity, Destination = CardZoneType.ExhaustPile, Reason = "EndTurnExhaust" });
                    }, .05f);
                }
            }
        }

        private void OnCardMoveRequested(CardMoveRequested evt)
        {
            if (evt == null || evt.Card == null) return;
            var deckEntity = evt.Deck ?? EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;

            var from = GetZoneOf(deck, evt.Card);
            int handCountBefore = deck.Hand.Count;

            LoggingService.Append("CardZoneSystem.OnCardMoveRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = evt.Card.Id,
                ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["from"] = from.ToString(),
                ["destination"] = evt.Destination.ToString(),
                ["reason"] = evt.Reason,
                ["handCountBefore"] = handCountBefore,
                ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
            });

            // Intercept Hand/HandStaged/CostSelected -> Discard/Exhaust on PlayCard/PayCost to run animati
            // on first; finalize will mutate zones and publish CardMoved
            if (evt.Destination == CardZoneType.DiscardPile || evt.Destination == CardZoneType.ExhaustPile)
            {
                bool isFromHand = deck.Hand.Contains(evt.Card);
                bool isFromCostSelected = evt.Card.GetComponent<SelectedForPayment>() != null;
                bool isFromHandStaged = false;
                {
                    var payEntity = EntityManager.GetEntitiesWithComponent<PayCostOverlayState>().FirstOrDefault();
                    var payState = payEntity?.GetComponent<PayCostOverlayState>();
                    if (payState != null && (payState.IsOpen || payState.IsReturning) && payState.CardToPlay == evt.Card)
                    {
                        isFromHandStaged = true;
                    }
                }

                if (isFromHand || isFromCostSelected || isFromHandStaged)
                {
                    if (evt.Card.GetComponent<AnimatingHandToZone>() == null && evt.Card.GetComponent<AnimatingHandToDiscard>() == null)
                    {
                        // Store the destination in the component so the animation system knows where to finalize
                        EntityManager.AddComponent(evt.Card, new AnimatingHandToZone { Destination = evt.Destination });
                        var uiAnim = evt.Card.GetComponent<UIElement>();
                        if (uiAnim != null)
                        {
                            uiAnim.IsInteractable = false;
                            uiAnim.IsHovered = false;
                            uiAnim.IsClicked = false;
                        }
                        EventManager.Publish(new PlayCardToDiscardAnimationRequested
                        {
                            Card = evt.Card,
                            Deck = deckEntity,
                            ContextId = evt.ContextId
                        });
                    }
                    LoggingService.Append("CardZoneSystem.deferredMove", new System.Text.Json.Nodes.JsonObject
                    {
                        ["entityId"] = evt.Card.Id,
                        ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                        ["from"] = from.ToString(),
                        ["destination"] = evt.Destination.ToString(),
                        ["reason"] = evt.Reason,
                        ["animationComponent"] = "AnimatingHandToZone",
                        ["handCount"] = deck.Hand.Count,
                        ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
                    });
                    return;
                }
            }

            var cardBase = evt.Card.GetComponent<CardData>().Card;
            if (cardBase.IsWeapon)
            {
                // Weapons are not moved to any zone; they are kept in hand until re-added by phase rules
                LoggingService.Append("CardZoneSystem.weaponMoveIgnored", new System.Text.Json.Nodes.JsonObject
                {
                    ["entityId"] = evt.Card.Id,
                    ["cardId"] = cardBase.CardId ?? "unknown",
                    ["from"] = from.ToString(),
                    ["destination"] = evt.Destination.ToString(),
                    ["reason"] = evt.Reason,
                    ["handCount"] = deck.Hand.Count
                });
                return;
            }

            ClearTransientStateForStableZone(evt.Card, from, evt.Destination);

            if (evt.Destination == CardZoneType.Hand
                && evt.Reason == "ReturnAfterAssignment"
                && from == CardZoneType.AssignedBlock
                && deck.Hand.Contains(evt.Card))
            {
                FinalizeReservedAssignedBlockReturn(evt.Card);
                EventManager.Publish(new CardMoved
                {
                    Card = evt.Card,
                    Deck = deckEntity,
                    From = from,
                    To = evt.Destination,
                    ContextId = evt.ContextId
                });
                LoggingService.Append("CardZoneSystem.ReservedAssignedBlockReturn", new System.Text.Json.Nodes.JsonObject
                {
                    ["entityId"] = evt.Card.Id,
                    ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                    ["from"] = from.ToString(),
                    ["to"] = evt.Destination.ToString(),
                    ["reason"] = evt.Reason,
                    ["handCountBefore"] = handCountBefore,
                    ["handCountAfter"] = deck.Hand.Count,
                    ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
                });
                return;
            }

            // Intercept Hand -> DrawPile to run animation first; finalize will mutate zones and publish CardMoved
            if (evt.Destination == CardZoneType.DrawPile)
            {
                bool isFromHand = deck.Hand.Contains(evt.Card);

                if (isFromHand)
                {
                    if (evt.Card.GetComponent<AnimatingHandToDrawPile>() == null)
                    {
                        EntityManager.AddComponent(evt.Card, new AnimatingHandToDrawPile());
                        var uiAnim = evt.Card.GetComponent<UIElement>();
                        if (uiAnim != null)
                        {
                            uiAnim.IsInteractable = false;
                            uiAnim.IsHovered = false;
                            uiAnim.IsClicked = false;
                        }
                        EventManager.Publish(new PlayCardToDrawPileAnimationRequested
                        {
                            Card = evt.Card,
                            Deck = deckEntity,
                            ContextId = evt.ContextId
                        });
                    }
                    LoggingService.Append("CardZoneSystem.deferredMove", new System.Text.Json.Nodes.JsonObject
                    {
                        ["entityId"] = evt.Card.Id,
                        ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                        ["from"] = from.ToString(),
                        ["destination"] = evt.Destination.ToString(),
                        ["reason"] = evt.Reason,
                        ["animationComponent"] = "AnimatingHandToDrawPile",
                        ["handCount"] = deck.Hand.Count,
                        ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
                    });
                    return;
                }
            }

            // Remove from all known deck lists first to avoid duplicates
            deck.DrawPile.Remove(evt.Card);
            deck.Hand.Remove(evt.Card);
            deck.DiscardPile.Remove(evt.Card);
            deck.ExhaustPile.Remove(evt.Card);

            switch (evt.Destination)
            {
                case CardZoneType.Hand:
                {
                    // When returning to hand, remove assignment component and insert at index if provided
                    var abc = evt.Card.GetComponent<AssignedBlockCard>();
                    if (abc != null)
                    {
                        EntityManager.RemoveComponent<AssignedBlockCard>(evt.Card);
						EntityManager.RemoveComponent<AssignedBlockPresentation>(evt.Card);
                    }
                    var sfp = evt.Card.GetComponent<SelectedForPayment>();
                    if (sfp != null)
                    {
                        EntityManager.RemoveComponent<SelectedForPayment>(evt.Card);
                    }
                    // Defensive: remove any stale HotKey that may have leaked from block assignment
                    var hk = evt.Card.GetComponent<HotKey>();
                    if (hk != null)
                    {
                        EntityManager.RemoveComponent<HotKey>(evt.Card);
                    }
                    if (evt.InsertIndex.HasValue)
                    {
                        int idx = evt.InsertIndex.Value;
                        if (idx < 0) idx = 0;
                        if (idx > deck.Hand.Count) idx = deck.Hand.Count;
                        deck.Hand.Insert(idx, evt.Card);
                    }
                    else
                    {
                        if (!deck.Hand.Contains(evt.Card))
                        {
                            deck.Hand.Add(evt.Card);
                        }
                    }
                    // Make card interactable in hand
                    var uiH = evt.Card.GetComponent<UIElement>();
                    if (uiH != null)
                    {
                        uiH.SuppressCount = 0; // clear any phase suppression carried from draw pile
                        uiH.IsInteractable = true;
                        uiH.IsHovered = false;
                        uiH.IsClicked = false;
                        uiH.EventType = UIElementEventType.CardClicked;
                        uiH.LayerType = UILayerType.Default;
                    }
                    // Fallback restore of tooltip config if a backup still exists (e.g., if BlockAssignmentRemoved didn't run)
                    {
                        var backup = evt.Card.GetComponent<TooltipOverrideBackup>();
                        var ui = evt.Card.GetComponent<UIElement>();
                        if (backup != null && ui != null)
                        {
                            ui.TooltipType = backup.OriginalType;
                            ui.TooltipPosition = backup.OriginalPosition;
                            ui.TooltipOffsetPx = backup.OriginalOffsetPx;
                            var ct = evt.Card.GetComponent<CardTooltip>();
                            if (backup.HadCardTooltip)
                            {
                                if (ct == null) { EntityManager.AddComponent(evt.Card, new CardTooltip { CardId = backup.OriginalCardTooltipId ?? string.Empty }); }
                                else { ct.CardId = backup.OriginalCardTooltipId ?? string.Empty; }
                            }
                            else
                            {
                                if (ct != null) { EntityManager.RemoveComponent<CardTooltip>(evt.Card); }
                            }
                            EntityManager.RemoveComponent<TooltipOverrideBackup>(evt.Card);
                        }
                    }
                    var t = evt.Card.GetComponent<Transform>();
                    if (t != null && t.Position == Vector2.Zero)
                    {
                        t.Position = Vector2.Zero;
                        t.Rotation = 0f;
                        t.Scale = Vector2.One;
                    }
                    break;
                }
                case CardZoneType.HandStaged:
                {
                    // Card is staged outside of any deck list; keep it non-interactable
                    var uiS = evt.Card.GetComponent<UIElement>();
                    if (uiS != null)
                    {
                        uiS.IsInteractable = false;
                        uiS.IsHovered = false;
                        uiS.IsClicked = false;
                        uiS.EventType = UIElementEventType.None;
                        uiS.LayerType = UILayerType.Default;
                    }
                    break;
                }
                case CardZoneType.CostSelected:
                {
                    // Temporarily selected to pay a cost: keep interactable so it can be unselected
                    var ui = evt.Card.GetComponent<UIElement>();
                    if (ui != null)
                    {
                        ui.IsInteractable = true;
                        ui.IsHovered = false;
                        ui.IsClicked = false;
                        ui.EventType = UIElementEventType.None;
                    }
                    var t = evt.Card.GetComponent<Transform>();
                    if (t != null) t.ZOrder = 29500;
                    if (evt.Card.GetComponent<SelectedForPayment>() == null)
                    {
                        EntityManager.AddComponent(evt.Card, new SelectedForPayment());
                    }
                    break;
                }
                case CardZoneType.AssignedBlock:
                {
                    var t = evt.Card.GetComponent<Transform>();
                    var inheritedHotKey = evt.Card.GetComponent<HotKey>();
                    if (inheritedHotKey != null)
                    {
                        EntityManager.RemoveComponent<HotKey>(evt.Card);
                    }
                    var abc = evt.Card.GetComponent<AssignedBlockCard>();
                    if (abc == null)
                    {
                        var cd = evt.Card.GetComponent<CardData>();
                        var bg = ResolveCardBgColor(cd?.Color ?? CardData.CardColor.White);
                        var fg = ResolveFgForBg(bg);
                        abc = new AssignedBlockCard
                        {
                            BlockAmount = BlockValueService.GetTotalBlockValue(evt.Card),
                            AssignedAtTicks = DateTime.UtcNow.Ticks,
                            IsEquipment = false,
							ColorKeys = CardColorQualificationService.GetQualifiedColors(evt.Card).ToList(),
                            Tooltip = ResolveCardName(cd),
                            DisplayBgColor = bg,
                            DisplayFgColor = fg
                        };
                        EntityManager.AddComponent(evt.Card, abc);
                    }
					var presentation = evt.Card.GetComponent<AssignedBlockPresentation>();
					if (presentation == null)
					{
						var start = t?.Position ?? Vector2.Zero;
						EntityManager.AddComponent(evt.Card, new AssignedBlockPresentation
						{
							StartPos = start,
							CurrentPos = start,
							TargetPos = start,
							StartScale = t?.Scale.X ?? 1f,
							CurrentScale = t?.Scale.X ?? 1f,
							Phase = AssignedBlockPresentation.PhaseState.Pullback,
						});
					}
                    // Do not assign HotKey here; it will be added when animation reaches Idle
                    // Ensure the card is not still in hand to prevent later clicks from finding it
                    deck.Hand.Remove(evt.Card);
                    EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.Equip, Volume = 0.5f });
                    break;
                }
                case CardZoneType.DiscardPile:
                {
                    deck.DiscardPile.Add(evt.Card);
                    // Disable interactions for discarded cards
                    var uiD = evt.Card.GetComponent<UIElement>();
                    if (uiD != null)
                    {
                        uiD.IsInteractable = false;
                        uiD.IsHovered = false;
                        uiD.IsClicked = false;
                        uiD.EventType = UIElementEventType.None;
                        uiD.LayerType = UILayerType.Default;
                    }
                    var sfpD = evt.Card.GetComponent<SelectedForPayment>();
                    if (sfpD != null)
                    {
                        EntityManager.RemoveComponent<SelectedForPayment>(evt.Card);
                    }
                    // Push discarded cards behind UI
                    var t = evt.Card.GetComponent<Transform>();
                    if (t != null) t.ZOrder = -10000;
                    break;
                }
                case CardZoneType.DrawPile:
                {
                    deck.DrawPile.Add(evt.Card);
                    if (evt.Card.GetComponent<MarkedForBottomOfDrawPile>() != null)
                    {
                        EntityManager.RemoveComponent<MarkedForBottomOfDrawPile>(evt.Card);
                    }
                    // Not interactable in draw pile
                    var uiDP = evt.Card.GetComponent<UIElement>();
                    if (uiDP != null)
                    {
                        uiDP.IsInteractable = false;
                        uiDP.IsHovered = false;
                        uiDP.IsClicked = false;
                        uiDP.EventType = UIElementEventType.None;
                        uiDP.LayerType = UILayerType.Default;
                    }
                    // Reset transform so highlight hit-test uses proper defaults when re-drawn
                    var tdp = evt.Card.GetComponent<Transform>();
                    if (tdp != null)
                    {
                        tdp.Rotation = 0f;
                        if (tdp.Position == Vector2.Zero) { tdp.Position = Vector2.Zero; }
                        tdp.Scale = Vector2.One;
                    }
                    break;
                }
                case CardZoneType.ExhaustPile:
                {
                    RunDeckService.ExhaustRunCard(EntityManager, evt.Card);
                    break;
                }
            }

            EventManager.Publish(new CardMoved
            {
                Card = evt.Card,
                Deck = deckEntity,
                From = from,
                To = evt.Destination,
                ContextId = evt.ContextId
            });

            LoggingService.Append("CardZoneSystem.CardMoveInProgress", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = evt.Card.Id,
                ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["from"] = from.ToString(),
                ["to"] = evt.Destination.ToString(),
                ["reason"] = evt.Reason,
                ["handCountBefore"] = handCountBefore,
                ["handCountAfter"] = deck.Hand.Count,
                ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
            });
        }

        private void OnCardMoveFinalizeRequested(CardMoveFinalizeRequested evt)
        {
            if (evt == null || evt.Card == null) return;
            var deckEntity = evt.Deck ?? EntityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
            var deck = deckEntity?.GetComponent<Deck>();
            if (deck == null) return;

            var from = GetZoneOf(deck, evt.Card);
            int handCountBefore = deck.Hand.Count;
            // Normalize lists to avoid duplicates
            deck.DrawPile.Remove(evt.Card);
            deck.Hand.Remove(evt.Card);
            deck.DiscardPile.Remove(evt.Card);
            deck.ExhaustPile.Remove(evt.Card);

            ClearTransientStateForStableZone(evt.Card, from, evt.Destination);

            switch (evt.Destination)
            {
                case CardZoneType.DiscardPile:
                {
                    deck.DiscardPile.Add(evt.Card);
                    var uiD = evt.Card.GetComponent<UIElement>();
                    if (uiD != null)
                    {
                        uiD.IsInteractable = false;
                        uiD.IsHovered = false;
                        uiD.IsClicked = false;
                        uiD.EventType = UIElementEventType.None;
                        uiD.LayerType = UILayerType.Default;
                    }
                    var sfpD = evt.Card.GetComponent<SelectedForPayment>();
                    if (sfpD != null)
                    {
                        EntityManager.RemoveComponent<SelectedForPayment>(evt.Card);
                    }
                    var t = evt.Card.GetComponent<Transform>();
                    if (t != null) t.ZOrder = -10000;
                    break;
                }
                case CardZoneType.ExhaustPile:
                {
                    RunDeckService.ExhaustRunCard(EntityManager, evt.Card);
                    break;
                }
                case CardZoneType.DrawPile:
                {
                    deck.DrawPile.Add(evt.Card);
                    if (evt.Card.GetComponent<MarkedForBottomOfDrawPile>() != null)
                    {
                        EntityManager.RemoveComponent<MarkedForBottomOfDrawPile>(evt.Card);
                    }
                    // Not interactable in draw pile
                    var uiDP = evt.Card.GetComponent<UIElement>();
                    if (uiDP != null)
                    {
                        uiDP.IsInteractable = false;
                        uiDP.IsHovered = false;
                        uiDP.IsClicked = false;
                        uiDP.EventType = UIElementEventType.None;
                        uiDP.LayerType = UILayerType.Default;
                    }
                    // Reset transform so highlight hit-test uses proper defaults when re-drawn
                    var tdp = evt.Card.GetComponent<Transform>();
                    if (tdp != null)
                    {
                        tdp.Rotation = 0f;
                        if (tdp.Position == Vector2.Zero) { tdp.Position = Vector2.Zero; }
                        tdp.Scale = Vector2.One;
                    }
                    break;
                }
                default:
                {
                    // If finalize was called for an unsupported destination, no-op back to hand
                    if (!deck.Hand.Contains(evt.Card))
                    {
                        deck.Hand.Add(evt.Card);
                    }
                    break;
                }
            }

            // Clear animation markers if present
            var anim = evt.Card.GetComponent<AnimatingHandToDiscard>();
            if (anim != null) { EntityManager.RemoveComponent<AnimatingHandToDiscard>(evt.Card); }
            var animZone = evt.Card.GetComponent<AnimatingHandToZone>();
            if (animZone != null) { EntityManager.RemoveComponent<AnimatingHandToZone>(evt.Card); }
            var animDrawPile = evt.Card.GetComponent<AnimatingHandToDrawPile>();
            if (animDrawPile != null) { EntityManager.RemoveComponent<AnimatingHandToDrawPile>(evt.Card); }

            EventManager.Publish(new CardMoved
            {
                Card = evt.Card,
                Deck = deckEntity,
                From = from,
                To = evt.Destination,
                ContextId = evt.ContextId
            });

            LoggingService.Append("CardZoneSystem.OnCardMoveFinalizeRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = evt.Card.Id,
                ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["from"] = from.ToString(),
                ["to"] = evt.Destination.ToString(),
                ["contextId"] = evt.ContextId,
                ["handCountBefore"] = handCountBefore,
                ["handCountAfter"] = deck.Hand.Count,
                ["stillInHandAfter"] = deck.Hand.Contains(evt.Card),
                ["hadAnimatingHandToDiscard"] = anim != null,
                ["hadAnimatingHandToZone"] = animZone != null,
                ["hadAnimatingHandToDrawPile"] = animDrawPile != null,
                ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
            });
        }

        private void FinalizeReservedAssignedBlockReturn(Entity card)
        {
            if (card == null) return;

            if (card.GetComponent<AssignedBlockCard>() != null)
            {
                EntityManager.RemoveComponent<AssignedBlockCard>(card);
				EntityManager.RemoveComponent<AssignedBlockPresentation>(card);
            }

            var sfp = card.GetComponent<SelectedForPayment>();
            if (sfp != null)
            {
                EntityManager.RemoveComponent<SelectedForPayment>(card);
            }

            var hk = card.GetComponent<HotKey>();
            if (hk != null)
            {
                EntityManager.RemoveComponent<HotKey>(card);
            }

            var ui = card.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.SuppressCount = 0;
                ui.IsInteractable = true;
                ui.IsHovered = false;
                ui.IsClicked = false;
                ui.EventType = UIElementEventType.CardClicked;
                ui.LayerType = UILayerType.Default;
            }

            RestoreTooltipOverride(card);
        }

        private void RestoreTooltipOverride(Entity card)
        {
            var backup = card?.GetComponent<TooltipOverrideBackup>();
            var ui = card?.GetComponent<UIElement>();
            if (backup == null || ui == null) return;

            ui.TooltipType = backup.OriginalType;
            ui.TooltipPosition = backup.OriginalPosition;
            ui.TooltipOffsetPx = backup.OriginalOffsetPx;
            var ct = card.GetComponent<CardTooltip>();
            if (backup.HadCardTooltip)
            {
                if (ct == null) { EntityManager.AddComponent(card, new CardTooltip { CardId = backup.OriginalCardTooltipId ?? string.Empty }); }
                else { ct.CardId = backup.OriginalCardTooltipId ?? string.Empty; }
            }
            else
            {
                if (ct != null) { EntityManager.RemoveComponent<CardTooltip>(card); }
            }
            EntityManager.RemoveComponent<TooltipOverrideBackup>(card);
        }

        private void OnBeginDefeatPresentation(BeginDefeatPresentationEvent evt)
        {
            if (evt?.IsPreview == true) return;
            QueuedDiscardAssignedBlocksEvent.ResolveImmediately(
                EntityManager,
                QueuedDiscardAssignedBlocksEvent.ShouldDiscardSpentBlocks(EntityManager));
        }

        private void ClearTransientStateForStableZone(Entity card, CardZoneType source, CardZoneType destination)
        {
            if (!IsStableZone(destination)) return;

            CardTransientStateService.ClearHandVisibilityFilters(EntityManager, card);
            if (source == CardZoneType.AssignedBlock)
            {
                CardTransientStateService.ClearAssignedBlockHotKey(EntityManager, card);
                if (card.GetComponent<AssignedBlockCard>() != null)
                {
                    EntityManager.RemoveComponent<AssignedBlockCard>(card);
					EntityManager.RemoveComponent<AssignedBlockPresentation>(card);
                }
                RestoreTooltipOverride(card);
            }
        }

        private static bool IsStableZone(CardZoneType destination)
        {
            return destination == CardZoneType.Hand
                || destination == CardZoneType.DiscardPile
                || destination == CardZoneType.DrawPile
                || destination == CardZoneType.ExhaustPile;
        }

        private static Microsoft.Xna.Framework.Color ResolveCardBgColor(CardData.CardColor color)
        {
            switch (color)
            {
                case CardData.CardColor.Red: return Color.DarkRed;
                case CardData.CardColor.Black: return Color.Black;
                case CardData.CardColor.White:
                default: return Color.White;
            }
        }

        private static Microsoft.Xna.Framework.Color ResolveFgForBg(Microsoft.Xna.Framework.Color bg)
        {
            return (bg == Color.White) ? Color.Black : Color.White;
        }

        private static string ResolveCardName(CardData cd)
        {
            if (cd == null) return string.Empty;
            try
            {
                var cardObj = CardFactory.Create(cd.Card.CardId);
                if (cardObj != null)
                {
                    return cardObj.Name ?? cardObj.CardId ?? string.Empty;
                }
            }
            catch { }
            return cd.Card.CardId ?? string.Empty;
        }

        private static CardZoneType GetZoneOf(Deck deck, Entity card)
        {
            if (card.HasComponent<AssignedBlockCard>()) return CardZoneType.AssignedBlock;
            if (deck.Hand.Contains(card)) return CardZoneType.Hand;
            if (deck.DrawPile.Contains(card)) return CardZoneType.DrawPile;
            if (deck.DiscardPile.Contains(card)) return CardZoneType.DiscardPile;
            if (deck.ExhaustPile.Contains(card)) return CardZoneType.ExhaustPile;
            return CardZoneType.ExhaustPile;
        }
    }
}
