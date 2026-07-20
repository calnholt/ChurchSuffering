using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Owns the layout and hover presentation of cards in the player's hand.
    /// </summary>
    [DebugTab("Hand Display")]
    public class HandDisplaySystem : Core.System
    {
        private readonly List<Entity> _visibleHand = new();
        private readonly HashSet<int> _visibleHandIds = new();
        private readonly Dictionary<int, float> _handScaleByEntityId = new();
        private readonly List<int> _staleScaleEntityIds = new();
        private readonly List<HandReconciliationEntry> _lastHandReconciliation = new();
        private bool _hasHandReconciliationSnapshot;

        private readonly record struct HandReconciliationEntry(
            int EntityId,
            string LayoutExclusionReason);

        [DebugEditable(DisplayName = "Bottom Margin", Step = 2f, Min = 0f, Max = 1000f)]
        public float HandBottomMargin { get; set; } = 168f;

        [DebugEditable(DisplayName = "Horizontal Screen Padding", Step = 2f, Min = 0f, Max = 500f)]
        public float HandHorizontalScreenPadding { get; set; } = 124f;

        [DebugEditable(DisplayName = "Max Angle (deg)", Step = 0.5f, Min = 0f, Max = 45f)]
        public float HandFanMaxAngleDeg { get; set; } = 5f;

        [DebugEditable(DisplayName = "Arc Radius", Step = 2f, Min = 0f, Max = 2000f)]
        public float HandFanRadius { get; set; }

        [DebugEditable(DisplayName = "Curve Height", Step = 1f, Min = 0f, Max = 500f)]
        public float HandFanCurveHeight { get; set; } = 24f;

        [DebugEditable(DisplayName = "Curve Offset Y", Step = 2f, Min = -1000f, Max = 1000f)]
        public float HandFanCurveOffset { get; set; }

        [DebugEditable(DisplayName = "Rest Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float HandRestScale { get; set; } = 0.85f;

        [DebugEditable(DisplayName = "Hovered Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float HandHoveredScale { get; set; } = 1.1f;

        [DebugEditable(DisplayName = "Hover Fan Out", Step = 2f, Min = 0f, Max = 500f)]
        public float HandHoverFanOut { get; set; } = 50f;

        [DebugEditable(DisplayName = "Hover Bottom Padding", Step = 1f, Min = -100f, Max = 200f)]
        public float HandHoverBottomPadding { get; set; } = -3f;

        [DebugEditable(DisplayName = "Scale Down Speed", Step = 0.5f, Min = 0.1f, Max = 60f)]
        public float HandScaleDownTweenSpeed { get; set; } = 30f;

        [DebugEditable(DisplayName = "Z Base", Step = 1, Min = -10000, Max = 10000)]
        public int HandZBase { get; set; } = 100;

        [DebugEditable(DisplayName = "Z Step", Step = 1, Min = -1000, Max = 1000)]
        public int HandZStep { get; set; } = 1;

        [DebugEditable(DisplayName = "Z Hover Boost", Step = 10, Min = 0, Max = 10000)]
        public int HandZHoverBoost { get; set; } = 1000;

        [DebugEditable(DisplayName = "Position Tween Speed", Step = 0.5f, Min = 0.1f, Max = 60f)]
        public float HandPositionTweenSpeed { get; set; } = 21f;

        public HandDisplaySystem(EntityManager entityManager)
            : base(entityManager)
        {
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive || !IsBattleSceneActive()) return;

            var deck = EntityManager
                .GetEntitiesWithComponent<Deck>()
                .FirstOrDefault()
                ?.GetComponent<Deck>();
            if (deck == null) return;

            LogHandReconciliationIfChanged(deck);
            BuildVisibleHand(deck);
            PruneScaleState();
            if (_visibleHand.Count == 0) return;

            CardGeometrySettings settings = CardGeometryService.GetSettings(EntityManager);
            LayoutVisibleHand(settings, gameTime);
        }

        private bool IsBattleSceneActive()
        {
            return EntityManager
                .GetEntitiesWithComponent<SceneState>()
                .FirstOrDefault()
                ?.GetComponent<SceneState>()
                ?.Current == SceneId.Battle;
        }

        private void BuildVisibleHand(Deck deck)
        {
            _visibleHand.Clear();
            _visibleHandIds.Clear();

            foreach (Entity card in deck.Hand)
            {
                if (!CountsForHandLayout(card)) continue;
                _visibleHand.Add(card);
                _visibleHandIds.Add(card.Id);
            }
        }

        private void LayoutVisibleHand(CardGeometrySettings settings, GameTime gameTime)
        {
            int count = _visibleHand.Count;
            int hoveredIndex = GetHoveredIndex(_visibleHand);
            float screenWidth = Game1.VirtualWidth;
            float screenHeight = Game1.VirtualHeight;
            var pivot = new Vector2(
                screenWidth * 0.5f,
                screenHeight - HandBottomMargin);
            float mid = (count - 1) * 0.5f;
            float maxAngleRad = MathHelper.ToRadians(HandFanMaxAngleDeg);
            float cardWidth = settings?.CardWidth ?? CardGeometrySettings.DefaultWidth;
            float cardHeight = settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
            float idealCardSpacing = settings != null
                ? settings.CardWidth + settings.CardGap
                : CardGeometrySettings.DefaultWidth + CardGeometrySettings.DefaultGap;
            if (idealCardSpacing <= 0f)
            {
                idealCardSpacing = CardGeometrySettings.DefaultWidth + CardGeometrySettings.DefaultGap;
            }

            float spacingScale = MathF.Max(HandRestScale, HandHoveredScale);
            float cardSpacing = GetClampedCardSpacing(
                count,
                idealCardSpacing,
                screenWidth,
                cardWidth,
                cardHeight,
                maxAngleRad,
                spacingScale,
                hoveredIndex);

            for (int cardIndex = 0; cardIndex < count; cardIndex++)
            {
                LayoutCard(
                    _visibleHand[cardIndex],
                    cardIndex,
                    hoveredIndex,
                    mid,
                    maxAngleRad,
                    cardSpacing,
                    pivot,
                    screenHeight,
                    settings,
                    gameTime);
            }
        }

        private void LayoutCard(
            Entity entity,
            int cardIndex,
            int hoveredIndex,
            float mid,
            float maxAngleRad,
            float cardSpacing,
            Vector2 pivot,
            float screenHeight,
            CardGeometrySettings settings,
            GameTime gameTime)
        {
            var transform = entity.GetComponent<Transform>();
            if (transform == null) return;

            float indexDelta = cardIndex - mid;
            float normalizedIndex = mid <= 0f ? 0f : indexDelta / Math.Max(1f, mid);
            float angleRad = normalizedIndex * maxAngleRad;
            float x = pivot.X
                + indexDelta * cardSpacing
                + GetHoverFanOutOffset(cardIndex, hoveredIndex);
            float yArc = GetVerticalArc(angleRad, maxAngleRad);
            bool hovered = entity.GetComponent<UIElement>()?.IsHovered == true;
            float visualScale = UpdateVisualScale(entity, hovered, gameTime);
            float y = hovered
                ? GetBottomAnchoredY(settings, screenHeight - HandHoverBottomPadding, visualScale)
                : pivot.Y + HandFanCurveOffset + yArc;

            var tween = entity.GetComponent<PositionTween>();
            if (tween != null)
            {
                tween.Target = new Vector2(x, y);
                tween.Speed = HandPositionTweenSpeed;
            }
            else
            {
                transform.Position = new Vector2(x, y);
            }

            transform.Rotation = hovered ? 0f : angleRad;
            transform.Scale = new Vector2(visualScale);
            transform.ZOrder = HandZBase
                + cardIndex * HandZStep
                + (hovered ? HandZHoverBoost : 0);
        }

        private float GetVerticalArc(float angleRad, float maxAngleRad)
        {
            if (HandFanCurveHeight > 0f && maxAngleRad > 0f)
            {
                float denominator = 1f - MathF.Cos(MathF.Max(0.0001f, maxAngleRad));
                float numerator = 1f - MathF.Cos(MathF.Abs(angleRad));
                return HandFanCurveHeight * (numerator / denominator);
            }

            return HandFanRadius * (1f - MathF.Cos(angleRad));
        }

        private float GetClampedCardSpacing(
            int count,
            float idealSpacing,
            float screenWidth,
            float cardWidth,
            float cardHeight,
            float maxAngleRad,
            float visualScale,
            int hoveredIndex)
        {
            if (count <= 1) return idealSpacing;

            float scale = MathF.Max(0.01f, visualScale);
            float footprint = GetRotatedHorizontalFootprint(
                cardWidth * scale,
                cardHeight * scale,
                maxAngleRad);
            float minimumCenterX = HandHorizontalScreenPadding + footprint * 0.5f;
            float maximumCenterX = screenWidth - HandHorizontalScreenPadding - footprint * 0.5f;
            float pivotX = screenWidth * 0.5f;
            float mid = (count - 1) * 0.5f;
            float maximumSpacing = idealSpacing;

            for (int cardIndex = 0; cardIndex < count; cardIndex++)
            {
                float indexDelta = cardIndex - mid;
                float fanOutOffset = GetHoverFanOutOffset(cardIndex, hoveredIndex);
                if (indexDelta < 0f)
                {
                    float candidate = (pivotX + fanOutOffset - minimumCenterX) / -indexDelta;
                    maximumSpacing = MathF.Min(maximumSpacing, candidate);
                }
                else if (indexDelta > 0f)
                {
                    float candidate = (maximumCenterX - pivotX - fanOutOffset) / indexDelta;
                    maximumSpacing = MathF.Min(maximumSpacing, candidate);
                }
            }

            return MathF.Max(0f, MathF.Min(idealSpacing, maximumSpacing));
        }

        private static float GetRotatedHorizontalFootprint(
            float width,
            float height,
            float angleRad)
        {
            float absAngle = MathF.Abs(angleRad);
            return width * MathF.Abs(MathF.Cos(absAngle))
                + height * MathF.Abs(MathF.Sin(absAngle));
        }

        private static int GetHoveredIndex(IReadOnlyList<Entity> visibleHand)
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
            if (hoveredIndex < 0 || cardIndex == hoveredIndex) return 0f;
            return cardIndex < hoveredIndex ? -HandHoverFanOut : HandHoverFanOut;
        }

        private float UpdateVisualScale(Entity entity, bool hovered, GameTime gameTime)
        {
            float restScale = MathF.Max(0.01f, HandRestScale);
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
                currentScale = MathHelper.Lerp(
                    currentScale,
                    restScale,
                    MathHelper.Clamp(alpha, 0f, 1f));
                if (MathF.Abs(currentScale - restScale) < 0.001f)
                {
                    currentScale = restScale;
                }
            }

            _handScaleByEntityId[entity.Id] = currentScale;
            return currentScale;
        }

        private void PruneScaleState()
        {
            _staleScaleEntityIds.Clear();
            foreach (int entityId in _handScaleByEntityId.Keys)
            {
                if (!_visibleHandIds.Contains(entityId))
                {
                    _staleScaleEntityIds.Add(entityId);
                }
            }

            foreach (int entityId in _staleScaleEntityIds)
            {
                _handScaleByEntityId.Remove(entityId);
            }
        }

        private static float GetBottomAnchoredY(
            CardGeometrySettings settings,
            float visualBottomY,
            float scale)
        {
            int height = settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
            int offsetYExtra = settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra;
            return visualBottomY - height * scale * 0.5f + offsetYExtra * scale;
        }

        private static bool CountsForHandLayout(Entity entity)
        {
            return HandStateLoggingService.CountsForHandLayout(entity);
        }

        /// <summary>
        /// Publishes render events for cards currently displayed in the hand.
        /// </summary>
        public void DrawHand()
        {
            var deck = EntityManager
                .GetEntitiesWithComponent<Deck>()
                .FirstOrDefault()
                ?.GetComponent<Deck>();
            if (deck == null) return;

            var cardsInHand = deck.Hand
                .Where(CountsForHandLayout)
                .Where(ShouldDrawInHand)
                .OrderBy(entity => entity.GetComponent<Transform>()?.ZOrder ?? 0);

            foreach (Entity entity in cardsInHand)
            {
                var transform = entity.GetComponent<Transform>();
                if (transform == null) continue;

                var renderEvent = new CardRenderEvent
                {
                    Card = entity,
                    Position = transform.Position,
                    IsInHand = true,
                    PreferCachedBase = IsStableForCachedBase(
                        entity,
                        _handScaleByEntityId.TryGetValue(entity.Id, out float scale)
                            ? scale
                            : transform.Scale.X,
                        HandRestScale),
                };
#if DEBUG
                EventManager.PublishPartitioned(
                    renderEvent,
                    CardRenderEvent.BaseRendererPriority,
                    action => FrameProfiler.Measure("Battle.HandBaseCards", action),
                    action => FrameProfiler.Measure("Battle.HandDecorations", action));
#else
                EventManager.Publish(renderEvent);
#endif
            }
        }

        internal static bool IsStableForCachedBase(
            Entity entity,
            float currentHandScale,
            float restScale)
        {
            if (entity == null) return false;
            var transform = entity.GetComponent<Transform>();
            if (transform == null) return false;
            if (entity.GetComponent<UIElement>()?.IsHovered == true) return false;
            if (MathF.Abs(currentHandScale - restScale) > 0.001f ||
                MathF.Abs(transform.Scale.X - restScale) > 0.001f ||
                MathF.Abs(transform.Scale.Y - restScale) > 0.001f)
            {
                return false;
            }

            var tween = entity.GetComponent<PositionTween>();
            if (tween != null && (!tween.Initialized ||
                Vector2.DistanceSquared(tween.Current, tween.Target) > 0.0625f))
            {
                return false;
            }

            var animation = entity.GetComponent<Animation>();
            if (animation?.IsPlaying == true) return false;
            if (entity.GetComponent<SelectedForPayment>() != null) return false;
            if (entity.GetComponent<CardToDiscardFlight>() != null) return false;
            if (entity.GetComponent<PlunderSnatchFlight>() != null) return false;
            if (entity.GetComponent<PlunderRescueFlight>() != null) return false;
            return entity.GetComponent<AssignedBlockPresentation>()?.Phase !=
                AssignedBlockPresentation.PhaseState.Returning;
        }

        private static bool ShouldDrawInHand(Entity entity)
        {
            if (entity.HasComponent<SuppressCardZoneRender>()) return false;
            var assigned = entity.GetComponent<AssignedBlockCard>();
            var presentation = entity.GetComponent<AssignedBlockPresentation>();
            return assigned == null || (!assigned.IsEquipment
                && presentation?.Phase == AssignedBlockPresentation.PhaseState.Returning);
        }

        private void LogHandReconciliationIfChanged(Deck deck)
        {
            bool changed = !_hasHandReconciliationSnapshot
                || deck.Hand.Count != _lastHandReconciliation.Count;
            if (!changed)
            {
                for (int i = 0; i < deck.Hand.Count; i++)
                {
                    Entity card = deck.Hand[i];
                    var current = new HandReconciliationEntry(
                        card.Id,
                        HandStateLoggingService.GetLayoutExclusionReason(card));
                    if (current == _lastHandReconciliation[i]) continue;
                    changed = true;
                    break;
                }
            }

            if (!changed) return;

            _lastHandReconciliation.Clear();
            foreach (Entity card in deck.Hand)
            {
                _lastHandReconciliation.Add(new HandReconciliationEntry(
                    card.Id,
                    HandStateLoggingService.GetLayoutExclusionReason(card)));
            }

            _hasHandReconciliationSnapshot = true;
            SubPhase? phase = EntityManager
                .GetEntitiesWithComponent<PhaseState>()
                .FirstOrDefault()
                ?.GetComponent<PhaseState>()
                ?.Sub;
            HandStateLoggingService.AppendHandSnapshot(
                "HandDisplaySystem.HandReconciliation",
                deck,
                "HandLayoutChanged",
                phase);
        }
    }
}
