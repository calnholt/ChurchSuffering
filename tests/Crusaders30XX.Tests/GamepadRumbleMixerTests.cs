using Crusaders30XX.ECS.Input;
using Xunit;

namespace Crusaders30XX.Tests;

public class GamepadRumbleMixerTests
{
    [Fact]
    public void Overlapping_pulses_combine_with_sum_clamp()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.PlayPulse(0.4f, 0.3f, 1f);
        mixer.PlayPulse(0.5f, 0.4f, 1f);

        (float low, float high) = mixer.Combine();

        Assert.Equal(0.9f, low, 3);
        Assert.Equal(0.7f, high, 3);
    }

    [Fact]
    public void Combined_output_clamps_each_motor_to_one()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.PlayPulse(0.8f, 0.9f, 1f);
        mixer.PlayPulse(0.5f, 0.5f, 1f);

        (float low, float high) = mixer.Combine();

        Assert.Equal(1f, low);
        Assert.Equal(1f, high);
    }

    [Fact]
    public void Expired_pulse_leaves_other_pulse_active()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.PlayPulse(0.2f, 0.1f, 0.05f);
        mixer.PlayPulse(0.4f, 0.3f, 1f);

        mixer.Tick(0.06f);

        (float low, float high) = mixer.Combine();

        Assert.Equal(0.4f, low);
        Assert.Equal(0.3f, high);
    }

    [Fact]
    public void Channel_and_pulse_combine_additively()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.SetChannel("booster-pack-opening", 0.35f, 0.55f);
        mixer.PlayPulse(0.3f, 0.2f, 1f, RumbleGroup.UiHover);

        (float low, float high) = mixer.Combine();

        Assert.Equal(0.65f, low, 3);
        Assert.Equal(0.75f, high, 3);
    }

    [Fact]
    public void ClearRumbleGroup_removes_only_matching_pulses()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.SetChannel("booster-pack-opening", 0.35f, 0.55f);
        mixer.PlayPulse(0.3f, 0.2f, 1f, RumbleGroup.UiHover);
        mixer.PlayPulse(0.1f, 0.1f, 1f, RumbleGroup.Default);

        mixer.ClearGroup(RumbleGroup.UiHover);

        (float low, float high) = mixer.Combine();

        Assert.Equal(0.45f, low, 3);
        Assert.Equal(0.65f, high, 3);
    }

    [Fact]
    public void ClearRumbleChannel_removes_only_that_channel()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.SetChannel("booster-pack-opening", 0.35f, 0.55f);
        mixer.SetChannel("other", 0.1f, 0.1f);
        mixer.PlayPulse(0.3f, 0.2f, 1f);

        mixer.ClearChannel("booster-pack-opening");

        (float low, float high) = mixer.Combine();

        Assert.Equal(0.4f, low, 3);
        Assert.Equal(0.3f, high, 3);
    }
}
