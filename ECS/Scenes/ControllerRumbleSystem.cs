using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
    [DebugTab("Controller Rumble")]
    public class ControllerRumbleSystem : Core.System
    {
        private readonly IPlayerInputSource _inputSource;
        private float _rumbleTimeRemaining;

        [DebugEditable(DisplayName = "Rumble Duration (s)", Step = 0.01f, Min = 0f, Max = 1f)]
        public float RumbleDurationSeconds { get; set; } = 0.04f;

        [DebugEditable(DisplayName = "Rumble Low Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RumbleLow { get; set; } = 0.3f;

        [DebugEditable(DisplayName = "Rumble High Intensity", Step = 0.05f, Min = 0f, Max = 1f)]
        public float RumbleHigh { get; set; } = 0.2f;

        public ControllerRumbleSystem(EntityManager entityManager, IPlayerInputSource inputSource)
            : base(entityManager)
        {
            _inputSource = inputSource;
            EventManager.Subscribe<UIElementHoverEnteredEvent>(OnUIElementHoverEntered);
            EventManager.Subscribe<PlayerInputEvent>(OnPlayerInput);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return EntityManager.GetEntitiesWithComponent<SceneState>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
            if (_rumbleTimeRemaining <= 0f) return;

            _rumbleTimeRemaining -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_rumbleTimeRemaining <= 0f)
            {
                _inputSource.SetVibration(0f, 0f);
            }
        }

        private void OnUIElementHoverEntered(UIElementHoverEnteredEvent e)
        {
            if (e.Source != PlayerInputDevice.Gamepad) return;
            if (e.Entity?.GetComponent<UIElement>()?.IsInteractable != true) return;

            _rumbleTimeRemaining = RumbleDurationSeconds;
            _inputSource.SetVibration(RumbleLow, RumbleHigh);
        }

        private void OnPlayerInput(PlayerInputEvent e)
        {
            if (e.Frame.Device == PlayerInputDevice.Gamepad && e.Frame.IsWindowActive) return;

            StopRumble();
        }

        private void StopRumble()
        {
            if (_rumbleTimeRemaining <= 0f) return;

            _rumbleTimeRemaining = 0f;
            _inputSource.SetVibration(0f, 0f);
        }
    }
}
