using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Input
{
    public sealed class HotKeyHoldTracker
    {
        private readonly Dictionary<Entity, float> _progress = new();
        private readonly Dictionary<Entity, FaceButton> _buttons = new();

        public IReadOnlyDictionary<Entity, float> Progress => _progress;

        public void Start(Entity entity, FaceButton button)
        {
            if (entity == null || _progress.ContainsKey(entity)) return;
            _progress[entity] = 0f;
            _buttons[entity] = button;
        }

        public FaceButton GetButton(Entity entity)
        {
            return _buttons[entity];
        }

        public bool Advance(
            Entity entity,
            float elapsedSeconds,
            float durationSeconds,
            bool isEligible)
        {
            if (!_progress.ContainsKey(entity)) return false;
            if (!isEligible)
            {
                Cancel(entity);
                return false;
            }

            _progress[entity] += elapsedSeconds;
            if (_progress[entity] < durationSeconds) return false;
            Cancel(entity);
            return true;
        }

        public void Cancel(Entity entity)
        {
            _progress.Remove(entity);
            _buttons.Remove(entity);
        }
    }
}
