using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace ChurchSuffering.ECS.Input
{
    public sealed class MonoGamePlayerInputAdapter : IPlayerInputSource
    {
        private readonly GamepadRumbleMixer _rumbleMixer = new();
        private KeyboardState _previousKeyboard;
        private MouseState _previousMouse;
        private GamePadState _previousGamepad;
        private PlayerInputDevice _device = PlayerInputDevice.KeyboardMouse;
        private long _sequence;
		private bool _rumbleEnabled = true;

        public MonoGamePlayerInputAdapter()
        {
            _previousKeyboard = Keyboard.GetState();
            _previousMouse = Mouse.GetState();
            _previousGamepad = GamePad.GetState(PlayerIndex.One);
        }

        public PlayerInputFrame Capture(
            bool isWindowActive,
            Rectangle renderDestination,
            int virtualWidth,
            int virtualHeight)
        {
            var keyboard = Keyboard.GetState();
            var mouse = Mouse.GetState();
            var gamepad = GamePad.GetState(PlayerIndex.One);
            var previousDevice = _device;

            bool keyboardActivity = HasKeyboardActivity(keyboard)
                || mouse.Position != _previousMouse.Position
                || mouse.LeftButton != _previousMouse.LeftButton
                || mouse.RightButton != _previousMouse.RightButton
                || mouse.ScrollWheelValue != _previousMouse.ScrollWheelValue;
            bool gamepadActivity = gamepad.IsConnected && HasGamepadActivity(gamepad);

            if (gamepadActivity)
            {
                _device = PlayerInputDevice.Gamepad;
            }
            else if (keyboardActivity || !gamepad.IsConnected)
            {
                _device = PlayerInputDevice.KeyboardMouse;
            }

            var down = BuildDownMask(keyboard, mouse, gamepad);
            var previousDown = BuildDownMask(_previousKeyboard, _previousMouse, _previousGamepad);
            var pressed = down & ~previousDown;
            var released = previousDown & ~down;
            var pointer = ToVirtualPosition(mouse.Position, renderDestination, virtualWidth, virtualHeight);
            var previousPointer = ToVirtualPosition(_previousMouse.Position, renderDestination, virtualWidth, virtualHeight);
            int scrollRaw = mouse.ScrollWheelValue - _previousMouse.ScrollWheelValue;

            var capabilities = GamePad.GetCapabilities(PlayerIndex.One);
            string controllerName = (capabilities.DisplayName ?? string.Empty)
                + " "
                + (capabilities.Identifier ?? string.Empty);

            var frame = new PlayerInputFrame(
                ++_sequence,
                isWindowActive,
                gamepad.IsConnected,
                _device,
                previousDevice,
                IsPlayStation(controllerName) ? GamepadGlyphStyle.PlayStation : GamepadGlyphStyle.Xbox,
                pointer,
                pointer - previousPointer,
                scrollRaw == 0 ? 0f : Math.Sign(scrollRaw),
                gamepad.IsConnected ? gamepad.ThumbSticks.Left : Vector2.Zero,
                gamepad.IsConnected ? gamepad.ThumbSticks.Right : Vector2.Zero,
                gamepad.IsConnected ? gamepad.Triggers.Left : 0f,
                gamepad.IsConnected ? gamepad.Triggers.Right : 0f,
                isWindowActive ? down : PlayerButtonMask.None,
                isWindowActive ? pressed : PlayerButtonMask.None,
                isWindowActive ? released : PlayerButtonMask.None);

            _previousKeyboard = keyboard;
            _previousMouse = mouse;
            _previousGamepad = gamepad;
            return frame;
        }

        public void SetRumbleChannel(string channelId, RumbleMotorState motors)
        {
            if (!_rumbleEnabled) return;
            _rumbleMixer.SetChannel(channelId, motors);
            ApplyMixedRumble();
        }

        public void ClearRumbleChannel(string channelId)
        {
            _rumbleMixer.ClearChannel(channelId);
            ApplyMixedRumble();
        }

        public void PlayRumblePattern(RumblePattern pattern, RumbleGroup group = RumbleGroup.Default)
        {
            if (!_rumbleEnabled) return;
            _rumbleMixer.PlayPattern(pattern, group);
            ApplyMixedRumble();
        }

        public void ClearRumbleGroup(RumbleGroup group)
        {
            _rumbleMixer.ClearGroup(group);
            ApplyMixedRumble();
        }

		public void ClearAllRumble()
		{
			_rumbleMixer.ClearAll();
			ApplyMixedRumble();
		}

		public void SetRumbleEnabled(bool enabled)
		{
			if (_rumbleEnabled == enabled) return;
			_rumbleEnabled = enabled;
			if (!enabled) _rumbleMixer.ClearAll();
			ApplyMixedRumble();
		}

        public void TickRumble(float deltaSeconds)
        {
            _rumbleMixer.Tick(deltaSeconds);
            ApplyMixedRumble();
        }

        private void ApplyMixedRumble()
        {
			RumbleMotorState motors = _rumbleEnabled
				? _rumbleMixer.Combine()
				: RumbleMotorState.Zero;
            GamePad.SetVibration(
				PlayerIndex.One,
				motors.LowFrequency,
				motors.HighFrequency,
				motors.LeftTrigger,
				motors.RightTrigger);
        }

        private bool HasKeyboardActivity(KeyboardState keyboard)
        {
            return !keyboard.Equals(_previousKeyboard);
        }

        private bool HasGamepadActivity(GamePadState gamepad)
        {
            if (!gamepad.Equals(_previousGamepad))
            {
                return true;
            }

            return gamepad.ThumbSticks.Left.LengthSquared() > 0.01f
                || gamepad.ThumbSticks.Right.LengthSquared() > 0.01f
                || gamepad.Triggers.Left > 0.05f
                || gamepad.Triggers.Right > 0.05f;
        }

        private static PlayerButtonMask BuildDownMask(
            KeyboardState keyboard,
            MouseState mouse,
            GamePadState gamepad)
        {
            PlayerButtonMask mask = PlayerButtonMask.None;
            Add(ref mask, PlayerButton.Primary,
                mouse.LeftButton == ButtonState.Pressed
                || gamepad.Buttons.A == ButtonState.Pressed);
            Add(ref mask, PlayerButton.Secondary,
                mouse.RightButton == ButtonState.Pressed
                || gamepad.Buttons.X == ButtonState.Pressed);
            Add(ref mask, PlayerButton.Cancel,
                keyboard.IsKeyDown(Keys.Escape)
                || gamepad.Buttons.Back == ButtonState.Pressed
                || gamepad.Buttons.B == ButtonState.Pressed);
            Add(ref mask, PlayerButton.Escape, keyboard.IsKeyDown(Keys.Escape));
            Add(ref mask, PlayerButton.Back, gamepad.Buttons.Back == ButtonState.Pressed);
            Add(ref mask, PlayerButton.FaceB, gamepad.Buttons.B == ButtonState.Pressed);
            Add(ref mask, PlayerButton.FaceX, gamepad.Buttons.X == ButtonState.Pressed);
            Add(ref mask, PlayerButton.FaceY, gamepad.Buttons.Y == ButtonState.Pressed);
            Add(ref mask, PlayerButton.Start, gamepad.Buttons.Start == ButtonState.Pressed);
            Add(ref mask, PlayerButton.LeftShoulder, gamepad.Buttons.LeftShoulder == ButtonState.Pressed);
            Add(ref mask, PlayerButton.RightShoulder, gamepad.Buttons.RightShoulder == ButtonState.Pressed);
            Add(ref mask, PlayerButton.LeftStick, gamepad.Buttons.LeftStick == ButtonState.Pressed);
            Add(ref mask, PlayerButton.Space, keyboard.IsKeyDown(Keys.Space));
            Add(ref mask, PlayerButton.Enter, keyboard.IsKeyDown(Keys.Enter));
            Add(ref mask, PlayerButton.Shift, keyboard.IsKeyDown(Keys.LeftShift) || keyboard.IsKeyDown(Keys.RightShift));
            Add(ref mask, PlayerButton.MoveUp, keyboard.IsKeyDown(Keys.W));
            Add(ref mask, PlayerButton.MoveDown, keyboard.IsKeyDown(Keys.S));
            Add(ref mask, PlayerButton.MoveLeft, keyboard.IsKeyDown(Keys.A));
            Add(ref mask, PlayerButton.MoveRight, keyboard.IsKeyDown(Keys.D));
            Add(ref mask, PlayerButton.F11, keyboard.IsKeyDown(Keys.F11));
            Add(ref mask, PlayerButton.DebugMenu, keyboard.IsKeyDown(Keys.D));
            Add(ref mask, PlayerButton.EntityList, keyboard.IsKeyDown(Keys.W));
            Add(ref mask, PlayerButton.DebugDamage, keyboard.IsKeyDown(Keys.K));
            Add(ref mask, PlayerButton.Profiler, keyboard.IsKeyDown(Keys.P));
            Add(ref mask, PlayerButton.Quit, keyboard.IsKeyDown(Keys.Escape));
            return mask;
        }

        private static void Add(ref PlayerButtonMask mask, PlayerButton button, bool isDown)
        {
            if (isDown)
            {
                mask |= PlayerInputFrame.Mask(button);
            }
        }

        private static Vector2 ToVirtualPosition(
            Point screenPosition,
            Rectangle destination,
            int virtualWidth,
            int virtualHeight)
        {
            float scaleX = destination.Width > 0 ? (float)destination.Width / virtualWidth : 1f;
            float scaleY = destination.Height > 0 ? (float)destination.Height / virtualHeight : 1f;
            float x = (screenPosition.X - destination.X) / Math.Max(0.001f, scaleX);
            float y = (screenPosition.Y - destination.Y) / Math.Max(0.001f, scaleY);
            return new Vector2(
                MathHelper.Clamp(x, 0f, virtualWidth),
                MathHelper.Clamp(y, 0f, virtualHeight));
        }

        private static bool IsPlayStation(string controllerName)
        {
            string name = controllerName.ToLowerInvariant();
            return name.Contains("dualshock")
                || name.Contains("dualsense")
                || name.Contains("playstation")
                || name.Contains("sony")
                || name.Contains("wireless controller")
                || name.Contains("ps4")
                || name.Contains("ps5");
        }
    }
}
