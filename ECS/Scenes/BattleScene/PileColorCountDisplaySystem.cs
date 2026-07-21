using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Systems
{
    /// <summary>
    /// Renders vertically-stacked color-coded pills beside each pile panel:
    /// draw pile counts to the left of the bottom-right panel, discard pile counts
    /// to the right of the bottom-left panel. Each stack shows Red, White, and Black
    /// card counts; when colorless cards are present, a fourth gray pill is shown at the bottom.
    /// </summary>
    [DebugTab("Pile Color Count")]
    public class PileColorCountDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;

        private const string DrawRedEntityName       = "UI_ColorCount_Red";
        private const string DrawWhiteEntityName     = "UI_ColorCount_White";
        private const string DrawBlackEntityName     = "UI_ColorCount_Black";
        private const string DrawColorlessEntityName = "UI_ColorCount_Colorless";

        private const string DiscardRedEntityName       = "UI_ColorCount_Discard_Red";
        private const string DiscardWhiteEntityName     = "UI_ColorCount_Discard_White";
        private const string DiscardBlackEntityName     = "UI_ColorCount_Discard_Black";
        private const string DiscardColorlessEntityName = "UI_ColorCount_Discard_Colorless";

        private int _drawRedCount;
        private int _drawWhiteCount;
        private int _drawBlackCount;
        private int _drawColorlessCount;

        private int _discardRedCount;
        private int _discardWhiteCount;
        private int _discardBlackCount;
        private int _discardColorlessCount;

        private Texture2D _pillTex;
        private int _cachedPillWidth;
        private int _cachedPillHeight;
        private int _cachedCornerRadius;

        [DebugEditable(DisplayName = "Pill Width", Step = 1, Min = 10, Max = 500)]
        public int PillWidth { get; set; } = 26;

        [DebugEditable(DisplayName = "Pill Height", Step = 1, Min = 6, Max = 200)]
        public int PillHeight { get; set; } = 22;

        [DebugEditable(DisplayName = "Pill Spacing", Step = 1, Min = 0, Max = 200)]
        public int PillSpacing { get; set; } = 26;

        [DebugEditable(DisplayName = "Pill Gap", Step = 1, Min = 0, Max = 200)]
        public int PillGap { get; set; } = 8;

        [DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 100)]
        public int CornerRadius { get; set; } = 4;

        [DebugEditable(DisplayName = "Text Scale", Step = 0.01f, Min = 0.01f, Max = 2f)]
        public float TextScale { get; set; } = 0.11f;

        // Reference copies of pile display layout — no cross-system coupling
        [DebugEditable(DisplayName = "Pile Ref Width", Step = 1, Min = 0, Max = 500)]
        public int PileRefWidth { get; set; } = 60;

        [DebugEditable(DisplayName = "Pile Ref Height", Step = 1, Min = 0, Max = 500)]
        public int PileRefHeight { get; set; } = 101;

        [DebugEditable(DisplayName = "Pile Ref Margin", Step = 1, Min = 0, Max = 500)]
        public int PileRefMargin { get; set; } = 74;

        [DebugEditable(DisplayName = "Colorless Background R", Step = 1, Min = 0, Max = 255)]
        public int ColorlessBackgroundR { get; set; } = 92;

        [DebugEditable(DisplayName = "Colorless Background G", Step = 1, Min = 0, Max = 255)]
        public int ColorlessBackgroundG { get; set; } = 96;

        [DebugEditable(DisplayName = "Colorless Background B", Step = 1, Min = 0, Max = 255)]
        public int ColorlessBackgroundB { get; set; } = 102;

        [DebugEditable(DisplayName = "Colorless Foreground R", Step = 1, Min = 0, Max = 255)]
        public int ColorlessForegroundR { get; set; } = 235;

        [DebugEditable(DisplayName = "Colorless Foreground G", Step = 1, Min = 0, Max = 255)]
        public int ColorlessForegroundG { get; set; } = 235;

        [DebugEditable(DisplayName = "Colorless Foreground B", Step = 1, Min = 0, Max = 255)]
        public int ColorlessForegroundB { get; set; } = 235;

        public PileColorCountDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = FontSingleton.ContentFont;
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<Deck>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var deck = entity.GetComponent<Deck>();
            if (deck == null) return;

            CountColors(deck.DrawPile, out _drawRedCount, out _drawWhiteCount, out _drawBlackCount, out _drawColorlessCount);
            CountColors(deck.DiscardPile, out _discardRedCount, out _discardWhiteCount, out _discardBlackCount, out _discardColorlessCount);

            EnsureEntitiesForPile(DrawRedEntityName, DrawWhiteEntityName, DrawBlackEntityName, DrawColorlessEntityName);
            EnsureEntitiesForPile(DiscardRedEntityName, DiscardWhiteEntityName, DiscardBlackEntityName, DiscardColorlessEntityName);
            UpdatePositionsForPile(PileAnchorSide.LeftOfPanel, DrawRedEntityName, DrawWhiteEntityName, DrawBlackEntityName, DrawColorlessEntityName, _drawColorlessCount);
            UpdatePositionsForPile(PileAnchorSide.RightOfPanel, DiscardRedEntityName, DiscardWhiteEntityName, DiscardBlackEntityName, DiscardColorlessEntityName, _discardColorlessCount);
            EnsureTexture();
            UpdateUIBoundsForPile(DrawRedEntityName, DrawWhiteEntityName, DrawBlackEntityName, DrawColorlessEntityName, _drawColorlessCount);
            UpdateUIBoundsForPile(DiscardRedEntityName, DiscardWhiteEntityName, DiscardBlackEntityName, DiscardColorlessEntityName, _discardColorlessCount);
        }

        public void Draw(bool showDrawPileCounts, bool showDiscardPileCounts)
        {
            if (showDrawPileCounts)
            {
                DrawPileCounts(
                    DrawRedEntityName,
                    DrawWhiteEntityName,
                    DrawBlackEntityName,
                    DrawColorlessEntityName,
                    _drawRedCount,
                    _drawWhiteCount,
                    _drawBlackCount,
                    _drawColorlessCount);
            }

            if (showDiscardPileCounts)
            {
                DrawPileCounts(
                    DiscardRedEntityName,
                    DiscardWhiteEntityName,
                    DiscardBlackEntityName,
                    DiscardColorlessEntityName,
                    _discardRedCount,
                    _discardWhiteCount,
                    _discardBlackCount,
                    _discardColorlessCount);
            }
        }

        private static void CountColors(
            IReadOnlyList<Entity> pile,
            out int red,
            out int white,
            out int black,
            out int colorless)
        {
            red = 0;
            white = 0;
            black = 0;
            colorless = 0;

            foreach (var cardEntity in pile)
            {
                if (cardEntity.HasComponent<Colorless>())
                {
                    colorless++;
                    continue;
                }

				foreach (var color in CardColorQualificationService.GetQualifiedColors(cardEntity))
				{
					switch (color)
					{
						case CardData.CardColor.Red: red++; break;
						case CardData.CardColor.White: white++; break;
						case CardData.CardColor.Black: black++; break;
					}
				}
            }
        }

        private void DrawPileCounts(
            string redEntityName,
            string whiteEntityName,
            string blackEntityName,
            string colorlessEntityName,
            int redCount,
            int whiteCount,
            int blackCount,
            int colorlessCount)
        {
            DrawPill(redEntityName, new Color(204, 34, 34), Color.White, redCount.ToString());
            DrawPill(whiteEntityName, Color.White, Color.Black, whiteCount.ToString());
            DrawPill(blackEntityName, new Color(20, 20, 20), Color.White, blackCount.ToString());
            if (colorlessCount > 0)
            {
                DrawPill(
                    colorlessEntityName,
                    ColorlessBackground,
                    ColorlessForeground,
                    colorlessCount.ToString());
            }
        }

        private void DrawPill(string entityName, Color bgColor, Color textColor, string text)
        {
            var entity = EntityManager.GetEntity(entityName);
            var t = entity?.GetComponent<Transform>();
            if (t == null || _pillTex == null) return;

            var rect = GetPillRect(t);

            _spriteBatch.Draw(_pillTex, rect, bgColor);

            if (_font == null) return;
            var size = _font.MeasureString(text) * TextScale;
            var pos = new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f);
            _spriteBatch.DrawString(_font, text, pos, textColor, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
        }

        private void UpdateUIBoundsForPile(
            string redEntityName,
            string whiteEntityName,
            string blackEntityName,
            string colorlessEntityName,
            int colorlessCount)
        {
            UpdateUIBoundsForEntity(redEntityName);
            UpdateUIBoundsForEntity(whiteEntityName);
            UpdateUIBoundsForEntity(blackEntityName);
            if (colorlessCount > 0)
                UpdateUIBoundsForEntity(colorlessEntityName);
            else
                ClearUIBoundsForEntity(colorlessEntityName);
        }

        private void UpdateUIBoundsForEntity(string entityName)
        {
            var entity = EntityManager.GetEntity(entityName);
            var t = entity?.GetComponent<Transform>();
            if (t == null) return;

            var rect = GetPillRect(t);
            var ui = entity.GetComponent<UIElement>();
            if (ui == null)
                EntityManager.AddComponent(entity, new UIElement { Bounds = rect, IsInteractable = false });
            else
            {
                ui.Bounds = rect;
                ui.IsInteractable = false;
            }
        }

        private void ClearUIBoundsForEntity(string entityName)
        {
            var entity = EntityManager.GetEntity(entityName);
            if (entity == null) return;

            var ui = entity.GetComponent<UIElement>();
            if (ui == null)
                EntityManager.AddComponent(entity, new UIElement { Bounds = Rectangle.Empty, IsInteractable = false });
            else
            {
                ui.Bounds = Rectangle.Empty;
                ui.IsInteractable = false;
            }
        }

        private Rectangle GetPillRect(Transform t) =>
            new Rectangle(
                (int)Math.Round(t.Position.X - PillWidth / 2f),
                (int)Math.Round(t.Position.Y - PillHeight / 2f),
                PillWidth,
                PillHeight);

        private void EnsureEntitiesForPile(
            string redEntityName,
            string whiteEntityName,
            string blackEntityName,
            string colorlessEntityName)
        {
            EnsureEntity(redEntityName);
            EnsureEntity(whiteEntityName);
            EnsureEntity(blackEntityName);
            EnsureEntity(colorlessEntityName);
        }

        private void EnsureEntity(string name)
        {
            if (EntityManager.GetEntity(name) != null) return;
            var e = EntityManager.CreateEntity(name);
            EntityManager.AddComponent(e, new Transform { Position = Vector2.Zero, ZOrder = 10000 });
            EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
        }

        private void UpdatePositionsForPile(
            PileAnchorSide side,
            string redEntityName,
            string whiteEntityName,
            string blackEntityName,
            string colorlessEntityName,
            int colorlessCount)
        {
            int vw = Game1.VirtualWidth;
            int vh = Game1.VirtualHeight;

            float stackCenterX = side == PileAnchorSide.LeftOfPanel
                ? vw - PileRefWidth - PileRefMargin - PillGap - PillWidth / 2f
                : PileRefWidth + PileRefMargin + PillGap + PillWidth / 2f;
            float stackCenterY = vh - PileRefHeight / 2f - PileRefMargin;

            SetPosition(redEntityName, new Vector2(stackCenterX, stackCenterY - PillSpacing));
            SetPosition(whiteEntityName, new Vector2(stackCenterX, stackCenterY));
            SetPosition(blackEntityName, new Vector2(stackCenterX, stackCenterY + PillSpacing));
            if (colorlessCount > 0)
                SetPosition(colorlessEntityName, new Vector2(stackCenterX, stackCenterY + PillSpacing * 2));
        }

        private void SetPosition(string name, Vector2 position)
        {
            var t = EntityManager.GetEntity(name)?.GetComponent<Transform>();
            if (t != null) t.Position = position;
        }

        private void EnsureTexture()
        {
            if (_pillTex != null &&
                _cachedPillWidth    == PillWidth &&
                _cachedPillHeight   == PillHeight &&
                _cachedCornerRadius == CornerRadius)
                return;

            _pillTex?.Dispose();
            _pillTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, PillWidth, PillHeight, CornerRadius);
            _cachedPillWidth    = PillWidth;
            _cachedPillHeight   = PillHeight;
            _cachedCornerRadius = CornerRadius;
        }

        private Color ColorlessBackground => new(
            ClampByte(ColorlessBackgroundR),
            ClampByte(ColorlessBackgroundG),
            ClampByte(ColorlessBackgroundB));

        private Color ColorlessForeground => new(
            ClampByte(ColorlessForegroundR),
            ClampByte(ColorlessForegroundG),
            ClampByte(ColorlessForegroundB));

        private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);

        private enum PileAnchorSide
        {
            LeftOfPanel,
            RightOfPanel
        }
    }
}
