using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Factories;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
    public static class HandStateLoggingService
    {
        public static bool CountsForHandLayout(Entity card)
        {
            return GetLayoutExclusionReason(card) == "Visible";
        }

        public static string GetLayoutExclusionReason(Entity card)
        {
            if (card == null) return "Null";
            if (card.HasComponent<AnimatingHandToDiscard>()) return "AnimatingHandToDiscard";
            if (card.HasComponent<AnimatingHandToZone>()) return "AnimatingHandToZone";
            if (card.HasComponent<AnimatingHandToDrawPile>()) return "AnimatingHandToDrawPile";
            if (card.HasComponent<FilteredFromHand>()) return "FilteredFromHand";
            return "Visible";
        }

        public static bool CountsForDraw(Entity card)
        {
            return GetDrawCountReason(card, out bool counts) == "non-weapon" && counts
                || counts;
        }

        public static string GetDrawCountReason(Entity card)
        {
            return GetDrawCountReason(card, out _);
        }

        public static int CountVisibleHand(IEnumerable<Entity> hand)
        {
            return hand?.Count(CountsForHandLayout) ?? 0;
        }

        public static int CountEffectiveDrawHand(IEnumerable<Entity> hand)
        {
            return hand?.Count(CountsForDraw) ?? 0;
        }

        public static JsonObject BuildHandSnapshot(Deck deck, string reason, SubPhase? phase = null)
        {
            var hand = deck?.Hand ?? new List<Entity>();
            int visibleHandCount = CountVisibleHand(hand);
            int effectiveDrawHandCount = CountEffectiveDrawHand(hand);

            var cards = new JsonArray();
            foreach (var card in hand)
            {
                cards.Add(BuildCardSnapshot(card));
            }

            return new JsonObject
            {
                ["phase"] = phase?.ToString() ?? "Unknown",
                ["reason"] = reason ?? string.Empty,
                ["deckHandCount"] = hand.Count,
                ["visibleHandCount"] = visibleHandCount,
                ["effectiveDrawHandCount"] = effectiveDrawHandCount,
                ["drawPileCount"] = deck?.DrawPile?.Count ?? 0,
                ["discardPileCount"] = deck?.DiscardPile?.Count ?? 0,
                ["mismatch"] = visibleHandCount != effectiveDrawHandCount || (visibleHandCount == 0 && effectiveDrawHandCount > 0),
                ["cards"] = cards
            };
        }

        public static JsonObject BuildCardSnapshot(Entity card)
        {
            bool countsForDraw = CountsForDraw(card);
            string drawCountReason = GetDrawCountReason(card);
            string layoutExclusionReason = GetLayoutExclusionReason(card);
            var cardData = card?.GetComponent<CardData>();
            var transform = card?.GetComponent<Transform>();
            var ui = card?.GetComponent<UIElement>();
            var inputContext = card?.GetComponent<InputContextMember>();

            return new JsonObject
            {
                ["entityId"] = card?.Id ?? -1,
                ["cardId"] = cardData?.Card?.CardId ?? "unknown",
                ["name"] = cardData?.Card?.Name ?? cardData?.Card?.CardId ?? "unknown",
                ["isActive"] = card?.IsActive ?? false,
                ["zoneMarkers"] = BuildZoneMarkers(card),
                ["countsForDraw"] = countsForDraw,
                ["drawCountReason"] = drawCountReason,
                ["countsForLayout"] = layoutExclusionReason == "Visible",
                ["layoutExclusionReason"] = layoutExclusionReason,
                ["transformPosition"] = BuildVector(transform?.Position),
                ["uiBounds"] = BuildRectangle(ui?.Bounds),
                ["uiEventType"] = ui?.EventType.ToString() ?? "None",
                ["isInteractable"] = ui?.IsInteractable ?? false,
                ["suppressCount"] = ui?.SuppressCount ?? 0,
                ["isHidden"] = ui?.IsHidden ?? false,
                ["uiLayerType"] = ui?.LayerType.ToString() ?? UILayerType.Default.ToString(),
                ["isPreventDefaultClick"] = ui?.IsPreventDefaultClick ?? false,
                ["inputContextId"] = inputContext?.ContextId ?? string.Empty,
                ["zOrder"] = transform?.ZOrder ?? 0
            };
        }

        public static void AppendHandSnapshot(string context, Deck deck, string reason, SubPhase? phase = null)
        {
            LoggingService.Append(context, BuildHandSnapshot(deck, reason, phase));
        }

        private static string GetDrawCountReason(Entity card, out bool counts)
        {
            counts = false;
            if (card == null) return "Null";
            if (card.HasComponent<AnimatingHandToDiscard>()) return "AnimatingHandToDiscard";
            if (card.HasComponent<AnimatingHandToZone>()) return "AnimatingHandToZone";
            if (card.HasComponent<AnimatingHandToDrawPile>()) return "AnimatingHandToDrawPile";
            if (card.HasComponent<Pledge>()) return "Pledge";

            var cardData = card.GetComponent<CardData>();
            if (cardData == null) return "no CardData";

            string id = cardData.Card?.CardId ?? string.Empty;
            if (string.IsNullOrEmpty(id)) return "empty CardId";

            var cardOnEntity = cardData.Card;
            if (cardOnEntity != null)
            {
                if (cardOnEntity.IsWeapon) return "weapon";
                if (cardOnEntity.IsToken) return "token";
            }

            var cardBase = CardFactory.Create(id);
            if (cardBase == null)
            {
                counts = true;
                return "factory returned null";
            }

            if (cardBase.IsWeapon) return "weapon";
            if (cardBase.IsToken) return "token";

            counts = true;
            return "non-weapon";
        }

        private static JsonArray BuildZoneMarkers(Entity card)
        {
            var markers = new JsonArray();
            if (card == null) return markers;

            AddMarker(markers, card.HasComponent<Pledge>(), "Pledge");
            AddMarker(markers, card.HasComponent<FilteredFromHand>(), "FilteredFromHand");
            AddMarker(markers, card.HasComponent<AnimatingHandToDiscard>(), "AnimatingHandToDiscard");
            AddMarker(markers, card.HasComponent<AnimatingHandToZone>(), "AnimatingHandToZone");
            AddMarker(markers, card.HasComponent<AnimatingHandToDrawPile>(), "AnimatingHandToDrawPile");
            AddMarker(markers, card.HasComponent<SelectedForPayment>(), "SelectedForPayment");
            AddMarker(markers, card.HasComponent<AssignedBlockCard>(), "AssignedBlockCard");
            return markers;
        }

        private static void AddMarker(JsonArray markers, bool hasMarker, string marker)
        {
            if (hasMarker) markers.Add(marker);
        }

        private static JsonObject BuildVector(Vector2? vector)
        {
            return new JsonObject
            {
                ["x"] = vector?.X ?? 0,
                ["y"] = vector?.Y ?? 0
            };
        }

        private static JsonObject BuildRectangle(Rectangle? rectangle)
        {
            return new JsonObject
            {
                ["x"] = rectangle?.X ?? 0,
                ["y"] = rectangle?.Y ?? 0,
                ["width"] = rectangle?.Width ?? 0,
                ["height"] = rectangle?.Height ?? 0
            };
        }
    }
}
