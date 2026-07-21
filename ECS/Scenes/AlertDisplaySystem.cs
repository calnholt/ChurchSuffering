using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Displays temporary alert notifications as animated trapezoids sliding in from the right.
    /// Multiple alerts can be displayed simultaneously, stacking vertically and centered on screen.
    /// Subscribes to AlertEvent for generic alerts and AchievementCompletedEvent for achievements.
    /// </summary>
    [DebugTab("Alert Display")]
    public class AlertDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _titleFont;
        private readonly SpriteFont _typeFont;

        // List of active alerts (can display multiple simultaneously)
        private readonly List<ActiveAlert> _activeAlerts = new();

        private enum AnimationPhase { Entering, Holding, Exiting }

        private class ActiveAlert
        {
            public string Title;
            public string Type;
            public AnimationPhase Phase;
            public float PhaseTime;      // Time in current phase
            public float X;              // Current X position
            public float Y;              // Current Y position
            public float TargetY;        // Target Y position (for smooth repositioning)
            public float Alpha;          // Current alpha (0-1)
        }

        #region Debug Editable Properties - Trapezoid

        [DebugEditable(DisplayName = "Trapezoid Width", Step = 10f, Min = 100f, Max = 800f)]
        public float TrapezoidWidth { get; set; } = 500f;

        [DebugEditable(DisplayName = "Trapezoid Height", Step = 10f, Min = 50f, Max = 200f)]
        public float TrapezoidHeight { get; set; } = 110f;

        [DebugEditable(DisplayName = "Left Side Offset", Step = 5f, Min = 0f, Max = 100f)]
        public float LeftSideOffset { get; set; } = 25f;

        [DebugEditable(DisplayName = "Top Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
        public float TopEdgeAngleDegrees { get; set; } = 3f;

        [DebugEditable(DisplayName = "Right Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
        public float RightEdgeAngleDegrees { get; set; } = -8f;

        [DebugEditable(DisplayName = "Bottom Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
        public float BottomEdgeAngleDegrees { get; set; } = -3f;

        [DebugEditable(DisplayName = "Left Edge Angle (deg)", Step = 1f, Min = -45f, Max = 45f)]
        public float LeftEdgeAngleDegrees { get; set; } = 13f;

        #endregion

        #region Debug Editable Properties - Text

        [DebugEditable(DisplayName = "Title Text Scale", Step = 0.01f, Min = 0.1f, Max = 2f)]
        public float TitleTextScale { get; set; } = 0.25f;

        [DebugEditable(DisplayName = "Type Text Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
        public float TypeTextScale { get; set; } = 0.1f;

        [DebugEditable(DisplayName = "Title Padding X", Step = 5f, Min = 0f, Max = 100f)]
        public float TitlePaddingX { get; set; } = 20f;

        [DebugEditable(DisplayName = "Title Padding Y", Step = 5f, Min = 0f, Max = 100f)]
        public float TitlePaddingY { get; set; } = 12f;

        [DebugEditable(DisplayName = "Type Padding X", Step = 5f, Min = 0f, Max = 100f)]
        public float TypePaddingX { get; set; } = 15f;

        [DebugEditable(DisplayName = "Type Padding Y", Step = 5f, Min = 0f, Max = 100f)]
        public float TypePaddingY { get; set; } = 25f;

        #endregion

        #region Debug Editable Properties - Animation

        [DebugEditable(DisplayName = "Enter Duration (s)", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float EnterDurationSeconds { get; set; } = 0.35f;

        [DebugEditable(DisplayName = "Hold Duration (s)", Step = 0.1f, Min = 0.5f, Max = 10f)]
        public float HoldDurationSeconds { get; set; } = 2f;

        [DebugEditable(DisplayName = "Exit Duration (s)", Step = 0.05f, Min = 0.1f, Max = 2f)]
        public float ExitDurationSeconds { get; set; } = 0.3f;

        [DebugEditable(DisplayName = "Y Reposition Speed", Step = 50f, Min = 100f, Max = 2000f)]
        public float YRepositionSpeed { get; set; } = 600f;

        #endregion

        #region Debug Editable Properties - Position

        [DebugEditable(DisplayName = "Vertical Offset", Step = 10f, Min = -500f, Max = 500f)]
        public float VerticalOffset { get; set; } = -100f;

        [DebugEditable(DisplayName = "Right Margin", Step = 5f, Min = 0f, Max = 200f)]
        public float RightMargin { get; set; } = 10f;

        [DebugEditable(DisplayName = "Alert Spacing", Step = 5f, Min = 0f, Max = 100f)]
        public float AlertSpacing { get; set; } = 15f;

        #endregion

        public AlertDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _titleFont = FontSingleton.TitleFont;
            _typeFont = FontSingleton.ContentFont ?? FontSingleton.TitleFont;

            // Subscribe to generic alert events
            EventManager.Subscribe<AlertEvent>(OnAlertEvent);

            // Subscribe to achievement completion events
            EventManager.Subscribe<AchievementCompletedEvent>(OnAchievementCompleted);
        }

        private void OnAlertEvent(AlertEvent evt)
        {
            if (evt == null) return;
            AddAlert(evt.Title ?? string.Empty, evt.Type ?? string.Empty);
        }

        private void OnAchievementCompleted(AchievementCompletedEvent evt)
        {
            if (evt == null) return;
            AddAlert(evt.Name ?? "Achievement", "Achievement Completed");
        }

        private void AddAlert(string title, string type)
        {
            float screenCenterY = Game1.VirtualHeight / 2f + VerticalOffset;

            var alert = new ActiveAlert
            {
                Title = title,
                Type = type,
                Phase = AnimationPhase.Entering,
                PhaseTime = 0f,
                X = GetOffScreenX(),
                Y = screenCenterY - TrapezoidHeight / 2f, // Start at center, will be adjusted
                TargetY = 0f,
                Alpha = 0f
            };

            _activeAlerts.Add(alert);
            RecalculateTargetPositions();
        }

        /// <summary>
        /// Recalculate target Y positions for all active alerts so they're centered as a group.
        /// </summary>
        private void RecalculateTargetPositions()
        {
            int count = _activeAlerts.Count;
            if (count == 0) return;

            // Total height of all alerts including spacing
            float totalHeight = count * TrapezoidHeight + (count - 1) * AlertSpacing;

            // Center point (with offset)
            float centerY = Game1.VirtualHeight / 2f + VerticalOffset;

            // Top of the first alert
            float startY = centerY - totalHeight / 2f;

            for (int i = 0; i < count; i++)
            {
                _activeAlerts[i].TargetY = startY + i * (TrapezoidHeight + AlertSpacing);
            }
        }

        private float GetOffScreenX()
        {
            return Game1.VirtualWidth + TrapezoidWidth;
        }

        private float GetTargetX()
        {
            return Game1.VirtualWidth - TrapezoidWidth - RightMargin;
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            yield break; // No entities needed
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            if (_activeAlerts.Count == 0) return;

            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            float offScreenX = GetOffScreenX();
            float targetX = GetTargetX();

            // Track alerts to remove after iteration
            var toRemove = new List<ActiveAlert>();

            foreach (var alert in _activeAlerts)
            {
                alert.PhaseTime += dt;

                // Update X and Alpha based on phase
                switch (alert.Phase)
                {
                    case AnimationPhase.Entering:
                        if (alert.PhaseTime >= EnterDurationSeconds)
                        {
                            alert.Phase = AnimationPhase.Holding;
                            alert.PhaseTime = 0f;
                            alert.X = targetX;
                            alert.Alpha = 1f;
                        }
                        else
                        {
                            float progress = alert.PhaseTime / EnterDurationSeconds;
                            float eased = EaseOutCubic(progress);
                            alert.X = MathHelper.Lerp(offScreenX, targetX, eased);
                            alert.Alpha = eased;
                        }
                        break;

                    case AnimationPhase.Holding:
                        alert.X = targetX;
                        alert.Alpha = 1f;
                        if (alert.PhaseTime >= HoldDurationSeconds)
                        {
                            alert.Phase = AnimationPhase.Exiting;
                            alert.PhaseTime = 0f;
                        }
                        break;

                    case AnimationPhase.Exiting:
                        if (alert.PhaseTime >= ExitDurationSeconds)
                        {
                            toRemove.Add(alert);
                        }
                        else
                        {
                            float progress = alert.PhaseTime / ExitDurationSeconds;
                            float eased = EaseInCubic(progress);
                            alert.X = MathHelper.Lerp(targetX, offScreenX, eased);
                            alert.Alpha = 1f - eased;
                        }
                        break;
                }

                // Smoothly animate Y position toward target
                if (Math.Abs(alert.Y - alert.TargetY) > 0.5f)
                {
                    float direction = Math.Sign(alert.TargetY - alert.Y);
                    float maxMove = YRepositionSpeed * dt;
                    float distance = Math.Abs(alert.TargetY - alert.Y);
                    float move = Math.Min(maxMove, distance);
                    alert.Y += direction * move;
                }
                else
                {
                    alert.Y = alert.TargetY;
                }
            }

            // Remove finished alerts and recalculate positions
            if (toRemove.Count > 0)
            {
                foreach (var alert in toRemove)
                {
                    _activeAlerts.Remove(alert);
                }
                RecalculateTargetPositions();
            }
        }

        private float EaseOutCubic(float t)
        {
            float f = t - 1f;
            return f * f * f + 1f;
        }

        private float EaseInCubic(float t)
        {
            return t * t * t;
        }

        public void Draw()
        {
            if (_activeAlerts.Count == 0) return;

            var trapezoidTexture = PrimitiveTextureFactory.GetAntialiasedTrapezoid(
                _graphicsDevice,
                TrapezoidWidth,
                TrapezoidHeight,
                LeftSideOffset,
                TopEdgeAngleDegrees,
                RightEdgeAngleDegrees,
                BottomEdgeAngleDegrees,
                LeftEdgeAngleDegrees
            );
            if (trapezoidTexture == null) return;

            foreach (var alert in _activeAlerts)
            {
                if (string.IsNullOrEmpty(alert.Title) && string.IsNullOrEmpty(alert.Type)) continue;

                Rectangle destRect = new Rectangle(
                    (int)alert.X,
                    (int)alert.Y,
                    (int)TrapezoidWidth,
                    (int)TrapezoidHeight
                );

                // Black trapezoid with alpha
                Color trapezoidColor = Color.Black * alert.Alpha;
                _spriteBatch.Draw(trapezoidTexture, destRect, trapezoidColor);

                // Title text (larger, left side)
                if (_titleFont != null && !string.IsNullOrEmpty(alert.Title))
                {
                    Vector2 titlePos = new Vector2(
                        alert.X + LeftSideOffset + TitlePaddingX,
                        alert.Y + TitlePaddingY
                    );
                    Color titleColor = Color.White * alert.Alpha;
                    _spriteBatch.DrawString(_titleFont, alert.Title, titlePos, titleColor, 0f, Vector2.Zero, TitleTextScale, SpriteEffects.None, 0f);
                }

                // Type text (smaller, bottom-right)
                if (_typeFont != null && !string.IsNullOrEmpty(alert.Type))
                {
                    Vector2 typeSize = _typeFont.MeasureString(alert.Type) * TypeTextScale;
                    Vector2 typePos = new Vector2(
                        alert.X + TrapezoidWidth - typeSize.X - TypePaddingX,
                        alert.Y + TrapezoidHeight - typeSize.Y - TypePaddingY
                    );
                    Color typeColor = Color.White * alert.Alpha;
                    _spriteBatch.DrawString(_typeFont, alert.Type, typePos, typeColor, 0f, Vector2.Zero, TypeTextScale, SpriteEffects.None, 0f);
                }
            }
        }

        /// <summary>
        /// Debug action to test alert display.
        /// </summary>
        [DebugAction("Test Alert")]
        public void TestAlert()
        {
            EventManager.Publish(new AlertEvent
            {
                Title = "Slayed 10 Skeletons",
                Type = "Achievement Completed"
            });
        }

        /// <summary>
        /// Debug action to test multiple alerts at once.
        /// </summary>
        [DebugAction("Test 3 Alerts")]
        public void TestMultipleAlerts()
        {
            EventManager.Publish(new AlertEvent { Title = "First Achievement", Type = "Achievement Completed" });
            EventManager.Publish(new AlertEvent { Title = "Second Achievement", Type = "Achievement Completed" });
            EventManager.Publish(new AlertEvent { Title = "Third Achievement", Type = "Achievement Completed" });
        }
    }
}
