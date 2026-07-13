#nullable enable

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Crusaders30XX.ECS.DataOriented.Rendering.Diagnostics;

/// <summary>
/// One deterministic, documented snapshot invocation. <see cref="LaunchVariantIndex"/>
/// identifies the command/output contract; <see cref="MaterializerVariantIndex"/> identifies
/// the generic ECS-050 authored shell which can currently be used for that invocation.
/// </summary>
public readonly record struct SnapshotLaunchOutput(
    string FixtureId,
    int LaunchVariantIndex,
    int MaterializerVariantIndex,
    string Arguments,
    string OutputFileName,
    string BaselineDirectory,
    string BaselineFileName,
    bool HasDistinctMaterializerVariant);

/// <summary>
/// Immutable host-side catalog for the snapshot commands documented in
/// docs/display-snapshots.md and its verification scripts. It contains no ECS state and does
/// no filesystem discovery, so command resolution is deterministic on every host.
/// </summary>
public sealed class SnapshotLaunchOutputCatalog
{
    private static readonly CatalogData Data = BuildEntries();
    private static readonly SnapshotLaunchOutput[] Entries = Data.Entries;
    private static readonly SnapshotLaunchAlias[] Aliases = Data.Aliases;

    public ReadOnlySpan<SnapshotLaunchOutput> Registered => Entries;

    public static string GetCaptureFileName(in SnapshotLaunchOutput output, float renderScale = 1f)
    {
        if (!float.IsFinite(renderScale) || renderScale <= 0f || renderScale > 2f)
            throw new ArgumentOutOfRangeException(nameof(renderScale), renderScale,
                "Snapshot render scale must be finite, greater than zero, and no greater than two.");
        if (renderScale == 1f) return output.OutputFileName;

        string stem = output.OutputFileName[..^4];
        string scale = renderScale.ToString("0.###", CultureInfo.InvariantCulture);
        return $"{stem}@{scale}x.png";
    }

    public bool TryResolve(
        ReadOnlySpan<char> fixtureId,
        ReadOnlySpan<string> fixtureArguments,
        out SnapshotLaunchOutput output)
    {
        for (var index = 0; index < Entries.Length; index++)
        {
            ref readonly SnapshotLaunchOutput candidate = ref Entries[index];
            if (!fixtureId.Equals(candidate.FixtureId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!ArgumentsEqual(candidate.Arguments.AsSpan(), fixtureArguments)) continue;
            output = candidate;
            return true;
        }

        for (var index = 0; index < Aliases.Length; index++)
        {
            ref readonly SnapshotLaunchAlias alias = ref Aliases[index];
            if (!fixtureId.Equals(alias.FixtureId, StringComparison.OrdinalIgnoreCase)) continue;
            if (!ArgumentsEqual(alias.Arguments.AsSpan(), fixtureArguments)) continue;
            return TryResolve(alias.FixtureId, alias.LaunchVariantIndex, out output);
        }

        output = default;
        return false;
    }

    public bool TryResolve(
        ReadOnlySpan<char> fixtureId,
        int launchVariantIndex,
        out SnapshotLaunchOutput output)
    {
        for (var index = 0; index < Entries.Length; index++)
        {
            ref readonly SnapshotLaunchOutput candidate = ref Entries[index];
            if (candidate.LaunchVariantIndex != launchVariantIndex) continue;
            if (!fixtureId.Equals(candidate.FixtureId, StringComparison.OrdinalIgnoreCase)) continue;
            output = candidate;
            return true;
        }

        output = default;
        return false;
    }

    private static bool ArgumentsEqual(ReadOnlySpan<char> expected, ReadOnlySpan<string> actual)
    {
        var actualIndex = 0;
        var expectedIndex = 0;
        while (true)
        {
            SkipSpaces(expected, ref expectedIndex);
            while (actualIndex < actual.Length && IsHostOnlyArgument(actual[actualIndex])) actualIndex++;

            bool expectedEnded = expectedIndex >= expected.Length;
            bool actualEnded = actualIndex >= actual.Length;
            if (expectedEnded || actualEnded) return expectedEnded && actualEnded;

            int tokenStart = expectedIndex;
            while (expectedIndex < expected.Length && expected[expectedIndex] != ' ') expectedIndex++;
            if (!expected[tokenStart..expectedIndex].Equals(actual[actualIndex], StringComparison.OrdinalIgnoreCase))
                return false;
            actualIndex++;
        }
    }

    private static void SkipSpaces(ReadOnlySpan<char> value, ref int index)
    {
        while (index < value.Length && value[index] == ' ') index++;
    }

    private static bool IsHostOnlyArgument(string value) =>
        value.Equals("no-shaders", StringComparison.OrdinalIgnoreCase);

    private static CatalogData BuildEntries()
    {
        var entries = new List<SnapshotLaunchOutput>(160);
        var aliases = new List<SnapshotLaunchAlias>(8);
        var host = new NewWorldSnapshotFixtureHost();

        void Add(string fixtureId, string arguments, string outputFileName)
        {
            if (!host.TryResolve(fixtureId, out NewWorldSnapshotFixture fixture))
                throw new InvalidOperationException($"Snapshot catalog fixture '{fixtureId}' is not registered.");

            string png = EnsurePng(outputFileName);
            for (var index = 0; index < entries.Count; index++)
            {
                SnapshotLaunchOutput existing = entries[index];
                if (!string.Equals(existing.FixtureId, fixtureId, StringComparison.Ordinal)) continue;
                if (!string.Equals(existing.OutputFileName, png, StringComparison.Ordinal)) continue;
                aliases.Add(new SnapshotLaunchAlias(fixtureId, arguments, existing.LaunchVariantIndex));
                return;
            }

            int launchIndex = 0;
            for (var index = 0; index < entries.Count; index++)
                if (string.Equals(entries[index].FixtureId, fixtureId, StringComparison.Ordinal)) launchIndex++;

            int materializerIndex = Math.Min(launchIndex, fixture.VariantCount - 1);
            entries.Add(new SnapshotLaunchOutput(
                fixtureId,
                launchIndex,
                materializerIndex,
                arguments,
                png,
                $"tests/VisualBaselines/{fixtureId}",
                png,
                launchIndex < fixture.VariantCount));
        }

        void AddSlugs(string fixtureId, params string[] slugs)
        {
            foreach (string slug in slugs) Add(fixtureId, slug, slug);
        }

        Add("card", "", "strike");
        Add("card", "strike", "strike");
        Add("card", "fireball", "fireball");

        Add("brittle-card", "", "strike");
        Add("brittle-card", "strike", "strike");
        Add("brittle-card", "fireball", "fireball");
        Add("brittle-card", "strike --scale 0.6 --rotation -25", "strike-s0_6-rn25");
        Add("brittle-card", "strike --scale 1.35 --rotation 30", "strike-s1_35-r30");
        Add("brittle-card", "strike --rotation 20 --pledge", "strike-s1-r20-pledge");

        Add("frozen-card", "", "strike");
        Add("frozen-card", "strike", "strike");
        Add("frozen-card", "strike --scale 0.6 --rotation -25", "strike-s0_6-rn25");
        Add("frozen-card", "strike --rotation 20 --brittle", "strike-s1-r20-brittle");

        Add("thorned-card", "", "strike");
        Add("thorned-card", "strike", "strike");
        Add("thorned-card", "strike --scale 0.6 --rotation -25", "strike-s0_6-rn25");
        Add("thorned-card", "strike --frozen", "strike-s1-r0-frozen");

        // These two fixtures survived registration but never received sections or baselines in
        // the canonical snapshot document. Their legacy deterministic defaults remain explicit.
        Add("scorched-card", "", "strike");
        Add("cursed-card", "", "strike");

        Add("colorless-card", "", "all-printed-colors");

        const string questDefault =
            "gold-500-deck-offer-smite-red-unburdened_strike-black-smite-white-upgraded";
        Add("quest-reward-modal", "", questDefault);
        Add("quest-reward-modal",
            "--gold 500 --exchange strike|white smite|red --exchange reckoning|white unburdened_strike|black --upgrade smite|white",
            questDefault);
        Add("quest-reward-modal", "--card strike|white", "gold-500-deck-offer-strike-white");

        Add("booster-pack-opening", "", "time-5.14-seed-1337");
        foreach (string time in new[] { "0.45", "1.70", "2.40", "3.10", "3.95", "4.70", "5.14" })
            Add("booster-pack-opening", $"--time {time} --seed 1337", $"time-{time}-seed-1337");

        AddModular(Add);

        Add("passive-application", "burn hold player single", "burn-hold-player-single");
        Add("passive-application", "aegis entry player single", "aegis-entry-player-single");
        Add("passive-application", "fear hold enemy single", "fear-hold-enemy-single");
        Add("passive-application", "frostbite exit player single", "frostbite-exit-player-single");
        Add("passive-application", "burn hold enemy multi", "burn-hold-enemy-multi");
        Add("passive-application", "wounded hold player attack", "wounded-hold-player-attack");

        Add("narrative-event-modal", "", "icebound-tithe-options-3");
        Add("narrative-event-modal", "--event pruned_vocation", "pruned-vocation-options-3");
        Add("narrative-event-modal", "--event icebound_tithe --options 1", "icebound-tithe-options-1");
        Add("narrative-event-modal", "--event icebound_tithe --options 2", "icebound-tithe-options-2");

        Add("waystation", "", "default");
        AddSlugs("player-hud", "default", "unavailable", "incoming-damage", "low-health", "expanded", "enemy-health");
        AddSlugs("equipment-tooltip", "active", "passive", "used");
        AddSlugs("enemy-damage-meter", "initial", "transition", "settled", "absorb");
        AddSlugs("enemy-attack-banner", "anticipation", "impact", "settled", "hover", "pulse", "absorb");
        AddSlugs("assigned-block-rail", "single-card", "mixed-row", "dense-row", "hover", "entry-impact", "returning");
        AddSlugs("enemy-defeat-burst", "assembled", "peak-jitter", "exploding");
        AddSlugs("guardian-angel", "idle", "message", "card-hop", "medal-loop", "enemy-recoil");
        AddSlugs("pause-menu", "rumble-on", "rumble-off");
        AddSlugs("hotkey-hints", "keyboard", "xbox", "playstation");
        AddSlugs("battle-phase-transition", "start-hold", "block-entry", "block-hold", "action-hold", "action-exit", "pledge-hold", "victory-hold");

        Add("achievement-overview", "", "achievement-overview");
        Add("achievement-detail", "", "achievement-detail");

        foreach (string fixtureId in new[]
        {
            "climb-no-events", "climb-hazard-event", "climb-character-event",
            "climb-hazard-hover-preview", "climb-character-hover-preview",
            "climb-hazard-confirmation", "climb-character-summary", "climb-active-events",
            "climb-hover-preview", "climb-medal-tooltip-hover", "climb-sold-shop-slot",
            "climb-encounter-reward-modal", "climb-replacement-modal", "climb-inventory-overlay",
            "climb-inventory-equipment-tooltip", "card-list-modal-top", "card-list-modal-middle",
            "card-list-modal-bottom",
        }) Add(fixtureId, "", fixtureId);

        Add("climb-character-dialog", "intro", "intro");
        Add("climb-character-dialog", "settled", "settled");
        Add("climb-character-dialog", "", "settled");
        AddSlugs("climb-header", "normal", "preview-delta", "pulse", "overview-hover");
        AddSlugs("climb-resource-acquisition", "entry", "fall", "catch", "pulse");

        return new CatalogData(entries.ToArray(), aliases.ToArray());
    }

    private static void AddModular(Action<string, string, string> add)
    {
        foreach ((string preset, string sample) in new[]
        {
            ("heavy-hammer", "start"), ("heavy-hammer", "impact"), ("heavy-hammer", "late"),
            ("holy-strike", "impact"), ("enemy-rock-blast", "impact"), ("enemy-bite", "impact"),
            ("enemy-slash", "impact"), ("light-slash", "impact"),
        }) add("modular-fx", $"{preset} {sample}", $"{preset}-{sample}");

        AddModule("actor-lunge", "start", "right");
        AddModule("actor-squash-stretch", "impact", "left");
        AddModule("smoke-screen", "impact", "right");
        AddModule("claw-slash", "impact", "left");
        AddModule("halo", "impact", "left");
        AddModule("shards", "late", "right");
        AddModule("cracks", "impact", "right");
        AddModule("cracks", "impact", "right", seed: 7331);
        AddModule("slash-band", "impact", "left");
        AddModule("slash-band", "impact", "right");
        AddModule("arrow-shot", "impact", "right");
        AddModule("thrown-blade-volley", "impact", "right");
        AddModule("energy-bolt", "start", "right");
        AddModule("spin-slash", "impact", "right");
        AddModule("flame-burst", "impact", "right");
        AddModule("frost-burst", "impact", "left");
        AddModule("shadow-tendrils", "late", "left");
        AddModule("poison-cloud", "late", "left");
        AddModule("shield-ward", "impact", "left");
        AddModule("shield-shatter", "late", "right");
        AddModule("soul-siphon", "impact", "right");
        AddModule("resource-motes", "late", "left");
        AddModule("seal-stamp", "impact", "right", targetCard: true);
        AddModule("frost-bind", "impact", "right", targetCard: true);
        AddModule("brittle-fracture", "impact", "right", targetCard: true);
        AddModule("color-drain", "late", "right", targetCard: true);
        AddModule("energy-bolt", "impact", "right", palette: "fire");
        AddModule("energy-bolt", "impact", "right", palette: "ice");

        // The documentation also shows the default-seed form and a seal-stamp palette sample;
        // retain those exact token orders as accepted commands.
        add(
            "modular-fx",
            "--module energy-bolt --sample impact --palette fire --direction right",
            "module-energy-bolt-impact-right-seed-1337-fire");
        add(
            "modular-fx",
            "--module seal-stamp --sample impact --palette arcane --target card",
            "module-seal-stamp-impact-right-seed-1337-arcane-card");

        void AddModule(
            string module,
            string sample,
            string direction,
            int seed = 1337,
            string? palette = null,
            bool targetCard = false)
        {
            string arguments = $"--module {module} --sample {sample} --seed {seed} --direction {direction}";
            string slug = $"module-{module}-{sample}-{direction}-seed-{seed}";
            if (palette is not null)
            {
                arguments += $" --palette {palette}";
                slug += $"-{palette}";
            }
            if (targetCard)
            {
                arguments += " --target card";
                slug += "-card";
            }
            add("modular-fx", arguments, slug);
        }
    }

    private static string EnsurePng(string value) =>
        value.EndsWith(".png", StringComparison.OrdinalIgnoreCase) ? value : $"{value}.png";

    private readonly record struct SnapshotLaunchAlias(
        string FixtureId,
        string Arguments,
        int LaunchVariantIndex);

    private sealed record CatalogData(
        SnapshotLaunchOutput[] Entries,
        SnapshotLaunchAlias[] Aliases);
}
