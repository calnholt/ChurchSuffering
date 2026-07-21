using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Animates the plunder "snatch" effect when an enemy steals a card from the player's deck.
    /// Multi-phase animation: Lift -> Pause -> Arc -> Settle -> Complete
    /// </summary>
    [DebugTab("Plunder Snatch Display")]
    public class PlunderSnatchDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;

        private class SnatchAnim
        {
            public Entity Card;
            public PlunderSnatchPhase Phase;
            public Vector2 OriginPos;
            public Vector2 LiftedPos;
            public Vector2 TargetPos;
            public float PhaseElapsed;
            public float CurrentScale;
            public float CurrentRotation;
            public int DamageThreshold;
            public readonly List<TrailNode> Trail = new List<TrailNode>();
        }

        private struct TrailNode
        {
            public Vector2 Pos;
            public float Age;
        }

        private readonly List<SnatchAnim> _anims = new List<SnatchAnim>();

        private class RescueAnim
        {
            public Entity Card;
            public PlunderRescuePhase Phase;
            public Vector2 StartPos;
            public Vector2 TargetPos;
            public float PhaseElapsed;
            public float CurrentScale;
            public float CurrentRotation;
            public readonly List<TrailNode> Trail = new List<TrailNode>();
        }

        private readonly List<RescueAnim> _rescueAnims = new List<RescueAnim>();

        #region Phase Durations
        [DebugEditable(DisplayName = "Lift Duration (s)", Step = 0.01f, Min = 0.01f, Max = 0.5f)]
        public float LiftDuration { get; set; } = 0.12f;

        [DebugEditable(DisplayName = "Pause Duration (s)", Step = 0.01f, Min = 0.01f, Max = 0.5f)]
        public float PauseDuration { get; set; } = 0.15f;

        [DebugEditable(DisplayName = "Arc Duration (s)", Step = 0.01f, Min = 0.1f, Max = 1.0f)]
        public float ArcDuration { get; set; } = 0.35f;

        [DebugEditable(DisplayName = "Settle Duration (s)", Step = 0.01f, Min = 0.01f, Max = 0.3f)]
        public float SettleDuration { get; set; } = 0.1f;
        #endregion

        #region Animation Parameters
        [DebugEditable(DisplayName = "Lift Height (px)", Step = 5, Min = 0, Max = 100)]
        public int LiftHeightPx { get; set; } = 20;

        [DebugEditable(DisplayName = "Lift Scale", Step = 0.02f, Min = 1.0f, Max = 1.5f)]
        public float LiftScale { get; set; } = 1.15f;

        [DebugEditable(DisplayName = "Arc Height (px)", Step = 10, Min = 50, Max = 400)]
        public int ArcHeightPx { get; set; } = 180;

        [DebugEditable(DisplayName = "End Scale", Step = 0.02f, Min = 0.3f, Max = 1.0f)]
        public float EndScale { get; set; } = 0.55f;

        [DebugEditable(DisplayName = "Ease Power", Step = 0.1f, Min = 1.0f, Max = 5.0f)]
        public float EasePower { get; set; } = 2.5f;

        [DebugEditable(DisplayName = "Max Rotation (deg)", Step = 1, Min = 0, Max = 90)]
        public int MaxRotationDeg { get; set; } = 25;

        [DebugEditable(DisplayName = "Settle Bounce Scale", Step = 0.01f, Min = 0.5f, Max = 0.8f)]
        public float SettleBounceScale { get; set; } = 0.58f;
        #endregion

        #region Trail Settings
        [DebugEditable(DisplayName = "Trail Lifetime (s)", Step = 0.01f, Min = 0.05f, Max = 1.0f)]
        public float TrailLifetime { get; set; } = 0.35f;

        [DebugEditable(DisplayName = "Trail Core Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
        public float TrailCoreAlpha { get; set; } = 0.50f;

        [DebugEditable(DisplayName = "Trail Glow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
        public float TrailGlowAlpha { get; set; } = 0.20f;

        [DebugEditable(DisplayName = "Trail Core Scale", Step = 0.01f, Min = 0.05f, Max = 2.0f)]
        public float TrailCoreScale { get; set; } = 0.30f;

        [DebugEditable(DisplayName = "Trail Glow Scale", Step = 0.01f, Min = 0.1f, Max = 3.0f)]
        public float TrailGlowScale { get; set; } = 0.9f;

        [DebugEditable(DisplayName = "Trail Radius (px)", Step = 1, Min = 2, Max = 128)]
        public int TrailRadiusPx { get; set; } = 32;

        [DebugEditable(DisplayName = "Trail Color R", Step = 5, Min = 0, Max = 255)]
        public int TrailR { get; set; } = 160;

        [DebugEditable(DisplayName = "Trail Color G", Step = 5, Min = 0, Max = 255)]
        public int TrailG { get; set; } = 50;

        [DebugEditable(DisplayName = "Trail Color B", Step = 5, Min = 0, Max = 255)]
        public int TrailB { get; set; } = 180;
        #endregion

        #region Rescue Animation Parameters
        [DebugEditable(DisplayName = "Rescue Arc Duration (s)", Step = 0.01f, Min = 0.1f, Max = 1.0f)]
        public float RescueArcDuration { get; set; } = 0.40f;

        [DebugEditable(DisplayName = "Rescue Settle Duration (s)", Step = 0.01f, Min = 0.01f, Max = 0.3f)]
        public float RescueSettleDuration { get; set; } = 0.12f;

        [DebugEditable(DisplayName = "Rescue Arc Height (px)", Step = 10, Min = 50, Max = 400)]
        public int RescueArcHeightPx { get; set; } = 150;

        [DebugEditable(DisplayName = "Rescue Start Scale", Step = 0.02f, Min = 0.3f, Max = 1.0f)]
        public float RescueStartScale { get; set; } = 0.55f;

        [DebugEditable(DisplayName = "Rescue End Scale", Step = 0.02f, Min = 0.8f, Max = 1.5f)]
        public float RescueEndScale { get; set; } = 1.0f;

        [DebugEditable(DisplayName = "Rescue Trail Color R", Step = 5, Min = 0, Max = 255)]
        public int RescueTrailR { get; set; } = 50;

        [DebugEditable(DisplayName = "Rescue Trail Color G", Step = 5, Min = 0, Max = 255)]
        public int RescueTrailG { get; set; } = 200;

        [DebugEditable(DisplayName = "Rescue Trail Color B", Step = 5, Min = 0, Max = 255)]
        public int RescueTrailB { get; set; } = 100;
        #endregion

        public PlunderSnatchDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            EventManager.Subscribe<PlunderSnatchAnimationRequested>(OnAnimRequested);
            EventManager.Subscribe<PlunderRescueAnimationRequested>(OnRescueAnimRequested);
            EventManager.Subscribe<DeleteCachesEvent>(_ => { _anims.Clear(); _rescueAnims.Clear(); });
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f) return;

            for (int i = _anims.Count - 1; i >= 0; i--)
            {
                var a = _anims[i];
                a.PhaseElapsed += dt;

                // Age trail nodes and cull old ones
                for (int t = a.Trail.Count - 1; t >= 0; t--)
                {
                    var node = a.Trail[t];
                    node.Age += dt;
                    if (node.Age > TrailLifetime) { a.Trail.RemoveAt(t); }
                    else { a.Trail[t] = node; }
                }

                UpdatePhase(a);

                if (a.Phase == PlunderSnatchPhase.Complete)
                {
                    // Animation done - publish completion event
                    EventManager.Publish(new PlunderSnatchAnimationCompleted
                    {
                        Card = a.Card,
                        DamageThreshold = a.DamageThreshold
                    });
                    _anims.RemoveAt(i);
                }
            }

            // Process rescue animations
            for (int i = _rescueAnims.Count - 1; i >= 0; i--)
            {
                var r = _rescueAnims[i];
                r.PhaseElapsed += dt;

                // Age trail nodes and cull old ones
                for (int t = r.Trail.Count - 1; t >= 0; t--)
                {
                    var node = r.Trail[t];
                    node.Age += dt;
                    if (node.Age > TrailLifetime) { r.Trail.RemoveAt(t); }
                    else { r.Trail[t] = node; }
                }

                UpdateRescuePhase(r);

                if (r.Phase == PlunderRescuePhase.Complete)
                {
                    EventManager.Publish(new PlunderRescueAnimationCompleted { Card = r.Card });
                    _rescueAnims.RemoveAt(i);
                }
            }
        }

        private void UpdatePhase(SnatchAnim a)
        {
            switch (a.Phase)
            {
                case PlunderSnatchPhase.Lift:
                    UpdateLift(a);
                    break;
                case PlunderSnatchPhase.Pause:
                    UpdatePause(a);
                    break;
                case PlunderSnatchPhase.Arc:
                    UpdateArc(a);
                    break;
                case PlunderSnatchPhase.Settle:
                    UpdateSettle(a);
                    break;
            }
        }

        private void UpdateLift(SnatchAnim a)
        {
            float t = MathHelper.Clamp(a.PhaseElapsed / Math.Max(0.001f, LiftDuration), 0f, 1f);
            float eased = EaseOut(t);

            // Position: rise upward
            a.LiftedPos = new Vector2(a.OriginPos.X, a.OriginPos.Y - LiftHeightPx);
            var currentPos = Vector2.Lerp(a.OriginPos, a.LiftedPos, eased);

            // Scale: grow slightly
            a.CurrentScale = MathHelper.Lerp(1.0f, LiftScale, eased);

            // Update component for rendering
            UpdateFlightComponent(a, currentPos);

            if (a.PhaseElapsed >= LiftDuration)
            {
                a.Phase = PlunderSnatchPhase.Pause;
                a.PhaseElapsed = 0f;
            }
        }

        private void UpdatePause(SnatchAnim a)
        {
            // Hold position and scale at lifted state
            UpdateFlightComponent(a, a.LiftedPos);

            if (a.PhaseElapsed >= PauseDuration)
            {
                a.Phase = PlunderSnatchPhase.Arc;
                a.PhaseElapsed = 0f;
            }
        }

        private void UpdateArc(SnatchAnim a)
        {
            float t = MathHelper.Clamp(a.PhaseElapsed / Math.Max(0.001f, ArcDuration), 0f, 1f);
            float eased = EaseIn(t, EasePower);

            // Position: arc from lifted to target
            var pos = GetArcPosition(a.LiftedPos, a.TargetPos, ArcHeightPx, eased);

            // Scale: shrink from lift scale to end scale
            a.CurrentScale = MathHelper.Lerp(LiftScale, EndScale, eased);

            // Rotation: sin curve that peaks mid-flight
            float rotationRad = (float)Math.Sin(Math.PI * t) * MathHelper.ToRadians(MaxRotationDeg);
            a.CurrentRotation = rotationRad;

            // Add trail node during arc phase
            a.Trail.Add(new TrailNode { Pos = pos, Age = 0f });

            UpdateFlightComponent(a, pos);

            if (a.PhaseElapsed >= ArcDuration)
            {
                a.Phase = PlunderSnatchPhase.Settle;
                a.PhaseElapsed = 0f;
            }
        }

        private void UpdateSettle(SnatchAnim a)
        {
            float t = MathHelper.Clamp(a.PhaseElapsed / Math.Max(0.001f, SettleDuration), 0f, 1f);
            float eased = EaseOut(t);

            // Subtle bounce: scale slightly larger then back to end scale
            a.CurrentScale = MathHelper.Lerp(SettleBounceScale, EndScale, eased);

            // Rotation: ease back to 0
            a.CurrentRotation = MathHelper.Lerp(a.CurrentRotation, 0f, eased);

            UpdateFlightComponent(a, a.TargetPos);

            if (a.PhaseElapsed >= SettleDuration)
            {
                a.Phase = PlunderSnatchPhase.Complete;
            }
        }

        private void UpdateFlightComponent(SnatchAnim a, Vector2 pos)
        {
            var flight = a.Card.GetComponent<PlunderSnatchFlight>();
            if (flight != null)
            {
                flight.CurrentPos = pos;
                flight.CurrentScale = a.CurrentScale;
                flight.CurrentRotation = a.CurrentRotation;
                flight.Phase = a.Phase;
            }
        }

        private void UpdateRescuePhase(RescueAnim r)
        {
            switch (r.Phase)
            {
                case PlunderRescuePhase.Arc:
                    UpdateRescueArc(r);
                    break;
                case PlunderRescuePhase.Settle:
                    UpdateRescueSettle(r);
                    break;
            }
        }

        private void UpdateRescueArc(RescueAnim r)
        {
            float t = MathHelper.Clamp(r.PhaseElapsed / Math.Max(0.001f, RescueArcDuration), 0f, 1f);
            float eased = EaseOut(t);

            // Position: arc from start (enemy) to target (offscreen right for hand entry)
            var pos = GetArcPosition(r.StartPos, r.TargetPos, RescueArcHeightPx, eased);

            // Scale: grow from plundered scale to full scale
            r.CurrentScale = MathHelper.Lerp(RescueStartScale, RescueEndScale, eased);

            // Rotation: sin curve that peaks mid-flight (opposite direction from snatch)
            float rotationRad = (float)Math.Sin(Math.PI * t) * MathHelper.ToRadians(-MaxRotationDeg);
            r.CurrentRotation = rotationRad;

            // Add trail node during arc phase
            r.Trail.Add(new TrailNode { Pos = pos, Age = 0f });

            UpdateRescueFlightComponent(r, pos);

            if (r.PhaseElapsed >= RescueArcDuration)
            {
                r.Phase = PlunderRescuePhase.Settle;
                r.PhaseElapsed = 0f;
            }
        }

        private void UpdateRescueSettle(RescueAnim r)
        {
            float t = MathHelper.Clamp(r.PhaseElapsed / Math.Max(0.001f, RescueSettleDuration), 0f, 1f);
            float eased = EaseOut(t);

            // Scale: subtle bounce then settle
            float bounceScale = RescueEndScale * 1.05f;
            r.CurrentScale = MathHelper.Lerp(bounceScale, RescueEndScale, eased);

            // Rotation: ease back to 0
            r.CurrentRotation = MathHelper.Lerp(r.CurrentRotation, 0f, eased);

            UpdateRescueFlightComponent(r, r.TargetPos);

            if (r.PhaseElapsed >= RescueSettleDuration)
            {
                r.Phase = PlunderRescuePhase.Complete;
            }
        }

        private void UpdateRescueFlightComponent(RescueAnim r, Vector2 pos)
        {
            var flight = r.Card.GetComponent<PlunderRescueFlight>();
            if (flight != null)
            {
                flight.CurrentPos = pos;
                flight.CurrentScale = r.CurrentScale;
                flight.CurrentRotation = r.CurrentRotation;
                flight.Phase = r.Phase;
            }
        }

        public void Draw()
        {
            if (_anims.Count == 0 && _rescueAnims.Count == 0) return;

            // Draw trails first (behind cards)
            DrawTrails();
            DrawRescueTrails();

            // Draw snatch animation cards
            for (int i = 0; i < _anims.Count; i++)
            {
                var a = _anims[i];
                var flight = a.Card.GetComponent<PlunderSnatchFlight>();
                if (flight == null) continue;

                // Use CardRenderScaledRotatedEvent to render the card
                var t = a.Card.GetComponent<Transform>();
                if (t != null)
                {
                    t.Rotation = flight.CurrentRotation;
                }

                EventManager.Publish(new CardRenderScaledRotatedEvent
                {
                    Card = a.Card,
                    Position = flight.CurrentPos,
                    Scale = flight.CurrentScale
                });
            }

            // Draw rescue animation cards
            for (int i = 0; i < _rescueAnims.Count; i++)
            {
                var r = _rescueAnims[i];
                var flight = r.Card.GetComponent<PlunderRescueFlight>();
                if (flight == null) continue;

                var t = r.Card.GetComponent<Transform>();
                if (t != null)
                {
                    t.Rotation = flight.CurrentRotation;
                }

                EventManager.Publish(new CardRenderScaledRotatedEvent
                {
                    Card = r.Card,
                    Position = flight.CurrentPos,
                    Scale = flight.CurrentScale
                });
            }
        }

        private void DrawTrails()
        {
            var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, Math.Max(1, TrailRadiusPx));
            var origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
            var coreColor = new Color(ClampByte(TrailR), ClampByte(TrailG), ClampByte(TrailB));

            for (int i = 0; i < _anims.Count; i++)
            {
                var a = _anims[i];
                for (int j = 0; j < a.Trail.Count; j++)
                {
                    var node = a.Trail[j];
                    float w = 1f - MathHelper.Clamp(node.Age / Math.Max(0.001f, TrailLifetime), 0f, 1f);

                    // Outer glow
                    _spriteBatch.Draw(
                        circle,
                        node.Pos,
                        null,
                        coreColor * (TrailGlowAlpha * w),
                        0f,
                        origin,
                        TrailGlowScale * MathHelper.Lerp(0.6f, 1f, w),
                        SpriteEffects.None,
                        0f
                    );

                    // Inner core
                    _spriteBatch.Draw(
                        circle,
                        node.Pos,
                        null,
                        coreColor * (TrailCoreAlpha * w),
                        0f,
                        origin,
                        TrailCoreScale * MathHelper.Lerp(0.5f, 1f, w),
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void DrawRescueTrails()
        {
            var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, Math.Max(1, TrailRadiusPx));
            var origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
            var coreColor = new Color(ClampByte(RescueTrailR), ClampByte(RescueTrailG), ClampByte(RescueTrailB));

            for (int i = 0; i < _rescueAnims.Count; i++)
            {
                var r = _rescueAnims[i];
                for (int j = 0; j < r.Trail.Count; j++)
                {
                    var node = r.Trail[j];
                    float w = 1f - MathHelper.Clamp(node.Age / Math.Max(0.001f, TrailLifetime), 0f, 1f);

                    // Outer glow
                    _spriteBatch.Draw(
                        circle,
                        node.Pos,
                        null,
                        coreColor * (TrailGlowAlpha * w),
                        0f,
                        origin,
                        TrailGlowScale * MathHelper.Lerp(0.6f, 1f, w),
                        SpriteEffects.None,
                        0f
                    );

                    // Inner core
                    _spriteBatch.Draw(
                        circle,
                        node.Pos,
                        null,
                        coreColor * (TrailCoreAlpha * w),
                        0f,
                        origin,
                        TrailCoreScale * MathHelper.Lerp(0.5f, 1f, w),
                        SpriteEffects.None,
                        0f
                    );
                }
            }
        }

        private void OnAnimRequested(PlunderSnatchAnimationRequested evt)
        {
            LoggingService.Append("PlunderSnatchDisplaySystem.OnAnimRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = evt.Card?.Id ?? -1,
                ["damageThreshold"] = evt.DamageThreshold
            });
            if (evt == null || evt.Card == null) return;

            var anim = new SnatchAnim
            {
                Card = evt.Card,
                Phase = PlunderSnatchPhase.Lift,
                OriginPos = evt.StartPos,
                LiftedPos = new Vector2(evt.StartPos.X, evt.StartPos.Y - LiftHeightPx),
                TargetPos = evt.TargetPos,
                PhaseElapsed = 0f,
                CurrentScale = 1f,
                CurrentRotation = 0f,
                DamageThreshold = evt.DamageThreshold
            };

            // Add flight component to card for tracking
            if (!evt.Card.HasComponent<PlunderSnatchFlight>())
            {
                EntityManager.AddComponent(evt.Card, new PlunderSnatchFlight
                {
                    Owner = evt.Card,
                    Phase = PlunderSnatchPhase.Lift,
                    OriginPos = evt.StartPos,
                    LiftedPos = anim.LiftedPos,
                    TargetPos = evt.TargetPos,
                    CurrentPos = evt.StartPos,
                    PhaseElapsed = 0f,
                    CurrentScale = 1f,
                    CurrentRotation = 0f
                });
            }

            _anims.Add(anim);
        }

        private void OnRescueAnimRequested(PlunderRescueAnimationRequested evt)
        {
            LoggingService.Append("PlunderSnatchDisplaySystem.OnRescueAnimRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["cardId"] = evt.Card?.Id ?? -1
            });
            if (evt == null || evt.Card == null) return;

            var anim = new RescueAnim
            {
                Card = evt.Card,
                Phase = PlunderRescuePhase.Arc,
                StartPos = evt.StartPos,
                TargetPos = evt.TargetPos,
                PhaseElapsed = 0f,
                CurrentScale = RescueStartScale,
                CurrentRotation = 0f
            };

            // Add flight component to card for tracking
            if (!evt.Card.HasComponent<PlunderRescueFlight>())
            {
                EntityManager.AddComponent(evt.Card, new PlunderRescueFlight
                {
                    Owner = evt.Card,
                    Phase = PlunderRescuePhase.Arc,
                    StartPos = evt.StartPos,
                    TargetPos = evt.TargetPos,
                    CurrentPos = evt.StartPos,
                    PhaseElapsed = 0f,
                    CurrentScale = RescueStartScale,
                    CurrentRotation = 0f
                });
            }

            _rescueAnims.Add(anim);
        }

        private static float EaseOut(float t)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return 1f - (float)Math.Pow(1f - t, 2);
        }

        private static float EaseIn(float t, float pow)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return (float)Math.Pow(t, pow);
        }

        private static Vector2 GetArcPosition(Vector2 a, Vector2 b, float arcHeight, float t)
        {
            var p = Vector2.Lerp(a, b, t);
            var ab = b - a;
            if (ab.LengthSquared() < 0.000001f) return p;
            var n = new Vector2(-ab.Y, ab.X);
            n.Normalize();
            // Ensure arc generally goes upward on screen (negative Y)
            if (n.Y > 0f) n = -n;
            float wave = (float)Math.Sin(Math.PI * t);
            return p + n * arcHeight * wave;
        }

        private static byte ClampByte(int v)
        {
            if (v < 0) return 0;
            if (v > 255) return 255;
            return (byte)v;
        }

        public bool IsAnimating => _anims.Count > 0 || _rescueAnims.Count > 0;
    }
}
