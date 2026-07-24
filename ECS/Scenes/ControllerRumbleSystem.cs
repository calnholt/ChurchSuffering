using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Input;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    [DebugTab("Controller Rumble")]
    public class ControllerRumbleSystem : Core.System
    {
        private readonly IPlayerInputSource _inputSource;
		private PlayerInputFrame _latestFrame;
		private int _level;

        [DebugEditable(DisplayName = "Rumble Duration (s)", Step = 0.01f, Min = 0f, Max = 1f)]
        public float RumbleDurationSeconds { get; set; } = 0.04f;

        [DebugEditable(DisplayName = "Rumble Low Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RumbleLow { get; set; } = 0.3f;

        [DebugEditable(DisplayName = "Rumble High Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RumbleHigh { get; set; } = 0.2f;

        public ControllerRumbleSystem(
			EntityManager entityManager,
			IPlayerInputSource inputSource,
			int rumbleLevel = 50)
            : base(entityManager)
        {
            _inputSource = inputSource;
			_level = MathHelper.Clamp(rumbleLevel, 0, 100);
			_inputSource.SetRumbleLevel(_level);
            EventManager.Subscribe<UIElementHoverEnteredEvent>(OnUIElementHoverEntered);
            EventManager.Subscribe<PlayerInputEvent>(OnPlayerInput);
			EventManager.Subscribe<RumbleRequested>(OnRumbleRequested);
			EventManager.Subscribe<AchievementCompletedEvent>(_ => PlayProfile(
				RumbleProfile.AchievementUnlock,
				1f,
				RumbleGroup.Achievement));
			EventManager.Subscribe<RumbleSettingsChangedEvent>(OnRumbleSettingsChanged);
			EventManager.Subscribe<RumbleGroupCleared>(evt => _inputSource.ClearRumbleGroup(evt.Group));
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }

        private void OnUIElementHoverEntered(UIElementHoverEnteredEvent e)
        {
            if (e.Source != PlayerInputDevice.Gamepad) return;
            if (EntityManager.GetEntity("BoosterPackOpeningOverlay")
                ?.GetComponent<BoosterPackOpeningOverlayState>()?.IsOpen == true) return;
            if (e.Entity?.GetComponent<UIElement>()?.IsInteractable != true) return;

			var pattern = new RumblePattern(new RumbleSegment(
				new RumbleMotorState(RumbleLow, RumbleHigh),
				RumbleMotorState.Zero,
				RumbleDurationSeconds));
			_inputSource.PlayRumblePattern(pattern, RumbleGroup.UiHover);
        }

        private void OnPlayerInput(PlayerInputEvent e)
        {
			_latestFrame = e.Frame;
			if (!e.Frame.IsWindowActive || !e.Frame.IsGamepadConnected)
			{
				_inputSource.ClearAllRumble();
				return;
			}
			if (e.Frame.Device == PlayerInputDevice.Gamepad) return;

            _inputSource.ClearRumbleGroup(RumbleGroup.UiHover);
			_inputSource.ClearRumbleGroup(RumbleGroup.HotKeyHold);
        }

		private void OnRumbleRequested(RumbleRequested request)
		{
			if (request == null) return;
			PlayProfile(request.Profile, request.Scale, request.Group);
		}

		private void PlayProfile(RumbleProfile profile, float scale, RumbleGroup group)
		{
			if (_level <= 0
				|| !_latestFrame.IsWindowActive
				|| !_latestFrame.IsGamepadConnected
				|| profile == RumbleProfile.None)
			{
				return;
			}

			RumblePattern pattern = RumblePatternCatalog.Resolve(profile).Scaled(scale);
			_inputSource.PlayRumblePattern(pattern, group);
		}

		private void OnRumbleSettingsChanged(RumbleSettingsChangedEvent evt)
		{
			_level = MathHelper.Clamp(evt?.Level ?? 0, 0, 100);
			_inputSource.SetRumbleLevel(_level);
			if (_level <= 0) _inputSource.ClearAllRumble();
		}
    }
}
