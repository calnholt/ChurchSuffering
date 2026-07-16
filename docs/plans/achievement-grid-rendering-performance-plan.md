# Achievement Grid Rendering Performance Plan

## Document Status

- **Status:** Ready for implementation.
- **Primary goal:** Collapse the achievement grid's repeated immediate-mode rectangle submissions into a small deferred batch while preserving cell ordering and animations.
- **Required final verification:** `dotnet build`, Achievement tests/snapshots, and a deterministic Achievement performance capture.
- **Save compatibility:** None required.

## Evidence and Target

`AchievementGridDisplaySystem.Draw` averaged 1.47 ms of the scene's 2.07 ms CPU draw time. The 15x10 grid draws one fill and two four-edge borders per cell: at least 1,350 `SpriteBatch.Draw` calls before rails, panel chrome, hover borders, or exclamation marks. The scene inherits the global `SpriteSortMode.Immediate` batch, turning the same-texture rectangle stream into immediate submissions.

Acceptance in deterministic overview and detail fixtures:

- Reduce `AchievementGridDisplaySystem.Draw` average CPU time to 0.35 ms or less and P95 to 0.75 ms or less.
- Reduce the grid's measured draw calls by at least 80%; target no more than 12 draw calls in the idle overview.
- Preserve pixel-identical overview/detail snapshots and all hover, reveal, completion, explosion, and exclamation animations.
- Do not move interaction or animation state updates into `Draw`.

## Implementation Plan

### 1. Add grid-specific workload measurement

- Add `AchievementGridDisplaySystem.Draw` to the GPU/workload scope set so its draw calls, sprites, textures, GPU time, and target changes appear directly in reports.
- Add a DEBUG-only deterministic Achievement profile using the existing snapshot fixture state: mixed hidden/visible/completed/completed-unseen cells, plus separate idle, hover, and reveal-animation measurement windows.
- Record baseline CPU, GPU, draw calls, sprites, and allocations before changing batching.

### 2. Render the rectangle stream in a deferred pass

- Make the Achievement scene owner bracket the grid draw in a `SpriteSortMode.Deferred` pass with the same blend state, sampler, depth state, rasterizer, and `Game1.Display.SpriteBatchTransform` used by the surrounding scene.
- End the preceding batch, begin the deferred grid pass, draw the grid, end it, and restore the exact surrounding batch before later scene/global overlays. Centralize restoration in the same pass helper used by the Battle/Climb performance plans if available.
- Preserve submission order inside `AchievementGridDisplaySystem`: panel and rails first, then cells in existing dictionary insertion order, then each cell's fill, outer border, inner border, hover, and exclamation content.
- Keep font/texture switches for exclamation marks in the same logical order initially. Only separate them into a final overlay pass if snapshots prove that enlarged/animated cells never rely on cross-cell overlap ordering.

### 3. Remove avoidable CPU work without adding draw-time state

- Replace dictionary iteration with a fixed row-major entity array created alongside the 15x10 grid. Keep the dictionary only for ID lookup APIs if still required.
- Cache each entity's `AchievementBase` reference on grid creation or store the state needed for rendering on `AchievementGridItem`. Refresh it from achievement completion/seen/progress/reveal events during update/event handling, not by dictionary lookup in every cell draw.
- Precompute base cell rectangles and centers when grid layout settings change. During update, derive animated bounds only for cells whose scale/offset/alpha is changing; idle cells reuse cached bounds.
- Continue computing transient hover/reveal geometry during `UpdateHoverStatesAndClicks`. `DrawGridCell` receives a display-ready item and issues draw commands only.
- Use the shared `UiDraw.Border` when `ui-draw-primitives-consolidation-plan.md` lands, but retain the same four-edge geometry and colors. Helper consolidation alone does not satisfy this plan's batching target.

### 4. Preserve resource lifecycle

- Reuse the existing pixel texture or the shared `ImageAssetService` pixel after the draw-primitives plan lands; do not allocate additional per-cell textures.
- If the pass helper or future implementation introduces GPU buffers/targets, own and dispose them in the Achievement display system and clear them on `DeleteCachesEvent`/scene cleanup. The initial deferred implementation should not require a render target.
- Do not pre-render the whole grid to a render target in the first implementation: current cells can move, scale, fade, and explode, and deferred same-texture batching should remove the dominant submission cost without dirty-cache state in `Draw`.

## Interfaces and Tests

- **Diagnostics change:** add the grid to `FrameProfiler`'s GPU/workload scope set and add the deterministic Achievement profile variants.
- **Internal representation:** fixed row-major grid array plus cached achievement/display state; existing `GetGridEntity(row, col)` behavior remains unchanged.
- Add tests for row/column indexing, event-driven cached state refresh, layout-setting invalidation, and animation bounds updating only affected cells.
- Verify `achievement-overview` and `achievement-detail` snapshots with unchanged baselines. Add reveal/explosion snapshot coverage only if the deterministic performance fixture exposes an untested render state; follow `docs/display-snapshots.md` for any new baseline.
- Manually verify hover overlap at grid edges, multiple simultaneous explosions, completed-unseen pulsing, clicking empty cells, scene re-entry, render-scale changes, and shaders enabled/disabled.

## Dependencies and Boundaries

- This plan changes grid batching and cached presentation only. It does not alter achievement progression, adjacency rules, save data, or scene-module ownership.
- Coordinate SpriteBatch state restoration with the Battle/Climb plans; only one shared helper should exist.
- Keep `AchievementGridDisplaySystem.Update` responsible for interaction and animation state. `Draw` remains render-only.

