# Battle Render Submission Performance Plan

## Document Status

- **Status:** Ready for implementation, with benchmark instrumentation as the first required slice.
- **Primary goal:** Reduce the recurring CPU and GPU cost of Battle rendering without changing draw order or visuals.
- **Required final verification:** `dotnet build`, relevant tests and display snapshots, and a before/after `profile-gpu` benchmark on the same machine.
- **Save compatibility:** None required.

## Evidence and Target

The July 15 performance capture spent 132,924 of 180,424 rendered frames in Battle. `BattleSceneSystem.Draw` averaged 1.20 ms CPU and 12.40 ms GPU with a 26.00 ms GPU P95. It submitted 330.3 draw calls, 565.6 sprites, 5.8 render-target changes, and 13.3 shader changes per call. `HandDisplaySystem.DrawHand`, nested inside that scope, accounted for 0.48 ms CPU, 1.18 ms GPU, 107.9 draw calls, and 259 sprites.

The main causes are the scene-wide use of `SpriteSortMode.Immediate`, repeated batch/target transitions, the flat fan-out across roughly 60 battle display calls, and full card-face rendering for every visible hand card. The event fan-out for card decorations is secondary; preserve it unless measurement shows a specific subscriber is expensive.

Acceptance on the deterministic benchmark:

- Reduce `BattleSceneSystem.Draw` average draw calls by at least 40%.
- Reduce its GPU average and P95 by at least 25% without increasing CPU average or P95.
- Reduce `HandDisplaySystem.DrawHand` average draw calls by at least 50% in an idle hand.
- Keep all relevant snapshot baselines pixel-identical and preserve current render ordering.

## Implementation Plan

### 1. Add a repeatable Battle benchmark

- Add a DEBUG-only `battle-render-profile profile-gpu` launch path modeled after the existing card-list profile. Build a fixed battle with a deterministic enemy, hand, HUD state, card statuses, background effect, and render scale.
- Warm up before measurement, reset `FrameProfiler`, measure a fixed number of frames, wait only for the existing bounded GPU-query settle period, write a dedicated report, and exit with a pass/fail code.
- Record separate GPU/workload scopes for the background/composite pass, ordinary battle UI, hand base-card rendering, hand decorations, and global overlays. Keep scopes non-overlapping where totals will be compared.
- Store baseline numbers in the eventual implementation change description, not as hardware-specific constants in production code. The relative acceptance thresholds above are the gate.

### 2. Introduce explicit SpriteBatch pass boundaries

- Keep render-target backgrounds and shader/composite systems in `Immediate`; they mutate graphics state and require submission before target/shader changes.
- Split the current battle UI fan-out into contiguous pass segments without reordering any display call. State-compatible sprite-only segments use `Deferred` with the same blend, sampler, depth, rasterizer, and transform currently supplied by `Game1`/`BattleSceneSystem`.
- Keep card/status shader segments and any system that changes render targets, scissor state, blend state, or effects in isolated `Immediate` passes. Flush before entering one and restore the exact prior state afterward.
- Add a small rendering helper that owns `End`/`Begin` restoration for these pass transitions. It may carry graphics resources/state, but it must not be a game-state service or another ECS system.
- Do not switch the whole game or whole Battle scene to `Deferred` in one edit. Each converted segment must be benchmarked and snapshot-verified before converting the next segment.

### 3. Reuse the existing card-base surface cache for stable hand cards

- Extend `CardRenderEvent` with a `PreferCachedBase` flag matching `CardRenderScaledEvent` semantics. `CardDisplaySystem` should route that flag through its existing `GetOrCreateCachedBase` and `WeightedLruCache<CardBaseRenderModel, CachedCardSurface>` path.
- Have `HandDisplaySystem` request the cached base only when the card transform has settled: no position tween, hover-scale interpolation, return animation, rotation animation, alpha transition, or clip animation. Animated cards retain the current live render path so intermediate transforms do not create one cache entry per frame.
- Keep pledge, seal, shackle, frozen, recoil, highlight, and other dynamic overlays outside the cached base. They continue to render through the existing event pipeline on top of the cached card face.
- Confirm that `CardBaseRenderModel` includes every visual input that can change the static face: definition/upgrade/color, effective values and costs, alternate-play profile, relevant phase, render scale, and other printed-state modifiers. Add missing fields rather than manually invalidating individual cards.
- Continue clearing/disposal through `DeleteCachesEvent` and the existing 64 MB weighted cache budget. Add cache hit, miss, and bypass counters to the benchmark report so an apparent gain is not hiding transform-driven cache churn.

### 4. Remove proven redundant submissions

- After batching and card caching land, use the new per-pass workload scopes to identify remaining pass transitions, clears, or full-screen copies that execute with identical inputs.
- Remove only transitions proven redundant by the benchmark and graphics-state audit. The battle background ping-pong targets, Bloodshot path, biome composites, card additive pass, backdrop/foreground splits, and dialogue/modal suppression must retain their current ordering and conditional behavior.
- Do not combine this work with `scene-module-composition-plan.md`; that plan may later own the same ordered list, but this plan changes pass execution and caching only.

## Interfaces and Tests

- **Changed event:** `CardRenderEvent.PreferCachedBase : bool`, defaulting to `false` for unchanged callers.
- **Diagnostics:** deterministic Battle benchmark plus card-base cache hit/miss/bypass and pass-level rendering workload.
- Add tests for stable-hand cache eligibility and every bypass condition; verify changes to printed card state produce a different `CardBaseRenderModel`.
- Run `./scripts/verify-card-render-pipeline-snapshots.sh --verify`, relevant card/status snapshots, player HUD snapshots, assigned-block rail snapshots, and the deterministic Battle benchmark at the same render scale as its baseline.
- Manually verify card hover, play, return, assignment, status shaders, dialogue suppression, reward-modal suppression, and each biome background.

## Dependencies and Boundaries

- The profiler-overhead plan should land before final performance numbers are accepted, but the Battle benchmark can be added first.
- `ui-draw-primitives-consolidation-plan.md` may change helper call sites but must not be used as evidence of batching; `SpriteBatch` workload counters are authoritative.
- Preserve the current event-driven card-decoration architecture and current draw order. No scene-module refactor, render graph, or save changes belong in this plan.

