using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Data.Tutorials;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Utils;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.ECS.Data.Locations;

namespace ChurchSuffering.ECS.Systems
{
    [DebugTab("Tutorial")]
    public class TutorialDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly ImageAssetService _imageAssets;
        private readonly SpriteFont _font;
        private readonly TutorialManager _tutorialManager;
        private readonly TutorialOverlay _overlay;

        private Texture2D _pixel;
        private Texture2D _bubbleTexture;
        private Texture2D _angelTexture;
        private int _bubbleTexW, _bubbleTexH;

        private bool _isActive = false;
        private TutorialDefinition _currentTutorial = null;
        private List<Rectangle> _targetBounds = new List<Rectangle>();
        private string _wrappedText = string.Empty;
        private Rectangle _bubbleRect;

        private const string ContinueEntityName = "TutorialContinueButton";
        private const string TutorialContextEntityName = "TutorialInputContext";

        // Overlay settings
        [DebugEditable(DisplayName = "Overlay Alpha (0-255)", Step = 5, Min = 0, Max = 255)]
        public int OverlayAlpha { get; set; } = 180;

        [DebugEditable(DisplayName = "Cutout Padding", Step = 2, Min = 0, Max = 50)]
        public int CutoutPadding { get; set; } = 8;

        // Bubble settings
        [DebugEditable(DisplayName = "Bubble Padding X", Step = 1, Min = 0, Max = 50)]
        public int BubblePadX { get; set; } = 12;

        [DebugEditable(DisplayName = "Bubble Padding Y", Step = 1, Min = 0, Max = 50)]
        public int BubblePadY { get; set; } = 10;

        [DebugEditable(DisplayName = "Bubble Corner Radius", Step = 1, Min = 0, Max = 30)]
        public int BubbleCornerRadius { get; set; } = 12;

        [DebugEditable(DisplayName = "Bubble Max Width", Step = 10, Min = 100, Max = 600)]
        public int BubbleMaxWidth { get; set; } = 320;

        [DebugEditable(DisplayName = "Bubble Text Scale", Step = 0.01f, Min = 0.1f, Max = 1f)]
        public float BubbleTextScale { get; set; } = 0.14f;

        [DebugEditable(DisplayName = "Bubble Gap From Target", Step = 2, Min = 0, Max = 100)]
        public int BubbleGap { get; set; } = 12;

        [DebugEditable(DisplayName = "Bubble BG Alpha", Step = 5, Min = 0, Max = 255)]
        public int BubbleBgAlpha { get; set; } = 245;

        // Pointer triangle settings
        [DebugEditable(DisplayName = "Pointer Width", Step = 2, Min = 4, Max = 40)]
        public int PointerWidth { get; set; } = 16;

        [DebugEditable(DisplayName = "Pointer Height", Step = 2, Min = 4, Max = 40)]
        public int PointerHeight { get; set; } = 12;

        // Guardian Angel settings
        [DebugEditable(DisplayName = "Angel Offset X", Step = 5, Min = -200, Max = 200)]
        public int AngelOffsetX { get; set; } = 0;

        [DebugEditable(DisplayName = "Angel Offset Y", Step = 5, Min = -200, Max = 200)]
        public int AngelOffsetY { get; set; } = -60;

        [DebugEditable(DisplayName = "Angel Scale", Step = 0.01f, Min = 0.02f, Max = 0.2f)]
        public float AngelScale { get; set; } = 0.06f;

        // Z Order
        [DebugEditable(DisplayName = "Z Order", Step = 100, Min = 0, Max = 100000)]
        public int ZOrder { get; set; } = 50000;

        public TutorialDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, 
            SpriteBatch spriteBatch, ImageAssetService imageAssets, TutorialManager tutorialManager)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _imageAssets = imageAssets;
            _font = FontSingleton.ChakraPetchFont;
            _tutorialManager = tutorialManager;
            _overlay = new TutorialOverlay(graphicsDevice);

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            EventManager.Subscribe<TutorialStartedEvent>(OnTutorialStarted);
            EventManager.Subscribe<TutorialCompletedEvent>(OnTutorialCompleted);
            EventManager.Subscribe<AllTutorialsCompletedEvent>(OnAllTutorialsCompleted);
            EventManager.Subscribe<HotKeyHoldCompletedEvent>(OnHotKeyHoldCompleted);
            EventManager.Subscribe<LoadSceneEvent>(_ => CleanUp());
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Array.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            base.Update(gameTime);

            if (_isActive && _currentTutorial?.key == "threat" && IsThreatDisabled())
            {
                EventManager.Publish(new AdvanceTutorialEvent());
                return;
            }

            // Update target bounds each frame (entities may move)
            if (_isActive && _tutorialManager != null)
            {
                _targetBounds = _tutorialManager.ResolveTargetBounds();
                UpdateBubblePosition();
                StateSingleton.IsTutorialActive = true;
            }
            else 
            {
                StateSingleton.IsTutorialActive = false;
            }
        }

        private bool IsThreatDisabled()
        {
            var queued = EntityManager.GetEntity("QueuedEvents")?.GetComponent<QueuedEvents>();
            return queued != null && queued.QuestIndex == 0;
        }

        private void OnTutorialStarted(TutorialStartedEvent evt)
        {
            _currentTutorial = evt.Tutorial;
            _isActive = true;

            // Block input
            StateSingleton.IsTutorialActive = true;
            SetTutorialContextActive(true);

            // Resolve target bounds
            _targetBounds = _tutorialManager.ResolveTargetBounds();

            // Prepare bubble text
            PrepareText();

            // Update bubble position
            UpdateBubblePosition();

            // Create continue button with HotKey for hold-to-continue
            CreateContinueButton();

            LoggingService.Append("TutorialDisplaySystem.OnTutorialStarted", new System.Text.Json.Nodes.JsonObject { ["key"] = _currentTutorial.key });
        }

        private void OnTutorialCompleted(TutorialCompletedEvent evt)
        {
            // Clean up current tutorial state but don't fully deactivate
            // (next tutorial will start immediately if queue has more)
            _currentTutorial = null;
            _targetBounds.Clear();
            DestroyContinueButton();
            SetTutorialContextActive(false);
        }

        private void OnAllTutorialsCompleted(AllTutorialsCompletedEvent evt)
        {
            CleanUp();
        }

        private void OnHotKeyHoldCompleted(HotKeyHoldCompletedEvent evt)
        {
            var continueEntity = EntityManager.GetEntity(ContinueEntityName);
            if (evt.Entity == continueEntity && _isActive)
            {
                EventManager.Publish(new AdvanceTutorialEvent());
            }
        }

        private void CleanUp()
        {
            _isActive = false;
            _currentTutorial = null;
            _targetBounds.Clear();

            // Restore input
            StateSingleton.PreventClicking = false;
            StateSingleton.IsTutorialActive = false;
            SetTutorialContextActive(false);

            // Destroy continue button
            DestroyContinueButton();

            LoggingService.Append("TutorialDisplaySystem.OnAllTutorialsCompleted", new System.Text.Json.Nodes.JsonObject { ["message"] = "cleaned up" });
        }

        private void PrepareText()
        {
            if (_currentTutorial == null || _font == null)
            {
                _wrappedText = string.Empty;
                return;
            }

            string text = _currentTutorial.text ?? string.Empty;
            if (_currentTutorial.key == "teach_pledge")
            {
                bool gamepad = PlayerInputService.GetFrame(EntityManager).IsGamepadConnected;
                string button = gamepad ? "Press X" : "Press Spacebar";
                text = $"{text}\n{button} while hovering over a card to pledge it.";
            }
            var lines = TextUtils.WrapText(_font, text, BubbleTextScale, BubbleMaxWidth - BubblePadX * 2);
            _wrappedText = string.Join("\n", lines);
        }

        private void UpdateBubblePosition()
        {
            if (_currentTutorial == null || _font == null)
                return;

            // Measure text
            float lineHeight = _font.MeasureString("A").Y * BubbleTextScale;
            var lines = _wrappedText.Split('\n');
            int textW = 0;
            foreach (var ln in lines)
            {
                textW = Math.Max(textW, (int)Math.Ceiling(_font.MeasureString(ln).X * BubbleTextScale));
            }
            int textH = (int)Math.Ceiling(lineHeight * Math.Max(1, lines.Length));
            int w = Math.Max(1, textW + BubblePadX * 2);
            int h = Math.Max(1, textH + BubblePadY * 2);

            // Rebuild bubble texture if size changed
            int r = Math.Max(0, Math.Min(BubbleCornerRadius, Math.Min(w, h) / 2));
            if (_bubbleTexture == null || w != _bubbleTexW || h != _bubbleTexH)
            {
                _bubbleTexture?.Dispose();
                _bubbleTexture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, w, h, r);
                _bubbleTexW = w;
                _bubbleTexH = h;
            }

            // Determine anchor point for bubble placement
            Rectangle anchor = _targetBounds.Count > 0 ? _targetBounds[0] : new Rectangle(Game1.VirtualWidth / 2, Game1.VirtualHeight / 2, 1, 1);

            // Position bubble based on orientation
            string orientation = _currentTutorial.bubbleOrientation ?? "top";
            _bubbleRect = ComputeBubblePlacement(anchor, new Point(w, h), orientation);
            UpdateContinueButtonAnchor();
        }

        private Rectangle ComputeBubblePlacement(Rectangle anchor, Point size, string orientation)
        {
            int w = size.X, h = size.Y;
            int screenW = Game1.VirtualWidth;
            int screenH = Game1.VirtualHeight;

            int x = 0, y = 0;

            switch (orientation.ToLowerInvariant())
            {
                case "top":
                    x = anchor.X + (anchor.Width - w) / 2;
                    y = anchor.Y - h - BubbleGap - PointerHeight;
                    break;
                case "bottom":
                    x = anchor.X + (anchor.Width - w) / 2;
                    y = anchor.Bottom + BubbleGap + PointerHeight;
                    break;
                case "left":
                    x = anchor.X - w - BubbleGap - PointerHeight;
                    y = anchor.Y + (anchor.Height - h) / 2;
                    break;
                case "right":
                    x = anchor.Right + BubbleGap + PointerHeight;
                    y = anchor.Y + (anchor.Height - h) / 2;
                    break;
                default:
                    x = anchor.X + (anchor.Width - w) / 2;
                    y = anchor.Y - h - BubbleGap - PointerHeight;
                    break;
            }

            // Clamp to screen bounds
            x = Math.Clamp(x, 8, screenW - w - 8);
            y = Math.Clamp(y, 8, screenH - h - 8);

            return new Rectangle(x, y, w, h);
        }

        private void CreateContinueButton()
        {
            // Destroy any existing button
            DestroyContinueButton();

            // Create a UI entity for the hold-to-continue hotkey
            var entity = EntityManager.CreateEntity(ContinueEntityName);

            EntityManager.AddComponent(entity, new Transform
            {
                Position = new Vector2(_bubbleRect.X, _bubbleRect.Y),
                ZOrder = ZOrder + 100
            });

            EntityManager.AddComponent(entity, new UIElement
            {
                Bounds = _bubbleRect,
                IsInteractable = false,
                IsPreventDefaultClick = true,
                IsHidden = false,
                LayerType = UILayerType.Overlay
            });

            EntityManager.AddComponent(entity, new HotKey
            {
                Button = FaceButton.X,
                RequiresHold = true,
                HoldDurationSeconds = 0.5f,
                Position = MapBubbleOrientationToHotKeyPosition(_currentTutorial?.bubbleOrientation),
                IsActive = true,
                AllowWhenNonInteractable = true
            });
            InputContextService.EnsureMember(
                EntityManager,
                entity,
                "overlay.tutorial");

            LoggingService.Append("TutorialDisplaySystem.CreateContinueButton", new System.Text.Json.Nodes.JsonObject { ["message"] = "created continue button" });
        }

        private void UpdateContinueButtonAnchor()
        {
            var entity = EntityManager.GetEntity(ContinueEntityName);
            if (entity == null) return;

            var transform = entity.GetComponent<Transform>();
            if (transform != null)
            {
                transform.Position = new Vector2(_bubbleRect.X, _bubbleRect.Y);
                transform.ZOrder = ZOrder + 100;
            }

            var ui = entity.GetComponent<UIElement>();
            if (ui != null)
            {
                ui.Bounds = _bubbleRect;
                ui.IsInteractable = false;
                ui.IsPreventDefaultClick = true;
                ui.IsHidden = false;
                ui.LayerType = UILayerType.Overlay;
            }

            var hotKey = entity.GetComponent<HotKey>();
            if (hotKey != null)
            {
                hotKey.Position = MapBubbleOrientationToHotKeyPosition(_currentTutorial?.bubbleOrientation);
            }
        }

        internal static HotKeyPosition MapBubbleOrientationToHotKeyPosition(string orientation)
        {
            return (orientation ?? "top").ToLowerInvariant() switch
            {
                "bottom" => HotKeyPosition.Below,
                "left" => HotKeyPosition.Left,
                "right" => HotKeyPosition.Right,
                _ => HotKeyPosition.Top,
            };
        }

        private void DestroyContinueButton()
        {
            var entity = EntityManager.GetEntity(ContinueEntityName);
            if (entity != null)
            {
                EntityManager.DestroyEntity(entity.Id);
            }
        }

        private void SetTutorialContextActive(bool active)
        {
            Entity contextEntity = EntityManager.GetEntity(TutorialContextEntityName);
            if (contextEntity == null)
            {
                contextEntity = EntityManager.CreateEntity(TutorialContextEntityName);
                EntityManager.AddComponent(contextEntity, new DontDestroyOnLoad());
            }
            InputContextService.EnsureContext(
                EntityManager,
                contextEntity,
                "overlay.tutorial",
                900,
                active);
        }

        public void Draw()
        {
            if (!_isActive || _currentTutorial == null)
                return;

            // Draw overlay with cutouts
            _overlay.Draw(_spriteBatch, Game1.VirtualWidth, Game1.VirtualHeight, _targetBounds, OverlayAlpha, CutoutPadding);

            // Draw bubble
            DrawBubble();

            // Draw guardian angel
            // DrawGuardianAngel();
        }

        private void DrawBubble()
        {
            if (_bubbleTexture == null || _font == null)
                return;

            // Draw bubble background
            var bgColor = new Color(255, 255, 255, Math.Clamp(BubbleBgAlpha, 0, 255));
            _spriteBatch.Draw(_bubbleTexture, _bubbleRect, bgColor);

            // Draw pointer triangle
            DrawPointer();

            // Draw text
            var textPos = new Vector2(_bubbleRect.X + BubblePadX, _bubbleRect.Y + BubblePadY);
            _spriteBatch.DrawString(_font, _wrappedText, textPos, Color.Black, 0f, Vector2.Zero, BubbleTextScale, SpriteEffects.None, 0f);
        }

        private void DrawPointer()
        {
            if (_targetBounds.Count == 0)
                return;

            Rectangle anchor = _targetBounds[0];
            string orientation = _currentTutorial?.bubbleOrientation ?? "top";

            Vector2 tip = Vector2.Zero;
            Vector2 left = Vector2.Zero;
            Vector2 right = Vector2.Zero;

            int halfWidth = PointerWidth / 2;

            switch (orientation.ToLowerInvariant())
            {
                case "top":
                    // Pointer points down from bottom of bubble
                    tip = new Vector2(anchor.X + anchor.Width / 2, anchor.Y - BubbleGap);
                    left = new Vector2(tip.X - halfWidth, _bubbleRect.Bottom);
                    right = new Vector2(tip.X + halfWidth, _bubbleRect.Bottom);
                    break;
                case "bottom":
                    // Pointer points up from top of bubble
                    tip = new Vector2(anchor.X + anchor.Width / 2, anchor.Bottom + BubbleGap);
                    left = new Vector2(tip.X - halfWidth, _bubbleRect.Top);
                    right = new Vector2(tip.X + halfWidth, _bubbleRect.Top);
                    break;
                case "left":
                    // Pointer points right from right side of bubble
                    tip = new Vector2(anchor.X - BubbleGap, anchor.Y + anchor.Height / 2);
                    left = new Vector2(_bubbleRect.Right, tip.Y - halfWidth);
                    right = new Vector2(_bubbleRect.Right, tip.Y + halfWidth);
                    break;
                case "right":
                    // Pointer points left from left side of bubble
                    tip = new Vector2(anchor.Right + BubbleGap, anchor.Y + anchor.Height / 2);
                    left = new Vector2(_bubbleRect.Left, tip.Y - halfWidth);
                    right = new Vector2(_bubbleRect.Left, tip.Y + halfWidth);
                    break;
            }

            // Draw triangle using 3 lines (filled triangle would be better but this works)
            DrawTriangle(tip, left, right, new Color(255, 255, 255, Math.Clamp(BubbleBgAlpha, 0, 255)));
        }

        private void DrawTriangle(Vector2 a, Vector2 b, Vector2 c, Color color)
        {
            // Fill triangle by drawing horizontal lines (scanline approach)
            // Sort vertices by Y
            Vector2[] verts = { a, b, c };
            Array.Sort(verts, (v1, v2) => v1.Y.CompareTo(v2.Y));
            Vector2 top = verts[0];
            Vector2 mid = verts[1];
            Vector2 bot = verts[2];

            // Draw filled triangle using scanlines
            float yStart = MathF.Floor(top.Y);
            float yEnd = MathF.Ceiling(bot.Y);

            for (float y = yStart; y <= yEnd; y++)
            {
                float xLeft = float.MaxValue;
                float xRight = float.MinValue;

                // Interpolate X for each edge
                void CheckEdge(Vector2 v1, Vector2 v2)
                {
                    if (v1.Y > v2.Y) { var tmp = v1; v1 = v2; v2 = tmp; }
                    if (y < v1.Y || y > v2.Y) return;
                    float t = (v2.Y - v1.Y) < 0.001f ? 0f : (y - v1.Y) / (v2.Y - v1.Y);
                    float x = v1.X + t * (v2.X - v1.X);
                    xLeft = MathF.Min(xLeft, x);
                    xRight = MathF.Max(xRight, x);
                }

                CheckEdge(top, mid);
                CheckEdge(mid, bot);
                CheckEdge(top, bot);

                if (xLeft <= xRight)
                {
                    int ix = (int)MathF.Floor(xLeft);
                    int iw = (int)MathF.Ceiling(xRight - xLeft) + 1;
                    if (iw > 0)
                    {
                        _spriteBatch.Draw(_pixel, new Rectangle(ix, (int)y, iw, 1), color);
                    }
                }
            }
        }

        private void DrawGuardianAngel()
        {
            // Load angel texture if not loaded
            if (_angelTexture == null)
            {
                try
                {
                    _angelTexture = _imageAssets.TryGetTexture("guardian_angel");
                }
                catch
                {
                    _angelTexture = null;
                }
            }

            if (_angelTexture == null)
                return;

            // Position angel near the bubble
            Vector2 bubblePos = new Vector2(_bubbleRect.X, _bubbleRect.Y);
            Vector2 angelPos = bubblePos + new Vector2(AngelOffsetX, AngelOffsetY);

            // Clamp to screen
            float halfW = _angelTexture.Width * AngelScale / 2;
            float halfH = _angelTexture.Height * AngelScale / 2;
            angelPos.X = Math.Clamp(angelPos.X, halfW, Game1.VirtualWidth - halfW);
            angelPos.Y = Math.Clamp(angelPos.Y, halfH, Game1.VirtualHeight - halfH);

            var origin = new Vector2(_angelTexture.Width / 2f, _angelTexture.Height / 2f);
            _spriteBatch.Draw(_angelTexture, angelPos, null, Color.White, 0f, origin, AngelScale, SpriteEffects.None, 0f);
        }

    }
}
