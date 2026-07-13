#nullable enable

using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Integration;

public sealed class Ecs052HostSupportParityTests
{
    [Fact]
    public void Test_fight_parser_accepts_case_insensitive_stable_ids()
    {
        bool parsed = TestFightLaunchOptions.TryParse(
            ["TEST-FIGHT", "Hammer", "Training-Demon", "HARD"],
            out TestFightLaunchOptions? options);

        Assert.True(parsed);
        Assert.NotNull(options);
        Assert.Equal("hammer", options.WeaponId);
        Assert.Equal("training-demon", options.EnemyId);
        Assert.Equal(ClimbDifficulty.Hard, options.Difficulty);
    }

    [Fact]
    public void Test_fight_parser_ignores_other_launch_modes()
    {
        Assert.False(TestFightLaunchOptions.TryParse(["snapshot", "card", "strike"], out var options));
        Assert.Null(options);
    }

    [Theory]
    [InlineData("axe", "skeleton", "hard")]
    [InlineData("hammer", "unknown_enemy", "hard")]
    [InlineData("hammer", "skeleton", "nightmare")]
    public void Test_fight_parser_rejects_unknown_arguments(
        string weapon,
        string enemy,
        string difficulty)
    {
        Assert.Throws<TestFightSetupException>(() =>
            TestFightLaunchOptions.TryParse(["test-fight", weapon, enemy, difficulty], out _));
    }

    [Fact]
    public void Test_fight_parser_rejects_missing_arguments_with_usage()
    {
        TestFightSetupException exception = Assert.Throws<TestFightSetupException>(() =>
            TestFightLaunchOptions.TryParse(["test-fight", "hammer", "skeleton"], out _));

        Assert.Contains("Usage:", exception.Message);
    }

    [Fact]
    public void Rumble_mixer_adds_and_clamps_overlapping_pulses()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.PlayPattern(Constant(0.8f, 0.9f, 1f, 0.8f, 0.7f));
        mixer.PlayPattern(Constant(0.5f, 0.5f, 1f, 0.5f, 0.6f));

        Assert.Equal(new RumbleMotorState(1f, 1f, 1f, 1f), mixer.Combine());
    }

    [Fact]
    public void Rumble_expiration_keeps_other_pulses_active()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.PlayPattern(Constant(0.2f, 0.1f, 0.05f));
        mixer.PlayPattern(Constant(0.4f, 0.3f, 1f));

        mixer.Tick(0.06f);

        Assert.Equal(new RumbleMotorState(0.4f, 0.3f), mixer.Combine());
    }

    [Fact]
    public void Rumble_channels_and_pulses_combine_and_clear_independently()
    {
        var mixer = new GamepadRumbleMixer();
        mixer.SetChannel("booster", new RumbleMotorState(0.35f, 0.55f, 0.1f, 0.1f));
        mixer.SetChannel("other", new RumbleMotorState(0.1f, 0.1f));
        mixer.PlayPattern(Constant(0.3f, 0.2f, 1f, 0.2f, 0.3f), RumbleGroup.UiHover);
        mixer.PlayPattern(Constant(0.1f, 0.1f, 1f), RumbleGroup.Default);

        mixer.ClearGroup(RumbleGroup.UiHover);
        mixer.ClearChannel("booster");

        RumbleMotorState motors = mixer.Combine();
        Assert.Equal(0.2f, motors.LowFrequency, 3);
        Assert.Equal(0.2f, motors.HighFrequency, 3);
        Assert.Equal(0f, motors.LeftTrigger);
        Assert.Equal(0f, motors.RightTrigger);
    }

    [Fact]
    public void Rumble_pattern_interpolates_all_motors_and_supports_silent_gaps()
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

public sealed class Ecs052DisplayMetricsParityTests
{
    [Theory]
    [InlineData(1280, 720, 1280, 720)]
    [InlineData(1920, 1080, 1920, 1080)]
    [InlineData(2560, 1440, 2560, 1440)]
    [InlineData(3840, 2160, 3840, 2160)]
    [InlineData(5120, 2880, 3840, 2160)]
    public void Native_content_area_is_preserved_and_capped_at_4k(
        int backBufferWidth,
        int backBufferHeight,
        int expectedRenderWidth,
        int expectedRenderHeight)
    {
        DisplayMetrics metrics = DisplayMetrics.Calculate(backBufferWidth, backBufferHeight);

        Assert.Equal(expectedRenderWidth, metrics.RenderWidth);
        Assert.Equal(expectedRenderHeight, metrics.RenderHeight);
        Assert.Equal(new Rectangle(0, 0, backBufferWidth, backBufferHeight), metrics.RenderDestination);
    }

    [Fact]
    public void Ultrawide_and_tall_backbuffers_preserve_the_logical_aspect()
    {
        DisplayMetrics ultrawide = DisplayMetrics.Calculate(3440, 1440);
        Assert.Equal(new Rectangle(440, 0, 2560, 1440), ultrawide.RenderDestination);

        DisplayMetrics tall = DisplayMetrics.Calculate(1080, 1920);
        Assert.Equal(new Rectangle(0, 656, 1080, 607), tall.RenderDestination);
    }

    [Fact]
    public void Render_scale_and_coordinate_conversions_remain_deterministic()
    {
        DisplayMetrics doubled = DisplayMetrics.Calculate(1920, 1080, renderScaleOverride: 2f);
        Assert.Equal(3840, doubled.RenderWidth);
        Assert.Equal(2160, doubled.RenderHeight);

        DisplayMetrics metrics = DisplayMetrics.Calculate(3440, 1440);
        Assert.Equal(new Vector2(960f, 540f), metrics.ScreenToLogical(new Point(1720, 720)));
        Assert.Equal(Vector2.Zero, metrics.ScreenToLogical(Point.Zero));
        Assert.Equal(
            new Vector2(DisplayMetrics.LogicalWidth, DisplayMetrics.LogicalHeight),
            metrics.ScreenToLogical(new Point(3440, 1440)));

        DisplayMetrics scaled = DisplayMetrics.Calculate(2560, 1440);
        Assert.Equal(new Rectangle(13, 13, 15, 15), scaled.LogicalToRender(new Rectangle(10, 10, 11, 11)));
    }
}
