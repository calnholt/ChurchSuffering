using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Input
{
    public sealed class GamepadRumbleMixer
    {
        private readonly Dictionary<string, RumbleMotorState> _channels = new();
        private readonly List<ActiveRumblePattern> _patterns = new();

        public void SetChannel(string channelId, RumbleMotorState motors)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return;
            _channels[channelId] = motors.Clamped();
        }

        public void ClearChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return;
            _channels.Remove(channelId);
        }

        public void PlayPattern(RumblePattern pattern, RumbleGroup group = RumbleGroup.Default)
        {
            if (pattern == null || pattern.DurationSeconds <= 0f) return;
            _patterns.Add(new ActiveRumblePattern(pattern, group));
        }

        public void ClearGroup(RumbleGroup group)
        {
            _patterns.RemoveAll(pattern => pattern.Group == group);
        }

		public void ClearAll()
		{
			_channels.Clear();
			_patterns.Clear();
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            for (int i = _patterns.Count - 1; i >= 0; i--)
            {
                ActiveRumblePattern pattern = _patterns[i];
                pattern.ElapsedSeconds += deltaSeconds;
                if (pattern.ElapsedSeconds >= pattern.Pattern.DurationSeconds)
                {
                    _patterns.RemoveAt(i);
                }
                else
                {
                    _patterns[i] = pattern;
                }
            }
        }

        public RumbleMotorState Combine()
        {
            RumbleMotorState combined = RumbleMotorState.Zero;

            foreach (RumbleMotorState channel in _channels.Values)
            {
                combined = RumbleMotorState.Add(combined, channel);
            }

            foreach (ActiveRumblePattern pattern in _patterns)
            {
                combined = RumbleMotorState.Add(combined, pattern.Pattern.Sample(pattern.ElapsedSeconds));
            }

            return combined.Clamped();
        }

        private struct ActiveRumblePattern
        {
            public ActiveRumblePattern(RumblePattern pattern, RumbleGroup group)
            {
                Pattern = pattern;
                ElapsedSeconds = 0f;
                Group = group;
            }

            public RumblePattern Pattern { get; }
            public float ElapsedSeconds { get; set; }
            public RumbleGroup Group { get; }
        }
    }
}
