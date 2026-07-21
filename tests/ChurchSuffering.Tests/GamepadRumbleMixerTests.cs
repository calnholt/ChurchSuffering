using ChurchSuffering.ECS.Input;
using Xunit;

namespace ChurchSuffering.Tests;

public class GamepadRumbleMixerTests
{
    [Fact]
    public void Overlapping_pulses_combine_with_sum_clamp()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.PlayPattern(Constant(0.4f, 0.3f, 1f));
        mixer.PlayPattern(Constant(0.5f, 0.4f, 1f));

		RumbleMotorState motors = mixer.Combine();

		Assert.Equal(0.9f, motors.LowFrequency, 3);
		Assert.Equal(0.7f, motors.HighFrequency, 3);
    }

    [Fact]
    public void Combined_output_clamps_each_motor_to_one()
    {
        var mixer = new GamepadRumbleMixer();
		mixer.PlayPattern(Constant(0.8f, 0.9f, 1f, 0.8f, 0.7f));
		mixer.PlayPattern(Constant(0.5f, 0.5f, 1f, 0.5f, 0.6f));

		RumbleMotorState motors = mixer.Combine();

		Assert.Equal(1f, motors.LowFrequency);
		Assert.Equal(1f, motors.HighFrequency);
		Assert.Equal(1f, motors.LeftTrigger);
		Assert.Equal(1f, motors.RightTrigger);
    }

    [Fact]
    public void Expired_pulse_leaves_other_pulse_active()
    {
        var mixer = new GamepadRumbleMixer();
		mixer.PlayPattern(Constant(0.2f, 0.1f, 0.05f));
		mixer.PlayPattern(Constant(0.4f, 0.3f, 1f));

        mixer.Tick(0.06f);

		RumbleMotorState motors = mixer.Combine();

		Assert.Equal(0.4f, motors.LowFrequency);
		Assert.Equal(0.3f, motors.HighFrequency);
    }

    [Fact]
    public void Channel_and_pulse_combine_additively()
    {
        var mixer = new GamepadRumbleMixer();
		mixer.SetChannel("booster-pack-opening", new RumbleMotorState(0.35f, 0.55f, 0.1f, 0.1f));
		mixer.PlayPattern(Constant(0.3f, 0.2f, 1f, 0.2f, 0.3f), RumbleGroup.UiHover);

		RumbleMotorState motors = mixer.Combine();

		Assert.Equal(0.65f, motors.LowFrequency, 3);
		Assert.Equal(0.75f, motors.HighFrequency, 3);
		Assert.Equal(0.3f, motors.LeftTrigger, 3);
		Assert.Equal(0.4f, motors.RightTrigger, 3);
    }

    [Fact]
    public void ClearRumbleGroup_removes_only_matching_pulses()
    {
        var mixer = new GamepadRumbleMixer();
		mixer.SetChannel("booster-pack-opening", new RumbleMotorState(0.35f, 0.55f));
		mixer.PlayPattern(Constant(0.3f, 0.2f, 1f), RumbleGroup.UiHover);
		mixer.PlayPattern(Constant(0.1f, 0.1f, 1f), RumbleGroup.Default);

        mixer.ClearGroup(RumbleGroup.UiHover);

		RumbleMotorState motors = mixer.Combine();

		Assert.Equal(0.45f, motors.LowFrequency, 3);
		Assert.Equal(0.65f, motors.HighFrequency, 3);
    }

    [Fact]
    public void ClearRumbleChannel_removes_only_that_channel()
    {
        var mixer = new GamepadRumbleMixer();
		mixer.SetChannel("booster-pack-opening", new RumbleMotorState(0.35f, 0.55f));
		mixer.SetChannel("other", new RumbleMotorState(0.1f, 0.1f));
		mixer.PlayPattern(Constant(0.3f, 0.2f, 1f));

        mixer.ClearChannel("booster-pack-opening");

		RumbleMotorState motors = mixer.Combine();

		Assert.Equal(0.4f, motors.LowFrequency, 3);
		Assert.Equal(0.3f, motors.HighFrequency, 3);
    }

	[Fact]
	public void Pattern_interpolates_all_motors_and_supports_silent_gaps()
	{
		var mixer = new GamepadRumbleMixer();
		mixer.PlayPattern(new RumblePattern(
			new RumbleSegment(new RumbleMotorState(1f, 0.8f, 0.6f, 0.4f), RumbleMotorState.Zero, 1f),
			new RumbleSegment(RumbleMotorState.Zero, RumbleMotorState.Zero, 0.5f)));

		mixer.Tick(0.5f);
		RumbleMotorState halfway = mixer.Combine();
		Assert.Equal(0.5f, halfway.LowFrequency, 3);
		Assert.Equal(0.4f, halfway.HighFrequency, 3);
		Assert.Equal(0.3f, halfway.LeftTrigger, 3);
		Assert.Equal(0.2f, halfway.RightTrigger, 3);

		mixer.Tick(0.6f);
		Assert.Equal(RumbleMotorState.Zero, mixer.Combine());
	}

	private static RumblePattern Constant(
		float low,
		float high,
		float duration,
		float leftTrigger = 0f,
		float rightTrigger = 0f)
	{
		var motors = new RumbleMotorState(low, high, leftTrigger, rightTrigger);
		return new RumblePattern(new RumbleSegment(motors, motors, duration));
	}
}
