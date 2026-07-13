#nullable enable

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rendering;

public sealed class Ecs052SnapshotLaunchOutputCatalogTests
{
    [Fact]
    public void Every_registered_fixture_has_unique_contiguous_documented_launch_cases()
    {
        var catalog = new SnapshotLaunchOutputCatalog();
        SnapshotLaunchOutput[] entries = catalog.Registered.ToArray();
        NewWorldSnapshotFixture[] fixtures = new NewWorldSnapshotFixtureHost().Registered.ToArray();

        Assert.NotEmpty(entries);
        Assert.Equal(fixtures.Length, entries.Select(entry => entry.FixtureId).Distinct(StringComparer.Ordinal).Count());
        foreach (NewWorldSnapshotFixture fixture in fixtures)
        {
            SnapshotLaunchOutput[] fixtureEntries = entries
                .Where(entry => entry.FixtureId == fixture.Id)
                .OrderBy(entry => entry.LaunchVariantIndex)
                .ToArray();
            Assert.NotEmpty(fixtureEntries);
            Assert.Equal(Enumerable.Range(0, fixtureEntries.Length), fixtureEntries.Select(entry => entry.LaunchVariantIndex));
            Assert.Equal(
                fixtureEntries.Length,
                fixtureEntries.Select(entry => entry.Arguments).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.All(fixtureEntries, entry => Assert.InRange(entry.MaterializerVariantIndex, 0, fixture.VariantCount - 1));
        }
    }

    [Fact]
    public void Every_catalog_entry_resolves_by_command_and_by_launch_index_exactly_once()
    {
        var catalog = new SnapshotLaunchOutputCatalog();
        foreach (SnapshotLaunchOutput expected in catalog.Registered)
        {
            string[] arguments = Split(expected.Arguments);
            Assert.True(catalog.TryResolve(expected.FixtureId, arguments, out SnapshotLaunchOutput byCommand));
            Assert.Equal(expected, byCommand);
            Assert.True(catalog.TryResolve(expected.FixtureId, expected.LaunchVariantIndex, out SnapshotLaunchOutput byIndex));
            Assert.Equal(expected, byIndex);
            Assert.Equal($"tests/VisualBaselines/{expected.FixtureId}", expected.BaselineDirectory);
            Assert.Equal(expected.OutputFileName, expected.BaselineFileName);
            Assert.EndsWith(".png", expected.OutputFileName, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public void Every_approved_baseline_file_has_exactly_one_catalog_mapping()
    {
        var catalog = new SnapshotLaunchOutputCatalog();
        string root = FindRepositoryRoot();
        string baselineRoot = Path.Combine(root, "tests", "VisualBaselines");
        string[] approved = Directory.GetFiles(baselineRoot, "*.png", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(root, path).Replace(Path.DirectorySeparatorChar, '/'))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var mapped = catalog.Registered.ToArray()
            .GroupBy(
                entry => $"{entry.BaselineDirectory}/{entry.BaselineFileName}",
                StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        Assert.Equal(107, approved.Length);
        foreach (string path in approved)
        {
            Assert.True(mapped.TryGetValue(path, out int count), $"Approved baseline '{path}' has no catalog entry.");
            Assert.Equal(1, count);
        }
    }

    [Theory]
    [MemberData(nameof(DocumentedCommandCases))]
    public void Current_documented_launch_args_resolve_deterministically(
        string fixtureId,
        string[] arguments,
        int launchVariantIndex,
        int materializerVariantIndex,
        string outputFileName)
    {
        var catalog = new SnapshotLaunchOutputCatalog();

        Assert.True(catalog.TryResolve(fixtureId, arguments, out SnapshotLaunchOutput first));
        Assert.True(catalog.TryResolve(fixtureId, arguments, out SnapshotLaunchOutput second));
        Assert.Equal(first, second);
        Assert.Equal(launchVariantIndex, first.LaunchVariantIndex);
        Assert.Equal(materializerVariantIndex, first.MaterializerVariantIndex);
        Assert.Equal(outputFileName, first.OutputFileName);
        Assert.Equal($"tests/VisualBaselines/{fixtureId}", first.BaselineDirectory);
        Assert.Equal(outputFileName, first.BaselineFileName);
    }

    [Fact]
    public void Host_only_no_shaders_argument_does_not_change_snapshot_identity()
    {
        var catalog = new SnapshotLaunchOutputCatalog();

        Assert.True(catalog.TryResolve(
            "booster-pack-opening",
            new[] { "--time", "4.70", "--seed", "1337" },
            out SnapshotLaunchOutput fullEffects));
        Assert.True(catalog.TryResolve(
            "booster-pack-opening",
            new[] { "--time", "4.70", "--seed", "1337", "no-shaders" },
            out SnapshotLaunchOutput noShaders));

        Assert.Equal(fullEffects, noShaders);
        Assert.Equal("time-4.70-seed-1337.png", noShaders.OutputFileName);
    }

    [Fact]
    public void Debug_render_scale_uses_the_documented_capture_suffix_without_changing_baseline()
    {
        var catalog = new SnapshotLaunchOutputCatalog();
        Assert.True(catalog.TryResolve("climb-header", new[] { "pulse" }, out SnapshotLaunchOutput output));

        Assert.Equal("pulse.png", SnapshotLaunchOutputCatalog.GetCaptureFileName(in output));
        Assert.Equal("pulse@2x.png", SnapshotLaunchOutputCatalog.GetCaptureFileName(in output, 2f));
        Assert.Equal("pulse.png", output.BaselineFileName);
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            SnapshotLaunchOutputCatalog.GetCaptureFileName(in output, 2.01f));
    }

    [Fact]
    public void Unknown_or_incomplete_commands_fail_without_hash_fallback()
    {
        var catalog = new SnapshotLaunchOutputCatalog();

        Assert.False(catalog.TryResolve("missing-fixture", Array.Empty<string>(), out _));
        Assert.False(catalog.TryResolve("enemy-attack-banner", new[] { "unknown" }, out _));
        Assert.False(catalog.TryResolve("booster-pack-opening", new[] { "--time" }, out _));
        Assert.False(catalog.TryResolve("climb-header", 99, out _));
    }

    public static IEnumerable<object[]> DocumentedCommandCases()
    {
        yield return Case("card", [], 0, 0, "strike.png");
        yield return Case("card", ["fireball"], 1, 1, "fireball.png");
        yield return Case("brittle-card", ["strike", "--rotation", "20", "--pledge"], 4, 0, "strike-s1-r20-pledge.png");
        yield return Case("frozen-card", ["strike", "--rotation", "20", "--brittle"], 2, 1, "strike-s1-r20-brittle.png");
        yield return Case("thorned-card", ["strike", "--frozen"], 2, 1, "strike-s1-r0-frozen.png");
        yield return Case("colorless-card", [], 0, 0, "all-printed-colors.png");
        yield return Case("quest-reward-modal", [], 0, 0,
            "gold-500-deck-offer-smite-red-unburdened_strike-black-smite-white-upgraded.png");
        yield return Case("booster-pack-opening", ["--time", "5.14", "--seed", "1337"], 0, 0,
            "time-5.14-seed-1337.png");
        yield return Case("modular-fx", ["heavy-hammer", "impact"], 1, 0, "heavy-hammer-impact.png");
        yield return Case("modular-fx",
            ["--module", "energy-bolt", "--sample", "impact", "--seed", "1337", "--direction", "right", "--palette", "fire"],
            34, 0, "module-energy-bolt-impact-right-seed-1337-fire.png");
        yield return Case("passive-application", ["wounded", "hold", "player", "attack"], 5, 2,
            "wounded-hold-player-attack.png");
        yield return Case("narrative-event-modal", ["--event", "icebound_tithe", "--options", "2"], 3, 3,
            "icebound-tithe-options-2.png");
        yield return Case("waystation", [], 0, 0, "default.png");
        yield return Case("player-hud", ["enemy-health"], 5, 5, "enemy-health.png");
        yield return Case("enemy-attack-banner", ["pulse"], 4, 4, "pulse.png");
        yield return Case("battle-phase-transition", ["victory-hold"], 6, 6, "victory-hold.png");
        yield return Case("achievement-detail", [], 0, 0, "achievement-detail.png");
        yield return Case("climb-character-dialog", ["settled"], 1, 0, "settled.png");
        yield return Case("card-list-modal-bottom", ["no-shaders"], 0, 0, "card-list-modal-bottom.png");
        yield return Case("climb-header", ["overview-hover"], 3, 3, "overview-hover.png");
        yield return Case("climb-resource-acquisition", ["catch"], 2, 2, "catch.png");
    }

    private static object[] Case(
        string fixtureId,
        string[] arguments,
        int launchVariantIndex,
        int materializerVariantIndex,
        string outputFileName) =>
        [fixtureId, arguments, launchVariantIndex, materializerVariantIndex, outputFileName];

    private static string[] Split(string arguments) =>
        string.IsNullOrEmpty(arguments)
            ? Array.Empty<string>()
            : arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(Directory.GetCurrentDirectory());
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Crusaders30XX.csproj"))) return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not find the Crusaders30XX repository root.");
    }
}
