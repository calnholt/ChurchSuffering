using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Events;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Renders the draw pile asset at bottom-right with the current draw pile count.
    /// </summary>
    [DebugTab("Draw Pile Display")]
    public class DrawPileDisplaySystem : Core.System
    {
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private readonly SpriteFont _font = FontSingleton.ContentFont;
        private Texture2D _pileTexture;
        private const string RootEntityName = "UI_DrawPileRoot";
        private const string PileAsset = "Battle_UI/draw_pile";
        private double _pulseTimeRemaining = 0.0;
        private const double PulseDuration = 0.15; // seconds
        private const float PulseAmplitude = 0.12f; // 12% size bump at peak

        [DebugEditable(DisplayName = "Asset Scale", Step = 0.01f, Min = 0.01f, Max = 5f)]
        public float AssetScale { get; set; } = 0.25f;
        [DebugEditable(DisplayName = "Panel Margin", Step = 1, Min = 0, Max = 500)]
        public int PanelMargin { get; set; } = 30;
        [DebugEditable(DisplayName = "Text Scale", Step = 0.01f, Min = 0.01f, Max = 10f)]
        public float TextScale { get; set; } = 0.23f;
        [DebugEditable(DisplayName = "Count Text Offset Y", Step = 1, Min = 0, Max = 500)]
        public int CountTextOffsetY { get; set; } = 2;
        [DebugEditable(DisplayName = "Count Text Color R", Step = 1, Min = 0, Max = 255)]
        public int CountTextColorR { get; set; } = 150;
        [DebugEditable(DisplayName = "Count Text Color G", Step = 1, Min = 0, Max = 255)]
        public int CountTextColorG { get; set; } = 0;
        [DebugEditable(DisplayName = "Count Text Color B", Step = 1, Min = 0, Max = 255)]
        public int CountTextColorB { get; set; } = 0;

        public DrawPileDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
            : base(entityManager)
        {
            _spriteBatch = spriteBatch;
            _imageAssets = imageAssets;

            EventManager.Subscribe<CardsDrawnEvent>(OnCardsDrawn);
            LoggingService.Append("DrawPileDisplaySystem.constructor", new System.Text.Json.Nodes.JsonObject { ["action"] = "Subscribed to CardsDrawnEvent" });
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<Deck>();
        }

        public override void Update(GameTime gameTime)
        {
            if (_pulseTimeRemaining > 0.0)
            {
                _pulseTimeRemaining = Math.Max(0.0, _pulseTimeRemaining - gameTime.ElapsedGameTime.TotalSeconds);
            }
            base.Update(gameTime);
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            EnsurePileTexture();
            EnsureRootEntity();
            int vw = Game1.VirtualWidth;
            int vh = Game1.VirtualHeight;
            var root = EntityManager.GetEntity(RootEntityName);
            var t = root?.GetComponent<Transform>();
            if (t != null && _pileTexture != null)
            {
                int displayW = GetDisplayWidth(1f);
                int displayH = GetDisplayHeight(1f);
                int m = PanelMargin;
                var center = new Vector2(vw - displayW / 2f - m, vh - displayH / 2f - m);
                t.Position = center;
            }
        }

        public void Draw()
        {
            EnsurePileTexture();
            if (_pileTexture == null) return;

            var deckEntity = GetRelevantEntities().FirstOrDefault();
            if (deckEntity == null) return;
            var deck = deckEntity.GetComponent<Deck>();
            if (deck == null) return;

            var root = EntityManager.GetEntity(RootEntityName);
            var tRoot = root?.GetComponent<Transform>();
            if (tRoot == null) return;

            float pulseScale = GetPulseScale();
            var center = new Vector2(tRoot.Position.X, tRoot.Position.Y);
            var bounds = GetBounds(center, pulseScale);

            _spriteBatch.Draw(_pileTexture, bounds, Color.White);

            if (_font != null)
            {
                string text = deck.DrawPile.Count.ToString();
                float textScale = TextScale * pulseScale;
                var countColor = new Color(
                    Math.Clamp(CountTextColorR, 0, 255),
                    Math.Clamp(CountTextColorG, 0, 255),
                    Math.Clamp(CountTextColorB, 0, 255));
                var size = _font.MeasureString(text) * textScale;
                var pos = new Vector2(
                    bounds.Center.X - size.X / 2f,
                    bounds.Bottom - size.Y - CountTextOffsetY * pulseScale);
                _spriteBatch.DrawString(_font, text, pos, countColor, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
            }

            var rootUi = root.GetComponent<UIElement>();
            if (rootUi == null)
            {
                EntityManager.AddComponent(root, new UIElement { Bounds = bounds, IsInteractable = true, Tooltip = "View Draw Pile", EventType = UIElementEventType.ViewDeck });
            }
            else
            {
                rootUi.Bounds = bounds;
                rootUi.IsInteractable = true;
                rootUi.Tooltip = "View Draw Pile";
            }
        }

        private float GetPulseScale()
        {
            if (_pulseTimeRemaining <= 0.0) return 1f;
            float t = (float)(1.0 - (_pulseTimeRemaining / PulseDuration));
            float wave = (float)Math.Sin(t * Math.PI);
            return 1f + PulseAmplitude * wave;
        }

        private float GetDisplayScale(float pulseScale) => AssetScale * pulseScale;

        private int GetDisplayWidth(float pulseScale) =>
            (int)Math.Round(_pileTexture.Width * GetDisplayScale(pulseScale));

        private int GetDisplayHeight(float pulseScale) =>
            (int)Math.Round(_pileTexture.Height * GetDisplayScale(pulseScale));

        private Rectangle GetBounds(Vector2 center, float pulseScale)
        {
            int w = GetDisplayWidth(pulseScale);
            int h = GetDisplayHeight(pulseScale);
            return new Rectangle(
                (int)Math.Round(center.X - w / 2f),
                (int)Math.Round(center.Y - h / 2f),
                w,
                h);
        }

        private void EnsurePileTexture()
        {
            if (_pileTexture != null) return;
            _pileTexture = _imageAssets.TryGetTexture(PileAsset);
        }

        private void OnCardsDrawn(CardsDrawnEvent evt)
        {
            _pulseTimeRemaining = PulseDuration;
        }

        private void EnsureRootEntity()
        {
            var e = EntityManager.GetEntity(RootEntityName);
            if (e == null)
            {
                e = EntityManager.CreateEntity(RootEntityName);
                int vw = Game1.VirtualWidth;
                int vh = Game1.VirtualHeight;
                int displayW = _pileTexture != null ? GetDisplayWidth(1f) : 0;
                int displayH = _pileTexture != null ? GetDisplayHeight(1f) : 0;
                int m = PanelMargin;
                var center = new Vector2(vw - displayW / 2f - m, vh - displayH / 2f - m);
                EntityManager.AddComponent(e, new Transform { Position = center, ZOrder = 10000 });
                EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
            }
        }
    }
}
