using System.Collections.Generic;
using Crusaders30XX.ECS.Input;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.Tests;

internal class MixedRumbleFakeInputSource : IPlayerInputSource
{
    private readonly GamepadRumbleMixer _mixer = new();
    private readonly Queue<PlayerInputFrame> _frames;

    public MixedRumbleFakeInputSource(params PlayerInputFrame[] frames)
    {
        _frames = new Queue<PlayerInputFrame>(frames);
    }

    public List<(float Low, float High)> VibrationCalls { get; } = new();

    public PlayerInputFrame Capture(
        bool isWindowActive,
        Rectangle renderDestination,
        int virtualWidth,
        int virtualHeight)
    {
        return _frames.Dequeue();
    }

    public void SetRumbleChannel(string channelId, float lowFrequency, float highFrequency)
    {
        _mixer.SetChannel(channelId, lowFrequency, highFrequency);
        ApplyMixedRumble();
    }

    public void ClearRumbleChannel(string channelId)
    {
        _mixer.ClearChannel(channelId);
        ApplyMixedRumble();
    }

    public void PlayRumblePulse(
        float lowFrequency,
        float highFrequency,
        float durationSeconds,
        RumbleGroup group = RumbleGroup.Default)
    {
        _mixer.PlayPulse(lowFrequency, highFrequency, durationSeconds, group);
        ApplyMixedRumble();
    }

    public void ClearRumbleGroup(RumbleGroup group)
    {
        _mixer.ClearGroup(group);
        ApplyMixedRumble();
    }

    public void TickRumble(float deltaSeconds)
    {
        _mixer.Tick(deltaSeconds);
        ApplyMixedRumble();
    }

    private void ApplyMixedRumble()
    {
        (float low, float high) = _mixer.Combine();
        VibrationCalls.Add((low, high));
    }
}
