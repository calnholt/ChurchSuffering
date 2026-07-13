# ECS-052 pause-menu and hotkey-hints snapshot parity

## Scope

This is the first fixture-specific unchanged-baseline cluster ported from the deleted snapshot
world. It covers these approved 1920x1080 captures without changing the PNGs:

- `pause-menu`: `rumble-on`, `rumble-off`
- `hotkey-hints`: `keyboard`, `xbox`, `playstation`

The legacy fixture and display implementations were reconstructed with `git show` from:

- `ECS/Diagnostics/Snapshots/Fixtures/PauseMenuSnapshotFixture.cs`
- `ECS/Diagnostics/Snapshots/Fixtures/HotKeySnapshotFixture.cs`
- `ECS/Scenes/Global/PauseMenuDisplaySystem.cs`
- `ECS/Scenes/Global/PauseMenuSliderDisplaySystem.cs`
- `ECS/Rendering/HotKeyGlyphRenderer.cs`
- `ECS/Scenes/HotKeySystem.cs`

## Production authoring

`SnapshotFixtureMaterializer` now dispatches these fixture IDs to deterministic static
compositions made only from `Transform`, `Sprite`, `TextPresentation`, and `OwnedByScene`.
All other registered fixtures continue through the generic shell.

The compositions retain the legacy constants that matter to the approved images:

- pause rail geometry, accent strips, 50% slider tracks/gradients/knobs, toggle state,
  footer action, and keyboard resume hint;
- the approved `rumble-off` quirk, which is only the OFF toggle on a cleared black frame;
- device-specific gallery positions, keyboard labels, Xbox face/system/shoulder treatments,
  PlayStation labels/colors, the sample action bounds, and all four hint positions;
- NewRocker display titles and ChakraPetch UI text through compact content/style IDs.

Each variant retains its two reserved compact texture IDs. The production host catalog binds
them to a generated white pixel and a generated 56x56 rounded primitive, so fixture authoring
does not depend on a runtime texture fallback or on unmanaged strings/assets inside ECS.

## Focused structural verification

`Ecs052PauseHotKeySnapshotAuthoringTests` locks down per-variant entity/text counts, key legacy
coordinates, device-specific text, compact texture ownership, and production recipe bindings.
The existing all-fixture text test now permits fixture-specific multi-label compositions while
still requiring every emitted content ID to resolve and remain owned by the fixture scene.

## Unchanged-baseline verification status

On 2026-07-13 all five required production-host commands passed with `--no-build` and
`--verify`; no `--accept` command was run and no approved baseline was written:

```text
dotnet run --no-build -- snapshot pause-menu rumble-on --verify
dotnet run --no-build -- snapshot pause-menu rumble-off --verify
dotnet run --no-build -- snapshot hotkey-hints keyboard --verify
dotnet run --no-build -- snapshot hotkey-hints xbox --verify
dotnet run --no-build -- snapshot hotkey-hints playstation --verify
```

The two scheduler conflicts exposed during the first attempts were resolved with explicit
dependencies (`ClimbRuntimeSystem` after `RunLifecycleRuntimeSystem`, and
`BattlePileInputSystem` after `UIInteractionSystem`). After rebuilding the production host, all
five commands exited successfully. The approved PNGs remained unchanged.
