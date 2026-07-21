using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Draws "Recoil X" text over cards that have the Recoil component.
    /// </summary>
    [DebugTab("Recoil Display")]
    public class RecoilDisplaySystem : Core.System
    {
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font = FontSingleton.ContentFont;
        private readonly GraphicsDevice _graphicsDevice;

        [DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
        public float TextScale { get; set; } = 0.3f;

        [DebugEditable(DisplayName = "Overlay Alpha", Step = 5, Min = 0, Max = 255)]
        public int OverlayAlpha { get; set; } = 120;

        [DebugEditable(DisplayName = "Overlay Corner Radius", Step = 1, Min = 0, Max = 64)]
        public int OverlayCornerRadius { get; set; } = 16;

        [DebugEditable(DisplayName = "Text Red", Step = 5, Min = 0, Max = 255)]
        public int TextRed { get; set; } = 220;

        [DebugEditable(DisplayName = "Text Green", Step = 5, Min = 0, Max = 255)]
        public int TextGreen { get; set; } = 60;

        [DebugEditable(DisplayName = "Text Blue", Step = 5, Min = 0, Max = 255)]
        public int TextBlue { get; set; } = 60;

        [DebugEditable(DisplayName = "Shadow Offset X", Step = 0.5f, Min = -10f, Max = 10f)]
        public float ShadowOffsetX { get; set; } = 3f;

        [DebugEditable(DisplayName = "Shadow Offset Y", Step = 0.5f, Min = -10f, Max = 10f)]
        public float ShadowOffsetY { get; set; } = 3f;

        private readonly System.Collections.Generic.Dictionary<(int w, int h, int r), Texture2D> _roundedRectCache = new();

        public RecoilDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;

            EventManager.Subscribe<CardRenderEvent>(evt => FrameProfiler.Measure("RecoilDisplaySystem.OnCardRenderEvent", () => OnCardRenderEvent(evt)));
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<Recoil>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public void Draw() { }

        private void OnCardRenderEvent(CardRenderEvent evt)
        {
            var card = evt.Card;
            var recoil = card?.GetComponent<Recoil>();
            if (recoil == null || card.GetComponent<SuppressCardVisualEffects>() != null) return;

            var ui = card.GetComponent<UIElement>();
            if (ui == null) return;

            var geometry = CardGeometryService.GetVisualGeometry(EntityManager, card, evt.Position);
            var bounds = geometry.Bounds;
            var center = geometry.Center;

            int cornerRadius = (int)Math.Round(OverlayCornerRadius * geometry.Scale);
            var overlayKey = (bounds.Width, bounds.Height, cornerRadius);
            if (!_roundedRectCache.TryGetValue(overlayKey, out var roundedRect))
            {
                roundedRect = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, bounds.Width, bounds.Height, cornerRadius);
                _roundedRectCache[overlayKey] = roundedRect;
            }
            _spriteBatch.Draw(roundedRect, center, null, new Color(0, 0, 0, OverlayAlpha), geometry.Rotation,
                new Vector2(bounds.Width / 2f, bounds.Height / 2f), 1f, SpriteEffects.None, 0f);

            string text = $"Recoil {recoil.Stacks}";
            var textOrigin = _font.MeasureString(text) / 2f;
            float textScale = TextScale * geometry.Scale;
            var shadowOffset = new Vector2(ShadowOffsetX * geometry.Scale, ShadowOffsetY * geometry.Scale);

            _spriteBatch.DrawString(_font, text, center + shadowOffset,
                Color.Black, geometry.Rotation, textOrigin, textScale, SpriteEffects.None, 0f);

            _spriteBatch.DrawString(_font, text, center, new Color(TextRed, TextGreen, TextBlue),
                geometry.Rotation, textOrigin, textScale, SpriteEffects.None, 0f);
        }
    }
}
