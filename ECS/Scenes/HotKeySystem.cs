using System;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.Systems
{
    [DebugTab("HotKeys")]
    public class HotKeySystem : Core.System
    {
        private readonly GraphicsDevice _graphicsDevice;
        private readonly SpriteBatch _spriteBatch;
        private readonly SpriteFont _font;
        private Texture2D _circleTexSmall;
        private readonly HotKeyHoldTracker _holdTracker = new();

        public IReadOnlyDictionary<Entity, float> HoldProgress => _holdTracker.Progress;

        [DebugEditable(DisplayName = "Hint Radius (px)", Step = 1f, Min = 4f, Max = 64f)]
        public int HintRadius { get; set; } = 20;

        [DebugEditable(DisplayName = "Hint X Gap (px)", Step = 1f, Min = -64f, Max = 128f)]
        public int HintGapX { get; set; } = 8;

        [DebugEditable(DisplayName = "Hint Y Gap (px)", Step = 1f, Min = -64f, Max = 128f)]
        public int HintGapY { get; set; } = 8;

        [DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.05f, Max = 2.5f)]
        public float TextScale { get; set; } = 0.15f;

        public HotKeySystem(EntityManager entityManager, GraphicsDevice gd, SpriteBatch sb)
            : base(entityManager)
        {
            _graphicsDevice = gd;
            _spriteBatch = sb;
            _font = FontSingleton.ChakraPetchFont;
            _circleTexSmall = _graphicsDevice == null
                ? null
                : PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, HintRadius);
            EventManager.Subscribe<HotKeyHoldCompletedEvent>(OnHotKeyHoldCompleted);
        }

        private void OnHotKeyHoldCompleted(HotKeyHoldCompletedEvent evt)
        {
            ProcessHotKeyClick(evt.Entity);
        }

        private void ProcessHotKeyClick(Entity entity)
        {
            if (GameOverOverlayDisplaySystem.IsOverlayActive(EntityManager)) return;

            var ui = entity.GetComponent<UIElement>();
            var hotKey = entity.GetComponent<HotKey>();

            if (hotKey != null && hotKey.ParentEntity != null)
            {
                Console.WriteLine($"Processing hotkey click for parent entity: {hotKey.ParentEntity.Id}");
                ProcessHotKeyClick(hotKey.ParentEntity);
                return;
            }

            Console.WriteLine($"Processing hotkey click for entity: {entity.Id} {entity.Name} ui={ui?.EventType} uiClicked={ui?.IsClicked}");

            if (ui != null && ui.EventType != UIElementEventType.None)
            {
                UIElementEventDelegateService.HandleEvent(ui.EventType, entity, EntityManager);
                if (ui.IsInteractable)
                {
                    EventManager.Publish(new HotKeySelectEvent { Entity = entity });
                }
            }
            else if (ui != null)
            {
                ui.IsClicked = true;
                if (ui.IsInteractable)
                {
                    EventManager.Publish(new HotKeySelectEvent { Entity = entity });
                }
            }
        }

        protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
        {
            // Process globally; we will iterate HotKey components during Update/Draw
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            if (!Game1.WindowIsActive || StateSingleton.IsActive) return;
            if (GameOverOverlayDisplaySystem.IsOverlayActive(EntityManager))
            {
                foreach (Entity heldEntity in _holdTracker.Progress.Keys.ToList())
                {
                    _holdTracker.Cancel(heldEntity);
                }
                return;
            }

            PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
            string contextId = InputContextResolver.ResolveCommandContext(EntityManager);
            bool gameplayBlocked = StateSingleton.PreventClicking
                && contextId == InputContextIds.Gameplay;
            FaceButton? pressed = GetPressedButton(frame);
            if (pressed.HasValue)
            {
                var target = FindTarget(pressed.Value, contextId, gameplayBlocked);
                if (target != null)
                {
                    if (target.GetComponent<HotKey>().RequiresHold)
                    {
                        _holdTracker.Start(target, pressed.Value);
                    }
                    else
                    {
                        ProcessHotKeyClick(target);
                    }
                }
                else if (pressed == FaceButton.X)
                {
                    ProcessHoveredSecondaryAction(contextId);
                }
            }

            UpdateHolds(frame, contextId, gameplayBlocked, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        private Entity FindTarget(FaceButton button, string contextId, bool gameplayBlocked)
        {
            return EntityManager.GetEntitiesWithComponent<HotKey>()
                .Select(entity => new
                {
                    Entity = entity,
                    HotKey = entity.GetComponent<HotKey>(),
                    UI = entity.GetComponent<UIElement>(),
                    Transform = entity.GetComponent<Transform>(),
                })
                .Where(item => item.HotKey != null
                    && item.HotKey.Button == button
                    && IsHotKeyEligible(item.Entity, item.HotKey, item.UI, contextId, gameplayBlocked))
                .OrderByDescending(item => item.Transform?.ZOrder ?? 0)
                .Select(item => item.Entity)
                .FirstOrDefault();
        }

        private void UpdateHolds(PlayerInputFrame frame, string contextId, bool gameplayBlocked, float elapsed)
        {
            foreach (Entity heldEntity in _holdTracker.Progress.Keys.ToList())
            {
                HotKey hotKey = heldEntity.GetComponent<HotKey>();
                UIElement ui = heldEntity.GetComponent<UIElement>();
                bool eligible = hotKey != null
                    && IsHotKeyEligible(heldEntity, hotKey, ui, contextId, gameplayBlocked)
                    && IsButtonDown(frame, _holdTracker.GetButton(heldEntity));
                if (_holdTracker.Advance(
                    heldEntity,
                    elapsed,
                    hotKey?.HoldDurationSeconds ?? 0f,
                    eligible))
                {
                    EventManager.Publish(new HotKeyHoldCompletedEvent { Entity = heldEntity });
                }
            }
        }

        internal static bool IsHotKeyEligible(Entity entity, HotKey hotKey, UIElement ui, string contextId, bool gameplayBlocked)
        {
            return hotKey != null
                && hotKey.IsActive
                && ui != null
                && !ui.IsHidden
                && (ui.IsInteractable || hotKey.AllowWhenNonInteractable)
                && InputContextResolver.IsMember(entity, contextId)
                && IsHotKeyInputAllowed(entity, gameplayBlocked);
        }

        private static bool IsHotKeyInputAllowed(Entity entity, bool gameplayBlocked)
        {
            return !gameplayBlocked || entity.HasComponent<TutorialInteractionPermitted>();
        }

        internal static FaceButton? GetPressedButton(PlayerInputFrame frame)
        {
            if (frame.Device == PlayerInputDevice.KeyboardMouse
                && frame.WasPressed(PlayerButton.Space))
            {
                return FaceButton.X;
            }
            if (frame.WasPressed(PlayerButton.FaceY)) return FaceButton.Y;
            if (frame.WasPressed(PlayerButton.Back)) return FaceButton.View;
            if (frame.WasPressed(PlayerButton.Cancel)) return FaceButton.B;
            if (frame.WasPressed(PlayerButton.FaceX)) return FaceButton.X;
            if (frame.WasPressed(PlayerButton.Start)) return FaceButton.Start;
            if (frame.WasPressed(PlayerButton.LeftShoulder)) return FaceButton.LB;
            if (frame.WasPressed(PlayerButton.RightShoulder)) return FaceButton.RB;
            if (frame.WasPressed(PlayerButton.Escape)) return FaceButton.View;
            return null;
        }

        private static bool IsButtonDown(PlayerInputFrame frame, FaceButton button)
        {
            return button switch
            {
                FaceButton.Y => frame.IsDown(PlayerButton.FaceY),
                FaceButton.B => frame.IsDown(PlayerButton.Cancel),
                FaceButton.X => frame.Device == PlayerInputDevice.KeyboardMouse
                    ? frame.IsDown(PlayerButton.Space)
                    : frame.IsDown(PlayerButton.FaceX),
                FaceButton.Start => frame.IsDown(PlayerButton.Start),
                FaceButton.LB => frame.IsDown(PlayerButton.LeftShoulder),
                FaceButton.RB => frame.IsDown(PlayerButton.RightShoulder),
                FaceButton.View => frame.Device == PlayerInputDevice.KeyboardMouse
                    ? frame.IsDown(PlayerButton.Escape)
                    : frame.IsDown(PlayerButton.Back),
                _ => false,
            };
        }

        private void ProcessHoveredSecondaryAction(string contextId)
        {
            var target = EntityManager.GetEntitiesWithComponent<UIElement>()
                .Select(entity => new
                {
                    Entity = entity,
                    UI = entity.GetComponent<UIElement>(),
                    Transform = entity.GetComponent<Transform>(),
                })
                .Where(x => x.UI != null
                    && x.UI.IsHovered
                    && x.UI.IsInteractable
                    && !x.UI.IsHidden
                    && x.UI.SecondaryEventType != UIElementEventType.None
                    && InputContextResolver.IsMember(x.Entity, contextId))
                .OrderByDescending(x => x.Transform?.ZOrder ?? 0)
                .FirstOrDefault();

            if (target == null) return;

            UIElementEventDelegateService.HandleEvent(
                target.UI.SecondaryEventType,
                target.Entity,
                EntityManager);
            EventManager.Publish(new HotKeySelectEvent { Entity = target.Entity });
        }

        public (int cx, int cy) CalculateHintPosition(Microsoft.Xna.Framework.Rectangle bounds, HotKeyPosition position, int hintRadius, int hintGapX, int hintGapY)
        {
            int cx, cy;
            switch (position)
            {
                case HotKeyPosition.Below:
                    cx = bounds.Center.X;
                    cy = bounds.Bottom + System.Math.Max(-64, hintGapY) + (int)System.Math.Round(hintRadius * 1.1f);
                    break;
                case HotKeyPosition.Top:
                    cx = bounds.Center.X;
                    cy = bounds.Top - System.Math.Max(-64, hintGapY) - (int)System.Math.Round(hintRadius * 1.1f);
                    break;
                case HotKeyPosition.Right:
                    cx = bounds.Right + System.Math.Max(-64, hintGapX) + (int)System.Math.Round(hintRadius * 1.1f);
                    cy = bounds.Center.Y;
                    break;
                case HotKeyPosition.Left:
                    cx = bounds.Left - System.Math.Max(-64, hintGapX) - (int)System.Math.Round(hintRadius * 1.1f);
                    cy = bounds.Center.Y;
                    break;
                default:
                    cx = bounds.Center.X;
                    cy = bounds.Bottom + System.Math.Max(-64, hintGapY) + (int)System.Math.Round(hintRadius * 1.1f);
                    break;
            }
            return (cx, cy);
        }

        public void Draw()
        {
            if (GameOverOverlayDisplaySystem.IsOverlayActive(EntityManager)) return;

            PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
            bool gamepadConnected = frame.IsGamepadConnected;
            string contextId = InputContextResolver.ResolveCommandContext(EntityManager);
            bool gameplayBlocked = StateSingleton.PreventClicking
                && contextId == InputContextIds.Gameplay;

            var items = EntityManager.GetEntitiesWithComponent<HotKey>()
                .Select(e => new { E = e, HK = e.GetComponent<HotKey>(), UI = e.GetComponent<UIElement>(), T = e.GetComponent<Transform>() })
                .Where(x => x.HK != null
                    && IsHotKeyEligible(x.E, x.HK, x.UI, contextId, gameplayBlocked))
                .OrderByDescending(x => x.T?.ZOrder ?? 0)
                .ToList();

            // Ensure radius texture matches current setting
            _circleTexSmall = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, System.Math.Max(2, HintRadius));
            bool isPS = frame.GamepadGlyphStyle == GamepadGlyphStyle.PlayStation;

            foreach (var x in items)
            {
                var r = x.UI.Bounds;
                if (r.Width < 2 || r.Height < 2) continue;
                var (cx, cy) = CalculateHintPosition(r, x.HK.Position, HintRadius, HintGapX, HintGapY);
                var pos = new Vector2(cx - HintRadius, cy - HintRadius);
                
                // When gamepad is not connected, show keyboard visuals for FaceButton.X and FaceButton.View
                if (!gamepadConnected)
                {
                    if (x.HK.Button == FaceButton.X)
                    {
                        DrawKeyboardKey(cx, cy);
                        continue;
                    }
                    if (x.HK.Button == FaceButton.View)
                    {
                        DrawKeyboardEsc(cx, cy);
                        continue;
                    }
                }

                // Skip drawing if gamepad is not connected and this is not an X or View button
                if (!gamepadConnected) continue;
                
                if (x.HK.Button == FaceButton.LB || x.HK.Button == FaceButton.RB)
                {
                    DrawBumperBadge(cx, cy, x.HK.Button == FaceButton.LB ? "LB" : "RB", isPS);
                }
                else if (x.HK.Button == FaceButton.View)
                {
                    DrawViewButtonHint(cx, cy, isPS);
                }
                else if (x.HK.Button == FaceButton.Start)
                {
                    if (isPS)
                    {
                        // PlayStation: rectangular oval
                        int ovalWidth = (int)System.Math.Round(HintRadius * 2.5f);
                        int ovalHeight = (int)System.Math.Round(HintRadius * 1.2f);
                        int cornerRadius = System.Math.Max(2, ovalHeight / 2);
                        var ovalTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, ovalWidth, ovalHeight, cornerRadius);
                        _spriteBatch.Draw(ovalTex, new Vector2(cx - ovalWidth / 2f, cy - ovalHeight / 2f), new Color(36, 36, 36));
                    }
                    else
                    {
                        // Xbox: three-line menu icon (hamburger menu)
                        int iconSize = (int)System.Math.Round(HintRadius * 1.5f);
                        int lineWidth = (int)System.Math.Round(iconSize * 0.7f);
                        int lineHeight = System.Math.Max(2, (int)System.Math.Round(iconSize * 0.15f));
                        int lineSpacing = (int)System.Math.Round(iconSize * 0.25f);
                        int startY = cy - iconSize / 2;
                        
                        // Draw three horizontal lines using rounded squares with minimal corner radius
                        var lineTex = PrimitiveTextureFactory.GetRoundedSquare(_graphicsDevice, lineHeight, System.Math.Max(1, lineHeight / 4));
                        float scaleX = lineWidth / (float)lineTex.Width;
                        
                        _spriteBatch.Draw(lineTex, new Vector2(cx - lineWidth / 2f, startY), null, Color.White, 0f, Vector2.Zero, new Vector2(scaleX, 1f), SpriteEffects.None, 0f);
                        _spriteBatch.Draw(lineTex, new Vector2(cx - lineWidth / 2f, startY + lineSpacing), null, Color.White, 0f, Vector2.Zero, new Vector2(scaleX, 1f), SpriteEffects.None, 0f);
                        _spriteBatch.Draw(lineTex, new Vector2(cx - lineWidth / 2f, startY + lineSpacing * 2), null, Color.White, 0f, Vector2.Zero, new Vector2(scaleX, 1f), SpriteEffects.None, 0f);
                    }
                }
                else
                {
                    if (!isPS)
                    {
                        var color = GetFaceButtonColor(x.HK.Button);
                        _spriteBatch.Draw(_circleTexSmall, pos, color);
                        string letter = GetFaceButtonLetter(x.HK.Button);
                        var size = _font.MeasureString(letter) * TextScale;
                        var textPos = new Vector2(cx - size.X / 2f, cy - size.Y / 2f - 2f);
                        _spriteBatch.DrawString(_font, letter, textPos, Color.Black, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
                    }
                    else
                    {
                        // PlayStation style: neutral dark badge + colored shape
                        _spriteBatch.Draw(_circleTexSmall, pos, new Color(36, 36, 36));
                        DrawPlayStationIcon(x.HK.Button, cx, cy);
                    }
                }
            }
        }

        private void DrawViewButtonHint(int cx, int cy, bool isPS)
        {
            if (isPS)
            {
                // PlayStation Create: dark rounded square with three vertical lines
                int badgeSize = (int)System.Math.Round(HintRadius * 1.5f);
                int cornerRadius = System.Math.Max(2, badgeSize / 6);
                var badgeTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, badgeSize, badgeSize, cornerRadius);
                _spriteBatch.Draw(badgeTex, new Vector2(cx - badgeSize / 2f, cy - badgeSize / 2f), new Color(36, 36, 36));

                int lineWidth = System.Math.Max(2, (int)System.Math.Round(badgeSize * 0.12f));
                int lineHeight = (int)System.Math.Round(badgeSize * 0.45f);
                int lineSpacing = (int)System.Math.Round(badgeSize * 0.18f);
                var lineTex = PrimitiveTextureFactory.GetRoundedSquare(_graphicsDevice, lineWidth, System.Math.Max(1, lineWidth / 4));
                float scaleY = lineHeight / (float)lineTex.Height;
                int totalWidth = lineWidth * 3 + lineSpacing * 2;
                int startX = cx - totalWidth / 2;
                int lineY = cy - lineHeight / 2;
                for (int i = 0; i < 3; i++)
                {
                    _spriteBatch.Draw(
                        lineTex,
                        new Vector2(startX + i * (lineWidth + lineSpacing), lineY),
                        null,
                        Color.White,
                        0f,
                        Vector2.Zero,
                        new Vector2(1f, scaleY),
                        SpriteEffects.None,
                        0f);
                }
            }
            else
            {
                // Xbox View: two overlapping rounded rectangles
                int rectSize = (int)System.Math.Round(HintRadius * 1.1f);
                int cornerRadius = System.Math.Max(2, rectSize / 5);
                int offset = (int)System.Math.Round(rectSize * 0.3f);
                var rectTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, rectSize, rectSize, cornerRadius);
                _spriteBatch.Draw(
                    rectTex,
                    new Vector2(cx - rectSize / 2f - offset / 2f, cy - rectSize / 2f - offset / 2f),
                    new Color(140, 140, 140));
                _spriteBatch.Draw(
                    rectTex,
                    new Vector2(cx - rectSize / 2f + offset / 2f, cy - rectSize / 2f + offset / 2f),
                    Color.White);
            }
        }

        private void DrawPlayStationIcon(FaceButton b, int cx, int cy)
        {
            int size = (int)System.Math.Round(HintRadius * 1.5f);
            Texture2D tex;
            Color tint;
            switch (b)
            {
                case FaceButton.Y:
                    tex = PrimitiveTextureFactory.GetEquilateralTriangle(_graphicsDevice, size);
                    tint = new Color(50, 200, 90); // triangle = green
                    break;
                case FaceButton.B:
                    tex = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, System.Math.Max(2, size / 2));
                    tint = new Color(220, 60, 60); // circle = red
                    break;
                case FaceButton.X:
                    tex = PrimitiveTextureFactory.GetRoundedSquare(_graphicsDevice, size, System.Math.Max(2, size / 6));
                    tint = new Color(200, 80, 200); // square = magenta
                    break;
                default:
                    return;
            }
            var drawPos = new Vector2(cx - tex.Width / 2f, cy - tex.Height / 2f);
            _spriteBatch.Draw(tex, drawPos, tint);
        }

        private static string GetFaceButtonLetter(FaceButton b)
        {
            switch (b)
            {
                case FaceButton.B: return "B";
                case FaceButton.X: return "X";
                case FaceButton.Y: return "Y";
                default: return "?";
            }
        }

        private static Color GetFaceButtonColor(FaceButton b)
        {
            switch (b)
            {
                case FaceButton.B: return new Color(220, 50, 50);
                case FaceButton.X: return new Color(60, 120, 220);
                case FaceButton.Y: return new Color(220, 200, 60);
                default: return Color.White;
            }
        }

        private void DrawBumperBadge(int cx, int cy, string label, bool isPS)
        {
            int badgeWidth = (int)System.Math.Round(HintRadius * 2.5f);
            int badgeHeight = (int)System.Math.Round(HintRadius * 1.2f);
            int cornerRadius = System.Math.Max(2, badgeHeight / 4);
            var badgeTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, badgeWidth, badgeHeight, cornerRadius);
            var badgePos = new Vector2(cx - badgeWidth / 2f, cy - badgeHeight / 2f);

            string text = isPS ? (label == "LB" ? "L1" : "R1") : label;
            _spriteBatch.Draw(badgeTex, badgePos, new Color(36, 36, 36));
            var textSize = _font.MeasureString(text) * TextScale;
            var textPos = new Vector2(cx - textSize.X / 2f, cy - textSize.Y / 2f - 2f);
            _spriteBatch.DrawString(_font, text, textPos, Color.White, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
        }

        private void DrawKeyboardKey(int cx, int cy)
        {
            // Create a wide rounded rectangle for the spacebar key
            int keyHeight = (int)System.Math.Round(HintRadius * 1.2f);
            int keyWidth = (int)System.Math.Round(keyHeight * 3.5f); // Spacebar is typically 3-4x wider than tall
            int cornerRadius = System.Math.Max(2, keyHeight / 4);
            
            var keyTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, keyWidth, keyHeight, cornerRadius);
            
            // Draw the key background (light gray, keyboard-like)
            var keyPos = new Vector2(cx - keyWidth / 2f, cy - keyHeight / 2f);
            _spriteBatch.Draw(keyTex, keyPos, new Color(200, 200, 200)); // Light gray key
            
            // Draw "SPACE" text centered inside
            string text = "SPACE";
            var textSize = _font.MeasureString(text) * TextScale;
            var textPos = new Vector2(cx - textSize.X / 2f, cy - textSize.Y / 2f - 2f);
            _spriteBatch.DrawString(_font, text, textPos, Color.Black, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
        }

        private void DrawKeyboardEsc(int cx, int cy)
        {
            int keyHeight = (int)System.Math.Round(HintRadius * 1.2f);
            int keyWidth = (int)System.Math.Round(keyHeight * 1.5f);
            int cornerRadius = System.Math.Max(2, keyHeight / 4);

            var keyTex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, keyWidth, keyHeight, cornerRadius);
            var keyPos = new Vector2(cx - keyWidth / 2f, cy - keyHeight / 2f);
            _spriteBatch.Draw(keyTex, keyPos, new Color(200, 200, 200));

            string text = "ESC";
            var textSize = _font.MeasureString(text) * TextScale;
            var textPos = new Vector2(cx - textSize.X / 2f, cy - textSize.Y / 2f - 2f);
            _spriteBatch.DrawString(_font, text, textPos, Color.Black, 0f, Vector2.Zero, TextScale, SpriteEffects.None, 0f);
        }
    }
}
