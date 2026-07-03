using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;
using System.Linq;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// System for managing the display of cards in the player's hand
    /// </summary>
    [DebugTab("Hand Display")] 
    public class HandDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
		
		// Auto scale for smaller screens
		[DebugEditable(DisplayName = "Auto Scale Threshold Height", Step = 10f, Min = 200f, Max = 4000f)]
		public int AutoScaleThresholdHeight { get; set; } = 1080;
		[DebugEditable(DisplayName = "Min Card Scale", Step = 0.05f, Min = 0.3f, Max = 1.0f)]
		public float MinCardScale { get; set; } = 0.75f;
		[DebugEditable(DisplayName = "Scale Curve Power", Step = 0.1f, Min = 0.1f, Max = 4f)]
		public float CardScalePower { get; set; } = 1.0f;
        
        // Hand layout settings (moved from CardConfig)
        [DebugEditable(DisplayName = "Bottom Margin", Step = 2f, Min = 0f, Max = 1000f)]
        public float HandBottomMargin { get; set; } = 168f;

        [DebugEditable(DisplayName = "Horizontal Screen Padding", Step = 2f, Min = 0f, Max = 500f)]
        public float HandHorizontalScreenPadding { get; set; } = 124f;

        [DebugEditable(DisplayName = "Max Angle (deg)", Step = 0.5f, Min = 0f, Max = 45f)]
        public float HandFanMaxAngleDeg { get; set; } = 5f;

        [DebugEditable(DisplayName = "Arc Radius", Step = 2f, Min = 0f, Max = 2000f)]
        public float HandFanRadius { get; set; } = 0f;

        // Height difference between center and ends of the fan. If > 0, this
        // overrides radius-based curvature to make tuning intuitive. The ends
        // will be lowered by at most this many pixels relative to the center.
        [DebugEditable(DisplayName = "Curve Height", Step = 1f, Min = 0f, Max = 500f)]
        public float HandFanCurveHeight { get; set; } = 24f;

        [DebugEditable(DisplayName = "Curve Offset Y", Step = 2f, Min = -1000f, Max = 1000f)]
        public float HandFanCurveOffset { get; set; } = 0f;

        [DebugEditable(DisplayName = "Rest Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float HandHoverScale { get; set; } = 0.85f;

        [DebugEditable(DisplayName = "Hovered Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float HandHoveredScale { get; set; } = 1.1f;

        [DebugEditable(DisplayName = "Hover Fan Out", Step = 2f, Min = 0f, Max = 500f)]
        public float HandHoverFanOut { get; set; } = 50f;

        [DebugEditable(DisplayName = "Hover Bottom Padding", Step = 1f, Min = -100f, Max = 200f)]
        public float HandHoverBottomPadding { get; set; } = -3f;

        [DebugEditable(DisplayName = "Scale Down Speed", Step = 0.5f, Min = 0.1f, Max = 60f)]
        public float HandScaleDownTweenSpeed { get; set; } = 12f;

        [DebugEditable(DisplayName = "Z Base", Step = 1, Min = -10000, Max = 10000)]
        public int HandZBase { get; set; } = 100;

        [DebugEditable(DisplayName = "Z Step", Step = 1, Min = -1000, Max = 1000)]
        public int HandZStep { get; set; } = 1;

        [DebugEditable(DisplayName = "Z Hover Boost", Step = 10, Min = 0, Max = 10000)]
        public int HandZHoverBoost { get; set; } = 1000;

        [DebugEditable(DisplayName = "Tween Speed", Step = 0.5f, Min = 0.1f, Max = 60f)]
        public float HandTweenSpeed { get; set; } = 21f;
        
		// Root entity removed; each card owns its transform base and parallax

		public HandDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
        }

		// Baseline snapshot of CardGeometrySettings captured once to compute scaled values from
		private bool _baselineCaptured;
		private int _baseCardWidth, _baseCardHeight, _baseCardGap, _baseCardCornerRadius, _baseHighlightBorderThickness, _baseCardOffsetYExtra;
		private float _lastAppliedScale = -1f;
		private string _lastHandReconciliationSignature = string.Empty;
        private readonly Dictionary<int, float> _handScaleByEntityId = new();


		private void CaptureBaselineIfNeeded(CardGeometrySettings s)
		{
			if (_baselineCaptured || s == null) return;
			_baseCardWidth = s.CardWidth;
			_baseCardHeight = s.CardHeight;
			_baseCardOffsetYExtra = s.CardOffsetYExtra;
			_baseCardGap = s.CardGap;
			_baseCardCornerRadius = s.CardCornerRadius;
			_baseHighlightBorderThickness = s.HighlightBorderThickness;
			_baselineCaptured = true;
		}

		private void ApplyViewportScalingIfNeeded(CardGeometrySettings s)
		{
			if (s == null) return;
			CaptureBaselineIfNeeded(s);
			if (!_baselineCaptured) return;

			int vh = Game1.VirtualHeight;
			float baseH = Math.Max(1, AutoScaleThresholdHeight);
			float raw = vh >= baseH ? 1f : (vh / baseH);
			float scaled = MathF.Pow(MathF.Max(MinCardScale, MathF.Min(1f, raw)), MathF.Max(0.1f, CardScalePower));
			if (MathF.Abs(scaled - _lastAppliedScale) < 0.001f) return; // no change
			_lastAppliedScale = scaled;

			// Scale integer dimensions and margins
			s.CardWidth = Math.Max(10, (int)MathF.Round(_baseCardWidth * scaled));
			s.CardHeight = Math.Max(10, (int)MathF.Round(_baseCardHeight * scaled));
			s.CardOffsetYExtra = (int)MathF.Round(_baseCardOffsetYExtra * scaled);
			s.CardGap = (int)MathF.Round(_baseCardGap * scaled);
			s.CardCornerRadius = Math.Max(0, (int)MathF.Round(_baseCardCornerRadius * scaled));
			s.HighlightBorderThickness = Math.Max(0, (int)MathF.Round(_baseHighlightBorderThickness * scaled));
		}

		private float GetClampedCardSpacing(int count, float idealSpacing, float screenWidth, float cardWidth, float cardHeight, float maxAngleRad, float visualScale)
		{
			if (count <= 1) return idealSpacing;

			float scale = MathF.Max(0.01f, visualScale);
			float rotatedFootprint = GetRotatedHorizontalFootprint(cardWidth * scale, cardHeight * scale, maxAngleRad);
			float availableCenterSpan = MathF.Max(0f, screenWidth - (HandHorizontalScreenPadding * 2f) - rotatedFootprint);
			float maxSpacing = availableCenterSpan / (count - 1);

			return MathF.Min(idealSpacing, maxSpacing);
		}

		private static float GetRotatedHorizontalFootprint(float width, float height, float angleRad)
		{
			float absAngle = MathF.Abs(angleRad);
			float cos = MathF.Abs(MathF.Cos(absAngle));
			float sin = MathF.Abs(MathF.Sin(absAngle));
			return width * cos + height * sin;
		}
        
        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<CardData>();
        }
        
        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            // Update card positions based on hand layout
            UpdateCardPosition(entity, gameTime);
        }
        
        private void UpdateCardPosition(Entity entity, GameTime gameTime)
        {
            var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
            if (scene == null || scene.Current != SceneId.Battle) return;

			var transform = entity.GetComponent<Transform>();
            var cardData = entity.GetComponent<CardData>();
            
            if (transform == null || cardData == null) return;
            
            // Find the deck entity and check if this card is in the hand
            var deckEntities = EntityManager.GetEntitiesWithComponent<Deck>();
            var deckEntity = deckEntities.FirstOrDefault();
            
            if (deckEntity != null)
            {
                var deck = deckEntity.GetComponent<Deck>();
                if (deck != null)
                {
                    LogHandReconciliationIfChanged(deck, "UpdateCardPosition");
                }
                if (deck != null && deck.Hand.Contains(entity) && CountsForHandLayout(entity))
                {
                    // Build visible hand excluding animating and filtered cards (equipped weapon at index 0 included for layout).
                    var visibleHand = deck.Hand.Where(CountsForHandLayout).ToList();
                    PruneScaleState(visibleHand);
                    var cardIndex = visibleHand.IndexOf(entity);
                    int hoveredIndex = GetHoveredIndex(visibleHand);

                    if (cardIndex >= 0)
                    {
                        // Compute fan layout positions around bottom-center pivot
                        int count = visibleHand.Count;
                        float screenWidth = Game1.VirtualWidth;
                        float screenHeight = Game1.VirtualHeight;

						// Scale bottom margin for small screens using the same scaling power
						int vh = Game1.VirtualHeight;
						float baseH = Math.Max(1, AutoScaleThresholdHeight);
						float rawScale = vh >= baseH ? 1f : (vh / baseH);
						float marginScale = MathF.Pow(MathF.Max(MinCardScale, MathF.Min(1f, rawScale)), MathF.Max(0.1f, CardScalePower));
						float bottomMarginScaled = HandBottomMargin * marginScale;
					// Pivot anchored to screen bottom-center; ParallaxLayer on each card will offset current Position
					var pivot = new Vector2(screenWidth / 2f, screenHeight - bottomMarginScaled);

                        // normalized index t in [-1, 1]
                        float mid = (count - 1) * 0.5f;
                        float t = (count == 1) ? 0f : (cardIndex - mid) / Math.Max(1f, mid);

                        // angle (in radians) based on normalized index
                        float maxAngleRad = (float)(Math.PI / 180.0) * HandFanMaxAngleDeg;
                        float angleRad = t * maxAngleRad;

                        // Horizontal spread scales with hand size
                        // Use unnormalized index delta so width grows with number of cards
                        float indexDelta = cardIndex - mid; // [-mid, +mid]
                        // Use shared settings for spacing
						var cvs = CardGeometryService.GetSettings(EntityManager);
						// Apply viewport-aware scaling to card visuals (1080p baseline -> scale down below)
						ApplyViewportScalingIfNeeded(cvs);
						float cardWidth = cvs?.CardWidth ?? CardGeometrySettings.DefaultWidth;
						float cardHeight = cvs?.CardHeight ?? CardGeometrySettings.DefaultHeight;
                        float idealCardSpacing = (cvs != null) ? (cvs.CardWidth + cvs.CardGap) : 0f;
                        if (idealCardSpacing <= 0f) { idealCardSpacing = CardGeometrySettings.DefaultWidth + CardGeometrySettings.DefaultGap; }
						float spacingScale = MathF.Max(HandHoverScale, HandHoveredScale);
						float cardSpacing = GetClampedCardSpacing(count, idealCardSpacing, screenWidth, cardWidth, cardHeight, maxAngleRad, spacingScale);
                        float x = pivot.X + indexDelta * cardSpacing;
                        x += GetHoverFanOutOffset(cardIndex, hoveredIndex);

                        // Vertical arc
                        // If curve height is specified, scale curvature so the ends are lowered by
                        // exactly HandFanCurveHeight regardless of angle settings. Otherwise fall back
                        // to the legacy radius-based formula.
                        float yArc;
                        if (HandFanCurveHeight > 0f && maxAngleRad > 0f)
                        {
                            float denom = 1f - (float)Math.Cos(Math.Max(0.0001f, maxAngleRad));
                            float numer = 1f - (float)Math.Cos(Math.Abs(angleRad));
                            float factor = numer / denom; // 0 at center, 1 at ends
                            yArc = HandFanCurveHeight * factor;
                        }
                        else
                        {
                            float cos = (float)Math.Cos(angleRad);
                            yArc = HandFanRadius * (1f - cos);
                        }
                        var ui = entity.GetComponent<UIElement>();
                        bool hovered = ui?.IsHovered == true;
                        float visualScale = UpdateVisualScale(entity, hovered, gameTime);
                        float y = hovered
                            ? GetBottomAnchoredY(cvs, screenHeight - HandHoverBottomPadding, visualScale)
                            : pivot.Y + HandFanCurveOffset + yArc;
                        float rotation = hovered ? 0f : angleRad;

                        var tween = entity.GetComponent<PositionTween>();
                        if (tween != null)
                        {
                            tween.Target = new Vector2(x, y);
                            tween.Speed = HandTweenSpeed;
                        }
                        else
                        {
                            transform.Position = new Vector2(x, y);
                        }
                        transform.Rotation = rotation;
                        transform.Scale = new Vector2(visualScale, visualScale);

                        // Z-order (ensures proper overlapping)
                        transform.ZOrder = HandZBase + (cardIndex * HandZStep) + (hovered ? HandZHoverBoost : 0);

                        // Update UI bounds to current card geometry.
                        if (ui != null)
                        {
                            ui.Bounds = CardGeometryService.GetVisualRect(cvs, transform.Position, visualScale);
                        }
                    }
                }
            }
        }

        private static int GetHoveredIndex(List<Entity> visibleHand)
        {
            for (int i = 0; i < visibleHand.Count; i++)
            {
                if (visibleHand[i].GetComponent<UIElement>()?.IsHovered == true)
                {
                    return i;
                }
            }

            return -1;
        }

        private float GetHoverFanOutOffset(int cardIndex, int hoveredIndex)
        {
            if (hoveredIndex < 0 || cardIndex == hoveredIndex)
            {
                return 0f;
            }

            return cardIndex < hoveredIndex ? -HandHoverFanOut : HandHoverFanOut;
        }

        private float UpdateVisualScale(Entity entity, bool hovered, GameTime gameTime)
        {
            float restScale = MathF.Max(0.01f, HandHoverScale);
            float hoveredScale = MathF.Max(0.01f, HandHoveredScale);

            if (!_handScaleByEntityId.TryGetValue(entity.Id, out float currentScale))
            {
                currentScale = restScale;
            }

            if (hovered)
            {
                currentScale = hoveredScale;
            }
            else
            {
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                float alpha = 1f - MathF.Exp(-MathF.Max(0.1f, HandScaleDownTweenSpeed) * dt);
                currentScale = MathHelper.Lerp(currentScale, restScale, MathHelper.Clamp(alpha, 0f, 1f));
                if (MathF.Abs(currentScale - restScale) < 0.001f)
                {
                    currentScale = restScale;
                }
            }

            _handScaleByEntityId[entity.Id] = currentScale;
            return currentScale;
        }

        private void PruneScaleState(List<Entity> visibleHand)
        {
            if (_handScaleByEntityId.Count == 0) return;

            var visibleIds = new HashSet<int>(visibleHand.Select(e => e.Id));
            foreach (int entityId in _handScaleByEntityId.Keys.ToList())
            {
                if (!visibleIds.Contains(entityId))
                {
                    _handScaleByEntityId.Remove(entityId);
                }
            }
        }

        private static float GetBottomAnchoredY(CardGeometrySettings settings, float visualBottomY, float scale)
        {
            int height = settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
            int offsetYExtra = settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra;
            return visualBottomY - (height * scale * 0.5f) + (offsetYExtra * scale);
        }

        private static bool CountsForHandLayout(Entity e)
        {
            return HandStateLoggingService.CountsForHandLayout(e);
        }

        /// <summary>
        /// Triggers deck shuffling and drawing of cards by publishing events
        /// </summary>
        public void TriggerDeckShuffleAndDraw(int drawCount = 4)
        {
            // Find the deck entity
            var deckEntities = EntityManager.GetEntitiesWithComponent<Deck>();
            var deckEntity = deckEntities.FirstOrDefault();
            
            if (deckEntity != null)
            {
                var deck = deckEntity.GetComponent<Deck>();
                if (deck != null)
                {
                    // Publish event for deck shuffling and drawing
                    EventManager.Publish(new DeckShuffleDrawEvent
                    {
                        Deck = deckEntity,
                        DrawCount = drawCount
                    });
                }
            }
        }
        
        /// <summary>
        /// Draws all cards in hand by publishing render events
        /// </summary>
        public void DrawHand()
        {
            // Find the deck entity and get cards that are actually in the hand
            var deckEntities = EntityManager.GetEntitiesWithComponent<Deck>();
            var deckEntity = deckEntities.FirstOrDefault();
            
            if (deckEntity != null)
            {
                var deck = deckEntity.GetComponent<Deck>();
                if (deck != null)
                {
                    LogHandReconciliationIfChanged(deck, "DrawHand");
                    // Only draw cards that are actually in the hand, not animating, and not filtered out by pay-cost overlay
                    var cardsInHand = deck.Hand
                        .Where(CountsForHandLayout)
                        .Where(ShouldDrawInHand)
                        .OrderBy(e =>
                    {
                        var transform = e.GetComponent<Transform>();
                        return transform?.ZOrder ?? 0;
                    });

                    foreach (var entity in cardsInHand)
                    {
                        var transform = entity.GetComponent<Transform>();
                        if (transform != null)
                        {
                            // Publish render event for each card
                            EventManager.Publish(new CardRenderEvent
                            {
                                Card = entity,
                                Position = transform.Position,
                                IsInHand = true
                            });
                        }
                    }
                }
            }
        }

        private static bool ShouldDrawInHand(Entity entity)
        {
            var assigned = entity.GetComponent<AssignedBlockCard>();
            return assigned == null || (!assigned.IsEquipment && assigned.Phase == AssignedBlockCard.PhaseState.Returning);
        }

        private void LogHandReconciliationIfChanged(Deck deck, string reason)
        {
            if (deck == null) return;

            var visibleIds = deck.Hand
                .Where(HandStateLoggingService.CountsForHandLayout)
                .Select(c => c.Id.ToString());
            var hiddenIds = deck.Hand
                .Where(c => !HandStateLoggingService.CountsForHandLayout(c))
                .Select(c => $"{c.Id}:{HandStateLoggingService.GetLayoutExclusionReason(c)}");
            string signature = string.Join(",", deck.Hand.Select(c => c.Id)) + "|" + string.Join(",", visibleIds) + "|" + string.Join(",", hiddenIds);
            if (signature == _lastHandReconciliationSignature) return;

            _lastHandReconciliationSignature = signature;
            var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>()?.Sub;
            HandStateLoggingService.AppendHandSnapshot("HandDisplaySystem.HandReconciliation", deck, reason, phase);
        }
    }
} 
