using System;
using System.IO;
using Crusaders30XX.Diagnostics.Snapshots;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class DisplaySnapshotFrameworkTests
{
    [Fact]
    public void TryParse_verifyFlag_setsModeAndExcludesFlagFromFixtureArgs()
    {
        bool parsed = DisplaySnapshotLaunchOptions.TryParse(
            new[] { "snapshot", "card", "strike", "--verify" },
            out var options);

        Assert.True(parsed);
        Assert.Equal("card", options.FixtureId);
        Assert.Equal(DisplaySnapshotBaselineMode.Verify, options.BaselineMode);
        Assert.Equal(new[] { "strike" }, options.Args);
    }

    [Fact]
    public void TryParse_acceptFlag_setsModeAndExcludesFlagFromFixtureArgs()
    {
        bool parsed = DisplaySnapshotLaunchOptions.TryParse(
            new[]
            {
                "snapshot",
                "quest-reward-modal",
                "--accept",
                "--gold",
                "500",
                "--card",
                "strike|white"
            },
            out var options);

        Assert.True(parsed);
        Assert.Equal(DisplaySnapshotBaselineMode.Accept, options.BaselineMode);
        Assert.Equal(
            new[] { "--gold", "500", "--card", "strike|white" },
            options.Args);
    }

    [Fact]
    public void TryParse_verifyAndAccept_throwsMutualExclusionError()
    {
        var exception = Assert.Throws<DisplaySnapshotSetupException>(() =>
            DisplaySnapshotLaunchOptions.TryParse(
                new[] { "snapshot", "card", "--verify", "--accept" },
                out _));

        Assert.Contains("mutually exclusive", exception.Message);
    }

    [Fact]
    public void TryParse_withoutBaselineFlag_preservesNormalFixtureArguments()
    {
        bool parsed = DisplaySnapshotLaunchOptions.TryParse(
            new[] { "snapshot", "narrative-event-modal", "--event", "icebound_tithe", "--options", "2" },
            out var options);

        Assert.True(parsed);
        Assert.Equal(DisplaySnapshotBaselineMode.None, options.BaselineMode);
        Assert.Equal(
            new[] { "--event", "icebound_tithe", "--options", "2" },
            options.Args);
    }

    [Theory]
    [InlineData("card", "strike", "card", "strike.png")]
    [InlineData("brittle-card", "strike.png", "brittle-card", "strike.png")]
    [InlineData("frozen-card", "strike.png", "frozen-card", "strike.png")]
    [InlineData("colorless-card", "all-printed-colors", "colorless-card", "all-printed-colors.png")]
    [InlineData("quest-reward-modal", "gold-500", "quest-reward-modal", "gold-500.png")]
    [InlineData("narrative-event-modal", "icebound-tithe-options-3", "narrative-event-modal", "icebound-tithe-options-3.png")]
    [InlineData("waystation", "default", "waystation", "default.png")]
    public void BuildPaths_existingFixtures_keepNormalCapturePath(
        string fixtureId,
        string outputFileName,
        string expectedDirectory,
        string expectedFileName)
    {
        string repositoryRoot = Path.Combine(Path.DirectorySeparatorChar.ToString(), "repo");

        var paths = DisplaySnapshotBaselineComparer.BuildPaths(
            repositoryRoot,
            fixtureId,
            outputFileName);

        Assert.Equal(
            Path.Combine(repositoryRoot, "debug", "snapshots", expectedDirectory, expectedFileName),
            paths.CapturePath);
    }

    [Fact]
    public void BuildPaths_returnsStableBaselineAndFailureArtifactPaths()
    {
        string repositoryRoot = Path.Combine(Path.DirectorySeparatorChar.ToString(), "repo");

        var paths = DisplaySnapshotBaselineComparer.BuildPaths(
            repositoryRoot,
            "player-hud",
            "incoming-damage.png");

        Assert.Equal(
            Path.Combine(
                repositoryRoot,
                "tests",
                "VisualBaselines",
                "player-hud",
                "incoming-damage.png"),
            paths.BaselinePath);
        Assert.Equal(
            Path.Combine(
                repositoryRoot,
                "debug",
                "snapshots",
                "player-hud",
                "incoming-damage-actual.png"),
            paths.FailureActualPath);
        Assert.Equal(
            Path.Combine(
                repositoryRoot,
                "debug",
                "snapshots",
                "player-hud",
                "incoming-damage-diff.png"),
            paths.FailureDiffPath);
    }

    [Theory]
    [InlineData(8, false)]
    [InlineData(9, true)]
    public void PixelDiffers_enforcesInclusivePerChannelTolerance(int channelDifference, bool expected)
    {
        var actual = new Color(100, 100, 100, 100);
        var baseline = new Color(
            100 + channelDifference,
            100,
            100,
            100);

        Assert.Equal(
            expected,
            DisplaySnapshotBaselineComparer.PixelDiffers(actual, baseline));
    }

    [Fact]
    public void PixelDiffers_checksAlphaChannel()
    {
        var actual = new Color(100, 100, 100, 100);
        var baseline = new Color(100, 100, 100, 109);

        Assert.True(DisplaySnapshotBaselineComparer.PixelDiffers(actual, baseline));
    }

    [Theory]
    [InlineData(5184, true)]
    [InlineData(5185, false)]
    public void DifferingPixelRatio_enforcesInclusiveQuarterPercentBoundary(
        int differingPixels,
        bool expected)
    {
        const int totalPixels =
            DisplaySnapshotBaselineComparer.CaptureWidth
            * DisplaySnapshotBaselineComparer.CaptureHeight;

        Assert.Equal(
            expected,
            DisplaySnapshotBaselineComparer.IsDifferingPixelRatioAcceptable(
                differingPixels,
                totalPixels));
    }

    [Fact]
    public void ComparePixelData_missingBaseline_failsVerification()
    {
        var result = DisplaySnapshotBaselineComparer.ComparePixelData(
            Array.Empty<Color>(),
            DisplaySnapshotBaselineComparer.CaptureWidth,
            DisplaySnapshotBaselineComparer.CaptureHeight,
            null,
            0,
            0,
            Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "missing.png"));

        Assert.False(result.Passed);
        Assert.Contains("Baseline is missing", result.FailureMessage);
    }

    [Fact]
    public void BaselineExists_missingPath_returnsFalse()
    {
        string path = Path.Combine(
            Path.GetTempPath(),
            Guid.NewGuid().ToString("N"),
            "missing.png");

        Assert.False(DisplaySnapshotBaselineComparer.BaselineExists(path));
    }
}
