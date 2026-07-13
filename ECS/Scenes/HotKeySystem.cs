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
        private readonly SpriteFont _font;
        private readonly HotKeyGlyphRenderer _glyphRenderer;
        private readonly HotKeyHoldTracker _holdTracker = new();
		private readonly Dictionary<Entity, int> _holdTickIndices = new();
		private readonly HashSet<Entity> _gamepadHolds = new();
		private readonly Dictionary<Entity, PlayerInputDevice> _holdDevices = new();

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
            _font = FontSingleton.ChakraPetchFont;
            _glyphRenderer = gd == null ? null : new HotKeyGlyphRenderer(gd, sb, _font);
            EventManager.Subscribe<HotKeyHoldCompletedEvent>(OnHotKeyHoldCompleted);
            EventManager.Subscribe<DeleteCachesEvent>(_ => _glyphRenderer?.Dispose());
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
					ClearHoldHaptics(heldEntity, clearActivePattern: true);
                }
                return;
            }

            PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
            string contextId = InputContextResolver.ResolveCommandContext(EntityManager);
            bool gameplayBlocked = StateSingleton.PreventClicking
                && contextId == InputContextIds.Gameplay;
            Entity target = FindPressedTarget(frame, contextId, gameplayBlocked);
            if (target != null)
            {
                HotKey hotKey = target.GetComponent<HotKey>();
                if (hotKey.RequiresHold)
                {
                    _holdTracker.Start(target, hotKey.Button);
					_holdDevices[target] = frame.Device;
					_holdTickIndices[target] = 0;
					if (frame.Device == PlayerInputDevice.Gamepad) _gamepadHolds.Add(target);
                }
                else
                {
                    ProcessHotKeyClick(target);
                }
            }
            else if (WasBindingPressed(frame, FaceButton.X, PlayerButton.Space))
            {
                ProcessHoveredSecondaryAction(contextId);
            }

            UpdateHolds(frame, contextId, gameplayBlocked, (float)gameTime.ElapsedGameTime.TotalSeconds);
        }

        private Entity FindPressedTarget(PlayerInputFrame frame, string contextId, bool gameplayBlocked)
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
                    && IsHotKeyEligible(item.Entity, item.HotKey, item.UI, contextId, gameplayBlocked)
                    && IsBindingPressed(frame, item.HotKey))
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
                PlayerInputDevice holdDevice = _holdDevices.TryGetValue(heldEntity, out PlayerInputDevice device)
                    ? device
                    : frame.Device;
                bool eligible = hotKey != null
                    && IsHotKeyEligible(heldEntity, hotKey, ui, contextId, gameplayBlocked)
					&& frame.Device == holdDevice
                    && IsBindingDown(frame, hotKey, holdDevice);
				float duration = Math.Max(0.001f, hotKey?.HoldDurationSeconds ?? 0f);
				float previousElapsed = _holdTracker.Progress.TryGetValue(heldEntity, out float current)
					? current
					: 0f;
				bool completed = eligible && previousElapsed + Math.Max(0f, elapsed) >= duration;
				if (_holdTracker.Advance(heldEntity, elapsed, duration, eligible))
                {
					if (_gamepadHolds.Contains(heldEntity))
					{
						PublishHoldRumble(RumbleProfile.HotKeyComplete);
					}
					ClearHoldHaptics(heldEntity, clearActivePattern: false);
                    EventManager.Publish(new HotKeyHoldCompletedEvent { Entity = heldEntity });
                }
				else if (!eligible)
				{
					ClearHoldHaptics(heldEntity, clearActivePattern: true);
				}
				else if (!completed && _gamepadHolds.Contains(heldEntity))
				{
					PublishCrossedHoldTick(heldEntity, (previousElapsed + Math.Max(0f, elapsed)) / duration);
				}
            }
        }

		private void PublishCrossedHoldTick(Entity entity, float progress)
		{
			int crossed = progress >= 0.75f ? 3 : progress >= 0.50f ? 2 : progress >= 0.25f ? 1 : 0;
			int previous = _holdTickIndices.TryGetValue(entity, out int value) ? value : 0;
			if (crossed <= previous) return;
			_holdTickIndices[entity] = crossed;
			PublishHoldRumble(crossed switch
			{
				1 => RumbleProfile.HotKeyTick25,
				2 => RumbleProfile.HotKeyTick50,
				_ => RumbleProfile.HotKeyTick75,
			});
		}

		private static void PublishHoldRumble(RumbleProfile profile)
		{
			EventManager.Publish(new RumbleRequested
			{
				Profile = profile,
				Group = RumbleGroup.HotKeyHold,
			});
		}

		private void ClearHoldHaptics(Entity entity, bool clearActivePattern)
		{
			_holdTickIndices.Remove(entity);
			_gamepadHolds.Remove(entity);
			_holdDevices.Remove(entity);
			if (clearActivePattern)
			{
				EventManager.Publish(new RumbleGroupCleared { Group = RumbleGroup.HotKeyHold });
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

        internal static PlayerButton? ResolveKeyboardButton(HotKey hotKey)
        {
            if (hotKey?.IsKeyboardMouseEnabled != true) return null;
            if (hotKey?.KeyboardButton != null) return hotKey.KeyboardButton;
            return hotKey?.Button switch
            {
                FaceButton.X => PlayerButton.Space,
                FaceButton.B or FaceButton.View => PlayerButton.Escape,
                _ => null,
            };
        }

        internal static bool IsBindingPressed(PlayerInputFrame frame, HotKey hotKey)
        {
            if (hotKey == null) return false;
            if (frame.Device == PlayerInputDevice.KeyboardMouse)
            {
                PlayerButton? keyboard = ResolveKeyboardButton(hotKey);
                return keyboard.HasValue && frame.WasPressed(keyboard.Value);
            }
            return frame.WasPressed(ToPlayerButton(hotKey.Button));
        }

        internal static bool WasBindingPressed(PlayerInputFrame frame, FaceButton gamepad, PlayerButton keyboard)
        {
            return frame.Device == PlayerInputDevice.KeyboardMouse
                ? frame.WasPressed(keyboard)
                : frame.WasPressed(ToPlayerButton(gamepad));
        }

        private static bool IsBindingDown(PlayerInputFrame frame, HotKey hotKey, PlayerInputDevice device)
        {
            if (device == PlayerInputDevice.KeyboardMouse)
            {
                PlayerButton? keyboard = ResolveKeyboardButton(hotKey);
                return keyboard.HasValue && frame.IsDown(keyboard.Value);
            }
            return frame.IsDown(ToPlayerButton(hotKey.Button));
        }

        private static PlayerButton ToPlayerButton(FaceButton button)
        {
            return button switch
            {
                FaceButton.B => PlayerButton.FaceB,
                FaceButton.X => PlayerButton.FaceX,
                FaceButton.Y => PlayerButton.FaceY,
                FaceButton.View => PlayerButton.Back,
                FaceButton.Start => PlayerButton.Start,
                FaceButton.LB => PlayerButton.LeftShoulder,
                FaceButton.RB => PlayerButton.RightShoulder,
                _ => throw new ArgumentOutOfRangeException(nameof(button)),
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

        public (int cx, int cy) CalculateHintPosition(Rectangle bounds, HotKeyPosition position, Point hintSize, int hintGapX, int hintGapY)
        {
            int cx, cy;
            switch (position)
            {
                case HotKeyPosition.Below:
                    cx = bounds.Center.X;
                    cy = bounds.Bottom + Math.Max(-64, hintGapY) + hintSize.Y / 2;
                    break;
                case HotKeyPosition.Top:
                    cx = bounds.Center.X;
                    cy = bounds.Top - Math.Max(-64, hintGapY) - hintSize.Y / 2;
                    break;
                case HotKeyPosition.Right:
                    cx = bounds.Right + Math.Max(-64, hintGapX) + hintSize.X / 2;
                    cy = bounds.Center.Y;
                    break;
                case HotKeyPosition.Left:
                    cx = bounds.Left - Math.Max(-64, hintGapX) - hintSize.X / 2;
                    cy = bounds.Center.Y;
                    break;
                default:
                    cx = bounds.Center.X;
                    cy = bounds.Bottom + Math.Max(-64, hintGapY) + hintSize.Y / 2;
                    break;
            }
            return (cx, cy);
        }

		internal Point GetHintSize(PlayerInputFrame frame, HotKey hotKey)
		{
			if (frame.Device == PlayerInputDevice.KeyboardMouse)
			{
				PlayerButton? keyboard = ResolveKeyboardButton(hotKey);
				return keyboard.HasValue
					? _glyphRenderer.MeasureKeyboard(keyboard.Value, HintRadius, TextScale)
					: Point.Zero;
			}
			return _glyphRenderer.MeasureGamepad(hotKey.Button, HintRadius);
		}

        public void Draw()
        {
            if (GameOverOverlayDisplaySystem.IsOverlayActive(EntityManager)) return;

            PlayerInputFrame frame = PlayerInputService.GetFrame(EntityManager);
            string contextId = InputContextResolver.ResolveCommandContext(EntityManager);
            bool gameplayBlocked = StateSingleton.PreventClicking
                && contextId == InputContextIds.Gameplay;

            var items = EntityManager.GetEntitiesWithComponent<HotKey>()
                .Select(e => new { E = e, HK = e.GetComponent<HotKey>(), UI = e.GetComponent<UIElement>(), T = e.GetComponent<Transform>() })
                .Where(x => x.HK != null
                    && IsHotKeyEligible(x.E, x.HK, x.UI, contextId, gameplayBlocked))
                .OrderByDescending(x => x.T?.ZOrder ?? 0)
                .ToList();

            foreach (var x in items)
            {
                var r = x.UI.Bounds;
                if (r.Width < 2 || r.Height < 2) continue;
                if (frame.Device == PlayerInputDevice.KeyboardMouse)
                {
                    PlayerButton? keyboard = ResolveKeyboardButton(x.HK);
                    if (!keyboard.HasValue) continue;
                    Point size = _glyphRenderer.MeasureKeyboard(keyboard.Value, HintRadius, TextScale);
                    var (cx, cy) = CalculateHintPosition(r, x.HK.Position, size, HintGapX, HintGapY);
                    _glyphRenderer.DrawKeyboard(keyboard.Value, cx, cy, HintRadius, TextScale);
                    continue;
                }

                Point gamepadSize = _glyphRenderer.MeasureGamepad(x.HK.Button, HintRadius);
                var (gamepadCx, gamepadCy) = CalculateHintPosition(r, x.HK.Position, gamepadSize, HintGapX, HintGapY);
                _glyphRenderer.DrawGamepad(x.HK.Button, frame.GamepadGlyphStyle, gamepadCx, gamepadCy, HintRadius, TextScale);
            }
        }
    }
}
