using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Animates a played card from Hand to the Discard pile along a slight arc,
    /// shrinking as it flies and leaving a red additive glow trail.
    /// Zone mutation is deferred until the animation completes.
    /// </summary>
    [DebugTab("Card Move Display")]
    public class CardMoveDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;

        private class MoveAnim
        {
            public Entity Card;
            public Entity Deck;
            public string ContextId;
            public CardZoneType Destination;
            public Vector2 Start;
            public Vector2 End;
            public float Duration;
            public float Elapsed;
            public float StartScale;
            public float EndScale;
            public float ArcHeight;
            public readonly List<TrailNode> Trail = new List<TrailNode>();
        }

        private struct TrailNode
        {
            public Vector2 Pos;
            public float Age;
        }

        private readonly List<MoveAnim> _anims = new List<MoveAnim>();

        // Motion tunables
        [DebugEditable(DisplayName = "Duration (s)", Step = 0.01f, Min = 0.05f, Max = 1.0f)]
        public float DurationSeconds { get; set; } = 0.28f;
        [DebugEditable(DisplayName = "Arc Height (px)", Step = 2, Min = -400, Max = 600)]
        public int ArcHeightPx { get; set; } = 140;
        [DebugEditable(DisplayName = "Start Scale", Step = 0.02f, Min = 0.1f, Max = 2.0f)]
        public float StartScale { get; set; } = 1.0f;
        [DebugEditable(DisplayName = "End Scale", Step = 0.02f, Min = 0.05f, Max = 1.0f)]
        public float EndScale { get; set; } = 0.30f;
        [DebugEditable(DisplayName = "Ease In Pow", Step = 0.1f, Min = 0.1f, Max = 5f)]
        public float EaseInPow { get; set; } = 1.0f;

        // Trail tunables (additive)
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
		[DebugEditable(DisplayName = "Show Additive Test Dot")]
		public bool ShowAdditiveTestDot { get; set; } = false;
        [DebugEditable(DisplayName = "Trail Color R", Step = 1, Min = 0, Max = 255)]
        public int TrailR { get; set; } = 255;
        [DebugEditable(DisplayName = "Trail Color G", Step = 1, Min = 0, Max = 255)]
        public int TrailG { get; set; } = 60;
        [DebugEditable(DisplayName = "Trail Color B", Step = 1, Min = 0, Max = 255)]
        public int TrailB { get; set; } = 60;

        public CardMoveDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            EventManager.Subscribe<PlayCardToDiscardAnimationRequested>(OnAnimRequested);
            EventManager.Subscribe<PlayCardToDrawPileAnimationRequested>(OnDrawPileAnimRequested);
            EventManager.Subscribe<DeleteCachesEvent>(_ => _anims.Clear());
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            // Single-frame update driver
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (dt <= 0f) return;
            for (int i = _anims.Count - 1; i >= 0; i--)
            {
                var a = _anims[i];
                a.Elapsed += dt;
                // Age trail nodes and cull old ones
                for (int t = a.Trail.Count - 1; t >= 0; t--)
                {
                    var node = a.Trail[t];
                    node.Age += dt;
                    if (node.Age > TrailLifetime) { a.Trail.RemoveAt(t); }
                    else { a.Trail[t] = node; }
                }
                if (a.Elapsed >= Math.Max(0.001f, a.Duration))
                {
                    // Finalize: request zone mutation and remove animation
                    var deck = a.Deck?.GetComponent<Deck>();
                    LoggingService.Append("CardMoveDisplaySystem.finalize", new System.Text.Json.Nodes.JsonObject
                    {
                        ["entityId"] = a.Card?.Id ?? -1,
                        ["cardId"] = a.Card?.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                        ["destination"] = a.Destination.ToString(),
                        ["contextId"] = a.ContextId,
                        ["duration"] = a.Duration,
                        ["elapsed"] = a.Elapsed,
                        ["inHandBeforeFinalize"] = deck?.Hand.Contains(a.Card) ?? false,
                        ["hasAnimatingHandToZone"] = a.Card?.HasComponent<AnimatingHandToZone>() ?? false,
                        ["hasAnimatingHandToDrawPile"] = a.Card?.HasComponent<AnimatingHandToDrawPile>() ?? false,
                        ["card"] = HandStateLoggingService.BuildCardSnapshot(a.Card)
                    });
                    EventManager.Publish(new CardMoveFinalizeRequested
                    {
                        Card = a.Card,
                        Deck = a.Deck,
                        Destination = a.Destination,
                        ContextId = a.ContextId
                    });
                    _anims.RemoveAt(i);
                }
                else
                {
                    // Append current head position to trail
                    var pos = GetArcPosition(a.Start, a.End, a.ArcHeight, EaseIn(a.Elapsed / Math.Max(0.001f, a.Duration), EaseInPow));
                    a.Trail.Add(new TrailNode { Pos = pos, Age = 0f });
                    _anims[i] = a;
                }
            }
        }

        public void DrawAlpha()
        {
            if (_anims.Count == 0) return;
            for (int i = 0; i < _anims.Count; i++)
            {
                var a = _anims[i];
                float tm = MathHelper.Clamp(a.Elapsed / Math.Max(0.001f, a.Duration), 0f, 1f);
                float t = EaseIn(tm, EaseInPow);
                var pos = GetArcPosition(a.Start, a.End, a.ArcHeight, t);
                float scl = MathHelper.Lerp(a.StartScale, a.EndScale, t);
                EventManager.Publish(new CardRenderScaledRotatedEvent
                {
                    Card = a.Card,
                    Position = pos,
                    Scale = scl
                });
            }
        }

        public void DrawAdditive()
        {
            if (_anims.Count == 0) return;
            // Shared circle for glow; AA circle ensures soft edges
            var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, Math.Max(1, TrailRadiusPx));
            var origin = new Vector2(circle.Width / 2f, circle.Height / 2f);
            var coreColor = new Color(ClampByte(TrailR), ClampByte(TrailG), ClampByte(TrailB));
            var glowColor = coreColor;

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
                        glowColor * (TrailGlowAlpha * w),
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
			// Optional on-screen test dot to validate additive pass
			if (ShowAdditiveTestDot)
			{
				var dot = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, 8);
				var dotOrigin = new Vector2(8, 8);
				_spriteBatch.Draw(dot, new Vector2(Game1.VirtualWidth * 0.5f, Game1.VirtualHeight * 0.5f), null, Color.White * 1f, 0f, dotOrigin, 1f, SpriteEffects.None, 0f);
			}
        }

        private void OnAnimRequested(PlayCardToDiscardAnimationRequested evt)
        {
            if (evt == null || evt.Card == null) return;
            LoggingService.Append("CardMoveDisplaySystem.OnAnimRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = evt.Card?.Id ?? -1,
                ["cardId"] = evt.Card?.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["contextId"] = evt.ContextId,
                ["deckHandContains"] = evt.Deck?.GetComponent<Deck>()?.Hand.Contains(evt.Card) ?? false,
                ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
            });
            var t = evt.Card.GetComponent<Transform>();
            var ui = evt.Card.GetComponent<UIElement>();
            if (t == null && ui == null) return;
            var startPos = t?.Position ?? Vector2.Zero;
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
            {
                startPos = new Vector2(ui.Bounds.Center.X, ui.Bounds.Center.Y);
            }
            
            // Read destination from component (supports both Discard and Exhaust)
            var animZone = evt.Card.GetComponent<AnimatingHandToZone>();
            var destination = animZone?.Destination ?? CardZoneType.DiscardPile;
            
            var anim = new MoveAnim
            {
                Card = evt.Card,
                Deck = evt.Deck,
                ContextId = evt.ContextId,
                Destination = destination,
                Start = startPos,
                End = ResolveDiscardAnchor(),
                Duration = DurationSeconds,
                Elapsed = 0f,
                StartScale = StartScale,
                EndScale = EndScale,
                ArcHeight = ArcHeightPx
            };
            _anims.Add(anim);
            LoggingService.Append("CardMoveDisplaySystem.animationQueued", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = evt.Card.Id,
                ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["destination"] = destination.ToString(),
                ["contextId"] = evt.ContextId,
                ["startX"] = startPos.X,
                ["startY"] = startPos.Y,
                ["endX"] = anim.End.X,
                ["endY"] = anim.End.Y,
                ["duration"] = anim.Duration,
                ["activeAnimationCount"] = _anims.Count
            });
        }

        private void OnDrawPileAnimRequested(PlayCardToDrawPileAnimationRequested evt)
        {
            if (evt == null || evt.Card == null) return;
            LoggingService.Append("CardMoveDisplaySystem.OnDrawPileAnimRequested", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = evt.Card?.Id ?? -1,
                ["cardId"] = evt.Card?.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["contextId"] = evt.ContextId,
                ["deckHandContains"] = evt.Deck?.GetComponent<Deck>()?.Hand.Contains(evt.Card) ?? false,
                ["card"] = HandStateLoggingService.BuildCardSnapshot(evt.Card)
            });
            var t = evt.Card.GetComponent<Transform>();
            var ui = evt.Card.GetComponent<UIElement>();
            if (t == null && ui == null) return;
            var startPos = t?.Position ?? Vector2.Zero;
            if (ui != null && ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
            {
                startPos = new Vector2(ui.Bounds.Center.X, ui.Bounds.Center.Y);
            }
            var anim = new MoveAnim
            {
                Card = evt.Card,
                Deck = evt.Deck,
                ContextId = evt.ContextId,
                Destination = CardZoneType.DrawPile,
                Start = startPos,
                End = ResolveDrawPileAnchor(),
                Duration = DurationSeconds,
                Elapsed = 0f,
                StartScale = StartScale,
                EndScale = EndScale,
                ArcHeight = ArcHeightPx
            };
            _anims.Add(anim);
            LoggingService.Append("CardMoveDisplaySystem.animationQueued", new System.Text.Json.Nodes.JsonObject
            {
                ["entityId"] = evt.Card.Id,
                ["cardId"] = evt.Card.GetComponent<CardData>()?.Card?.CardId ?? "unknown",
                ["destination"] = CardZoneType.DrawPile.ToString(),
                ["contextId"] = evt.ContextId,
                ["startX"] = startPos.X,
                ["startY"] = startPos.Y,
                ["endX"] = anim.End.X,
                ["endY"] = anim.End.Y,
                ["duration"] = anim.Duration,
                ["activeAnimationCount"] = _anims.Count
            });
        }

        private Vector2 ResolveDiscardAnchor()
        {
            var root = EntityManager.GetEntity("UI_DiscardPileRoot");
            var tr = root?.GetComponent<Transform>();
            if (tr != null) return tr.Position;
            return new Vector2(60, Game1.VirtualHeight - 60);
        }

        private Vector2 ResolveDrawPileAnchor()
        {
            var root = EntityManager.GetEntity("UI_DrawPileRoot");
            var tr = root?.GetComponent<Transform>();
            if (tr != null) return tr.Position;
            return new Vector2(Game1.VirtualWidth - 60, Game1.VirtualHeight - 60);
        }

        private static float EaseIn(float t, float pow)
        {
            t = MathHelper.Clamp(t, 0f, 1f);
            return (float)Math.Pow(t, pow);
        }

        private static Vector2 GetArcPosition(Vector2 a, Vector2 b, float arcHeight, float t)
        {
            // Lerp + perpendicular sinusoidal offset
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
    }
}





