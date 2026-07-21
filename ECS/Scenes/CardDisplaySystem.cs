using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using ChurchSuffering.ECS.Utils;
using ChurchSuffering.Diagnostics;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
    [DebugTab("Card Display")]
    public class CardDisplaySystem : Core.System, IDebugInspectableChildren
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private readonly CardRenderPipeline _pipeline;
        private readonly Dictionary<(string assetName, int artW, int artH, int artX, int artY, int cardW, int cardH, int radius), Texture2D> _clippedArtCache = new();
        private readonly WeightedLruCache<CardBaseRenderModel, CachedCardSurface> _baseSurfaceCache;
        private readonly Texture2D _pixelTexture;
        private SpriteFont _nameFont = FontSingleton.TitleFont;
        private SpriteFont _bodyFont = FontSingleton.ChakraPetchFont;
        private CardGeometrySettings _settings;
        private int _cacheRenderWidth;
        private int _cacheRenderHeight;
        private long _baseCacheHits;
        private long _baseCacheMisses;
        private long _baseCacheBypasses;
        private const long BaseSurfaceCacheBudgetBytes = 64L * 1024L * 1024L;

        internal readonly struct CardDescriptionTextLayout
        {
            public CardDescriptionTextLayout(float contentX, float contentWidth, float wrapScale, int wrapMaxWidth, float drawScale)
            {
                ContentX = contentX;
                ContentWidth = contentWidth;
                WrapScale = wrapScale;
                WrapMaxWidth = wrapMaxWidth;
                DrawScale = drawScale;
            }

            public float ContentX { get; }
            public float ContentWidth { get; }
            public float WrapScale { get; }
            public int WrapMaxWidth { get; }
            public float DrawScale { get; }
        }

        // Stripe
        [DebugEditable(DisplayName = "Stripe Width", Step = 1, Min = 0, Max = 30)]
        public int StripeWidth { get; set; } = 6;

        // Stat Gutter
        [DebugEditable(DisplayName = "Gutter X", Step = 1, Min = 0, Max = 60)]
        public int GutterX { get; set; } = 6;
        [DebugEditable(DisplayName = "Gutter Width", Step = 1, Min = 10, Max = 120)]
        public int GutterWidth { get; set; } = 55;
        [DebugEditable(DisplayName = "Gutter Top Y", Step = 1, Min = 30, Max = 100)]
        public int GutterTopY { get; set; } = 63;

        // Title Band
        [DebugEditable(DisplayName = "Title Band Pad Top", Step = 1, Min = 0, Max = 30)]
        public int TitleBandPadTop { get; set; } = 10;
        [DebugEditable(DisplayName = "Title Band Pad Left", Step = 1, Min = 0, Max = 30)]
        public int TitleBandPadLeft { get; set; } = 12;
        [DebugEditable(DisplayName = "Title Band Pad Right", Step = 1, Min = 0, Max = 30)]
        public int TitleBandPadRight { get; set; } = 12;
        [DebugEditable(DisplayName = "Type Row Margin Top", Step = 1, Min = 0, Max = 12)]
        public int TypeRowMarginTop { get; set; } = 2;
        [DebugEditable(DisplayName = "Rule Margin Top", Step = 1, Min = 0, Max = 12)]
        public int RuleMarginTop { get; set; } = 6;

        // Cost Pips (Diamond)
        [DebugEditable(DisplayName = "Cost Pip Size", Step = 1, Min = 4, Max = 20)]
        public int CostPipSize { get; set; } = 8;
        [DebugEditable(DisplayName = "Cost Pip Gap", Step = 1, Min = 0, Max = 12)]
        public int CostPipGap { get; set; } = 6;
        [DebugEditable(DisplayName = "Cost Label Gap", Step = 1, Min = 0, Max = 20)]
        public int CostLabelGap { get; set; } = 6;
        [DebugEditable(DisplayName = "Cost Label Font Scale", Step = 0.01f, Min = 0.02f, Max = 0.2f)]
        public float CostLabelFontScale { get; set; } = 0.065f;
        [DebugEditable(DisplayName = "Cost Pip Outline Frac", Step = 0.01f, Min = 0.0f, Max = 0.4f)]
        public float CostPipOutlineFrac { get; set; } = 0.15f;
        [DebugEditable(DisplayName = "Cost Pip Flash Min Alpha", Step = 0.01f, Min = 0.2f, Max = 0.5f)]
        public float CostPipFlashMinAlpha { get; set; } = 0.2f;
        [DebugEditable(DisplayName = "Cost Pip Flash Max Alpha", Step = 0.01f, Min = 0.2f, Max = 0.5f)]
        public float CostPipFlashMaxAlpha { get; set; } = 0.5f;
        [DebugEditable(DisplayName = "Cost Pip Flash Hz", Step = 0.01f, Min = 0.05f, Max = 1.0f)]
        public float CostPipFlashHz { get; set; } = 0.3f;

        private float _elapsedTime;
        private float _drawAlpha = 1f;
        private readonly Dictionary<CardType, Texture2D> _typeIconTextures = new();

        // Chip Layout
        [DebugEditable(DisplayName = "Chip Size", Step = 1, Min = 20, Max = 80)]
        public int ChipSize { get; set; } = 38;
        [DebugEditable(DisplayName = "Chip Corner Radius", Step = 1, Min = 0, Max = 16)]
        public int ChipCornerRadius { get; set; } = 4;
        [DebugEditable(DisplayName = "Chip Column X", Step = 1, Min = 0, Max = 60)]
        public int ChipColumnX { get; set; } = 13;
        [DebugEditable(DisplayName = "Chip Column Top Y", Step = 1, Min = 0, Max = 100)]
        public int ChipColumnTopY { get; set; } = 71;
        [DebugEditable(DisplayName = "Chip Slot Height", Step = 1, Min = 40, Max = 100)]
        public int ChipSlotHeight { get; set; } = 72;
        [DebugEditable(DisplayName = "Chip Border Thickness", Step = 1, Min = 1, Max = 6)]
        public int ChipBorderThickness { get; set; } = 3;
        [DebugEditable(DisplayName = "Chip Value Font Scale", Step = 0.01f, Min = 0.05f, Max = 1.0f)]
        public float ChipValueFontScale { get; set; } = 0.22f;
        [DebugEditable(DisplayName = "Chip Width", Step = 1, Min = 20, Max = 80)]
        public int ChipWidth { get; set; } = 42;
        [DebugEditable(DisplayName = "Chip Gap", Step = 1, Min = 0, Max = 20)]
        public int ChipGap { get; set; } = 4;
        [DebugEditable(DisplayName = "Chip Column Bottom Pad", Step = 1, Min = 0, Max = 60)]
        public int ChipColumnBottomPad { get; set; } = 14;

        // Label Slab
        [DebugEditable(DisplayName = "Label Slab Height", Step = 1, Min = 8, Max = 30)]
        public int LabelSlabHeight { get; set; } = 14;
        [DebugEditable(DisplayName = "Label Slab Font Scale", Step = 0.001f, Min = 0.02f, Max = 0.2f)]
        public float LabelSlabFontScale { get; set; } = 0.058f;

        // Delta Slab
        [DebugEditable(DisplayName = "Slab Width", Step = 1, Min = 20, Max = 80)]
        public int SlabWidth { get; set; } = 42;
        [DebugEditable(DisplayName = "Slab Height", Step = 1, Min = 8, Max = 30)]
        public int SlabHeight { get; set; } = 16;
        [DebugEditable(DisplayName = "Slab Corner Radius", Step = 1, Min = 0, Max = 16)]
        public int SlabCornerRadius { get; set; } = 4;
        [DebugEditable(DisplayName = "Slab Font Scale", Step = 0.01f, Min = 0.03f, Max = 0.5f)]
        public float SlabFontScale { get; set; } = 0.08f;

        // Content Area
        [DebugEditable(DisplayName = "Content Margin Left", Step = 1, Min = 30, Max = 120)]
        public int ContentMarginLeft { get; set; } = 68;
        [DebugEditable(DisplayName = "Content Pad Top", Step = 1, Min = 0, Max = 40)]
        public int ContentPadTop { get; set; } = 4;
        [DebugEditable(DisplayName = "Content Pad Right", Step = 1, Min = 0, Max = 40)]
        public int ContentPadRight { get; set; } = 4;
        [DebugEditable(DisplayName = "Type Icon Scale", Step = 0.01f, Min = 0.01f, Max = 2.0f)]
        public float TypeIconScale { get; set; } = 0.06f;
        [DebugEditable(DisplayName = "Type Icon Alpha", Step = 0.01f, Min = 0.0f, Max = 1.0f)]
        public float TypeIconAlpha { get; set; } = 0.5f;
        [DebugEditable(DisplayName = "Type Icon Bottom Pad", Step = 1, Min = 0, Max = 120)]
        public int TypeIconBottomPad { get; set; } = 8;

        // Name
        [DebugEditable(DisplayName = "Name Font Scale", Step = 0.01f, Min = 0.05f, Max = 1.0f)]
        public float NameFontScale { get; set; } = 0.14f;

        // Rule Line
        [DebugEditable(DisplayName = "Rule Height", Step = 1, Min = 1, Max = 6)]
        public int RuleHeight { get; set; } = 2;

        // Description
        [DebugEditable(DisplayName = "Desc Font Scale", Step = 0.01f, Min = 0.03f, Max = 0.5f)]
        public float DescFontScale { get; set; } = 0.11f;
        [DebugEditable(DisplayName = "Text Bg Padding X", Step = 1, Min = 0, Max = 30)]
        public int TextBackgroundPaddingX { get; set; } = 5;
        [DebugEditable(DisplayName = "Text Bg Padding Y", Step = 1, Min = 0, Max = 30)]
        public int TextBackgroundPaddingY { get; set; } = 2;
        [DebugEditable(DisplayName = "Text Bg Opacity", Step = 0.01f, Min = 0.0f, Max = 1.0f)]
        public float TextBackgroundOpacity { get; set; } = 0.5f;
        [DebugEditable(DisplayName = "Text Bg Border Radius", Step = 1, Min = 0, Max = 20)]
        public int TextBackgroundBorderRadius { get; set; } = 0;

        // Art
        [DebugEditable(DisplayName = "Art Width", Step = 1, Min = 50, Max = 300)]
        public int ArtWidth { get; set; } = 204;
        [DebugEditable(DisplayName = "Art Height", Step = 1, Min = 50, Max = 300)]
        public int ArtHeight { get; set; } = 270;
        [DebugEditable(DisplayName = "Art Offset Right", Step = 1, Min = -60, Max = 60)]
        public int ArtOffsetRight { get; set; } = -2;
        [DebugEditable(DisplayName = "Art Offset Bottom", Step = 1, Min = -60, Max = 60)]
        public int ArtOffsetBottom { get; set; } = 0;

        // Responsive chip scaling
        [DebugEditable(DisplayName = "Chip Scale With Title")]
        public bool ChipScaleWithTitle { get; set; } = true;

        [DebugEditable(DisplayName = "Colorless Background R", Step = 1, Min = 0, Max = 255)]
        public int ColorlessBackgroundR { get; set; } = 92;
        [DebugEditable(DisplayName = "Colorless Background G", Step = 1, Min = 0, Max = 255)]
        public int ColorlessBackgroundG { get; set; } = 96;
        [DebugEditable(DisplayName = "Colorless Background B", Step = 1, Min = 0, Max = 255)]
        public int ColorlessBackgroundB { get; set; } = 102;

        [DebugEditable(DisplayName = "Colorless Primary Text R", Step = 1, Min = 0, Max = 255)]
        public int ColorlessPrimaryTextR { get; set; } = 235;
        [DebugEditable(DisplayName = "Colorless Primary Text G", Step = 1, Min = 0, Max = 255)]
        public int ColorlessPrimaryTextG { get; set; } = 235;
        [DebugEditable(DisplayName = "Colorless Primary Text B", Step = 1, Min = 0, Max = 255)]
        public int ColorlessPrimaryTextB { get; set; } = 235;

        [DebugEditable(DisplayName = "Colorless Muted Text R", Step = 1, Min = 0, Max = 255)]
        public int ColorlessMutedTextR { get; set; } = 170;
        [DebugEditable(DisplayName = "Colorless Muted Text G", Step = 1, Min = 0, Max = 255)]
        public int ColorlessMutedTextG { get; set; } = 170;
        [DebugEditable(DisplayName = "Colorless Muted Text B", Step = 1, Min = 0, Max = 255)]
        public int ColorlessMutedTextB { get; set; } = 170;

        [DebugEditable(DisplayName = "Colorless Surface R", Step = 1, Min = 0, Max = 255)]
        public int ColorlessSurfaceR { get; set; } = 58;
        [DebugEditable(DisplayName = "Colorless Surface G", Step = 1, Min = 0, Max = 255)]
        public int ColorlessSurfaceG { get; set; } = 61;
        [DebugEditable(DisplayName = "Colorless Surface B", Step = 1, Min = 0, Max = 255)]
        public int ColorlessSurfaceB { get; set; } = 66;

        internal CardDisplaySystem(
            EntityManager entityManager,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            ImageAssetService imageAssets,
            CardRenderPipeline pipeline)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _imageAssets = imageAssets;
            _pipeline = pipeline;
            _pixelTexture = _imageAssets.GetPixel(Color.White);
            _baseSurfaceCache = new WeightedLruCache<CardBaseRenderModel, CachedCardSurface>(
                BaseSurfaceCacheBudgetBytes,
                surface => surface.Texture?.Dispose());
            _cacheRenderWidth = Game1.Display.RenderWidth;
            _cacheRenderHeight = Game1.Display.RenderHeight;

            LoadTypeIconTextures();
            EventManager.Subscribe<CardRenderEvent>(OnCardRenderEvent, CardRenderEvent.BaseRendererPriority);
            EventManager.Subscribe<CardRenderScaledEvent>(OnCardRenderScaledEvent);
            EventManager.Subscribe<CardRenderScaledRotatedEvent>(OnCardRenderScaledRotatedEvent);
            EventManager.Subscribe<DeleteCachesEvent>(_ => ResetRenderResources());
            _graphicsDevice.DeviceReset += (_, _) => ResetRenderResources();
        }

        private void LoadTypeIconTextures()
        {
            if (_typeIconTextures.Count > 0) return;
            _typeIconTextures[CardType.Attack] = GetOrLoadTexture("card_icon_attack");
            _typeIconTextures[CardType.Prayer] = GetOrLoadTexture("card_icon_prayer");
            _typeIconTextures[CardType.Block] = GetOrLoadTexture("card_icon_shield");
            _typeIconTextures[CardType.Relic] = GetOrLoadTexture("card_icon_relic");
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<CardData>();
        }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            _pipeline.Update(gameTime);
            base.Update(gameTime);
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            var ids = EntityManager.GetEntitiesWithComponent<CardData>().Select(e => e.Id).ToList();
            if (ids.Count == 0 || entity.Id != ids.Min()) return;
            _elapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
        }

        private float CW => GetSettings().CardWidth;
        private float CH => GetSettings().CardHeight;

        private Color Tint(Color color) => color * _drawAlpha;

        private CardGeometrySettings GetSettings()
        {
            _settings ??= CardGeometryService.GetSettings(EntityManager) ?? new CardGeometrySettings
            {
                CardWidth = CardGeometrySettings.DefaultWidth,
                CardHeight = CardGeometrySettings.DefaultHeight,
                CardOffsetYExtra = CardGeometrySettings.DefaultOffsetYExtra,
                CardGap = CardGeometrySettings.DefaultGap,
                CardCornerRadius = CardGeometrySettings.DefaultCornerRadius,
                HighlightBorderThickness = CardGeometrySettings.DefaultHighlightBorderThickness
            };
            return _settings;
        }

        private void DrawRectangleLocalScaled(Vector2 cc, float rot, Vector2 off, float w, float h, Color c, float s)
            => DrawRectangleRotatedLocalScaled(cc, rot, off, w, h, c, s, CW, CH);

        private void DrawTextureLocalScaled(Vector2 cc, float rot, Vector2 off, Texture2D tex, Vector2 sz, Color c, float s)
            => DrawTextureRotatedLocalScaled(cc, rot, off, tex, sz, c, s, CW, CH);

        private void DrawTextLocalScaled(Vector2 cc, float rot, Vector2 off, string txt, Color c, float sc, float os, SpriteFont font = null)
            => DrawCardTextRotatedSingleScaled(cc, rot, off, txt, c, sc, os, CW, CH, font);

        private void DrawWrappedLinesLocalScaled(Vector2 cc, float rot, Vector2 off, IReadOnlyList<string> lines, Color c, float sc, float os, SpriteFont font)
            => DrawCardTextWrappedLinesRotatedScaled(cc, rot, off, lines, c, sc, os, font, CW, CH);

        private void DrawRectangleRotatedLocalScaled(Vector2 cardCenter, float rotation, Vector2 localOffsetFromTopLeft, float width, float height, Color color, float visualScale, float cardW, float cardH)
        {
            float localX = -cardW * visualScale / 2f + localOffsetFromTopLeft.X;
            float localY = -cardH * visualScale / 2f + localOffsetFromTopLeft.Y;
            float cos = (float)Math.Cos(rotation);
            float sin = (float)Math.Sin(rotation);
            var rotated = new Vector2(localX * cos - localY * sin, localX * sin + localY * cos);
            var world = cardCenter + rotated;
            _spriteBatch.Draw(_pixelTexture, world, null, Tint(color), rotation, Vector2.Zero, new Vector2(width, height), SpriteEffects.None, 0f);
        }

        private void DrawTextureRotatedLocalScaled(Vector2 cardCenter, float rotation, Vector2 localOffsetFromTopLeft, Texture2D texture, Vector2 targetSize, Color color, float visualScale, float cardW, float cardH)
        {
            if (texture == null) return;
            float localX = -cardW * visualScale / 2f + localOffsetFromTopLeft.X;
            float localY = -cardH * visualScale / 2f + localOffsetFromTopLeft.Y;
            float cos = (float)Math.Cos(rotation);
            float sin = (float)Math.Sin(rotation);
            var rotated = new Vector2(localX * cos - localY * sin, localX * sin + localY * cos);
            var world = cardCenter + rotated;
            var scale = new Vector2(targetSize.X / texture.Width, targetSize.Y / texture.Height);
            _spriteBatch.Draw(texture, world, null, Tint(color), rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawCardTextRotatedSingleScaled(Vector2 cardCenter, float rotation, Vector2 localOffsetFromTopLeft, string text, Color color, float scale, float overallScale, float cardW, float cardH, SpriteFont font = null)
        {
            try
            {
                font ??= _nameFont;
                float localX = -cardW * overallScale / 2f + localOffsetFromTopLeft.X;
                float localY = -cardH * overallScale / 2f + localOffsetFromTopLeft.Y;
                float cos = (float)Math.Cos(rotation);
                float sin = (float)Math.Sin(rotation);
                var rotated = new Vector2(localX * cos - localY * sin, localX * sin + localY * cos);
                var world = cardCenter + rotated;
                _spriteBatch.DrawString(font, text, world, Tint(color), rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Font rendering error: {ex.Message}");
            }
        }

        private void DrawCardTextWrappedLinesRotatedScaled(Vector2 cardCenter, float rotation, Vector2 localOffsetFromTopLeft, IReadOnlyList<string> lines, Color color, float scale, float overallScale, SpriteFont font, float cardW, float cardH)
        {
            try
            {
                float lineHeight = font.LineSpacing * scale;
                float startLocalX = -cardW * overallScale / 2f + localOffsetFromTopLeft.X;
                float startLocalY = -cardH * overallScale / 2f + localOffsetFromTopLeft.Y;

                float currentY = startLocalY;
                foreach (var line in lines)
                {
                    var local = new Vector2(startLocalX, currentY);
                    float cos = (float)Math.Cos(rotation);
                    float sin = (float)Math.Sin(rotation);
                    var rotated = new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
                    var world = cardCenter + rotated;
                    _spriteBatch.DrawString(font, line, world, Tint(color), rotation, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    currentY += lineHeight;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Font rendering error: {ex.Message}");
            }
        }

        internal static CardDescriptionTextLayout CreateDescriptionTextLayout(
            CardGeometrySettings settings,
            float visualScale,
            float descFontScale,
            int contentMarginLeft,
            int contentPadRight)
        {
            if (settings == null) throw new ArgumentNullException(nameof(settings));

            float unscaledContentWidth = Math.Max(1f, settings.CardWidth - contentMarginLeft - contentPadRight);
            return new CardDescriptionTextLayout(
                contentMarginLeft * visualScale,
                unscaledContentWidth * visualScale,
                descFontScale,
                Math.Max(1, (int)Math.Round(unscaledContentWidth)),
                descFontScale * visualScale);
        }

        private Texture2D GetRoundedRectTexture(int width, int height, int radius)
        {
            return _imageAssets.GetRoundedRect(width, height, radius);
        }

        private Texture2D GetPerCornerRoundedRectTexture(int width, int height, int rTL, int rTR, int rBR, int rBL)
        {
            return _imageAssets.GetRoundedRectPerCorner(width, height, rTL, rTR, rBR, rBL);
        }

        private Texture2D GetOrLoadTexture(string assetName)
        {
            return _imageAssets.TryGetTexture(assetName);
        }

        // Event handlers
        private void OnCardRenderEvent(CardRenderEvent evt)
        {
            var transform = evt.Card.GetComponent<Transform>();
            CachedCardSurface cached = evt.PreferCachedBase
                ? GetOrCreateCachedBase(
                    evt.Card,
                    evt.Position,
                    transform?.Scale.X ?? 1f,
                    transform?.Rotation ?? 0f)
                : RecordBaseCacheBypass();
            RenderCard(
                evt.Card,
                evt.Position,
                transform?.Scale.X ?? 1f,
                transform?.Rotation ?? 0f,
                cached);
        }

        private void OnCardRenderScaledEvent(CardRenderScaledEvent evt)
        {
            _drawAlpha = MathHelper.Clamp(evt.Alpha, 0f, 1f);
            using var clip = CardRenderClipScope.Apply(_graphicsDevice, evt.ClipRect);
            try
            {
                var transform = evt.Card.GetComponent<Transform>();
                if (transform != null)
                {
                    Vector2 originalPosition = transform.Position;
                    Vector2 originalScale = transform.Scale;
                    float originalRotation = transform.Rotation;
                    try
                    {
                        transform.Position = evt.Position;
                        transform.Scale = new Vector2(evt.Scale, evt.Scale);
                        transform.Rotation = evt.Rotation;
                        CachedCardSurface cached = evt.PreferCachedBase
                            ? GetOrCreateCachedBase(evt.Card, evt.Position, evt.Scale, evt.Rotation)
                            : RecordBaseCacheBypass();
                        RenderCard(evt.Card, evt.Position, evt.Scale, evt.Rotation, cached);
                    }
                    finally
                    {
                        transform.Position = originalPosition;
                        transform.Scale = originalScale;
                        transform.Rotation = originalRotation;
                    }

                    var ui = evt.Card.GetComponent<UIElement>();
                    if (ui != null && !ui.IsHidden)
                    {
                        ui.Bounds = CardGeometryService.GetVisualRect(GetSettings(), evt.Position, evt.Scale);
                    }
                }
                else
                {
                    CachedCardSurface cached = evt.PreferCachedBase
                        ? GetOrCreateCachedBase(evt.Card, evt.Position, evt.Scale, evt.Rotation)
                        : RecordBaseCacheBypass();
                    RenderCard(evt.Card, evt.Position, evt.Scale, evt.Rotation, cached);
                }
            }
            finally
            {
                _drawAlpha = 1f;
            }
        }

        private void OnCardRenderScaledRotatedEvent(CardRenderScaledRotatedEvent evt)
        {
            var transform = evt.Card.GetComponent<Transform>();
            Vector2 originalScale = transform?.Scale ?? Vector2.One;
            if (transform != null)
            {
                transform.Scale = new Vector2(evt.Scale, evt.Scale);
                Vector2 originalPosition = transform.Position;
                transform.Position = evt.Position;
                var ui = evt.Card.GetComponent<UIElement>();
                RenderCard(evt.Card, evt.Position, evt.Scale, transform.Rotation);
                transform.Scale = originalScale;
                transform.Position = originalPosition;
                if (ui != null) ui.Bounds = CardGeometryService.GetVisualRect(GetSettings(), evt.Position, evt.Scale);
            }
            else
            {
                RenderCard(evt.Card, evt.Position, evt.Scale, 0f);
            }
        }

        private void RenderCard(
            Entity card,
            Vector2 position,
            float scale,
            float rotation,
            CachedCardSurface cachedBase = null)
        {
            var transform = card.GetComponent<Transform>();
            var ui = card.GetComponent<UIElement>();
            EventManager.Publish(new HighlightRenderEvent { Entity = card, Transform = transform, UI = ui });

			_pipeline.Render(
				new CardRenderRequest(card, position, scale, rotation),
				() =>
            {
                if (cachedBase != null)
                {
                    _spriteBatch.Draw(
                        cachedBase.Texture,
                        new Vector2(cachedBase.LogicalBounds.X, cachedBase.LogicalBounds.Y),
                        null,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        new Vector2(1f / Game1.Display.RenderScaleX, 1f / Game1.Display.RenderScaleY),
                        SpriteEffects.None,
                        0f);
                }
                else
				{
					DrawCard(card, position);
				}
			},
			() =>
			{
				var secondaryColor = card.GetComponent<DualColor>();
				if (secondaryColor != null)
				{
					DrawCard(card, position, secondaryColor.SecondaryColor);
				}
			});
        }

        private CachedCardSurface GetOrCreateCachedBase(Entity card, Vector2 position, float scale, float rotation)
        {
            if (!CanCacheBase(card, scale)) return RecordBaseCacheBypass();
            EnsureCacheMatchesRenderScale();

            Rectangle logicalBounds = CardRenderBoundsService.GetBaseBounds(
                EntityManager,
                card,
                position,
                scale,
                rotation);
            int physicalWidth = Math.Max(1, (int)MathF.Ceiling(logicalBounds.Width * Game1.Display.RenderScaleX));
            int physicalHeight = Math.Max(1, (int)MathF.Ceiling(logicalBounds.Height * Game1.Display.RenderScaleY));
            CardBaseRenderModel model = CreateRenderModel(card, scale, rotation, physicalWidth, physicalHeight);
            if (_baseSurfaceCache.TryGet(model, out CachedCardSurface cached) && !cached.Texture.IsDisposed)
            {
                _baseCacheHits++;
                return new CachedCardSurface(cached.Texture, logicalBounds);
            }

            _baseCacheMisses++;

            if (!SpriteBatchRenderTargetCompositor.TryGetPrimaryRenderTarget(
                _graphicsDevice,
                out RenderTargetBinding[] sceneTargets,
                out _)) return null;

            var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
            var target = new RenderTarget2D(
                _graphicsDevice,
                physicalWidth,
                physicalHeight,
                false,
                SurfaceFormat.Color,
                DepthFormat.None,
                0,
                RenderTargetUsage.PreserveContents);
            try
            {
                _spriteBatch.End();
                _graphicsDevice.SetRenderTarget(target);
                _graphicsDevice.Clear(Color.Transparent);
                Matrix localTransform = Matrix.CreateTranslation(-logicalBounds.X, -logicalBounds.Y, 0f) *
                    (Game1.Display.SpriteBatchTransform ?? Matrix.Identity);
                _spriteBatch.Begin(
                    SpriteSortMode.Immediate,
                    BlendState.AlphaBlend,
                    SamplerState.PointClamp,
                    DepthStencilState.None,
                    RasterizerState.CullNone,
                    null,
                    localTransform);
                DrawCard(card, position);
                _spriteBatch.End();
                SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, sceneTargets);
                SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);
            }
            catch
            {
                try { _spriteBatch.End(); } catch { }
                target.Dispose();
                SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, sceneTargets);
                SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);
                throw;
            }

            cached = new CachedCardSurface(target, logicalBounds);
            _baseSurfaceCache.Add(model, cached, physicalWidth * (long)physicalHeight * 4L);
            return cached;
        }

        private CachedCardSurface RecordBaseCacheBypass()
        {
            _baseCacheBypasses++;
            return null;
        }

        internal void ResetBaseCacheDiagnostics()
        {
            _baseCacheHits = 0;
            _baseCacheMisses = 0;
            _baseCacheBypasses = 0;
        }

        internal CardBaseCacheDiagnostics GetBaseCacheDiagnostics() => new(
            _baseCacheHits,
            _baseCacheMisses,
            _baseCacheBypasses);

        private bool CanCacheBase(Entity card, float scale)
        {
            CardBase definition = card?.GetComponent<CardData>()?.Card;
            if (definition == null || scale <= 0f) return false;
            int vigorStacks = VigorService.GetPlayerVigorStacks(EntityManager);
            return IsBaseCacheEligible(_drawAlpha, VigorService.GetWaivedPipCount(definition, vigorStacks));
        }

        internal static bool IsBaseCacheEligible(float alpha, int waivedPipCount)
            => alpha >= 0.999f && waivedPipCount <= 0;

        private CardBaseRenderModel CreateRenderModel(
            Entity entity,
            float scale,
            float rotation,
            int physicalWidth,
            int physicalHeight)
        {
            CardData data = entity.GetComponent<CardData>();
            CardBase card = data.Card;
            int effectiveBlock;
            try { effectiveBlock = BlockValueService.GetTotalBlockValue(entity); }
            catch { effectiveBlock = card.Block; }
            PhaseState phase = EntityManager.GetEntitiesWithComponent<PhaseState>()
                .FirstOrDefault()?.GetComponent<PhaseState>();
            var alternate = phase?.Sub == SubPhase.Action
                ? AlternateCardPlayService.GetProfile(EntityManager, entity, SubPhase.Action)
                : null;
            int effectiveDamage = alternate?.TreatsAsAttack == true
                ? GetAlternateAttackDamage(entity, alternate.AttackDamage)
                : GetEffectiveDamage(entity, card);
            return new CardBaseRenderModel(
                card.CardId ?? string.Empty,
                card.DisplayName ?? string.Empty,
                card.GetDisplayText() ?? string.Empty,
                string.Join("|", card.Cost ?? new List<string>()),
                data.Color,
				entity.GetComponent<DualColor>()?.SecondaryColor,
                card.Type,
                card.Damage,
                card.Block,
                effectiveDamage,
                effectiveBlock,
                card.IsFreeAction,
                card.IsWeapon,
                card.IsToken,
                card.IsUpgraded,
                entity.HasComponent<Colorless>(),
                entity.HasComponent<SuppressStatDeltaDisplay>(),
                alternate?.TreatsAsAttack == true,
                alternate?.AttackDamage ?? 0,
                alternate?.IsFreeAction == true,
                phase?.Sub ?? SubPhase.StartBattle,
                GetStyleFingerprint(),
                scale,
                rotation,
                physicalWidth,
                physicalHeight);
        }

        private int GetStyleFingerprint()
        {
            CardGeometrySettings settings = GetSettings();
            var hash = new HashCode();
            hash.Add(settings.CardWidth);
            hash.Add(settings.CardHeight);
            hash.Add(settings.CardOffsetYExtra);
            hash.Add(settings.CardCornerRadius);
            hash.Add(StripeWidth);
            hash.Add(GutterX);
            hash.Add(GutterWidth);
            hash.Add(GutterTopY);
            hash.Add(TitleBandPadTop);
            hash.Add(TitleBandPadLeft);
            hash.Add(TitleBandPadRight);
            hash.Add(TypeRowMarginTop);
            hash.Add(RuleMarginTop);
            hash.Add(NameFontScale);
            hash.Add(CostPipSize);
            hash.Add(CostPipGap);
            hash.Add(CostLabelGap);
            hash.Add(CostLabelFontScale);
            hash.Add(CostPipOutlineFrac);
            hash.Add(ContentMarginLeft);
            hash.Add(ContentPadTop);
            hash.Add(ContentPadRight);
            hash.Add(DescFontScale);
            hash.Add(TextBackgroundPaddingX);
            hash.Add(TextBackgroundPaddingY);
            hash.Add(TextBackgroundOpacity);
            hash.Add(TextBackgroundBorderRadius);
            hash.Add(ArtWidth);
            hash.Add(ArtHeight);
            hash.Add(ArtOffsetRight);
            hash.Add(ArtOffsetBottom);
            hash.Add(ChipSize);
            hash.Add(ChipWidth);
            hash.Add(ChipColumnX);
            hash.Add(ChipColumnTopY);
            hash.Add(ChipSlotHeight);
            hash.Add(ChipCornerRadius);
            hash.Add(ChipBorderThickness);
            hash.Add(ChipValueFontScale);
            hash.Add(ChipGap);
            hash.Add(ChipColumnBottomPad);
            hash.Add(LabelSlabHeight);
            hash.Add(LabelSlabFontScale);
            hash.Add(SlabWidth);
            hash.Add(SlabHeight);
            hash.Add(SlabCornerRadius);
            hash.Add(SlabFontScale);
            hash.Add(TypeIconScale);
            hash.Add(TypeIconAlpha);
            hash.Add(TypeIconBottomPad);
            hash.Add(RuleHeight);
            hash.Add(ChipScaleWithTitle);
            hash.Add(ColorlessBackgroundR);
            hash.Add(ColorlessBackgroundG);
            hash.Add(ColorlessBackgroundB);
            hash.Add(ColorlessPrimaryTextR);
            hash.Add(ColorlessPrimaryTextG);
            hash.Add(ColorlessPrimaryTextB);
            hash.Add(ColorlessMutedTextR);
            hash.Add(ColorlessMutedTextG);
            hash.Add(ColorlessMutedTextB);
            hash.Add(ColorlessSurfaceR);
            hash.Add(ColorlessSurfaceG);
            hash.Add(ColorlessSurfaceB);
            return hash.ToHashCode();
        }

        private void EnsureCacheMatchesRenderScale()
        {
            if (_cacheRenderWidth == Game1.Display.RenderWidth &&
                _cacheRenderHeight == Game1.Display.RenderHeight) return;
            ClearRenderCaches();
            _cacheRenderWidth = Game1.Display.RenderWidth;
            _cacheRenderHeight = Game1.Display.RenderHeight;
        }

        private void ClearRenderCaches()
        {
            _baseSurfaceCache.Clear();
            foreach (Texture2D texture in _clippedArtCache.Values) texture?.Dispose();
            _clippedArtCache.Clear();
        }

        private void ResetRenderResources()
        {
            ClearRenderCaches();
            _pipeline.Reset();
        }

        internal void SetPoisonOverlaySnapshotTime(float timeSeconds)
        {
            _pipeline.SetSnapshotTime<PoisonCardOverlayPass>(timeSeconds);
        }

        internal void SetAllOverlaySnapshotTimes(float timeSeconds)
        {
            _pipeline.SetAllSnapshotTimes(timeSeconds);
        }

        IEnumerable<object> IDebugInspectableChildren.GetDebugInspectableChildren()
        {
            return _pipeline.Passes.Cast<object>();
        }

		public void DrawCard(
			Entity entity,
			Vector2 position,
			CardData.CardColor? paletteColorOverride = null)
        {
            var cardData = entity.GetComponent<CardData>();
            var transform = entity.GetComponent<Transform>();
            if (cardData == null) return;

            var settings = GetSettings();
            float vs = transform?.Scale.X ?? 1f;
            float rotation = transform?.Rotation ?? 0f;
            CardBase card = cardData.Card;
            bool hasDef = card != null;
			var cc = paletteColorOverride ?? cardData.Color;
            bool isColorless = entity.HasComponent<Colorless>();

            var rect = CardGeometryService.GetVisualRect(settings, position, vs);
            var cardCenter = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);

            float sw = settings.CardWidth * vs;
            float sh = settings.CardHeight * vs;

            // 1. Background
            int bgW = (int)Math.Round(settings.CardWidth * vs);
            int bgH = (int)Math.Round(settings.CardHeight * vs);
            var bgTex = GetRoundedRectTexture(bgW, bgH, (int)(settings.CardCornerRadius * vs));
            var bgColor = isColorless
                ? ColorlessBackground
                : CardPalette.Background(cc);
            if ((card.IsWeapon || card.IsToken) && !isColorless)
            {
                bgColor = new Color(215, 186, 147);
            }
            _spriteBatch.Draw(bgTex,
                position: cardCenter,
                sourceRectangle: null,
                color: Tint(bgColor),
                rotation: rotation,
                origin: new Vector2(bgTex.Width / 2f, bgTex.Height / 2f),
                scale: Vector2.One,
                effects: SpriteEffects.None,
                layerDepth: 0f);

            // 2. Stripe (full height, rounded on left to match card corners)
            // var stripeColor = CardPalette.Stripe(cc);
            // int stripeW = (int)(StripeWidth * vs);
            // int stripeH = (int)sh;
            // int stripeCR = (int)(settings.CardCornerRadius * vs);
            // var stripeTex = GetPerCornerRoundedRectTexture(stripeW, stripeH, stripeCR, 0, 0, stripeCR);
            // DrawTextureLocalScaled(cardCenter, rotation, new Vector2(0, 0), stripeTex, new Vector2(stripeW, stripeH), stripeColor, vs);

            // 3. Title Band (full width)
            float titleBandEndY = 0f;
            if (hasDef)
            {
                int vigorStacks = VigorService.GetPlayerVigorStacks(EntityManager);
                int waivedPipCount = VigorService.GetWaivedPipCount(card, vigorStacks);
                titleBandEndY = DrawTitleBand(cardCenter, rotation, vs, cc, card, waivedPipCount, isColorless);
            }

            // 4. Stat Gutter (starts below title band)
            var gutterColor = isColorless
                ? ColorlessSurface
                : CardPalette.Gutter(cc);
            float gutterY = GutterTopY * vs;
            DrawRectangleLocalScaled(cardCenter, rotation, new Vector2(GutterX * vs, gutterY),
                GutterWidth * vs, sh - gutterY, gutterColor, vs);

            // 5. Stat Chips (with label slabs)
            if (hasDef)
            {
                DrawStatChips(cardCenter, rotation, vs, cc, entity, card, isColorless);
                DrawTypeIconInChipColumn(cardCenter, rotation, vs, card);
            }

            // 6. Card art, then backed card text (content area, below rule line, right of chips)
            var descLayout = CreateDescriptionTextLayout(settings, vs, DescFontScale, ContentMarginLeft, ContentPadRight);
            float contentX = descLayout.ContentX;
            float contentTop = titleBandEndY + ContentPadTop * vs;
            float cursorY = contentTop;

            if (hasDef)
            {
                DrawCardArt(cardCenter, rotation, vs, card);
            }

            if (hasDef)
            {
                string desc = card.GetDisplayText();
                if (!string.IsNullOrWhiteSpace(desc))
                {
                    var descLines = TextUtils.WrapText(_bodyFont, desc, descLayout.WrapScale, descLayout.WrapMaxWidth);
                    DrawTextBackground(cardCenter, rotation, vs, cc, card, isColorless, descLines, descLayout.DrawScale, contentX, cursorY);
                    var descColor = isColorless
                        ? ColorlessPrimaryText
                        : CardPalette.NameText(cc);
                    DrawWrappedLinesLocalScaled(cardCenter, rotation, new Vector2(contentX, cursorY), descLines, descColor, descLayout.DrawScale, vs, _bodyFont);
                }
            }
        }

        private void DrawCardArt(Vector2 cardCenter, float rotation, float vs, CardBase card)
        {
            if (string.IsNullOrEmpty(card.CardId)) return;

            string assetName = $"CardArt/{card.CardId}";
            var artTex = GetOrLoadTexture(assetName);
            if (artTex == null) return;

            float artW = ArtWidth;
            float artH = ArtHeight;

            float texAspect = artTex.Width / (float)artTex.Height;
            float boxAspect = artW / artH;
            if (texAspect > boxAspect) { artH = artW / texAspect; }
            else { artW = artH * texAspect; }

            float artLocalX = GetSettings().CardWidth - artW + ArtOffsetRight;
            float artLocalY = GetSettings().CardHeight - artH + ArtOffsetBottom;
            var clippedArt = GetClippedCardArtTexture(assetName, artTex, artW, artH, artLocalX, artLocalY);

            DrawTextureLocalScaled(
                cardCenter,
                rotation,
                new Vector2(artLocalX * vs, artLocalY * vs),
                clippedArt,
                new Vector2(artW * vs, artH * vs),
                Color.White,
                vs);
        }

        private Texture2D GetClippedCardArtTexture(string assetName, Texture2D source, float artW, float artH, float artLocalX, float artLocalY)
        {
            var settings = GetSettings();
            int texW = Math.Max(1, (int)Math.Round(artW));
            int texH = Math.Max(1, (int)Math.Round(artH));
            int artX = (int)Math.Round(artLocalX);
            int artY = (int)Math.Round(artLocalY);
            int cardW = Math.Max(1, settings.CardWidth);
            int cardH = Math.Max(1, settings.CardHeight);
            int radius = Math.Max(0, settings.CardCornerRadius);
            var key = (assetName, texW, texH, artX, artY, cardW, cardH, radius);
            if (_clippedArtCache.TryGetValue(key, out var cached)) return cached;

            var sourceData = new Color[source.Width * source.Height];
            source.GetData(sourceData);

            var clippedData = new Color[texW * texH];
            for (int y = 0; y < texH; y++)
            {
                for (int x = 0; x < texW; x++)
                {
                    float sourceX = texW == 1 ? 0f : x * (source.Width - 1f) / (texW - 1f);
                    float sourceY = texH == 1 ? 0f : y * (source.Height - 1f) / (texH - 1f);
                    Color sourceColor = MipmappedTextureUtility.SampleBilinear(sourceData, source.Width, source.Height, sourceX, sourceY);
                    float cardX = artLocalX + (x + 0.5f) * artW / texW;
                    float cardY = artLocalY + (y + 0.5f) * artH / texH;
                    float alpha = GetRoundedCardAlpha(cardX, cardY, cardW, cardH, radius);
                    clippedData[y * texW + x] = ApplyAlpha(sourceColor, alpha);
                }
            }

            var texture = MipmappedTextureUtility.CreateMipmappedTexture(_graphicsDevice, clippedData, texW, texH);
            _clippedArtCache[key] = texture;
            return texture;
        }

        private static float GetRoundedCardAlpha(float x, float y, int width, int height, int radius)
        {
            int r = Math.Max(0, Math.Min(radius, Math.Min(width, height) / 2));
            if (x < 0f || y < 0f || x >= width || y >= height) return 0f;
            if (r <= 0) return 1f;

            bool inCorner = false;
            float cx = 0f;
            float cy = 0f;
            if (x < r && y < r)
            {
                inCorner = true;
                cx = r - 0.5f;
                cy = r - 0.5f;
            }
            else if (x >= width - r && y < r)
            {
                inCorner = true;
                cx = width - r - 0.5f;
                cy = r - 0.5f;
            }
            else if (x >= width - r && y >= height - r)
            {
                inCorner = true;
                cx = width - r - 0.5f;
                cy = height - r - 0.5f;
            }
            else if (x < r && y >= height - r)
            {
                inCorner = true;
                cx = r - 0.5f;
                cy = height - r - 0.5f;
            }

            if (!inCorner) return 1f;

            float dx = x - cx;
            float dy = y - cy;
            float distance = MathF.Sqrt(dx * dx + dy * dy);
            if (distance <= r - 0.5f) return 1f;
            if (distance >= r + 0.5f) return 0f;
            return MathHelper.Clamp(r + 0.5f - distance, 0f, 1f);
        }

        private static Color ApplyAlpha(Color color, float alpha)
        {
            alpha = MathHelper.Clamp(alpha, 0f, 1f);
            return new Color(
                (byte)Math.Clamp((int)MathF.Round(color.R * alpha), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(color.G * alpha), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(color.B * alpha), 0, 255),
                (byte)Math.Clamp((int)MathF.Round(color.A * alpha), 0, 255));
        }

        private void DrawTextBackground(Vector2 cardCenter, float rotation, float vs,
            CardData.CardColor cc, CardBase card, bool isColorless, IReadOnlyList<string> lines, float textScale,
            float textX, float textY)
        {
            float maxLineWidth = lines.Select(line => _bodyFont.MeasureString(line).X * textScale).DefaultIfEmpty(0f).Max();
            if (maxLineWidth <= 0f) return;

            float padX = TextBackgroundPaddingX * vs;
            float padY = TextBackgroundPaddingY * vs;
            float bgX = Math.Max(0f, textX - padX);
            float bgY = Math.Max(0f, textY - padY);
            float cardRight = GetSettings().CardWidth * vs;
            float bgW = Math.Max(1f, cardRight - bgX - ContentPadRight * vs);
            float bgH = lines.Count * _bodyFont.LineSpacing * textScale + padY * 2f;

            int texW = Math.Max(1, (int)Math.Ceiling(bgW));
            int texH = Math.Max(1, (int)Math.Ceiling(bgH));
            int radius = Math.Min(
                Math.Max(0, (int)Math.Round(TextBackgroundBorderRadius * vs)),
                Math.Min(texW, texH) / 2);
            var bgTex = GetRoundedRectTexture(texW, texH, radius);
            bool usesWeaponPalette = (card.IsWeapon || card.IsToken) && !isColorless;
            var bgColor = usesWeaponPalette
                ? CardPalette.WeaponTextBackground
                : isColorless
                ? ColorlessSurface
                : CardPalette.TextBackground(cc);

            DrawTextureLocalScaled(cardCenter, rotation, new Vector2(bgX, bgY), bgTex, new Vector2(bgW, bgH),
                bgColor * TextBackgroundOpacity, vs);
        }

        private void DrawTypeIconInChipColumn(Vector2 cardCenter, float rotation, float vs, CardBase card)
        {
            if (!_typeIconTextures.TryGetValue(card.Type, out var tex) || tex == null) return;

            float iconW = tex.Width * TypeIconScale * vs;
            float iconH = tex.Height * TypeIconScale * vs;
            float chipColumnW = ChipWidth * vs;
            float iconX = ChipColumnX * vs + (chipColumnW - iconW) / 2f;
            float iconY = GetSettings().CardHeight * vs - TypeIconBottomPad * vs - iconH;
            var iconColor = Color.White * TypeIconAlpha;
            DrawTextureLocalScaled(cardCenter, rotation, new Vector2(iconX, iconY), tex, new Vector2(iconW, iconH), iconColor, vs);
        }

        /// <summary>
        /// Draws full-width title band: centered name, type row (cost left, type right), rule line.
        /// Returns the Y position where the title band ends (for content positioning below).
        /// </summary>
        private float DrawTitleBand(Vector2 cardCenter, float rotation, float vs,
            CardData.CardColor cc, CardBase card, int waivedPipCount, bool isColorless)
        {
            float padLeft = TitleBandPadLeft * vs;
            float padRight = TitleBandPadRight * vs;
            float cardWidth = GetSettings().CardWidth * vs;
            float cursorY = TitleBandPadTop * vs;

            // Card Name — centered across full card width
            string name = card.DisplayName ?? "";
            var nameColor = isColorless
                ? ColorlessPrimaryText
                : CardPalette.NameText(cc);
            float nameScale = NameFontScale * vs;
            var nameSize = _nameFont.MeasureString(name) * nameScale;
            float nameX = (cardWidth - nameSize.X) / 2f;
            DrawTextLocalScaled(cardCenter, rotation, new Vector2(nameX, cursorY), name, nameColor, nameScale, vs, _nameFont);
            cursorY += nameSize.Y + TypeRowMarginTop * vs;

            // Type Row — space-between: cost section (left) | type text (right)
            string typeLabel = GetTypeLabel(card.Type);
            var typeColor = isColorless
                ? ColorlessMutedText
                : CardPalette.TypeText(cc);
            float typeScale = CostLabelFontScale * vs;

            // Add letter spacing for type label measurement and drawing
            float savedTypeSpacing = _bodyFont.Spacing;
            _bodyFont.Spacing = 2f * vs;
            var typeSize = _bodyFont.MeasureString(typeLabel) * typeScale;

            var costs = card.Cost.ToArray();
            bool hasCost = costs != null && costs.Length > 0;

            if (hasCost)
            {
                // Left side: "DISCARD" label + diamond pips
                string costLabel = "DISCARD";
                var costLabelColor = isColorless
                    ? ColorlessMutedText
                    : CardPalette.CostLabel(cc);
                float costLabelScale = CostLabelFontScale * vs;

                // Letter spacing already active from type label setup (2f * vs)
                var costLabelSize = _bodyFont.MeasureString(costLabel) * costLabelScale;

                float leftX = padLeft;
                float textCenterY = cursorY + (typeSize.Y - costLabelSize.Y) / 2f;
                DrawTextLocalScaled(cardCenter, rotation, new Vector2(leftX, textCenterY), costLabel, costLabelColor, costLabelScale, vs, _bodyFont);

                // Diamond pips after label
                float pipStartX = leftX + costLabelSize.X + CostLabelGap * vs;
                float pipSize = CostPipSize * vs;
                float pipGap = CostPipGap * vs;
                float pipCenterY = cursorY + typeSize.Y / 2f;

                float flashT = (float)Math.Sin(_elapsedTime * MathHelper.TwoPi * CostPipFlashHz) * 0.5f + 0.5f;
                float flashAlpha = MathHelper.Lerp(CostPipFlashMinAlpha, CostPipFlashMaxAlpha, flashT);

                for (int i = 0; i < costs.Length; i++)
                {
                    float pipX = pipStartX + i * (pipSize + pipGap);
                    Color pipColor = GetCostPipColor(costs[i], cc, isColorless);
                    bool showOutline = NeedsPipOutline(costs[i], cc, isColorless);
                    bool isWaived = VigorService.IsWaivedPipIndex(i, costs.Length, waivedPipCount);
                    float alpha = isWaived ? flashAlpha : 1f;
                    DrawDiamondPip(cardCenter, rotation, vs, pipX, pipCenterY - pipSize / 2f, pipSize, pipColor, cc, showOutline, isColorless, alpha);
                }

                // Right side: type text
                float typeX = cardWidth - padRight - typeSize.X;
                DrawTextLocalScaled(cardCenter, rotation, new Vector2(typeX, cursorY), typeLabel, typeColor, typeScale, vs, _bodyFont);
            }
            else
            {
                // No cost — type text right-aligned only
                float typeX = cardWidth - padRight - typeSize.X;
                DrawTextLocalScaled(cardCenter, rotation, new Vector2(typeX, cursorY), typeLabel, typeColor, typeScale, vs, _bodyFont);
            }

            cursorY += typeSize.Y + RuleMarginTop * vs;
            _bodyFont.Spacing = savedTypeSpacing;

            // Rule Line — full width with padding
            var ruleColor = isColorless
                ? Color.Lerp(ColorlessSurface, ColorlessMutedText, 0.45f)
                : CardPalette.RuleLine(cc);
            float ruleWidth = cardWidth - padLeft - padRight;
            DrawRectangleLocalScaled(cardCenter, rotation, new Vector2(padLeft, cursorY), ruleWidth, RuleHeight * vs, ruleColor, vs);
            cursorY += RuleHeight * vs;

            return cursorY;
        }

        /// <summary>
        /// Draws a diamond-shaped pip (square rotated 45deg) at the given position.
        /// </summary>
        private void DrawDiamondPip(Vector2 cardCenter, float rotation, float vs,
            float x, float y, float size, Color color, CardData.CardColor cc, bool showOutline,
            bool isColorless, float alpha = 1f)
        {
            // Create a small square texture and draw it rotated 45 degrees
            int texSize = Math.Max(1, (int)Math.Ceiling(size));
            var tex = GetPerCornerRoundedRectTexture(texSize, texSize, 0, 0, 0, 0);

            // Position at center of the pip area, draw with 45deg rotation added to card rotation
            float halfSize = size / 2f;
            float centerX = x + halfSize;
            float centerY = y + halfSize;

            // Convert to world position using card-local transform
            float localX = -CW * vs / 2f + centerX;
            float localY = -CH * vs / 2f + centerY;
            float cos = (float)Math.Cos(rotation);
            float sin = (float)Math.Sin(rotation);
            var rotated = new Vector2(localX * cos - localY * sin, localX * sin + localY * cos);
            var world = cardCenter + rotated;

            float diamondRotation = rotation + MathHelper.PiOver4;
            var drawScale = new Vector2(size / texSize, size / texSize);

            color *= alpha * _drawAlpha;

            if (showOutline)
            {
                // Draw outline at full size, then fill at reduced scale
                var outlineColor = Tint(isColorless
                    ? Color.Black
                    : CardPalette.CostPipOutline(cc)) * alpha;
                _spriteBatch.Draw(tex, world, null, outlineColor, diamondRotation,
                    new Vector2(texSize / 2f, texSize / 2f),
                    drawScale,
                    SpriteEffects.None, 0f);

                float fillScale = Math.Max(0f, 1f - CostPipOutlineFrac * 2f);
                _spriteBatch.Draw(tex, world, null, color, diamondRotation,
                    new Vector2(texSize / 2f, texSize / 2f),
                    drawScale * fillScale,
                    SpriteEffects.None, 0f);
            }
            else
            {
                _spriteBatch.Draw(tex, world, null, color, diamondRotation,
                    new Vector2(texSize / 2f, texSize / 2f),
                    drawScale,
                    SpriteEffects.None, 0f);
            }
        }

        private void DrawStatChips(Vector2 cardCenter, float rotation, float vs, CardData.CardColor cc,
            Entity entity, CardBase card, bool isColorless)
        {
            float chipX = ChipColumnX * vs;
            bool suppressDelta = entity.HasComponent<SuppressStatDeltaDisplay>();

            int printedBlock = card.Block;
			int blackCardBlockBonus = CardColorQualificationService.QualifiesAs(
				entity,
				CardData.CardColor.Black) ? 1 : 0;
            int blockValue = suppressDelta
                ? printedBlock + blackCardBlockBonus
                : BlockValueService.GetTotalBlockValue(entity);
            int blockDelta = suppressDelta ? blackCardBlockBonus : blockValue - printedBlock;
            bool showBlock = blockValue > 0 && !card.IsWeapon && !card.IsToken;

            var phase = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
            var alternateProfile = phase?.Sub == SubPhase.Action
                ? AlternateCardPlayService.GetProfile(EntityManager, entity, SubPhase.Action)
                : null;
            bool alternateAttack = alternateProfile?.TreatsAsAttack == true;
            bool showAttack = card.Type == CardType.Attack || alternateAttack;
            bool showAp = (card.Type != CardType.Block && card.Type != CardType.Relic)
                || alternateProfile?.IsFreeAction == true;

            float effectiveChipSlotHeight = ChipSlotHeight;
            float effectiveChipSize = ChipSize;
            if (ChipScaleWithTitle)
            {
                float availableHeight = GetSettings().CardHeight - ChipColumnTopY - ChipColumnBottomPad;
                float totalNeeded = EstimateColumnHeight(ChipSize, ChipSlotHeight, showBlock, showAttack, showAp);
                if (totalNeeded > availableHeight && totalNeeded > 0f)
                {
                    float ratio = availableHeight / totalNeeded;
                    effectiveChipSlotHeight = ChipSlotHeight * ratio;
                    effectiveChipSize = ChipSize * ratio;
                }
            }

            float lastChipBottomY = ChipColumnTopY * vs;
            bool drewStatChip = false;

            // BLK chip (slot 0)
            if (showBlock)
            {
                float chipY = ChipColumnTopY * vs;
                bool hasDelta = blockDelta != 0;

                DrawChipLabelSlab(cardCenter, rotation, vs, cc, chipX, chipY, "BLOCK", ChipVariant.BLK, isColorless);
                float chipBodyY = chipY + LabelSlabHeight * vs;

                DrawChip(cardCenter, rotation, vs, cc, chipX, chipBodyY, blockValue.ToString(), ChipVariant.BLK, true, hasDelta, isColorless, effectiveChipSize);

                if (hasDelta)
                {
                    float slabY = chipBodyY + effectiveChipSize * vs;
                    DrawDeltaSlab(cardCenter, rotation, vs, cc, chipX, slabY, blockDelta);
                }

                lastChipBottomY = chipY + GetChipGroupHeight(vs, effectiveChipSize, reserveDeltaSlab: true);
                drewStatChip = true;
            }

            // ATK chip (slot 1)
            if (showAttack)
            {
                int damage;
                int printedDamage;
                if (alternateAttack)
                {
                    printedDamage = alternateProfile.AttackDamage;
                    damage = suppressDelta
                        ? printedDamage
                        : GetAlternateAttackDamage(entity, printedDamage);
                }
                else
                {
                    printedDamage = card.Damage;
                    damage = suppressDelta ? card.Damage : GetEffectiveDamage(entity, card);
                }

                int damageDelta = suppressDelta ? 0 : damage - printedDamage;
                float chipY = (ChipColumnTopY + effectiveChipSlotHeight) * vs;
                bool hasDelta = damageDelta != 0;

                DrawChipLabelSlab(cardCenter, rotation, vs, cc, chipX, chipY, "DAMAGE", ChipVariant.ATK, isColorless);
                float chipBodyY = chipY + LabelSlabHeight * vs;

                DrawChip(cardCenter, rotation, vs, cc, chipX, chipBodyY, damage.ToString(), ChipVariant.ATK, true, hasDelta, isColorless, effectiveChipSize);

                if (hasDelta)
                {
                    float slabY = chipBodyY + effectiveChipSize * vs;
                    DrawDeltaSlab(cardCenter, rotation, vs, cc, chipX, slabY, damageDelta);
                }

                // Always reserve delta slab space so AP position is stable when modifiers appear
                lastChipBottomY = chipY + GetChipGroupHeight(vs, effectiveChipSize, reserveDeltaSlab: true);
                drewStatChip = true;
            }

            // AP / FREE chip — flows below last stat chip; skip for Block and Relic cards
            if (showAp)
            {
                float apLabelY = drewStatChip
                    ? lastChipBottomY + ChipGap * vs
                    : ChipColumnTopY * vs;

                if (card.IsFreeAction || alternateProfile?.IsFreeAction == true)
                {
                    DrawChipLabelSlab(cardCenter, rotation, vs, cc, chipX, apLabelY, "FREE", ChipVariant.FREE, isColorless);
                    float chipBodyY = apLabelY + LabelSlabHeight * vs;
                    DrawChip(cardCenter, rotation, vs, cc, chipX, chipBodyY, "0", ChipVariant.FREE, true, false, isColorless, effectiveChipSize);
                }
                else
                {
                    DrawChipLabelSlab(cardCenter, rotation, vs, cc, chipX, apLabelY, "AP", ChipVariant.AP, isColorless);
                    float chipBodyY = apLabelY + LabelSlabHeight * vs;
                    DrawChip(cardCenter, rotation, vs, cc, chipX, chipBodyY, "1", ChipVariant.AP, true, false, isColorless, effectiveChipSize);
                }
            }
        }

        private float GetChipGroupHeight(float vs, float chipSize, bool reserveDeltaSlab)
            => (LabelSlabHeight + chipSize + (reserveDeltaSlab ? SlabHeight : 0)) * vs;

        /// <summary>
        /// Estimates total chip column height from column top, using worst-case delta slabs for scaling.
        /// </summary>
        private float EstimateColumnHeight(float chipSize, float slotHeight, bool showBlock, bool showAttack, bool showAp)
        {
            if (showAp && !showBlock && !showAttack)
                return LabelSlabHeight + chipSize;

            float bottom = ChipColumnTopY;

            if (showBlock)
                bottom = Math.Max(bottom, ChipColumnTopY + LabelSlabHeight + chipSize + SlabHeight);

            if (showAttack)
            {
                float atkTop = ChipColumnTopY + slotHeight;
                bottom = Math.Max(bottom, atkTop + LabelSlabHeight + chipSize + SlabHeight);
            }

            if (showAp)
                bottom += ChipGap + LabelSlabHeight + chipSize;

            return bottom - ChipColumnTopY;
        }

        private enum ChipVariant { BLK, ATK, AP, FREE }

        /// <summary>
        /// Draws a label slab above a chip (rounded top corners, flat bottom).
        /// </summary>
        private void DrawChipLabelSlab(Vector2 cardCenter, float rotation, float vs,
            CardData.CardColor cc, float x, float y, string label, ChipVariant variant, bool isColorless)
        {
            float slabW = ChipWidth * vs;
            float slabH = LabelSlabHeight * vs;
            int cr = (int)(ChipCornerRadius * vs);

            // Rounded top, flat bottom
            var slabTex = GetPerCornerRoundedRectTexture((int)slabW, (int)slabH, cr, cr, 0, 0);

            Color bgColor;
            Color textColor;

            switch (variant)
            {
                case ChipVariant.BLK:
                    bgColor = CardPalette.BlockLabelSlabBackground(cc);
                    textColor = CardPalette.BlockLabelSlabText(cc);
                    break;
                case ChipVariant.ATK:
                    bgColor = CardPalette.AttackLabelSlabBackground;
                    textColor = CardPalette.AttackLabelSlabText;
                    break;
                case ChipVariant.AP:
                    bgColor = CardPalette.ActionPointLabelSlabBackground(cc);
                    textColor = CardPalette.ActionPointLabelSlabText(cc);
                    break;
                case ChipVariant.FREE:
                    bgColor = Color.Transparent;
                    textColor = CardPalette.FreeLabelSlabText(cc);
                    break;
                default:
                    bgColor = Color.Transparent;
                    textColor = Color.White;
                    break;
            }
            if (isColorless && variant is ChipVariant.AP or ChipVariant.FREE)
            {
                bgColor = variant == ChipVariant.FREE
                    ? Color.Transparent
                    : Color.Lerp(ColorlessSurface, ColorlessBackground, 0.25f);
                textColor = ColorlessMutedText;
            }

            DrawTextureLocalScaled(cardCenter, rotation, new Vector2(x, y), slabTex, new Vector2(slabW, slabH), bgColor, vs);

            // Center label text within slab
            float labelScale = LabelSlabFontScale * vs;
            var labelSize = _bodyFont.MeasureString(label) * labelScale;
            float textX = x + (slabW - labelSize.X) / 2f;
            float textY = y + (slabH - labelSize.Y) / 2f;
            DrawTextLocalScaled(cardCenter, rotation, new Vector2(textX, textY), label, textColor, labelScale, vs, _bodyFont);
        }

        /// <summary>
        /// Draws a stat chip — value-only, no label inside.
        /// </summary>
        private void DrawChip(Vector2 cardCenter, float rotation, float vs, CardData.CardColor cc,
            float x, float y, string value, ChipVariant variant, bool hasLabelAbove, bool hasDeltaBelow,
            bool isColorless, float chipSizeOverride = -1)
        {
            float chipW = ChipWidth * vs;
            float chipH = (chipSizeOverride > 0 ? chipSizeOverride : ChipSize) * vs;
            int cr = (int)(ChipCornerRadius * vs);

            // Corner radii: flat top if label above, flat bottom if delta below
            int rTL = hasLabelAbove ? 0 : cr;
            int rTR = hasLabelAbove ? 0 : cr;
            int rBR = hasDeltaBelow ? 0 : cr;
            int rBL = hasDeltaBelow ? 0 : cr;

            switch (variant)
            {
                case ChipVariant.BLK:
                {
                    // Solid fill — steel blue tint
                    Color bgColor = CardPalette.BlockChipBackground(cc);
                    Color valColor = CardPalette.BlockChipText(cc);

                    var tex = GetPerCornerRoundedRectTexture((int)chipW, (int)chipH, rTL, rTR, rBR, rBL);
                    DrawTextureLocalScaled(cardCenter, rotation, new Vector2(x, y), tex, new Vector2(chipW, chipH), bgColor, vs);
                    DrawChipValue(cardCenter, rotation, vs, x, y, chipW, chipH, value, valColor);
                    break;
                }
                case ChipVariant.ATK:
                {
                    Color bgColor = new Color(204, 34, 34);
                    Color valColor = Color.White;

                    var tex = GetPerCornerRoundedRectTexture((int)chipW, (int)chipH, rTL, rTR, rBR, rBL);
                    DrawTextureLocalScaled(cardCenter, rotation, new Vector2(x, y), tex, new Vector2(chipW, chipH), bgColor, vs);
                    DrawChipValue(cardCenter, rotation, vs, x, y, chipW, chipH, value, valColor);
                    break;
                }
                case ChipVariant.AP:
                {
                    Color bgColor = isColorless
                        ? ColorlessSurface
                        : CardPalette.ActionPointChipBackground(cc);
                    Color valColor = isColorless
                        ? ColorlessPrimaryText
                        : CardPalette.ActionPointChipText(cc);

                    var tex = GetPerCornerRoundedRectTexture((int)chipW, (int)chipH, rTL, rTR, rBR, rBL);
                    DrawTextureLocalScaled(cardCenter, rotation, new Vector2(x, y), tex, new Vector2(chipW, chipH), bgColor, vs);
                    DrawChipValue(cardCenter, rotation, vs, x, y, chipW, chipH, value, valColor);
                    break;
                }
                case ChipVariant.FREE:
                {
                    // Dashed border — approximate with solid border + card bg fill
                    Color borderColor = isColorless
                        ? ColorlessMutedText
                        : CardPalette.FreeChipBorder(cc);
                    Color valColor = isColorless
                        ? ColorlessPrimaryText
                        : CardPalette.FreeChipText(cc);

                    var outerTex = GetPerCornerRoundedRectTexture((int)chipW, (int)chipH, rTL, rTR, rBR, rBL);
                    DrawTextureLocalScaled(cardCenter, rotation, new Vector2(x, y), outerTex, new Vector2(chipW, chipH), borderColor, vs);

                    int bt = (int)(ChipBorderThickness * vs);
                    float innerW = chipW - bt * 2;
                    float innerH = chipH - bt * 2;
                    if (innerW > 0 && innerH > 0)
                    {
                        var innerBg = isColorless
                            ? ColorlessBackground
                            : CardPalette.Background(cc);
                        var innerTex = GetPerCornerRoundedRectTexture((int)innerW, (int)innerH,
                            Math.Max(0, rTL - bt), Math.Max(0, rTR - bt),
                            Math.Max(0, rBR - bt), Math.Max(0, rBL - bt));
                        DrawTextureLocalScaled(cardCenter, rotation, new Vector2(x + bt, y + bt), innerTex, new Vector2(innerW, innerH), innerBg, vs);
                    }

                    DrawChipValue(cardCenter, rotation, vs, x, y, chipW, chipH, value, valColor);
                    break;
                }
            }
        }

        /// <summary>
        /// Draws chip value text centered in the chip area.
        /// </summary>
        private void DrawChipValue(Vector2 cardCenter, float rotation, float vs,
            float x, float y, float chipW, float chipH, string value, Color color)
        {
            float valScale = ChipValueFontScale * vs;
            var valSize = _nameFont.MeasureString(value) * valScale;
            float valX = x + (chipW - valSize.X) / 2f;
            float valY = y + (chipH - valSize.Y) / 2f;
            DrawTextLocalScaled(cardCenter, rotation, new Vector2(valX, valY), value, color, valScale, vs, _nameFont);
        }

        private void DrawDeltaSlab(Vector2 cardCenter, float rotation, float vs, CardData.CardColor cc, float x, float y, int delta)
        {
            bool isW = cc == CardData.CardColor.White;
            bool isPositive = delta > 0;

            float slabW = SlabWidth * vs;
            float slabH = SlabHeight * vs;
            int r = (int)(SlabCornerRadius * vs);

            // Bottom corners only
            var slabTex = GetPerCornerRoundedRectTexture((int)slabW, (int)slabH, 0, 0, r, r);

            Color bgColor;
            Color textColor;
            if (isPositive)
            {
                bgColor = isW ? new Color(42, 138, 42) : new Color(26, 90, 26);
                textColor = isW ? Color.White : new Color(94, 255, 94);
            }
            else
            {
                bgColor = isW ? new Color(122, 98, 16) : new Color(90, 66, 0);
                textColor = isW ? Color.White : new Color(255, 204, 68);
            }

            DrawTextureLocalScaled(cardCenter, rotation, new Vector2(x, y), slabTex, new Vector2(slabW, slabH), bgColor, vs);

            string deltaText = delta > 0 ? $"+{delta}" : delta.ToString();
            var textSize = _bodyFont.MeasureString(deltaText) * (SlabFontScale * vs);
            float textX = x + (slabW - textSize.X) / 2f;
            float textY = y + (slabH - textSize.Y) / 2f;
            DrawTextLocalScaled(cardCenter, rotation, new Vector2(textX, textY), deltaText, textColor, SlabFontScale * vs, vs, _bodyFont);
        }

        private Color GetCostPipColor(string costType, CardData.CardColor cc, bool isColorless)
        {
            return costType.Trim().ToLowerInvariant() switch
            {
                "red"   => CardPalette.CostPipRed,
                "white" => CardPalette.CostPipWhite,
                "black" => CardPalette.CostPipBlack,
                _       => isColorless
                    ? ColorlessMutedText
                    : CardPalette.CostPipAny(cc),
            };
        }

        /// <summary>
        /// Outline when a pip would blend into the card, plus gray Any pips on Colorless cards.
        /// </summary>
        private static bool NeedsPipOutline(string costType, CardData.CardColor cc, bool isColorless)
        {
            if (isColorless && string.Equals(costType.Trim(), "Any", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
            return costType.Trim().ToLowerInvariant() switch
            {
                "white" => cc == CardData.CardColor.White,
                "red"   => cc == CardData.CardColor.Red,
                "black" => cc == CardData.CardColor.Black,
                _       => false,
            };
        }

        private static string GetTypeLabel(CardType type) => type switch
        {
            CardType.Attack => "ATTACK",
            CardType.Prayer => "PRAYER",
            CardType.Block => "BLOCK",
            CardType.Relic => "RELIC",
            _ => "CARD"
        };

        private int GetAlternateAttackDamage(Entity entity, int baseDamage)
        {
            try
            {
                var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
                if (player == null || enemy == null)
                    return Math.Max(0, baseDamage);

                var preview = new ModifyHpRequestEvent
                {
                    Source = player,
                    Target = enemy,
                    AttackCard = entity,
                    DamageType = ModifyTypeEnum.Attack,
                };
                return Math.Max(0, AppliedPassivesService.GetPreviewAttackDamage(preview, baseDamage, ReadOnly: true));
            }
            catch
            {
                return Math.Max(0, baseDamage);
            }
        }

        private int GetEffectiveDamage(Entity entity, CardBase card)
        {
            try
            {
                int baseDamage = CardStatModifierService.GetCardDamage(EntityManager, entity, CardStatQueryMode.Preview).TotalValue;

                int finalDamage = baseDamage;
                try
                {
                    var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
                    var enemy = EntityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
                    if (player != null && enemy != null)
                    {
                        var preview = new ModifyHpRequestEvent
                        {
                            Source = player,
                            Target = enemy,
                            AttackCard = entity,
                            DamageType = ModifyTypeEnum.Attack
                        };
                        finalDamage = AppliedPassivesService.GetPreviewAttackDamage(preview, baseDamage, ReadOnly: true);
                    }
                }
                catch { finalDamage = baseDamage; }

                return Math.Max(0, finalDamage);
            }
            catch
            {
                return Math.Max(0, card.Damage);
            }
        }

        private Color ColorlessBackground => new(
            ClampByte(ColorlessBackgroundR),
            ClampByte(ColorlessBackgroundG),
            ClampByte(ColorlessBackgroundB));

        private Color ColorlessPrimaryText => new(
            ClampByte(ColorlessPrimaryTextR),
            ClampByte(ColorlessPrimaryTextG),
            ClampByte(ColorlessPrimaryTextB));

        private Color ColorlessMutedText => new(
            ClampByte(ColorlessMutedTextR),
            ClampByte(ColorlessMutedTextG),
            ClampByte(ColorlessMutedTextB));

        private Color ColorlessSurface => new(
            ClampByte(ColorlessSurfaceR),
            ClampByte(ColorlessSurfaceG),
            ClampByte(ColorlessSurfaceB));

        private static byte ClampByte(int value) => (byte)Math.Clamp(value, 0, 255);
    }
}
