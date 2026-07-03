using System.Linq;
using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Utils;
using Crusaders30XX.ECS.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Systems
{
    [DebugTab("Hint Tooltip")]
    public class HintTooltipDisplaySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private Texture2D _pixel;
        private Texture2D _rounded;
        private int _cachedW, _cachedH, _cachedR;

        // Runtime state
        private bool _targetVisible;
        private float _alpha01;
        private int _visibleForEntityId = -1;
        private Rectangle _bubbleRect;
        private string _wrappedText = string.Empty;
        private Entity _tooltipEntity;

        // Layout / visuals
        [DebugEditable(DisplayName = "Padding X", Step = 1, Min = 0, Max = 64)]
        public int PadX { get; set; } = 10;

        [DebugEditable(DisplayName = "Padding Y", Step = 1, Min = 0, Max = 64)]
        public int PadY { get; set; } = 8;

        [DebugEditable(DisplayName = "Corner Radius", Step = 1, Min = 0, Max = 64)]
        public int CornerRadius { get; set; } = 10;

        [DebugEditable(DisplayName = "Gap", Step = 1, Min = 0, Max = 64)]
        public int Gap { get; set; } = 8;

        [DebugEditable(DisplayName = "Max Width", Step = 10, Min = 60, Max = 1200)]
        public int MaxWidth { get; set; } = 360;

        [DebugEditable(DisplayName = "Text Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
        public float TextScale { get; set; } = 0.12f;

        [DebugEditable(DisplayName = "Screen Pad", Step = 1, Min = 0, Max = 200)]
        public int ScreenPad { get; set; } = 8;

        [DebugEditable(DisplayName = "Max BG Alpha", Step = 1, Min = 0, Max = 255)]
        public int MaxAlpha { get; set; } = 235;

        // Orientation preferences
        public enum PlacementMode { Auto, HorizontalOnly, VerticalOnly }

        [DebugEditable(DisplayName = "Placement Mode")]
        public PlacementMode Mode { get; set; } = PlacementMode.Auto;

        [DebugEditable(DisplayName = "Prefer Right")]
        public bool PreferRight { get; set; } = true;

        [DebugEditable(DisplayName = "Prefer Top")]
        public bool PreferTop { get; set; } = true;

        [DebugEditable(DisplayName = "Hide On Hover Change")]
        public bool HideOnHoverChange { get; set; } = true;

        // Fade
        [DebugEditable(DisplayName = "Fade In (s)", Step = 0.01f, Min = 0f, Max = 2f)]
        public float FadeInSeconds { get; set; } = 0.12f;

        [DebugEditable(DisplayName = "Fade Out (s)", Step = 0.01f, Min = 0f, Max = 2f)]
        public float FadeOutSeconds { get; set; } = 0.18f;

        // Z and angel
        [DebugEditable(DisplayName = "Z Order", Step = 10, Min = -10000, Max = 10000)]
        public int ZOrder { get; set; } = 9000;

        [DebugEditable(DisplayName = "Angel Scale", Step = 0.01f, Min = 0.01f, Max = 3f)]
        public float AngelScale { get; set; } = 0.07f;

        [DebugEditable(DisplayName = "Angel Offset X", Step = 1, Min = -1000, Max = 1000)]
        public int AngelOffsetX { get; set; } = 34;

        [DebugEditable(DisplayName = "Angel Offset Y", Step = 1, Min = -1000, Max = 1000)]
        public int AngelOffsetY { get; set; } = -54;

        public HintTooltipDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
            : base(entityManager)
        {
            _graphicsDevice = graphicsDevice;
            _spriteBatch = spriteBatch;
            _font = FontSingleton.ContentFont;
            _pixel = imageAssets.GetPixel(Color.White);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<UIElement>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

        public override void Update(GameTime gameTime)
        {
            if (!IsActive) return;
            if (!Game1.WindowIsActive) return;
            PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
            bool useGamepad = input.IsGamepadConnected;
            if (!useGamepad)
            {
                // Hide if switching away from gamepad
                _targetVisible = false;
                StepFade(gameTime);
                return;
            }

            // Hovered target with Hint
            var hovered = GetRelevantEntities()
                .Select(e => new { E = e, UI = e.GetComponent<UIElement>(), T = e.GetComponent<Transform>(), H = e.GetComponent<Hint>() })
                .Where(x => x.UI != null && x.UI.IsHovered && x.H != null)
                .OrderByDescending(x => x.T?.ZOrder ?? 0)
                .FirstOrDefault();

            // Hide when hover changes away
            if (HideOnHoverChange && _targetVisible && hovered != null && hovered.E.Id != _visibleForEntityId)
            {
                _targetVisible = false;
            }

            // Toggle on Left Stick press edge
            bool edgeL3 = input.WasPressed(PlayerButton.LeftStick);
            if (edgeL3 && hovered != null)
            {
                if (!_targetVisible || _visibleForEntityId != hovered.E.Id)
                {
                    ShowFor(hovered.E, hovered.UI, hovered.T, hovered.H);
                }
                else
                {
                    _targetVisible = false;
                }
            }

            StepFade(gameTime);
            base.Update(gameTime);
        }

        private void StepFade(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_targetVisible)
            {
                float step = (FadeInSeconds <= 0f) ? 1f : dt / MathHelper.Max(0.0001f, FadeInSeconds);
                _alpha01 = MathHelper.Clamp(_alpha01 + step, 0f, 1f);
            }
            else
            {
                float step = (FadeOutSeconds <= 0f) ? 1f : dt / MathHelper.Max(0.0001f, FadeOutSeconds);
                _alpha01 = MathHelper.Clamp(_alpha01 - step, 0f, 1f);
            }
        }

        private void EnsureTooltipEntity()
        {
            if (_tooltipEntity != null) return;
            _tooltipEntity = EntityManager.GetEntity("HintTooltip");
            if (_tooltipEntity == null)
            {
                _tooltipEntity = EntityManager.CreateEntity("HintTooltip");
                EntityManager.AddComponent(_tooltipEntity, new Transform { Position = Vector2.Zero, ZOrder = ZOrder });
            }
        }

        private void ShowFor(Entity e, UIElement ui, Transform t, Hint hint)
        {
            EnsureTooltipEntity();
            _visibleForEntityId = e.Id;
            _targetVisible = true;

            string text = hint?.Text ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text)) { _targetVisible = false; return; }

            // Wrap lines and compute size
            var lines = TextUtils.WrapText(_font, text, TextScale, MaxWidth);
            _wrappedText = string.Join("\n", lines);
            float lineHeight = _font.MeasureString("A").Y * TextScale;
            int textW = 0;
            foreach (var ln in lines) { textW = System.Math.Max(textW, (int)System.Math.Ceiling(_font.MeasureString(ln).X * TextScale)); }
            int textH = (int)System.Math.Ceiling(lineHeight * System.Math.Max(1, lines.Count));
            int w = System.Math.Max(1, textW + PadX * 2);
            int h = System.Math.Max(1, textH + PadY * 2);

            // Build rounded texture cache
            int r = System.Math.Max(0, System.Math.Min(CornerRadius, System.Math.Min(w, h) / 2));
            bool rebuild = _rounded == null || _cachedW != w || _cachedH != h || _cachedR != r;
            if (rebuild)
            {
                _rounded?.Dispose();
                _rounded = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, w, h, r);
                _cachedW = w; _cachedH = h; _cachedR = r;
            }

            // Anchor rect
            Rectangle anchor;
            if (ui != null && ui.Bounds.Width > 1 && ui.Bounds.Height > 1)
            {
                anchor = TransformResolverService.ResolveUIBounds(EntityManager, e, ui);
            }
            else
            {
                Vector2 pos = t?.Position ?? Vector2.Zero;
                anchor = new Rectangle((int)pos.X, (int)pos.Y, 1, 1);
            }

            _bubbleRect = ComputePlacement(anchor, new Point(w, h));

            // Position entity (top-left of bubble)
            var tt = _tooltipEntity.GetComponent<Transform>();
            if (tt != null)
            {
                tt.Position = new Vector2(_bubbleRect.X, _bubbleRect.Y);
                tt.ZOrder = ZOrder;
            }
        }

        private Rectangle ComputePlacement(Rectangle anchor, Point size)
        {
            int w = size.X, h = size.Y;
            int screenW = Game1.VirtualWidth;
            int screenH = Game1.VirtualHeight;

            // Candidate placements in order based on preferences and mode
            var candidates = new List<Vector2>();

            void AddRight() => candidates.Add(new Vector2(anchor.Right + Gap, anchor.Y + (anchor.Height - h) / 2));
            void AddLeft() => candidates.Add(new Vector2(anchor.X - w - Gap, anchor.Y + (anchor.Height - h) / 2));
            void AddTop() => candidates.Add(new Vector2(anchor.X + (anchor.Width - w) / 2, anchor.Y - h - Gap));
            void AddBottom() => candidates.Add(new Vector2(anchor.X + (anchor.Width - w) / 2, anchor.Bottom + Gap));

            if (Mode == PlacementMode.HorizontalOnly)
            {
                if (PreferRight) { AddRight(); AddLeft(); } else { AddLeft(); AddRight(); }
            }
            else if (Mode == PlacementMode.VerticalOnly)
            {
                if (PreferTop) { AddTop(); AddBottom(); } else { AddBottom(); AddTop(); }
            }
            else // Auto
            {
                // Try preferred horizontal first, then the other side, then vertical preferences
                if (PreferRight) { AddRight(); AddLeft(); } else { AddLeft(); AddRight(); }
                if (PreferTop) { AddTop(); AddBottom(); } else { AddBottom(); AddTop(); }
            }

            // Choose the first that fully fits; otherwise clamp last candidate
            foreach (var tl in candidates)
            {
                int x = (int)tl.X, y = (int)tl.Y;
                if (x >= ScreenPad && y >= ScreenPad && x + w <= screenW - ScreenPad && y + h <= screenH - ScreenPad)
                {
                    return new Rectangle(x, y, w, h);
                }
            }

            // Fallback: clamp preferred placement to screen
            var last = candidates.LastOrDefault();
            int cx = (int)MathHelper.Clamp(last.X, ScreenPad, System.Math.Max(ScreenPad, screenW - ScreenPad - w));
            int cy = (int)MathHelper.Clamp(last.Y, ScreenPad, System.Math.Max(ScreenPad, screenH - ScreenPad - h));
            return new Rectangle(cx, cy, w, h);
        }

        public void Draw()
        {
            if (_alpha01 <= 0f) return;
            if (_font == null) return;

            // Ensure angel texture (fail-soft if not present)
            // if (_angel == null)
            // {
            //     try { _angel = _content.Load<Texture2D>("guardian_angel"); }
            //     catch { _angel = null; }
            // }

            // Ensure tooltip entity
            EnsureTooltipEntity();
            var tt = _tooltipEntity.GetComponent<Transform>();
            if (tt == null) return;

            Rectangle rect = new Rectangle((int)tt.Position.X, (int)tt.Position.Y, _cachedW, _cachedH);
            int alpha = (int)System.Math.Round(MaxAlpha * _alpha01);
            var bg = new Color(255, 255, 255, System.Math.Clamp(alpha, 0, 255));
            _spriteBatch.Draw(_rounded, rect, bg);

            // Text
            var textPos = new Vector2(rect.X + PadX, rect.Y + PadY);
            var color = new Color(0, 0, 0) * _alpha01;
            _spriteBatch.DrawString(_font, _wrappedText, textPos, color, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);

            // Guardian angel image anchored to outside bottom-left
            // if (_angel != null)
            // {
            //     Vector2 pos = new Vector2(rect.Left - AngelOffsetX, rect.Bottom + AngelOffsetY);
            //     var origin = new Vector2(0f, _angel.Height); // bottom-left origin
            //     _spriteBatch.Draw(_angel, pos, null, Color.White * _alpha01, 0f, origin, AngelScale, SpriteEffects.None, 0f);
            // }
        }
    }
}
