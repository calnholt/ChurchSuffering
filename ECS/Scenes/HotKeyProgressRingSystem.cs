using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    [DebugTab("HotKeys")]
    public class HotKeyProgressRingSystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SystemManager _systemManager;
        private readonly Texture2D _pixel;

        [DebugEditable(DisplayName = "Progress Ring Radius Multiplier", Step = 0.1f, Min = 1f, Max = 3f)]
        public float ProgressRingRadius { get; set; } = 1.5f;

        [DebugEditable(DisplayName = "Progress Ring Thickness (px)", Step = 1f, Min = 1f, Max = 10f)]
        public int ProgressRingThickness { get; set; } = 3;

        [DebugEditable(DisplayName = "Progress Ring Color R", Step = 1f, Min = 0f, Max = 255f)]
        public int ProgressRingColorR { get; set; } = 255;

        [DebugEditable(DisplayName = "Progress Ring Color G", Step = 1f, Min = 0f, Max = 255f)]
        public int ProgressRingColorG { get; set; } = 255;

        [DebugEditable(DisplayName = "Progress Ring Color B", Step = 1f, Min = 0f, Max = 255f)]
        public int ProgressRingColorB { get; set; } = 255;

        [DebugEditable(DisplayName = "Progress Ring Start Angle (deg)", Step = 1f, Min = -360f, Max = 360f)]
        public float ProgressRingStartAngle { get; set; } = -90f;

        public HotKeyProgressRingSystem(
            EntityManager entityManager,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            SystemManager systemManager)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _systemManager = systemManager;
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Enumerable.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        public void Draw()
        {
            if (GameOverOverlayDisplaySystem.IsOverlayActive(EntityManager)) return;

            HotKeySystem hotKeySystem = _systemManager.GetSystem<HotKeySystem>();
            if (hotKeySystem == null) return;
            string contextId = InputContextResolver.ResolveCommandContext(EntityManager);
            bool gameplayBlocked = StateSingleton.PreventClicking
                && contextId == InputContextIds.Gameplay;
            PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);

            foreach ((Entity entity, float elapsed) in hotKeySystem.HoldProgress.ToList())
            {
                HotKey hotKey = entity.GetComponent<HotKey>();
                UIElement ui = entity.GetComponent<UIElement>();
                if (!HotKeySystem.IsHotKeyEligible(entity, hotKey, ui, contextId, gameplayBlocked))
                {
                    continue;
                }

                Rectangle bounds = ui.Bounds;
                if (bounds.Width < 2 || bounds.Height < 2) continue;
                Point hintSize = hotKeySystem.GetHintSize(frame, hotKey);
                if (hintSize == Point.Zero) continue;
                var (centerX, centerY) = hotKeySystem.CalculateHintPosition(
                    bounds,
                    hotKey.Position,
                    hintSize,
                    hotKeySystem.HintGapX,
                    hotKeySystem.HintGapY);
                float progress = MathHelper.Clamp(
                    elapsed / System.Math.Max(0.001f, hotKey.HoldDurationSeconds),
                    0f,
                    1f);
                int radius = (int)System.Math.Round(
                    hotKeySystem.HintRadius * ProgressRingRadius);
                DrawProgressArc(
                    new Vector2(centerX, centerY),
                    radius,
                    ProgressRingThickness,
                    progress,
                    ProgressRingStartAngle,
                    new Color(ProgressRingColorR, ProgressRingColorG, ProgressRingColorB));
            }
        }

        private void DrawProgressArc(
            Vector2 center,
            int radius,
            int thickness,
            float progress,
            float startAngleDegrees,
            Color color)
        {
            if (progress <= 0f || radius < 1 || thickness < 1) return;
            float start = MathHelper.ToRadians(startAngleDegrees);
            float end = start + progress * MathHelper.TwoPi;
            int segments = System.Math.Max(16, (int)System.Math.Round(progress * radius * 4f));
            float step = (end - start) / segments;
            float middleRadius = radius - thickness * 0.5f;
            for (int index = 0; index < segments; index++)
            {
                float angleA = start + step * index;
                float angleB = System.Math.Min(end, start + step * (index + 1.05f));
                Vector2 pointA = center + new Vector2(
                    System.MathF.Cos(angleA),
                    System.MathF.Sin(angleA)) * middleRadius;
                Vector2 pointB = center + new Vector2(
                    System.MathF.Cos(angleB),
                    System.MathF.Sin(angleB)) * middleRadius;
                DrawLine(pointA, pointB, color, thickness);
            }
        }

        private void DrawLine(Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 delta = end - start;
            float length = delta.Length();
            if (length <= 0.001f) return;
            _spriteBatch.Draw(
                _pixel,
                start,
                null,
                color,
                System.MathF.Atan2(delta.Y, delta.X),
                new Vector2(0f, 0.5f),
                new Vector2(length, thickness),
                SpriteEffects.None,
                0f);
        }
    }
}
