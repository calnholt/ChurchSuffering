# ECS-052 snapshot parity audit

Status: command/output parity catalogued; unchanged visual parity is blocked.

`SnapshotLaunchOutputCatalog` is the authoritative data-oriented host contract for the
fixture commands in `docs/display-snapshots.md` and the verification scripts. It contains 148
canonical command/output cases across all 46 registered fixture IDs. Duplicate documented
commands which produce the same image are private aliases. All 107 currently approved PNGs under
`tests/VisualBaselines/` have exactly one canonical catalog entry.

## Host integration

Replace snapshot argument hashing and the hard-coded `<fixture-id>.png` output with:

```csharp
var catalog = new SnapshotLaunchOutputCatalog();
if (!catalog.TryResolve(options.FixtureId, options.Args, out SnapshotLaunchOutput launch))
    throw new DisplaySnapshotSetupException("Unknown data-oriented snapshot command.");

snapshotScene = materializer.Materialize(
    runtime.World,
    launch.FixtureId,
    launch.MaterializerVariantIndex);

string captureFileName = SnapshotLaunchOutputCatalog.GetCaptureFileName(
    in launch,
    options.RenderScaleOverride ?? 1f);
```

Use `launch.OutputFileName` at scale 1, `launch.BaselineDirectory` and
`launch.BaselineFileName` for baseline comparison, and `captureFileName` for debug capture. The
capture helper preserves the documented `@2x` suffix. Unknown or incomplete commands must fail;
there is no hash fallback.

The legacy bare `snapshot card` command selected a random non-weapon card, so it could not have a
deterministic output filename. The new catalog deliberately resolves that documented bare command
to `strike.png`; explicit `strike` is an alias of the same canonical output.

## Materializer slot mismatches

The launch-case index is intentionally separate from the current generic materializer slot. These
documented output sets cannot receive a unique current shell:

| Fixture | Catalogued outputs | Generic materializer slots | Collapsed outputs |
| --- | ---: | ---: | ---: |
| `brittle-card` | 5 | 1 | 4 |
| `frozen-card` | 3 | 2 | 1 |
| `thorned-card` | 3 | 2 | 1 |
| `quest-reward-modal` | 2 | 1 | 1 |
| `modular-fx` | 37 | 1 | 36 |
| `passive-application` | 6 | 3 | 3 |
| `player-hud` | 6 | 4 | 2 |
| `climb-character-dialog` | 2 | 1 | 1 |
| **Total** | **64** | **15** | **49** |

`card` and `colorless-card` have more generic slots than output cases because one legacy capture
renders multiple printed colors in the same image. Extra generic tint slots are not separate
snapshot outputs.

## Unchanged-baseline blockers

Current materializers cannot match the approved baselines:

| Blocker | Current behavior | Required parity behavior |
| --- | --- | --- |
| Fixture authoring | Every fixture emits two generated rectangles and one fixture-ID label. | Author each fixture's production entities, component state, layout, and text. |
| Assets | Snapshot texture IDs resolve to semantic generated recipes. | Bind the same production textures, fonts, source rectangles, and scaling used by gameplay. |
| Cards and UI | No canonical card renderer, HUD, tooltip, modal, rail, or achievement composition is instantiated. | Recreate each characterized production display path from data-oriented components and packets. |
| Effects and shaders | No modular FX primitives, passive seals, status shaders, or post-process requests are authored. | Extract equivalent effect/shader packets with matching recipes, parameters, ordering, and samples. |
| State variants | Arguments only select a generic tint slot, and 49 outputs share an already-used slot. | Materialize distinct gameplay/presentation state for every catalogued output. |
| Timing | The root captures after two draw frames for every fixture. Legacy fixtures warm up for 1-10 frames and some set explicit timeline samples. | Carry per-case warmup/sample timing in authored diagnostics and capture at the characterized frame. |
| Interaction | Hover, disabled, expanded, pulse, return, absorb, and dialogue samples have no authored input/presentation state. | Set the same deterministic interaction and animation state before extraction. |

Passing command/output tests proves deterministic routing and exact baseline path selection only.
It is not evidence of pixel parity. Focused executable coverage is in
`tests/Crusaders30XX.Tests/DataOriented/Rendering/Ecs052SnapshotLaunchOutputCatalogTests.cs`.
