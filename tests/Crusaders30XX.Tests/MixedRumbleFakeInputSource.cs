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

	private bool _rumbleEnabled = true;

    public List<(float Low, float High, float LeftTrigger, float RightTrigger)> VibrationCalls { get; } = new();

    public PlayerInputFrame Capture(
        bool isWindowActive,
        Rectangle renderDestination,
        int virtualWidth,
        int virtualHeight)
    {
        return _frames.Dequeue();
    }

    public void SetRumbleChannel(string channelId, RumbleMotorState motors)
    {
		if (!_rumbleEnabled) return;
        _mixer.SetChannel(channelId, motors);
        ApplyMixedRumble();
    }

    public void ClearRumbleChannel(string channelId)
    {
        _mixer.ClearChannel(channelId);
        ApplyMixedRumble();
    }

    public void PlayRumblePattern(RumblePattern pattern, RumbleGroup group = RumbleGroup.Default)
    {
		if (!_rumbleEnabled) return;
        _mixer.PlayPattern(pattern, group);
        ApplyMixedRumble();
    }

	public void ClearAllRumble()
	{
		_mixer.ClearAll();
		ApplyMixedRumble();
	}

	public void SetRumbleEnabled(bool enabled)
	{
		_rumbleEnabled = enabled;
		if (!enabled) _mixer.ClearAll();
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
		RumbleMotorState motors = _rumbleEnabled ? _mixer.Combine() : RumbleMotorState.Zero;
        VibrationCalls.Add((motors.LowFrequency, motors.HighFrequency, motors.LeftTrigger, motors.RightTrigger));
    }
}
