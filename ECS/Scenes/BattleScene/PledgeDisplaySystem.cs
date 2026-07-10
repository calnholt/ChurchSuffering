using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Displays the pledge icon on cards that have the Pledge component.
    /// Plays a gravity-drop slam animation when a pledge is added, then leaves the static icon.
    /// </summary>
    [DebugTab("Pledge Display")]
    public class PledgeDisplaySystem : Core.System
    {
        private static readonly Color HudRed = new(196, 30, 58);
        private static readonly Color FlashColor = new(255, 220, 200);
        private static readonly Color DustColor = new(220, 215, 206);

        private static readonly float[] DropStopTimes = { 0f, 0.55f, 0.72f, 0.88f, 1f };

        private readonly SpriteBatch _spriteBatch;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly Texture2D _pledgeTexture;
        private readonly Dictionary<int, PledgeSlamAnimState> _animByEntityId = new();
        private readonly Dictionary<(int radius, int thickness), Texture2D> _ringCache = new();
        private CardGeometrySettings _settings;

        [DebugEditable(DisplayName = "Icon Scale", Step = 0.01f, Min = 0.01f, Max = 1.0f)]
        public float IconScale { get; set; } = 0.09f;

        [DebugEditable(DisplayName = "Icon Offset Y", Step = 1f, Min = -300f, Max = 300f)]
        public float IconOffsetY { get; set; } = -190f;

        [DebugEditable(DisplayName = "Drop Duration (s)", Step = 0.01f, Min = 0.01f, Max = 2f)]
        public float DropDurationSeconds { get; set; } = 0.55f;

        [DebugEditable(DisplayName = "Drop Start Y Offset", Step = 1f, Min = -500f, Max = 500f)]
        public float DropStartYOffset { get; set; } = -280f;

        [DebugEditable(DisplayName = "Drop Overshoot Y Offset", Step = 1f, Min = -100f, Max = 100f)]
        public float DropOvershootYOffset { get; set; } = 8f;

        [DebugEditable(DisplayName = "Drop Rebound Y Offset", Step = 1f, Min = -100f, Max = 100f)]
        public float DropReboundYOffset { get; set; } = -4f;

        [DebugEditable(DisplayName = "Drop Settle Y Offset", Step = 1f, Min = -100f, Max = 100f)]
        public float DropSettleYOffset { get; set; } = 2f;

        [DebugEditable(DisplayName = "Drop Start Scale", Step = 0.01f, Min = 0.1f, Max = 3f)]
        public float DropStartScale { get; set; } = 1.15f;

        [DebugEditable(DisplayName = "Drop Impact Scale X", Step = 0.01f, Min = 0.1f, Max = 3f)]
        public float DropImpactScaleX { get; set; } = 1.08f;

        [DebugEditable(DisplayName = "Drop Impact Scale Y", Step = 0.01f, Min = 0.1f, Max = 3f)]
        public float DropImpactScaleY { get; set; } = 0.88f;

        [DebugEditable(DisplayName = "Drop Start Rotation (deg)", Step = 0.5f, Min = -45f, Max = 45f)]
        public float DropStartRotationDeg { get; set; } = -8f;

        [DebugEditable(DisplayName = "Shadow Offset X", Step = 0.5f, Min = -20f, Max = 20f)]
        public float ShadowOffsetX { get; set; } = 0f;

        [DebugEditable(DisplayName = "Shadow Offset Y", Step = 0.5f, Min = -20f, Max = 20f)]
        public float ShadowOffsetY { get; set; } = 4f;

        [DebugEditable(DisplayName = "Shadow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
        public float ShadowAlpha { get; set; } = 0.45f;

        [DebugEditable(DisplayName = "Impact Start Delay (s)", Step = 0.01f, Min = 0f, Max = 2f)]
        public float ImpactStartDelaySeconds { get; set; } = 0.38f;

        [DebugEditable(DisplayName = "Ring Base Diameter (px)", Step = 1f, Min = 4f, Max = 200f)]
        public float RingBaseDiameterPx { get; set; } = 20f;

        [DebugEditable(DisplayName = "Ring Start Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
        public float RingStartScale { get; set; } = 0.3f;

        [DebugEditable(DisplayName = "Ring End Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
        public float RingEndScale { get; set; } = 3.5f;

        [DebugEditable(DisplayName = "Ring Duration (s)", Step = 0.01f, Min = 0.01f, Max = 2f)]
        public float RingDurationSeconds { get; set; } = 0.45f;

        [DebugEditable(DisplayName = "Ring Start Border (px)", Step = 0.5f, Min = 1f, Max = 10f)]
        public float RingStartBorderPx { get; set; } = 3f;

        [DebugEditable(DisplayName = "Flash Width (px)", Step = 1f, Min = 4f, Max = 400f)]
        public float FlashWidthPx { get; set; } = 80f;

        [DebugEditable(DisplayName = "Flash Height (px)", Step = 1f, Min = 4f, Max = 400f)]
        public float FlashHeightPx { get; set; } = 40f;

        [DebugEditable(DisplayName = "Flash Duration (s)", Step = 0.01f, Min = 0.01f, Max = 2f)]
        public float FlashDurationSeconds { get; set; } = 0.2f;

        [DebugEditable(DisplayName = "Dust Width (px)", Step = 1f, Min = 4f, Max = 400f)]
        public float DustWidthPx { get; set; } = 60f;

        [DebugEditable(DisplayName = "Dust Height (px)", Step = 1f, Min = 4f, Max = 400f)]
        public float DustHeightPx { get; set; } = 20f;

        [DebugEditable(DisplayName = "Dust Offset Y (px)", Step = 1f, Min = -100f, Max = 100f)]
        public float DustOffsetYPx { get; set; } = 20f;

        [DebugEditable(DisplayName = "Dust Duration (s)", Step = 0.01f, Min = 0.01f, Max = 2f)]
        public float DustDurationSeconds { get; set; } = 0.35f;

        private class PledgeSlamAnimState
        {
            public float Elapsed;
        }

        public PledgeDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _pledgeTexture = imageAssets.GetRequiredTexture("pledge");

            EventManager.Subscribe<CardRenderEvent>(evt => FrameProfiler.Measure("PledgeDisplaySystem.OnCardRenderEvent", () => OnCardRenderEvent(evt)));
            EventManager.Subscribe<CardRenderScaledEvent>(evt => FrameProfiler.Measure("PledgeDisplaySystem.OnCardRenderScaledEvent", () => OnCardRenderScaledEvent(evt)));
            EventManager.Subscribe<CardRenderScaledRotatedEvent>(evt => FrameProfiler.Measure("PledgeDisplaySystem.OnCardRenderScaledRotatedEvent", () => OnCardRenderScaledRotatedEvent(evt)));
            EventManager.Subscribe<PledgeAddedEvent>(OnPledgeAdded);
            EventManager.Subscribe<DeleteCachesEvent>(_ => OnDeleteCaches());
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<Pledge>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            if (entity == null || !_animByEntityId.TryGetValue(entity.Id, out var anim)) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            anim.Elapsed += dt;

            if (entity.GetComponent<Pledge>() == null || anim.Elapsed >= GetTotalAnimDurationSeconds())
                _animByEntityId.Remove(entity.Id);
        }

        public void Draw()
        {
            var pledgedIds = GetRelevantEntities().Select(e => e.Id).ToHashSet();
            foreach (var key in _animByEntityId.Keys.ToList())
            {
                if (!pledgedIds.Contains(key))
                    _animByEntityId.Remove(key);
            }
        }

        private float GetTotalAnimDurationSeconds()
        {
            float impactTail = Math.Max(RingDurationSeconds, Math.Max(FlashDurationSeconds, DustDurationSeconds));
            return ImpactStartDelaySeconds + impactTail;
        }

        private void OnPledgeAdded(PledgeAddedEvent evt)
        {
            if (evt?.Card == null) return;
            _animByEntityId[evt.Card.Id] = new PledgeSlamAnimState();
        }

        private void OnDeleteCaches()
        {
            foreach (var kv in _ringCache)
            {
                try { kv.Value?.Dispose(); } catch { }
            }
            _ringCache.Clear();
        }

        private void OnCardRenderEvent(CardRenderEvent evt)
        {
            var card = evt.Card;
            if (card == null || card.GetComponent<Pledge>() == null) return;

            var geometry = CardGeometryService.GetVisualGeometry(EntityManager, card, evt.Position);
            DrawPledgeForCard(card, evt.Position, geometry.Scale, geometry.Rotation);
        }

        private void OnCardRenderScaledEvent(CardRenderScaledEvent evt)
        {
            var card = evt.Card;
            if (card == null || card.GetComponent<Pledge>() == null) return;
            using var clip = CardRenderClipScope.Apply(_graphicsDevice, evt.ClipRect);

            DrawPledgeForCard(card, evt.Position, evt.Scale, evt.Rotation);
        }

        private void OnCardRenderScaledRotatedEvent(CardRenderScaledRotatedEvent evt)
        {
            var card = evt.Card;
            if (card == null || card.GetComponent<Pledge>() == null) return;
            var transform = card.GetComponent<Transform>();
            float rotation = transform?.Rotation ?? 0f;

            DrawPledgeForCard(card, evt.Position, evt.Scale, rotation);
        }

        private void DrawPledgeForCard(Entity card, Vector2 position, float cardScale, float cardRotation)
        {
            if (_animByEntityId.TryGetValue(card.Id, out var anim) && anim.Elapsed < GetTotalAnimDurationSeconds())
                DrawPledgeSlam(position, cardScale, cardRotation, anim.Elapsed);
            else
                DrawPledgeIcon(position, cardScale, cardRotation);
        }

        private void ComputeIconLandTransform(
            Vector2 cardPosition, float cardScale, float cardRotation,
            out Vector2 landPos, out float effectiveIconScale)
        {
            _settings ??= CardGeometryService.GetSettings(EntityManager);
            var bounds = CardGeometryService.GetVisualRect(_settings, cardPosition, cardScale);
            var center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f);
            landPos = center + RotateLocalOffset(0f, IconOffsetY * cardScale, cardRotation);
            effectiveIconScale = IconScale * cardScale;
        }

        private void DrawPledgeIcon(Vector2 position, float cardScale, float cardRotation)
        {
            ComputeIconLandTransform(position, cardScale, cardRotation, out var landPos, out var effectiveScale);
            DrawPledgeTexture(landPos, cardRotation, new Vector2(effectiveScale, effectiveScale), 1f);
        }

        private void DrawPledgeSlam(Vector2 position, float cardScale, float cardRotation, float elapsed)
        {
            ComputeIconLandTransform(position, cardScale, cardRotation, out var landPos, out var effectiveScale);
            DrawImpactEffects(landPos, cardScale, cardRotation, elapsed);

            if (elapsed < DropDurationSeconds)
            {
                SampleDropKeyframe(elapsed, out var yOff, out var scale, out var rotRad, out var alpha);
                var animOffset = RotateLocalOffset(0f, yOff * cardScale, cardRotation);
                var iconPos = landPos + animOffset;
                var drawScale = new Vector2(effectiveScale * scale.X, effectiveScale * scale.Y);
                var shadowOffset = RotateLocalOffset(ShadowOffsetX * cardScale, ShadowOffsetY * cardScale, cardRotation);
                DrawPledgeTexture(iconPos + shadowOffset, cardRotation + rotRad, drawScale, ShadowAlpha);
                DrawPledgeTexture(iconPos, cardRotation + rotRad, drawScale, alpha);
            }
            else
            {
                DrawPledgeTexture(landPos, cardRotation, new Vector2(effectiveScale, effectiveScale), 1f);
            }
        }

        private void DrawPledgeTexture(Vector2 position, float rotation, Vector2 drawScale, float alpha)
        {
            var origin = new Vector2(_pledgeTexture.Width / 2f, _pledgeTexture.Height / 2f);
            _spriteBatch.Draw(
                _pledgeTexture,
                position,
                null,
                Color.White * alpha,
                rotation,
                origin,
                drawScale,
                SpriteEffects.None,
                0f);
        }

        private void DrawImpactEffects(Vector2 landPos, float cardScale, float cardRotation, float elapsed)
        {
            float impactElapsed = elapsed - ImpactStartDelaySeconds;
            if (impactElapsed < 0f) return;

            SampleImpactDust(impactElapsed, out var dustScale, out var dustAlpha, out var dustYOffset);
            if (dustAlpha > 0.001f)
            {
                var dustCenter = landPos + RotateLocalOffset(0f, (DustOffsetYPx + dustYOffset) * cardScale, cardRotation);
                DrawEllipseBurst(dustCenter, DustWidthPx * cardScale, DustHeightPx * cardScale, cardRotation, dustScale, DustColor * (dustAlpha * 0.5f));
            }

            SampleImpactFlash(impactElapsed, out var flashScale, out var flashAlpha);
            if (flashAlpha > 0.001f)
                DrawEllipseBurst(landPos, FlashWidthPx * cardScale, FlashHeightPx * cardScale, cardRotation, new Vector2(flashScale, flashScale), FlashColor * (flashAlpha * 0.9f));

            SampleImpactRing(impactElapsed, out var ringScale, out var ringAlpha, out var borderPx);
            if (ringAlpha > 0.001f)
                DrawImpactRing(landPos, cardScale, cardRotation, ringScale, ringAlpha, borderPx);
        }

        private void DrawEllipseBurst(Vector2 center, float width, float height, float rotation, Vector2 scale, Color color)
        {
            int radius = Math.Max(1, (int)Math.Ceiling(Math.Max(width, height) * 0.5f));
            var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
            var origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
            float baseDiameter = circle.Width;
            var drawScale = new Vector2(width / baseDiameter * scale.X, height / baseDiameter * scale.Y);
            _spriteBatch.Draw(circle, center, null, color, rotation, origin, drawScale, SpriteEffects.None, 0f);
        }

        private void DrawImpactRing(Vector2 center, float cardScale, float cardRotation, float ringScale, float alpha, float borderPx)
        {
            int radius = Math.Max(1, (int)Math.Round(RingBaseDiameterPx * cardScale * 0.5f * ringScale));
            int thickness = Math.Max(1, (int)Math.Round(borderPx * cardScale));
            var ring = GetRingTexture(radius, thickness);
            _spriteBatch.Draw(
                ring,
                center,
                null,
                HudRed * alpha,
                cardRotation,
                new Vector2(radius, radius),
                1f,
                SpriteEffects.None,
                0f);
        }

        private Texture2D GetRingTexture(int radius, int thickness)
        {
            if (radius < 1) radius = 1;
            if (thickness < 1) thickness = 1;
            var key = (radius, thickness);
            if (_ringCache.TryGetValue(key, out var existing) && existing != null && !existing.IsDisposed)
                return existing;

            int d = radius * 2;
            var tex = new Texture2D(_graphicsDevice, d, d);
            var data = new Color[d * d];
            float outerRadius = radius - 0.5f;
            float innerRadius = Math.Max(0f, radius - thickness) + 0.5f;
            const float smooth = 1f;

            for (int y = 0; y < d; y++)
            {
                float dy = y - radius + 0.5f;
                for (int x = 0; x < d; x++)
                {
                    float dx = x - radius + 0.5f;
                    float dist = MathF.Sqrt(dx * dx + dy * dy);

                    float outerAlpha = dist <= outerRadius - smooth ? 1f
                        : dist >= outerRadius + smooth ? 0f
                        : 0.5f + 0.5f * (outerRadius - dist) / smooth;

                    float innerAlpha = dist <= innerRadius - smooth ? 1f
                        : dist >= innerRadius + smooth ? 0f
                        : 0.5f + 0.5f * (innerRadius - dist) / smooth;

                    float ringAlpha = MathHelper.Clamp(outerAlpha - innerAlpha, 0f, 1f);
                    byte a = (byte)MathHelper.Clamp((int)Math.Round(ringAlpha * 255f), 0, 255);
                    data[y * d + x] = Color.FromNonPremultiplied(255, 255, 255, a);
                }
            }

            tex.SetData(data);
            _ringCache[key] = tex;
            return tex;
        }

        private void SampleDropKeyframe(float elapsed, out float yOff, out Vector2 scale, out float rotRad, out float alpha)
        {
            float duration = Math.Max(0.0001f, DropDurationSeconds);
            float t = MathHelper.Clamp(elapsed / duration, 0f, 1f);

            float[] yValues =
            {
                DropStartYOffset,
                DropOvershootYOffset,
                DropReboundYOffset,
                DropSettleYOffset,
                0f
            };
            var scaleValues = new[]
            {
                new Vector2(DropStartScale, DropStartScale),
                new Vector2(DropImpactScaleX, DropImpactScaleY),
                new Vector2(0.96f, 1.04f),
                new Vector2(1.02f, 0.98f),
                Vector2.One
            };
            float[] rotDeg = { DropStartRotationDeg, 2f, -1f, 0.5f, 0f };
            float[] alphas = { 1f, 1f, 1f, 1f, 1f };

            int seg = 0;
            for (int i = 0; i < DropStopTimes.Length - 1; i++)
            {
                if (t <= DropStopTimes[i + 1] || i == DropStopTimes.Length - 2)
                {
                    seg = i;
                    break;
                }
            }

            float segStart = DropStopTimes[seg];
            float segEnd = DropStopTimes[seg + 1];
            float localU = segEnd > segStart ? (t - segStart) / (segEnd - segStart) : 1f;
            float eased = CubicBezierEase(localU, 0.22f, 1f, 0.36f, 1f);

            yOff = MathHelper.Lerp(yValues[seg], yValues[seg + 1], eased);
            scale = Vector2.Lerp(scaleValues[seg], scaleValues[seg + 1], eased);
            rotRad = MathHelper.ToRadians(MathHelper.Lerp(rotDeg[seg], rotDeg[seg + 1], eased));
            alpha = MathHelper.Lerp(alphas[seg], alphas[seg + 1], eased);
        }

        private void SampleImpactRing(float impactElapsed, out float scale, out float alpha, out float borderPx)
        {
            float t = EaseOut(MathHelper.Clamp(impactElapsed / Math.Max(0.0001f, RingDurationSeconds), 0f, 1f));
            scale = MathHelper.Lerp(RingStartScale, RingEndScale, t);
            alpha = MathHelper.Lerp(0.9f, 0f, t);
            borderPx = MathHelper.Lerp(RingStartBorderPx, 1f, t);
        }

        private void SampleImpactFlash(float impactElapsed, out float scale, out float alpha)
        {
            float t = EaseOut(MathHelper.Clamp(impactElapsed / Math.Max(0.0001f, FlashDurationSeconds), 0f, 1f));
            scale = MathHelper.Lerp(0.5f, 1.8f, t);
            alpha = MathHelper.Lerp(1f, 0f, t);
        }

        private void SampleImpactDust(float impactElapsed, out Vector2 scale, out float alpha, out float yOffset)
        {
            float t = EaseOut(MathHelper.Clamp(impactElapsed / Math.Max(0.0001f, DustDurationSeconds), 0f, 1f));
            scale = new Vector2(MathHelper.Lerp(0.4f, 1.6f, t), MathHelper.Lerp(0.6f, 0.3f, t));
            alpha = MathHelper.Lerp(0.7f, 0f, t);
            yOffset = MathHelper.Lerp(0f, 8f, t);
        }

        private static float EaseOut(float t) => 1f - (1f - t) * (1f - t);

        private static Vector2 RotateLocalOffset(float localX, float localY, float cardRotation)
        {
            float cos = MathF.Cos(cardRotation);
            float sin = MathF.Sin(cardRotation);
            return new Vector2(cos * localX - sin * localY, sin * localX + cos * localY);
        }

        private static float CubicBezierEase(float t, float p1x, float p1y, float p2x, float p2y)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            if (t <= 0f) return 0f;
            if (t >= 1f) return 1f;

            float s = t;
            for (int i = 0; i < 8; i++)
            {
                float x = BezierComponent(s, p1x, p2x);
                float dx = BezierDerivative(s, p1x, p2x);
                if (MathF.Abs(dx) < 0.0001f) break;
                s -= (x - t) / dx;
                s = MathHelper.Clamp(s, 0f, 1f);
            }

            for (int i = 0; i < 12; i++)
            {
                float x = BezierComponent(s, p1x, p2x);
                if (MathF.Abs(x - t) < 0.0001f) break;
                if (x < t) s = Math.Min(1f, s + 0.0625f);
                else s = Math.Max(0f, s - 0.0625f);
            }

            return BezierComponent(s, p1y, p2y);
        }

        private static float BezierComponent(float s, float p1, float p2)
        {
            float inv = 1f - s;
            return 3f * inv * inv * s * p1 + 3f * inv * s * s * p2 + s * s * s;
        }

        private static float BezierDerivative(float s, float p1, float p2)
        {
            float inv = 1f - s;
            return 3f * inv * inv * p1 + 6f * inv * s * (p2 - p1) + 3f * s * s * (1f - p2);
        }
    }
}
