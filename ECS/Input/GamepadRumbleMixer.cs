using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Input
{
    public sealed class GamepadRumbleMixer
    {
        private readonly Dictionary<string, (float Low, float High)> _channels = new();
        private readonly List<RumblePulse> _pulses = new();

        public void SetChannel(string channelId, float lowFrequency, float highFrequency)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return;

            _channels[channelId] = (
                MathHelper.Clamp(lowFrequency, 0f, 1f),
                MathHelper.Clamp(highFrequency, 0f, 1f));
        }

        public void ClearChannel(string channelId)
        {
            if (string.IsNullOrWhiteSpace(channelId)) return;
            _channels.Remove(channelId);
        }

        public void PlayPulse(
            float lowFrequency,
            float highFrequency,
            float durationSeconds,
            RumbleGroup group = RumbleGroup.Default)
        {
            if (durationSeconds <= 0f) return;

            _pulses.Add(new RumblePulse(
                MathHelper.Clamp(lowFrequency, 0f, 1f),
                MathHelper.Clamp(highFrequency, 0f, 1f),
                durationSeconds,
                group));
        }

        public void ClearGroup(RumbleGroup group)
        {
            _pulses.RemoveAll(pulse => pulse.Group == group);
        }

        public void Tick(float deltaSeconds)
        {
            if (deltaSeconds <= 0f) return;

            for (int i = _pulses.Count - 1; i >= 0; i--)
            {
                RumblePulse pulse = _pulses[i];
                pulse.RemainingSeconds -= deltaSeconds;
                if (pulse.RemainingSeconds <= 0f)
                {
                    _pulses.RemoveAt(i);
                }
                else
                {
                    _pulses[i] = pulse;
                }
            }
        }

        public (float Low, float High) Combine()
        {
            float low = 0f;
            float high = 0f;

            foreach ((float channelLow, float channelHigh) in _channels.Values)
            {
                low += channelLow;
                high += channelHigh;
            }

            foreach (RumblePulse pulse in _pulses)
            {
                low += pulse.LowFrequency;
                high += pulse.HighFrequency;
            }

            return (
                MathHelper.Clamp(low, 0f, 1f),
                MathHelper.Clamp(high, 0f, 1f));
        }

        private struct RumblePulse
        {
            public RumblePulse(
                float lowFrequency,
                float highFrequency,
                float durationSeconds,
                RumbleGroup group)
            {
                LowFrequency = lowFrequency;
                HighFrequency = highFrequency;
                RemainingSeconds = durationSeconds;
                Group = group;
            }

            public float LowFrequency { get; }
            public float HighFrequency { get; }
            public float RemainingSeconds { get; set; }
            public RumbleGroup Group { get; }
        }
    }
}
