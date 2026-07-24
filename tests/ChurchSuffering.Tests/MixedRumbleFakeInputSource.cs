using System;
using System.Collections.Generic;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Input;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.Tests;

internal class MixedRumbleFakeInputSource : IPlayerInputSource
{
    private readonly GamepadRumbleMixer _mixer = new();
    private readonly Queue<PlayerInputFrame> _frames;

    public MixedRumbleFakeInputSource(params PlayerInputFrame[] frames)
    {
        _frames = new Queue<PlayerInputFrame>(frames);
    }

	private int _rumbleLevel = SaveFile.DEFAULT_RUMBLE_LEVEL;

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
		if (_rumbleLevel <= 0) return;
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
		if (_rumbleLevel <= 0) return;
        _mixer.PlayPattern(pattern, group);
        ApplyMixedRumble();
    }

	public void ClearAllRumble()
	{
		_mixer.ClearAll();
		ApplyMixedRumble();
	}

	public void SetRumbleLevel(int level)
	{
		int clamped = Math.Clamp(level, 0, 100);
		_rumbleLevel = clamped;
		if (_rumbleLevel <= 0) _mixer.ClearAll();
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
		RumbleMotorState motors = _rumbleLevel <= 0
			? RumbleMotorState.Zero
			: _mixer.Combine().Scaled(_rumbleLevel / 100f);
        VibrationCalls.Add((motors.LowFrequency, motors.HighFrequency, motors.LeftTrigger, motors.RightTrigger));
    }
}
