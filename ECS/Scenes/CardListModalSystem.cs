using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
    /// <summary>
    /// Fullscreen card-list and climb inventory overlay.
    /// </summary>
    [DebugTab("Card List Modal")]
    public class CardListModalSystem : Core.System
    {
        private const string ContextId = "overlay.card-list";
        private const string CloseEntityName = "CardListModal_Close";
        private const string TooltipEntityPrefix = "CardListModal_Tooltip_";
        private const string EquipmentTooltipEntityName = "CardListModal_EquipmentTooltip";
        private const string WeaponPreviewEntityName = "CardListModal_WeaponPreview";
        private const string ReplacementInstruction = "Select a card to replace";

        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private readonly SpriteFont _titleFont;
        private readonly SpriteFont _bodyFont;
        private readonly Texture2D _pixel;
        private readonly RasterizerState _scissorRasterizer;
        private readonly Dictionary<int, bool> _previousCardHoverHighlight = new();
        private readonly HashSet<int> _modalCardIds = new();
        private readonly HashSet<int> _inventoryAuxCardIds = new();
        private readonly EquipmentTooltipDisplaySystem _equipmentTooltipDisplaySystem;
        private readonly List<Entity> _orderedCards = new();
        private readonly List<VisibleCardPresentation> _visibleCards = new();
        public bool UseCachedCardBasesForDiagnostics { get; set; } = true;
        private CardLedgerSummary _ledgerSummary;
        private CardListModal _presentationModal;

        private Entity _weaponPreviewCard;
        private string _weaponPreviewId = string.Empty;

        [DebugEditable(DisplayName = "Header Padding X", Step = 1, Min = 0, Max = 200)]
        public int HeaderPaddingX { get; set; } = 36;
        [DebugEditable(DisplayName = "Header Padding Top", Step = 1, Min = 0, Max = 120)]
        public int HeaderPaddingTop { get; set; } = 24;
        [DebugEditable(DisplayName = "Header Height", Step = 1, Min = 40, Max = 200)]
        public int HeaderHeight { get; set; } = 80;
        [DebugEditable(DisplayName = "Content Padding", Step = 1, Min = 0, Max = 120)]
        public int ContentPadding { get; set; } = 36;
        [DebugEditable(DisplayName = "Grid Gap", Step = 1, Min = 0, Max = 80)]
        public int GridGap { get; set; } = 24;
        [DebugEditable(DisplayName = "Build Panel Width", Step = 1, Min = 240, Max = 700)]
        public int BuildPanelWidth { get; set; } = 380;
        [DebugEditable(DisplayName = "Build Deck Gap", Step = 1, Min = 0, Max = 80)]
        public int BuildDeckGap { get; set; } = 20;
        [DebugEditable(DisplayName = "Close Height", Step = 1, Min = 24, Max = 100)]
        public int CloseHeight { get; set; } = 44;
        [DebugEditable(DisplayName = "Close Padding X", Step = 1, Min = 4, Max = 80)]
        public int ClosePaddingX { get; set; } = 18;
        [DebugEditable(DisplayName = "Scroll Step", Step = 1, Min = 0, Max = 240)]
        public int ScrollStep { get; set; } = 100;

        [DebugEditable(DisplayName = "Title Scale", Step = 0.01f, Min = 0.05f, Max = 1f)]
        public float TitleScale { get; set; } = 0.30f;
        [DebugEditable(DisplayName = "Instruction Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
        public float InstructionScale { get; set; } = 0.095f;
        [DebugEditable(DisplayName = "Close Text Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
        public float CloseTextScale { get; set; } = 0.10f;
        [DebugEditable(DisplayName = "Ledger Count Scale", Step = 0.01f, Min = 0.03f, Max = 0.5f)]
        public float LedgerCountScale { get; set; } = 0.19f;
        [DebugEditable(DisplayName = "Total Count Scale", Step = 0.01f, Min = 0.03f, Max = 0.5f)]
        public float TotalCountScale { get; set; } = 0.22f;
        [DebugEditable(DisplayName = "Section Head Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)]
        public float SectionHeadScale { get; set; } = 0.07f;
        [DebugEditable(DisplayName = "Body Text Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)]
        public float BodyTextScale { get; set; } = 0.085f;
        [DebugEditable(DisplayName = "Passive Text Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)]
        public float PassiveTextScale { get; set; } = 0.075f;

        [DebugEditable(DisplayName = "Gamepad Scroll Speed (px/s)", Step = 50, Min = 100, Max = 6000)]
        public float GamepadScrollSpeed { get; set; } = 1400f;
        [DebugEditable(DisplayName = "Right Stick Deadzone", Step = 0.01f, Min = 0f, Max = 0.5f)]
        public float RightStickDeadzone { get; set; } = 0.15f;
        [DebugEditable(DisplayName = "Speed Exponent", Step = 0.1f, Min = 0.1f, Max = 5f)]
        public float SpeedExponent { get; set; } = 1.2f;
        [DebugEditable(DisplayName = "Max Multiplier", Step = 0.1f, Min = 1f, Max = 10f)]
        public float MaxMultiplier { get; set; } = 3f;

        [DebugEditable(DisplayName = "Overlay Dim Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
        public float OverlayDimAlpha { get; set; } = 0.90f;
        private static readonly Color White1 = new(255, 255, 255);
        private static readonly Color White2 = new(240, 236, 230);
        private static readonly Color White3 = new(200, 192, 184);
        private static readonly Color Red3 = new(196, 30, 58);
        private static readonly Color Black2 = new(20, 20, 20);
        private static readonly Color Black3 = new(30, 30, 30);
        private static readonly Color Black4 = new(42, 42, 42);
        private static readonly Color CardWhiteBg = new(220, 215, 206);
        private static readonly Color CardWhiteStripe = new(153, 153, 153);
        private static readonly Color CardRedBg = new(78, 12, 12);
        private static readonly Color CardRedStripe = new(204, 34, 34);
        private static readonly Color CardBlackBg = new(19, 19, 19);
        private static readonly Color CardBlackStripe = new(51, 51, 51);

        public CardListModalSystem(
            EntityManager entityManager,
            GraphicsDevice graphicsDevice,
            SpriteBatch spriteBatch,
            ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _imageAssets = imageAssets;
            _titleFont = FontSingleton.TitleFont;
            _bodyFont = FontSingleton.ChakraPetchFont;
            _pixel = _imageAssets.GetPixel(Color.White);
            _scissorRasterizer = new RasterizerState { ScissorTestEnable = true, CullMode = CullMode.None };
            EnsureEquipmentTooltipEntity();
            _equipmentTooltipDisplaySystem = new EquipmentTooltipDisplaySystem(
                EntityManager,
                _graphicsDevice,
                _spriteBatch,
                _imageAssets,
                EquipmentTooltipEntityName);

            EventManager.Subscribe<OpenCardListModalEvent>(OpenModal);
            EventManager.Subscribe<CloseCardListModalEvent>(_ => CloseModal());
            EventManager.Subscribe<CardListModalCardSelectedEvent>(OnCardSelected);
            EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<CardListModal>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            base.Update(gameTime);
            var modalEntity = GetRelevantEntities().FirstOrDefault();
            if (modalEntity == null) return;
            var modal = modalEntity.GetComponent<CardListModal>();
            if (modal == null) return;

            EnsureModalRoot(modalEntity, modal);
            if (!modal.IsOpen || modal.Cards == null)
            {
                SetCloseButtonActive(false);
                CleanupOverlayTooltipEntities();
                RestoreModalHoverHighlightState();
                ResetEquipmentTooltipState();
                return;
            }

            var mode = ResolveMode(modal);
            var layout = ComputeLayout(mode, modal);
            if (!ReferenceEquals(modal, _presentationModal)) RebuildPresentationCache(modal);
            EnsureCloseButton(layout.CloseButton);
            UpdateScroll(modal, mode, layout, gameTime);
            LayoutCards(modal, layout);
            UpdateInventoryTooltipEntities(modal, mode, layout);
            UpdateEquipmentTooltip(gameTime, mode);
            TryPublishSelection(modal);
        }

        public void Draw()
        {
            DrawBackdrop();
            DrawForeground();
        }

        public void DrawBackdrop()
        {
            if (!TryGetOpenDrawState(out var modal, out _, out var layout)) return;

            _spriteBatch.Draw(
                _pixel,
                new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
                Color.Black * MathHelper.Clamp(OverlayDimAlpha, 0f, 1f));
            DrawHeader(modal, layout);
        }

        public void DrawForeground()
        {
            if (!TryGetOpenDrawState(out var modal, out var mode, out var layout)) return;

            if (mode == CardListModalMode.Inventory)
            {
                DrawInventoryBuildPanel(modal, layout);
                DrawCardGrid(layout.DeckClip);
            }
            else
            {
                DrawCardGrid(layout.DeckClip);
            }

            DrawCloseButton(layout.CloseButton);
            if (mode == CardListModalMode.Inventory)
            {
                _equipmentTooltipDisplaySystem.Draw();
            }
        }

        private bool TryGetOpenDrawState(out CardListModal modal, out CardListModalMode mode, out OverlayLayout layout)
        {
            modal = null;
            mode = CardListModalMode.CardList;
            layout = default;

            var modalEntity = GetRelevantEntities().FirstOrDefault();
            modal = modalEntity?.GetComponent<CardListModal>();
            if (modal == null || !modal.IsOpen) return false;

            mode = ResolveMode(modal);
            layout = ComputeLayout(mode, modal);
            return true;
        }

        private void EnsureEquipmentTooltipEntity()
        {
            var entity = EntityManager.GetEntity(EquipmentTooltipEntityName);
            if (entity == null)
            {
                entity = EntityManager.CreateEntity(EquipmentTooltipEntityName);
                EntityManager.AddComponent(entity, new EquipmentTooltipState());
                EntityManager.AddComponent(entity, new Transform { ZOrder = 20001 });
                EntityManager.AddComponent(entity, new UIElement
                {
                    Bounds = Rectangle.Empty,
                    IsInteractable = false,
                    IsHidden = true,
                    TooltipType = TooltipType.None,
                });
                EntityManager.AddComponent(entity, new DontDestroyOnLoad());
                return;
            }

            if (entity.GetComponent<EquipmentTooltipState>() == null) EntityManager.AddComponent(entity, new EquipmentTooltipState());
            if (entity.GetComponent<Transform>() == null) EntityManager.AddComponent(entity, new Transform { ZOrder = 20001 });
            if (entity.GetComponent<UIElement>() == null)
            {
                EntityManager.AddComponent(entity, new UIElement
                {
                    Bounds = Rectangle.Empty,
                    IsInteractable = false,
                    IsHidden = true,
                    TooltipType = TooltipType.None,
                });
            }
        }

        private void UpdateEquipmentTooltip(GameTime gameTime, CardListModalMode mode)
        {
            if (mode != CardListModalMode.Inventory)
            {
                ResetEquipmentTooltipState();
                return;
            }

            _equipmentTooltipDisplaySystem.Update(gameTime);
        }

        private void ResetEquipmentTooltipState()
        {
            var state = EntityManager.GetEntity(EquipmentTooltipEntityName)
                ?.GetComponent<EquipmentTooltipState>();
            if (state == null) return;
            state.TargetVisible = false;
            state.EquipmentEntity = null;
            state.AnchorEntity = null;
            state.Alpha01 = 0f;
        }

        private OverlayLayout ComputeLayout(CardListModalMode mode, CardListModal modal)
        {
            int w = Game1.VirtualWidth;
            int h = Game1.VirtualHeight;
            int headerH = Math.Max(40, HeaderHeight);
            var header = new Rectangle(0, 0, w, headerH);
            var content = new Rectangle(
                ContentPadding,
                headerH,
                w - ContentPadding * 2,
                h - headerH - ContentPadding);
            int closeW = MeasureCloseWidth();
            var close = new Rectangle(
                w - HeaderPaddingX - closeW,
                HeaderPaddingTop,
                closeW,
                CloseHeight);

            var ledger = new Rectangle(
                Math.Max(HeaderPaddingX, close.X - 360),
                HeaderPaddingTop + 4,
                Math.Max(1, close.X - HeaderPaddingX - Math.Max(HeaderPaddingX, close.X - 360) - 16),
                Math.Max(1, CloseHeight - 8));

            int instructionH = ShouldShowReplacementInstruction(modal)
                ? (int)Math.Ceiling(_bodyFont.LineSpacing * InstructionScale) + 12
                : 0;

            if (mode == CardListModalMode.Inventory)
            {
                var build = new Rectangle(content.X, content.Y + 8, BuildPanelWidth, content.Height - 8);
                var deck = new Rectangle(
                    build.Right + BuildDeckGap,
                    build.Y,
                    Math.Max(1, content.Right - build.Right - BuildDeckGap),
                    build.Height);
                return new OverlayLayout(header, content, close, ledger, deck, build, instructionH);
            }

            var deckClip = new Rectangle(
                content.X,
                content.Y + 8 + instructionH,
                content.Width,
                Math.Max(1, content.Height - 8 - instructionH));
            return new OverlayLayout(header, content, close, ledger, deckClip, Rectangle.Empty, instructionH);
        }

        private int MeasureCloseWidth()
        {
            if (_bodyFont == null) return 92;
            int textW = (int)Math.Ceiling(_bodyFont.MeasureString("Close").X * CloseTextScale);
            return Math.Max(70, textW + ClosePaddingX * 2);
        }

        private CardListModalMode ResolveMode(CardListModal modal)
        {
            if (modal == null) return CardListModalMode.CardList;
            if (modal.Mode == CardListModalMode.Inventory) return CardListModalMode.Inventory;
            if (modal.Mode == CardListModalMode.CardList) return CardListModalMode.CardList;
            if (modal.IsSelectable || string.Equals(modal.SelectionContext, CardListSelectionContexts.ClimbReplacement, StringComparison.OrdinalIgnoreCase))
            {
                return CardListModalMode.CardList;
            }

            return IsClimbScene() ? CardListModalMode.Inventory : CardListModalMode.CardList;
        }

        private bool IsClimbScene()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>()
                .FirstOrDefault()
                ?.GetComponent<SceneState>()
                ?.Current == SceneId.Climb;
        }

        private void UpdateScroll(CardListModal modal, CardListModalMode mode, OverlayLayout layout, GameTime gameTime)
        {
            PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
            if (!Game1.WindowIsActive || StateSingleton.IsActive) return;

            bool hasWheel = Math.Abs(input.ScrollDelta) > 0.001f;
            bool hasStick = MathF.Abs(input.RightStick.Y) > RightStickDeadzone;
            if (!hasWheel && !hasStick) return;

            bool useBuild = mode == CardListModalMode.Inventory
                && layout.BuildClip.Contains(input.PointerPosition);
            int maxScroll = useBuild
                ? Math.Max(0, CalculateBuildContentHeight() - layout.BuildClip.Height)
                : Math.Max(0, CalculateCardContentHeight(modal, layout.DeckClip.Width) - layout.DeckClip.Height);

            int current = useBuild ? modal.BuildScrollOffset : modal.ScrollOffset;
            if (hasWheel)
            {
                current -= (int)Math.Round(input.ScrollDelta) * ScrollStep;
            }

            if (hasStick)
            {
                float mag = MathF.Abs(input.RightStick.Y);
                float normalized = MathHelper.Clamp(mag, 0f, 1f);
                float speedMultiplier = MathHelper.Clamp((float)Math.Pow(normalized, SpeedExponent) * MaxMultiplier, 0f, 10f);
                float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
                current += (int)Math.Round(-Math.Sign(input.RightStick.Y) * GamepadScrollSpeed * speedMultiplier * dt);
            }

            current = Math.Clamp(current, 0, maxScroll);
            if (useBuild) modal.BuildScrollOffset = current;
            else modal.ScrollOffset = current;
        }

        private void DrawHeader(CardListModal modal, OverlayLayout layout)
        {
            string title = string.IsNullOrWhiteSpace(modal.Title) ? "Cards" : modal.Title;
            DrawTextWithShadow(_titleFont, title, new Vector2(HeaderPaddingX, HeaderPaddingTop), White1, TitleScale);
            DrawLedger(layout.Ledger);

            if (ShouldShowReplacementInstruction(modal))
            {
                Vector2 pos = new(HeaderPaddingX, layout.Content.Y + 8);
                _spriteBatch.DrawString(_bodyFont, ReplacementInstruction, pos, White3, 0f, Vector2.Zero, InstructionScale, SpriteEffects.None, 0f);
            }
        }

        private void DrawLedger(Rectangle rect)
        {
            if (_titleFont == null || rect.Width <= 0) return;

            int x = rect.Right;
            DrawLedgerPip(ref x, rect.Center.Y, _ledgerSummary.Black, CardBlackBg, CardBlackStripe, LedgerCountScale);
            DrawLedgerPip(ref x, rect.Center.Y, _ledgerSummary.Red, CardRedBg, CardRedStripe, LedgerCountScale);
            DrawLedgerPip(ref x, rect.Center.Y, _ledgerSummary.White, CardWhiteBg, CardWhiteStripe, LedgerCountScale);
            x -= 20;
            DrawRightAlignedText(_titleFont, _ledgerSummary.Total.ToString(), ref x, rect.Center.Y, White1, TotalCountScale);
        }

        private void DrawLedgerPip(ref int rightX, int centerY, int count, Color fill, Color stripe, float scale)
        {
            DrawRightAlignedText(_titleFont, count.ToString(), ref rightX, centerY, White1, scale);
            rightX -= 8;
            var swatch = new Rectangle(rightX - 16, centerY - 10, 16, 20);
            _spriteBatch.Draw(_pixel, swatch, fill);
            DrawBorder(swatch, stripe, 2);
            rightX = swatch.X - 20;
        }

        private void DrawRightAlignedText(SpriteFont font, string text, ref int rightX, int centerY, Color color, float scale)
        {
            Vector2 size = font.MeasureString(text) * scale;
            Vector2 pos = new(rightX - size.X, centerY - size.Y / 2f);
            _spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            rightX = (int)Math.Round(pos.X);
        }

        private void DrawCardGrid(Rectangle clip)
        {
            if (_visibleCards.Count == 0 || clip.Width <= 0 || clip.Height <= 0) return;

            var prevScissor = _graphicsDevice.ScissorRectangle;
            _spriteBatch.End();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, _scissorRasterizer, null, Game1.Display.SpriteBatchTransform);
            _graphicsDevice.ScissorRectangle = Game1.Display.LogicalToRender(IntersectWithScreen(clip));

            foreach (VisibleCardPresentation presentation in _visibleCards)
            {
                EventManager.Publish(new CardRenderScaledEvent
                {
                    Card = presentation.Card,
                    Position = presentation.Position,
                    Scale = 1f,
                    ClipRect = clip,
                    PreferCachedBase = UseCachedCardBasesForDiagnostics,
                });
            }

            _spriteBatch.End();
            _graphicsDevice.ScissorRectangle = prevScissor;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Game1.Display.SpriteBatchTransform);
        }

        private Rectangle IntersectWithScreen(Rectangle rect)
        {
            var screen = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
            return Rectangle.Intersect(screen, rect);
        }

        private void DrawInventoryBuildPanel(CardListModal modal, OverlayLayout layout)
        {
            var prevScissor = _graphicsDevice.ScissorRectangle;
            _spriteBatch.End();
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, _scissorRasterizer, null, Game1.Display.SpriteBatchTransform);
            _graphicsDevice.ScissorRectangle = Game1.Display.LogicalToRender(IntersectWithScreen(layout.BuildClip));

            var player = GetPlayer();
            int y = layout.BuildClip.Y - modal.BuildScrollOffset;
            if (IsBuildSectionVisible(layout.BuildClip, y, 92)) DrawHealthSection(player, layout.BuildClip.X, ref y);
            else y += 92;
            DrawWeaponSection(player, layout.BuildClip.X, ref y, layout.BuildClip);
            if (IsBuildSectionVisible(layout.BuildClip, y, 156)) DrawTemperanceSection(player, layout.BuildClip.X, ref y);
            else y += 156;
            if (IsBuildSectionVisible(layout.BuildClip, y, 210)) DrawEquipmentSection(player, layout.BuildClip.X, ref y);
            else y += 210;
            DrawMedalsSectionIfVisible(player, layout.BuildClip.X, ref y, layout.BuildClip);
            if (y < layout.BuildClip.Bottom) DrawPassivesSection(player, layout.BuildClip.X, ref y);

            _spriteBatch.End();
            _graphicsDevice.ScissorRectangle = prevScissor;
            _spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Game1.Display.SpriteBatchTransform);
        }

        private void DrawHealthSection(Entity player, int x, ref int y)
        {
            DrawSectionHead("Health", x, y);
            y += 22;
            var hp = player?.GetComponent<HP>();
            int displayCurrent = hp == null ? 0 : (IsClimbScene() ? hp.Max : hp.Current);
            string current = displayCurrent.ToString();
            string max = (hp?.Max ?? 0).ToString();
            _spriteBatch.DrawString(_titleFont, current, new Vector2(x, y), White1, 0f, Vector2.Zero, 0.28f, SpriteEffects.None, 0f);
            Vector2 currentSize = _titleFont.MeasureString(current) * 0.28f;
            _spriteBatch.DrawString(_titleFont, "/", new Vector2(x + currentSize.X + 8, y + 10), White1 * 0.25f, 0f, Vector2.Zero, 0.16f, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(_titleFont, max, new Vector2(x + currentSize.X + 28, y + 10), White1 * 0.4f, 0f, Vector2.Zero, 0.17f, SpriteEffects.None, 0f);

            var track = new Rectangle(x + 122, y + 16, Math.Max(1, BuildPanelWidth - 142), 22);
            _spriteBatch.Draw(_pixel, track, Color.Black * 0.6f);
            DrawBorder(track, Red3, 2);
            float pct = hp == null || hp.Max <= 0 ? 0f : MathHelper.Clamp(displayCurrent / (float)hp.Max, 0f, 1f);
            _spriteBatch.Draw(_pixel, new Rectangle(track.X + 2, track.Y + 2, (int)Math.Round((track.Width - 4) * pct), track.Height - 4), Red3);
            y += 70;
        }

        private void DrawWeaponSection(Entity player, int x, ref int y, Rectangle clip)
        {
            Entity weapon = ResolveWeaponPreview(player);
            int sectionHeight = weapon != null ? 22 + GridCellH + 22 : 110;
            if (!IsBuildSectionVisible(clip, y, sectionHeight))
            {
                y += sectionHeight;
                return;
            }

            DrawSectionHead("Weapon", x, y);
            y += 22;
            if (weapon != null)
            {
                int cardW = GridCellW;
                int cardH = GridCellH;
                int left = x + Math.Max(0, (BuildPanelWidth - cardW) / 2);
                var position = new Vector2(left + cardW / 2f, y + cardH / 2f + CardOffsetYExtra);
                EventManager.Publish(new CardRenderScaledEvent
                {
                    Card = weapon,
                    Position = position,
                    Scale = 1f,
                    ClipRect = clip,
                    PreferCachedBase = UseCachedCardBasesForDiagnostics,
                });
                y += cardH + 22;
            }
            else
            {
                DrawEmptyBox(new Rectangle(x, y, BuildPanelWidth, 66), "No Weapon");
                y += 88;
            }
        }

        private void DrawTemperanceSection(Entity player, int x, ref int y)
        {
            DrawSectionHead("Temperance", x, y);
            y += 22;
            var abilityId = player?.GetComponent<EquippedTemperanceAbility>()?.AbilityId;
            var ability = string.IsNullOrWhiteSpace(abilityId) ? null : TemperanceFactory.Create(abilityId);
            var temp = player?.GetComponent<Temperance>();
            int threshold = Math.Max(1, ability?.Threshold ?? 1);
            int filled = Math.Clamp(temp?.Amount ?? 0, 0, threshold);
            var rect = new Rectangle(x, y, BuildPanelWidth, 112);
            _spriteBatch.Draw(_pixel, rect, Color.Black * 0.55f);
            DrawBorder(rect, Color.White * 0.15f, 1);
            _spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, 5, rect.Height), White1);
            string name = ability?.Name ?? "None";
            _spriteBatch.DrawString(_titleFont, name, new Vector2(rect.X + 16, rect.Y + 12), White1, 0f, Vector2.Zero, 0.13f, SpriteEffects.None, 0f);
            DrawTemperanceChunks(rect.Right - 72, rect.Y + 15, threshold, filled);
            DrawWrappedBody(ability?.Text ?? string.Empty, new Rectangle(rect.X + 16, rect.Y + 48, rect.Width - 32, rect.Height - 56), White1 * 0.5f);
            y += rect.Height + 22;
        }

        private void DrawEquipmentSection(Entity player, int x, ref int y)
        {
            DrawSectionHead("Equipment", x, y);
            y += 22;
            var bySlot = GetPlayerEquipment(player).ToDictionary(e => e.Equipment.Slot, e => e);
            int slotW = (BuildPanelWidth - 10) / 2;
            int slotH = 88;
            var slots = new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Arms, EquipmentSlot.Legs };
            for (int i = 0; i < slots.Length; i++)
            {
                int col = i % 2;
                int row = i / 2;
                var rect = new Rectangle(x + col * (slotW + 10), y + row * slotH, slotW, 78);
                DrawEquipmentSlot(slots[i], bySlot.TryGetValue(slots[i], out var equipment) ? equipment : null, rect);
            }
            y += slotH * 2 + 12;
        }

        private void DrawMedalsSection(Entity player, int x, ref int y)
        {
            var medals = GetPlayerMedals(player);
            DrawSectionHead("Medals", x, y);
            y += 22;
            if (medals.Count == 0)
            {
                DrawEmptyBox(new Rectangle(x, y, BuildPanelWidth, 48), "No Medals");
                y += 70;
                return;
            }

            const int icon = 56;
            const int gap = 10;
            int cols = Math.Max(1, (BuildPanelWidth + gap) / (icon + gap));
            for (int i = 0; i < medals.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                var medal = medals[i];
                Vector2 center = new(x + col * (icon + gap) + icon / 2f, y + row * (icon + gap) + icon / 2f);
                MedalIconRenderService.DrawMedalIcon(_spriteBatch, _graphicsDevice, _titleFont, center, icon, medal.Medal.Id, _imageAssets);
                if (medal.Medal.MaxCount > 0)
                {
                    string count = $"{medal.Medal.CurrentCount}/{medal.Medal.MaxCount}";
                    DrawBadge(new Rectangle((int)center.X + 8, (int)center.Y + 10, 30, 18), count);
                }
            }
            int rows = (medals.Count + cols - 1) / cols;
            y += rows * (icon + gap) + 14;
        }

        private void DrawMedalsSectionIfVisible(Entity player, int x, ref int y, Rectangle clip)
        {
            var medals = GetPlayerMedals(player);
            const int icon = 56;
            const int gap = 10;
            int cols = Math.Max(1, (BuildPanelWidth + gap) / (icon + gap));
            int contentHeight = medals.Count == 0
                ? 70
                : ((medals.Count + cols - 1) / cols) * (icon + gap) + 14;
            int sectionHeight = 22 + contentHeight;
            if (IsBuildSectionVisible(clip, y, sectionHeight)) DrawMedalsSection(player, x, ref y);
            else y += sectionHeight;
        }

        private static bool IsBuildSectionVisible(Rectangle clip, int y, int height)
        {
            return height > 0 && new Rectangle(clip.X, y, clip.Width, height).Intersects(clip);
        }

        private void DrawPassivesSection(Entity player, int x, ref int y)
        {
            var passives = player?.GetComponent<AppliedPassives>()?.Passives;
            if (passives == null || passives.Count == 0) return;

            DrawSectionHead("Passives", x, y);
            y += 22;
            int cursorX = x;
            int cursorY = y;
            int rowH = 0;
            foreach (var kv in passives.OrderBy(kv => kv.Key.ToString()))
            {
                string label = $"{kv.Value} {StringUtils.ToTitleCase(StringUtils.ToSentenceCase(kv.Key.ToString()))}";
                Vector2 size = _bodyFont.MeasureString(label) * PassiveTextScale;
                int w = (int)Math.Ceiling(size.X) + 24;
                int h = (int)Math.Ceiling(size.Y) + 10;
                if (cursorX > x && cursorX + w > x + BuildPanelWidth)
                {
                    cursorX = x;
                    cursorY += rowH + 10;
                    rowH = 0;
                }

                var rect = new Rectangle(cursorX, cursorY, w, h);
                var tex = PrimitiveTextureFactory.GetAntialiasedTrapezoidMask(_graphicsDevice, w, h, 0f, 2f, -23f, -2f, 11f);
                _spriteBatch.Draw(tex, rect, Color.Black);
                _spriteBatch.DrawString(_bodyFont, label, new Vector2(rect.X + 12, rect.Y + 5), White1, 0f, Vector2.Zero, PassiveTextScale, SpriteEffects.None, 0f);
                cursorX += w + 10;
                rowH = Math.Max(rowH, h);
            }
            y = cursorY + rowH + 18;
        }

        private void DrawSectionHead(string label, int x, int y)
        {
            _spriteBatch.DrawString(_bodyFont, label.ToUpperInvariant(), new Vector2(x, y), White1 * 0.35f, 0f, Vector2.Zero, SectionHeadScale, SpriteEffects.None, 0f);
            _spriteBatch.Draw(_pixel, new Rectangle(x, y + 16, BuildPanelWidth, 1), Color.White * 0.08f);
        }

        private void DrawEquipmentSlot(EquipmentSlot slot, EquippedEquipment equipment, Rectangle rect)
        {
            var iconRect = new Rectangle(rect.Center.X - 30, rect.Y, 60, 60);
            if (equipment?.Equipment != null)
            {
                var tex = EquipmentArtService.GetTexture(_imageAssets, equipment.Equipment);
                if (tex != null)
                {
                    _spriteBatch.Draw(tex, EquipmentArtService.GetContainedBounds(tex, iconRect), White1);
                }
            }
            else
            {
                _spriteBatch.Draw(_pixel, iconRect, Color.Black * 0.2f);
                DrawBorder(iconRect, Color.White * 0.18f, 1);
            }

            string label = slot.ToString().ToUpperInvariant();
            Vector2 size = _bodyFont.MeasureString(label) * SectionHeadScale;
            _spriteBatch.DrawString(_bodyFont, label, new Vector2(rect.Center.X - size.X / 2f, iconRect.Bottom + 4), equipment == null ? White1 * 0.22f : White1 * 0.35f, 0f, Vector2.Zero, SectionHeadScale, SpriteEffects.None, 0f);
        }

        private void DrawTemperanceChunks(int x, int y, int threshold, int filled)
        {
            for (int i = 0; i < threshold; i++)
            {
                var rect = new Rectangle(x + i * 14, y, 12, 16);
                var tex = PrimitiveTextureFactory.GetParallelogramMask(_graphicsDevice, rect.Width, rect.Height, 18f);
                _spriteBatch.Draw(tex, rect, i < filled ? White1 : White1 * 0.14f);
            }
        }

        private void DrawWrappedBody(string text, Rectangle bounds, Color color)
        {
            if (string.IsNullOrWhiteSpace(text) || _bodyFont == null) return;
            float y = bounds.Y;
            foreach (string line in TextUtils.WrapText(_bodyFont, text, BodyTextScale, bounds.Width))
            {
                if (y > bounds.Bottom) break;
                _spriteBatch.DrawString(_bodyFont, line, new Vector2(bounds.X, y), color, 0f, Vector2.Zero, BodyTextScale, SpriteEffects.None, 0f);
                y += _bodyFont.LineSpacing * BodyTextScale;
            }
        }

        private void DrawEmptyBox(Rectangle rect, string text)
        {
            _spriteBatch.Draw(_pixel, rect, Color.Black * 0.2f);
            DrawBorder(rect, Color.White * 0.18f, 1);
            Vector2 size = _bodyFont.MeasureString(text) * BodyTextScale;
            _spriteBatch.DrawString(_bodyFont, text, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), White1 * 0.35f, 0f, Vector2.Zero, BodyTextScale, SpriteEffects.None, 0f);
        }

        private void DrawBadge(Rectangle rect, string text)
        {
            _spriteBatch.Draw(_pixel, rect, Red3);
            DrawBorder(rect, White1, 1);
            Vector2 size = _bodyFont.MeasureString(text) * 0.07f;
            _spriteBatch.DrawString(_bodyFont, text, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), White1, 0f, Vector2.Zero, 0.07f, SpriteEffects.None, 0f);
        }

        private void DrawCloseButton(Rectangle rect)
        {
            var closeEntity = EntityManager.GetEntity(CloseEntityName);
            bool hovered = closeEntity?.GetComponent<UIElement>()?.IsHovered == true;
            _spriteBatch.Draw(_pixel, rect, hovered ? new Color(160, 0, 0) : Color.Black * 0.55f);
            DrawBorder(rect, hovered ? Red3 : White1 * 0.85f, 2);
            string label = "Close";
            Vector2 size = _bodyFont.MeasureString(label) * CloseTextScale;
            _spriteBatch.DrawString(_bodyFont, label, new Vector2(rect.Center.X - size.X / 2f, rect.Center.Y - size.Y / 2f), White1, 0f, Vector2.Zero, CloseTextScale, SpriteEffects.None, 0f);
        }

        private void DrawTextWithShadow(SpriteFont font, string text, Vector2 pos, Color color, float scale)
        {
            if (font == null || string.IsNullOrEmpty(text)) return;
            _spriteBatch.DrawString(font, text, pos + new Vector2(0, 2), Color.Black * 0.9f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            _spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }


        private void DrawRoundedFilledBordered(Rectangle bounds, int radius, int border, Color fill, Color borderColor)
        {
            var outer = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, Math.Max(1, bounds.Width), Math.Max(1, bounds.Height), radius);
            _spriteBatch.Draw(outer, bounds, borderColor);
            var inner = Inset(bounds, border);
            if (inner.Width <= 0 || inner.Height <= 0) return;
            var innerTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, inner.Width, inner.Height, Math.Max(0, radius - border));
            _spriteBatch.Draw(innerTex, inner, fill);
        }

        private void DrawBorder(Rectangle r, Color color, int thickness)
        {
            if (r.Width <= 0 || r.Height <= 0 || thickness <= 0) return;
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, r.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Bottom - thickness, r.Width, thickness), color);
            _spriteBatch.Draw(_pixel, new Rectangle(r.X, r.Y, thickness, r.Height), color);
            _spriteBatch.Draw(_pixel, new Rectangle(r.Right - thickness, r.Y, thickness, r.Height), color);
        }

        private static Rectangle Inset(Rectangle r, int amount)
        {
            return new Rectangle(r.X + amount, r.Y + amount, r.Width - amount * 2, r.Height - amount * 2);
        }

        private void OpenModal(OpenCardListModalEvent evt)
        {
            RestoreModalHoverHighlightState();
            ReleaseTrackedModalCards();
            CleanupOverlayTooltipEntities();
            CleanupInventoryAuxCards();

            var entity = EntityManager.GetEntitiesWithComponent<CardListModal>().FirstOrDefault();
            if (entity == null)
            {
                entity = EntityManager.CreateEntity("CardListModal");
                EntityManager.AddComponent(entity, new CardListModal());
            }

            var modal = entity.GetComponent<CardListModal>();
            modal.Title = evt.Title;
            modal.Cards = evt.Cards ?? new List<Entity>();
            modal.IsOpen = true;
            modal.ScrollOffset = 0;
            modal.BuildScrollOffset = 0;
            modal.IsSelectable = evt.IsSelectable;
            modal.SelectionContext = evt.SelectionContext ?? string.Empty;
            modal.SelectedCardIndex = -1;
            modal.Mode = evt.Mode;
            RebuildPresentationCache(modal);

            if (evt.Mode == CardListModalMode.Inventory)
            {
                EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.OpenInventory, Volume = 0.5f });
            }

            EnsureModalRoot(entity, modal);
            foreach (Entity card in modal.Cards)
            {
                TrackModalCard(card);
                EnsureCardTooltip(card);
            }
        }

        public void OpenForSnapshot(string title, List<Entity> cards, bool isSelectable = false, string selectionContext = "")
        {
            OpenModal(new OpenCardListModalEvent
            {
                Title = title,
                Cards = cards ?? new List<Entity>(),
                IsSelectable = isSelectable,
                SelectionContext = selectionContext ?? string.Empty,
                Mode = CardListModalMode.CardList,
            });
        }

        public void OpenInventoryForSnapshot(string title, List<Entity> cards)
        {
            OpenModal(new OpenCardListModalEvent
            {
                Title = title,
                Cards = cards ?? new List<Entity>(),
                Mode = CardListModalMode.Inventory,
            });
        }

        private void CloseModal()
        {
            var entity = EntityManager.GetEntitiesWithComponent<CardListModal>().FirstOrDefault();
            var modal = entity?.GetComponent<CardListModal>();
            if (modal == null) return;

            bool shouldCancelClimbReplacement =
                modal.IsOpen
                && modal.IsSelectable
                && string.Equals(modal.SelectionContext, CardListSelectionContexts.ClimbReplacement, StringComparison.OrdinalIgnoreCase);

            bool wasInventory = modal.Mode == CardListModalMode.Inventory;

            modal.IsOpen = false;
            modal.SelectedCardIndex = -1;
            ReleaseTrackedModalCards();

            RestoreModalHoverHighlightState();
            modal.IsSelectable = false;
            modal.SelectionContext = string.Empty;
            modal.Mode = CardListModalMode.Auto;
            SetCloseButtonActive(false);
            CleanupOverlayTooltipEntities();
            CleanupInventoryAuxCards();
            ClearPresentationCache();

            if (shouldCancelClimbReplacement)
            {
                ClimbShopService.CancelReplacementOffer();
            }

            if (wasInventory)
            {
                EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.CloseInventory, Volume = 0.5f });
            }
        }

        private void EnsureModalRoot(Entity entity, CardListModal modal)
        {
            if (entity.GetComponent<UIElement>() != null)
            {
                EntityManager.RemoveComponent<UIElement>(entity);
            }

            InputContextService.EnsureContext(EntityManager, entity, ContextId, 740, modal?.IsOpen == true);
        }

        private void EnsureCloseButton(Rectangle bounds)
        {
            Entity closeButton = EntityManager.GetEntity(CloseEntityName);
            if (closeButton == null)
            {
                closeButton = EntityManager.CreateEntity(CloseEntityName);
                EntityManager.AddComponent(closeButton, new Transform { Position = new Vector2(bounds.X, bounds.Y), ZOrder = 20000 });
                EntityManager.AddComponent(closeButton, new UIElement
                {
                    LayerType = UILayerType.Overlay,
                    EventType = UIElementEventType.CardListModalClose,
                });
                EntityManager.AddComponent(closeButton, new HotKey { Button = FaceButton.B });
                EntityManager.AddComponent(closeButton, new CardListModalClose());
            }

            InputContextService.EnsureMember(EntityManager, closeButton, ContextId);
            var t = closeButton.GetComponent<Transform>();
            t.Position = new Vector2(bounds.X, bounds.Y);
            t.ZOrder = 20000;
            var ui = closeButton.GetComponent<UIElement>();
            ui.Bounds = bounds;
            ui.IsInteractable = true;
            ui.IsHidden = false;
            ui.LayerType = UILayerType.Overlay;
            ui.Tooltip = string.Empty;
            ui.TooltipType = TooltipType.None;
        }

        private void SetCloseButtonActive(bool active)
        {
            var close = EntityManager.GetEntity(CloseEntityName);
            var ui = close?.GetComponent<UIElement>();
            if (ui == null) return;
            ui.IsInteractable = active;
            ui.IsHidden = !active;
            if (!active) ui.Bounds = Rectangle.Empty;
        }

        private void LayoutCards(CardListModal modal, OverlayLayout layout)
        {
            var cards = GetOrderedCards(modal);
            _visibleCards.Clear();
            int cols = CalculateColumns(layout.DeckClip.Width);
            int offsetY = CardOffsetYExtra;
            int overscan = GridCellH + GridGap;
            (int firstRow, int lastRow) = CalculateOverscanRowRange(
                cards.Count,
                cols,
                GridCellH + GridGap,
                modal.ScrollOffset,
                layout.DeckClip.Height);
            var overscanClip = new Rectangle(
                layout.DeckClip.X,
                layout.DeckClip.Y - overscan,
                layout.DeckClip.Width,
                layout.DeckClip.Height + overscan * 2);
            for (int i = 0; i < cards.Count; i++)
            {
                int col = i % cols;
                int row = i / cols;
                var cell = new Rectangle(
                    layout.DeckClip.X + col * (GridCellW + GridGap),
                    layout.DeckClip.Y + row * (GridCellH + GridGap) - modal.ScrollOffset,
                    GridCellW,
                    GridCellH);
                var eventPosition = new Vector2(cell.X + cell.Width / 2f, cell.Y + cell.Height / 2f + offsetY);
                var visual = CardGeometryService.GetVisualRect(GetSettings(), eventPosition, 1f);
                var card = cards[i];
                Rectangle renderBounds = Rectangle.Empty;
                bool shouldRender = row >= firstRow && row <= lastRow;
                if (shouldRender)
                {
                    renderBounds = CardRenderBoundsService.GetBounds(EntityManager, card, eventPosition, 1f, 0f);
                    shouldRender = renderBounds.Intersects(overscanClip);
                }
                bool interactable = visual.Intersects(layout.DeckClip);
                if (shouldRender)
                {
                    _visibleCards.Add(new VisibleCardPresentation(card, i, eventPosition, renderBounds));
                }

                var transform = card.GetComponent<Transform>();
                if (transform != null)
                {
                    transform.Position = eventPosition;
                    transform.ZOrder = 15000;
                    transform.Rotation = 0f;
                }

                var ui = card.GetComponent<UIElement>();
                if (ui == null) continue;
                ui.LayerType = UILayerType.Overlay;
                ui.IsInteractable = modal.IsSelectable && interactable;
                ui.IsHidden = !interactable;
                ui.Bounds = interactable ? visual : Rectangle.Empty;
                ui.TooltipPosition = TooltipPosition.Right;
                ApplyModalHoverHighlightState(card, ui, modal);
                TrackModalCard(card);
                EnsureCardTooltip(card);
            }
        }

        private void UpdateInventoryTooltipEntities(CardListModal modal, CardListModalMode mode, OverlayLayout layout)
        {
            if (mode != CardListModalMode.Inventory)
            {
                CleanupOverlayTooltipEntities();
                CleanupInventoryAuxCards();
                return;
            }

            var liveKeys = new HashSet<string>();
            var player = GetPlayer();
            foreach (InventoryHitRegion region in MeasureInventoryHitRegions(player, modal, layout.BuildClip))
            {
                liveKeys.Add(region.Key);
                if (region.Key == "weapon")
                {
                    Entity weapon = ResolveWeaponPreview(player);
                    if (weapon == null) continue;

                    var ui = weapon.GetComponent<UIElement>();
                    if (ui != null)
                    {
                        ui.LayerType = UILayerType.Overlay;
                        ui.TooltipPosition = TooltipPosition.Right;
                        ui.Bounds = region.Bounds.Intersects(layout.BuildClip) ? region.Bounds : Rectangle.Empty;
                        ui.IsInteractable = false;
                        ui.IsHidden = ui.Bounds == Rectangle.Empty;
                    }

                    TrackModalCard(weapon);
                    _inventoryAuxCardIds.Add(weapon.Id);
                    EnsureCardTooltip(weapon);
                    continue;
                }

                var entity = EnsureTooltipEntity(region.Key, region.Bounds, layout.BuildClip, region.Type, region.Tooltip, region.TooltipKeywordSource);
                SyncEquipmentTooltipSource(entity, region.EquipmentEntity);
            }

            CleanupStaleTooltipEntities(liveKeys);
        }

        private IEnumerable<InventoryHitRegion> MeasureInventoryHitRegions(Entity player, CardListModal modal, Rectangle buildClip)
        {
            int x = buildClip.X;
            int y = buildClip.Y - modal.BuildScrollOffset;

            // Health
            y += 22 + 70;

            // Weapon
            y += 22;
            Entity weapon = ResolveWeaponPreview(player);
            if (weapon != null)
            {
                int left = x + Math.Max(0, (BuildPanelWidth - GridCellW) / 2);
                var position = new Vector2(left + GridCellW / 2f, y + GridCellH / 2f + CardOffsetYExtra);
                var visual = CardGeometryService.GetVisualRect(GetSettings(), position, 1f);
                yield return new InventoryHitRegion("weapon", visual, TooltipType.Card, string.Empty, string.Empty, null);
                y += GridCellH + 22;
            }
            else
            {
                y += 88;
            }

            // Temperance
            y += 22;
            var temperanceRect = new Rectangle(x, y, BuildPanelWidth, 112);
            yield return new InventoryHitRegion("temperance", temperanceRect, TooltipType.Text, string.Empty, BuildTemperanceTooltipKeywordSource(player), null);
            y += 112 + 22;

            // Equipment
            y += 22;
            var equipmentBySlot = GetPlayerEquipment(player).ToDictionary(e => e.Equipment.Slot, e => e);
            int slotW = (BuildPanelWidth - 10) / 2;
            int slotH = 88;
            var slots = new[] { EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Arms, EquipmentSlot.Legs };
            for (int i = 0; i < slots.Length; i++)
            {
                if (!equipmentBySlot.TryGetValue(slots[i], out var equipment)) continue;
                int col = i % 2;
                int row = i / 2;
                var rect = new Rectangle(x + col * (slotW + 10), y + row * slotH, slotW, 78);
                yield return new InventoryHitRegion(
                    "equipment_" + slots[i],
                    rect,
                    TooltipType.Equipment,
                    string.Empty,
                    string.Empty,
                    ResolveEquipmentEntity(equipment));
            }
            y += slotH * 2 + 12;

            // Medals
            y += 22;
            var medals = GetPlayerMedals(player);
            if (medals.Count == 0)
            {
                y += 70;
            }
            else
            {
                const int icon = 56;
                const int gap = 10;
                int cols = Math.Max(1, (BuildPanelWidth + gap) / (icon + gap));
                for (int i = 0; i < medals.Count; i++)
                {
                    int col = i % cols;
                    int row = i / cols;
                    var rect = new Rectangle(x + col * (icon + gap), y + row * (icon + gap), icon, icon);
                    yield return new InventoryHitRegion("medal_" + i, rect, TooltipType.Text, BuildMedalTooltip(medals[i]), string.Empty, null);
                }

                int rows = (medals.Count + cols - 1) / cols;
                y += rows * (icon + gap) + 14;
            }

            // Passives
            var passives = player?.GetComponent<AppliedPassives>()?.Passives;
            if (passives == null || passives.Count == 0) yield break;

            y += 22;
            int cursorX = x;
            int cursorY = y;
            int rowH = 0;
            foreach (var kv in passives.OrderBy(kv => kv.Key.ToString()))
            {
                string label = $"{kv.Value} {StringUtils.ToTitleCase(StringUtils.ToSentenceCase(kv.Key.ToString()))}";
                Vector2 size = _bodyFont.MeasureString(label) * PassiveTextScale;
                int chipW = (int)Math.Ceiling(size.X) + 24;
                int chipH = (int)Math.Ceiling(size.Y) + 10;
                if (cursorX > x && cursorX + chipW > x + BuildPanelWidth)
                {
                    cursorX = x;
                    cursorY += rowH + 10;
                    rowH = 0;
                }

                var rect = new Rectangle(cursorX, cursorY, chipW, chipH);
                yield return new InventoryHitRegion(
                    "passive_" + kv.Key,
                    rect,
                    TooltipType.Text,
                    TooltipTextService.GetPassiveText(kv.Key, isPlayer: true, kv.Value),
                    string.Empty,
                    null);
                cursorX += chipW + 10;
                rowH = Math.Max(rowH, chipH);
            }
        }

        private void CleanupInventoryAuxCards()
        {
            foreach (int id in _inventoryAuxCardIds.ToList())
            {
                var card = EntityManager.GetEntity(id);
                if (card == null) continue;
                InputContextService.RemoveMember(EntityManager, card, ContextId);
                var ui = card.GetComponent<UIElement>();
                if (ui != null)
                {
                    ui.Bounds = Rectangle.Empty;
                    ui.IsHovered = false;
                    ui.IsClicked = false;
                    ui.IsInteractable = false;
                    ui.IsHidden = false;
                    ui.LayerType = UILayerType.Default;
                }
            }
            _inventoryAuxCardIds.Clear();
        }

        private void TrackModalCard(Entity card)
        {
            if (card == null) return;
            _modalCardIds.Add(card.Id);
            InputContextService.EnsureMember(EntityManager, card, ContextId);
        }

        private void ReleaseTrackedModalCards()
        {
            foreach (int id in _modalCardIds.ToList())
            {
                ReleaseCardFromModal(EntityManager, EntityManager.GetEntity(id));
            }
            _modalCardIds.Clear();
        }

        internal static void ReleaseCardFromModal(EntityManager entityManager, Entity card)
        {
            if (entityManager == null || card == null) return;

            InputContextService.RemoveMember(entityManager, card, ContextId);
            var ui = card.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.LayerType = UILayerType.Default;
                ui.IsInteractable = false;
                ui.IsHovered = false;
                ui.IsClicked = false;
                ui.IsHidden = false;
                ui.Bounds = Rectangle.Empty;
            }

            if (card.GetComponent<CardListModalSelectionMetadata>() != null)
            {
                entityManager.RemoveComponent<CardListModalSelectionMetadata>(card);
            }
        }

        private void OnDeleteCaches(DeleteCachesEvent _)
        {
            var modal = EntityManager.GetEntitiesWithComponent<CardListModal>()
                .FirstOrDefault()
                ?.GetComponent<CardListModal>();
            if (modal != null)
            {
                modal.IsOpen = false;
                modal.SelectedCardIndex = -1;
                modal.IsSelectable = false;
                modal.SelectionContext = string.Empty;
                modal.Mode = CardListModalMode.Auto;
            }

            ReleaseTrackedModalCards();
            CleanupInventoryAuxCards();
            CleanupOverlayTooltipEntities();
            RestoreModalHoverHighlightState();
            SetCloseButtonActive(false);
            ClearPresentationCache();
        }

        private Entity EnsureTooltipEntity(string key, Rectangle bounds, Rectangle buildClip, TooltipType type, string tooltip, string tooltipKeywordSource)
        {
            string name = TooltipEntityPrefix + key;
            var entity = EntityManager.GetEntity(name);
            if (entity == null)
            {
                entity = EntityManager.CreateEntity(name);
                EntityManager.AddComponent(entity, new Transform { ZOrder = 19000 });
                EntityManager.AddComponent(entity, new UIElement());
            }

            InputContextService.EnsureMember(EntityManager, entity, ContextId);
            var transform = entity.GetComponent<Transform>();
            transform.ZOrder = 19000;
            var ui = entity.GetComponent<UIElement>();
            ui.Bounds = bounds;
            ui.IsInteractable = false;
            ui.IsHidden = bounds.Width <= 0 || bounds.Height <= 0 || !bounds.Intersects(buildClip);
            ui.LayerType = UILayerType.Overlay;
            ui.TooltipType = type;
            ui.Tooltip = tooltip ?? string.Empty;
            ui.TooltipKeywordSource = tooltipKeywordSource ?? string.Empty;
            ui.TooltipPosition = TooltipPosition.Right;
            ui.TooltipOffsetPx = 12;
            return entity;
        }

        private void SyncEquipmentTooltipSource(Entity entity, Entity equipmentEntity)
        {
            var source = entity.GetComponent<EquipmentTooltipSource>();
            if (equipmentEntity == null)
            {
                if (source != null) EntityManager.RemoveComponent<EquipmentTooltipSource>(entity);
                return;
            }

            if (source == null)
            {
                EntityManager.AddComponent(entity, new EquipmentTooltipSource
                {
                    EquipmentEntity = equipmentEntity,
                    TooltipEntityName = EquipmentTooltipEntityName,
                });
                return;
            }

            source.EquipmentEntity = equipmentEntity;
            source.TooltipEntityName = EquipmentTooltipEntityName;
        }

        private void CleanupStaleTooltipEntities(HashSet<string> liveKeys)
        {
            foreach (var entity in EntityManager.GetAllEntities()
                .Where(entity => entity.Name != null && entity.Name.StartsWith(TooltipEntityPrefix, StringComparison.Ordinal))
                .ToList())
            {
                string key = entity.Name.Substring(TooltipEntityPrefix.Length);
                if (liveKeys.Contains(key)) continue;
                EntityManager.DestroyEntity(entity.Id);
            }
        }

        private void CleanupOverlayTooltipEntities()
        {
            foreach (var entity in EntityManager.GetAllEntities()
                .Where(entity => entity.Name != null && entity.Name.StartsWith(TooltipEntityPrefix, StringComparison.Ordinal))
                .ToList())
            {
                EntityManager.DestroyEntity(entity.Id);
            }
        }

        private List<Entity> GetOrderedCards(CardListModal modal)
        {
            if (ReferenceEquals(modal, _presentationModal)) return _orderedCards;
            return OrderCardsForPresentation(modal?.Cards);
        }

        internal static List<Entity> OrderCardsForPresentation(IEnumerable<Entity> cards)
        {
            return (cards ?? Enumerable.Empty<Entity>())
                .Where(e => e != null && e.GetComponent<CardData>() != null)
                .OrderBy(e => GetCardSortName(e), StringComparer.OrdinalIgnoreCase)
                .ThenBy(e => GetColorSortRank(e.GetComponent<CardData>().Color))
                .ThenBy(e => e.Id)
                .ToList();
        }

        internal static (int FirstRow, int LastRow) CalculateOverscanRowRange(
            int cardCount,
            int columns,
            int rowStride,
            int scrollOffset,
            int viewportHeight,
            int overscanRows = 1)
        {
            if (cardCount <= 0 || columns <= 0 || rowStride <= 0 || viewportHeight <= 0) return (0, -1);
            int rowCount = (cardCount + columns - 1) / columns;
            int safeOverscan = Math.Max(0, overscanRows);
            int first = Math.Max(0, scrollOffset / rowStride - safeOverscan);
            int last = Math.Min(
                rowCount - 1,
                (Math.Max(0, scrollOffset) + viewportHeight - 1) / rowStride + safeOverscan);
            return (first, last);
        }

        private void RebuildPresentationCache(CardListModal modal)
        {
            _presentationModal = modal;
            _orderedCards.Clear();
            _orderedCards.AddRange(OrderCardsForPresentation(modal?.Cards));
            _ledgerSummary = new CardLedgerSummary(
                _orderedCards.Count,
				_orderedCards.Count(card => CardColorQualificationService.QualifiesAs(card, CardData.CardColor.White)),
				_orderedCards.Count(card => CardColorQualificationService.QualifiesAs(card, CardData.CardColor.Red)),
				_orderedCards.Count(card => CardColorQualificationService.QualifiesAs(card, CardData.CardColor.Black)));
            _visibleCards.Clear();
        }

        private void ClearPresentationCache()
        {
            _presentationModal = null;
            _orderedCards.Clear();
            _visibleCards.Clear();
            _ledgerSummary = default;
        }

        public void SetScrollOffsetForDiagnostics(int scrollOffset)
        {
            var modal = GetRelevantEntities().FirstOrDefault()?.GetComponent<CardListModal>();
            if (modal?.IsOpen != true) return;
            OverlayLayout layout = ComputeLayout(ResolveMode(modal), modal);
            int maxScroll = Math.Max(0, CalculateCardContentHeight(modal, layout.DeckClip.Width) - layout.DeckClip.Height);
            modal.ScrollOffset = Math.Clamp(scrollOffset, 0, maxScroll);
        }

        public void SetScrollFractionForDiagnostics(float fraction)
        {
            var modal = GetRelevantEntities().FirstOrDefault()?.GetComponent<CardListModal>();
            if (modal?.IsOpen != true) return;
            OverlayLayout layout = ComputeLayout(ResolveMode(modal), modal);
            int maxScroll = Math.Max(0, CalculateCardContentHeight(modal, layout.DeckClip.Width) - layout.DeckClip.Height);
            modal.ScrollOffset = (int)Math.Round(maxScroll * MathHelper.Clamp(fraction, 0f, 1f));
        }

        public int VisibleCardCountForDiagnostics => _visibleCards.Count;

        private static string GetCardSortName(Entity card)
        {
            var data = card.GetComponent<CardData>();
            return data?.Card?.Name ?? data?.Card?.CardId ?? card.Name ?? string.Empty;
        }

        private static int GetColorSortRank(CardData.CardColor color)
        {
            return color switch
            {
                CardData.CardColor.Black => 0,
                CardData.CardColor.Red => 1,
                _ => 2,
            };
        }

        private void TryPublishSelection(CardListModal modal)
        {
            if (modal == null || !modal.IsOpen || !modal.IsSelectable) return;
            var cards = GetOrderedCards(modal);
            for (int i = 0; i < cards.Count; i++)
            {
                var ui = cards[i].GetComponent<UIElement>();
                if (ui?.IsClicked != true) continue;
                ui.IsClicked = false;
                modal.SelectedCardIndex = i;
                EventManager.Publish(new CardListModalCardSelectedEvent
                {
                    Card = cards[i],
                    CardIndex = i,
                    SelectionContext = modal.SelectionContext ?? string.Empty,
                });
                CloseModal();
                return;
            }
        }

        public bool IsSelectableOpen()
        {
            return EntityManager
                .GetEntitiesWithComponent<CardListModal>()
                .FirstOrDefault()
                ?.GetComponent<CardListModal>()
                is { IsOpen: true, IsSelectable: true };
        }

        private void ApplyModalHoverHighlightState(Entity card, UIElement ui, CardListModal modal)
        {
            if (card == null || ui == null || !ShouldForceModalHoverHighlight(modal)) return;

            if (!_previousCardHoverHighlight.ContainsKey(card.Id))
            {
                _previousCardHoverHighlight[card.Id] = ui.ShowHoverHighlight;
            }

            ui.ShowHoverHighlight = true;
        }

        private static bool ShouldForceModalHoverHighlight(CardListModal modal)
        {
            return modal?.IsOpen == true
                && modal.IsSelectable
                && string.Equals(modal.SelectionContext, CardListSelectionContexts.ClimbReplacement, StringComparison.OrdinalIgnoreCase);
        }

        private void RestoreModalHoverHighlightState()
        {
            foreach (var entry in _previousCardHoverHighlight)
            {
                var card = EntityManager.GetEntity(entry.Key);
                var ui = card?.GetComponent<UIElement>();
                if (ui != null)
                {
                    ui.ShowHoverHighlight = entry.Value;
                }
            }

            _previousCardHoverHighlight.Clear();
        }

        private void OnCardSelected(CardListModalCardSelectedEvent evt)
        {
            if (!string.Equals(evt?.SelectionContext, CardListSelectionContexts.ClimbReplacement, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            TryFinalizeClimbReplacementSelection(evt.Card, EntityManager);
        }

        internal static bool TryFinalizeClimbReplacementSelection(Entity card, EntityManager entityManager)
        {
            var metadata = card?.GetComponent<CardListModalSelectionMetadata>();
            if (metadata == null
                || !string.Equals(metadata.SelectionContext, CardListSelectionContexts.ClimbReplacement, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (ClimbShopService.TryFinalizeReplacement(entityManager, metadata.EntryId))
            {
                RunDeckService.EnsureRunDeck(entityManager);
                return true;
            }

            return false;
        }

        private bool ShouldShowReplacementInstruction(CardListModal modal)
        {
            return modal?.IsSelectable == true
                && string.Equals(modal.SelectionContext, CardListSelectionContexts.ClimbReplacement, StringComparison.OrdinalIgnoreCase);
        }

        private int CalculateCardContentHeight(CardListModal modal, int width)
        {
            int count = GetOrderedCards(modal).Count;
            if (count == 0) return 0;
            int cols = CalculateColumns(width);
            int rows = (count + cols - 1) / cols;
            return rows * (GridCellH + GridGap) - GridGap;
        }

        private int CalculateBuildContentHeight()
        {
            var player = GetPlayer();
            int height = 70 + GridCellH + 44 + 134 + 198;
            var medals = GetPlayerMedals(player);
            const int icon = 56;
            const int gap = 10;
            int cols = Math.Max(1, (BuildPanelWidth + gap) / (icon + gap));
            int medalRows = medals.Count == 0 ? 1 : (medals.Count + cols - 1) / cols;
            height += medals.Count == 0 ? 70 : medalRows * (icon + gap) + 36;
            var passives = player?.GetComponent<AppliedPassives>()?.Passives;
            if (passives != null && passives.Count > 0)
            {
                height += 70;
            }
            return height;
        }

        private int CalculateColumns(int width)
        {
            return Math.Max(1, (width + GridGap) / (GridCellW + GridGap));
        }

        private int GridCellW => GetSettings()?.CardWidth ?? CardGeometrySettings.DefaultWidth;
        private int GridCellH => GetSettings()?.CardHeight ?? CardGeometrySettings.DefaultHeight;
        private int CardOffsetYExtra => GetSettings()?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra;
        private CardGeometrySettings GetSettings() => CardGeometryService.GetSettings(EntityManager);

        private Entity GetPlayer()
        {
            return EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
        }

        private List<EquippedEquipment> GetPlayerEquipment(Entity player)
        {
            if (player == null) return new List<EquippedEquipment>();
            return EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
                .Select(entity => entity.GetComponent<EquippedEquipment>())
                .Where(equipment => equipment?.EquippedOwner == player && equipment.Equipment != null)
                .OrderBy(equipment => equipment.Equipment.Slot)
                .ToList();
        }

        private Entity ResolveEquipmentEntity(EquippedEquipment equipment)
        {
            if (equipment?.Owner != null) return equipment.Owner;
            return EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
                .FirstOrDefault(entity => ReferenceEquals(entity.GetComponent<EquippedEquipment>(), equipment));
        }

        private List<EquippedMedal> GetPlayerMedals(Entity player)
        {
            if (player == null) return new List<EquippedMedal>();
            return EntityManager.GetEntitiesWithComponent<EquippedMedal>()
                .Select(entity => entity.GetComponent<EquippedMedal>())
                .Where(medal => medal?.EquippedOwner == player && medal.Medal != null)
                .OrderBy(medal => medal.Medal.Name)
                .ToList();
        }

        private Entity ResolveWeaponPreview(Entity player)
        {
            var equipped = player?.GetComponent<EquippedWeapon>();
            if (equipped?.SpawnedEntity != null) return equipped.SpawnedEntity;
            string weaponId = !string.IsNullOrWhiteSpace(equipped?.WeaponId) ? equipped.WeaponId : "sword";
            if (_weaponPreviewCard != null && string.Equals(_weaponPreviewId, weaponId, StringComparison.OrdinalIgnoreCase))
            {
                return _weaponPreviewCard;
            }

            if (_weaponPreviewCard != null)
            {
                EntityManager.DestroyEntity(_weaponPreviewCard.Id);
                _weaponPreviewCard = null;
            }

            _weaponPreviewId = weaponId;
            _weaponPreviewCard = EntityFactory.CreateCardFromDefinition(EntityManager, weaponId, CardData.CardColor.White, allowWeapons: true);
            if (_weaponPreviewCard != null)
            {
                _weaponPreviewCard.Name = WeaponPreviewEntityName;
                EnsureCardTooltip(_weaponPreviewCard);
            }
            return _weaponPreviewCard;
        }

        private void EnsureCardTooltip(Entity card)
        {
            CursedManagementSystem.RefreshCardTooltipPresentation(
                EntityManager,
                card,
                TooltipPosition.Right);
        }

        private string BuildMedalTooltip(EquippedMedal medal)
        {
            if (medal?.Medal == null) return string.Empty;
            return $"{medal.Medal.Name}\n\n{medal.Medal.Text}";
        }

        private string BuildTemperanceTooltipKeywordSource(Entity player)
        {
            string abilityId = player?.GetComponent<EquippedTemperanceAbility>()?.AbilityId;
            var ability = string.IsNullOrWhiteSpace(abilityId) ? null : TemperanceFactory.Create(abilityId);
            return ability?.Text ?? string.Empty;
        }

        private static Color GetCardColor(CardData.CardColor color)
        {
            return color switch
            {
                CardData.CardColor.Black => CardBlackStripe,
                CardData.CardColor.Red => CardRedStripe,
                _ => CardWhiteStripe,
            };
        }

        private readonly record struct OverlayLayout(
            Rectangle Header,
            Rectangle Content,
            Rectangle CloseButton,
            Rectangle Ledger,
            Rectangle DeckClip,
            Rectangle BuildClip,
            int InstructionHeight);

        private readonly record struct InventoryHitRegion(
            string Key,
            Rectangle Bounds,
            TooltipType Type,
            string Tooltip,
            string TooltipKeywordSource,
            Entity EquipmentEntity);

        private readonly record struct VisibleCardPresentation(
            Entity Card,
            int OrderedIndex,
            Vector2 Position,
            Rectangle RenderBounds);

        private readonly record struct CardLedgerSummary(int Total, int White, int Red, int Black);
    }
}
