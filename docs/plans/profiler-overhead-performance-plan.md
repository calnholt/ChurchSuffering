# Profiler Overhead Performance Plan

## Document Status

- **Status:** Ready for implementation.
- **Primary goal:** Keep DEBUG performance reporting useful while making closed-overlay overhead negligible and eliminating the periodic live-top-list spike.
- **Required final verification:** `dotnet build`, profiler unit tests, and matched captures with the overlay closed and open.
- **Save compatibility:** None required.

## Evidence and Target

`ProfilerSystem.Update` averaged 0.41 ms globally, reached a 4.40 ms P95, and ran on every captured frame. In Battle it averaged 0.45 ms with a 4.55 ms P95. Every 250 ms it scans the rolling `Entries` queue twice using `Where`, `GroupBy`, `Sum`, `OrderBy`, and `ToList`, even when the overlay is closed or no `ProfilerOverlay` entity exists.

Additional overhead is not attributed to leaf rows: DEBUG `SystemManager` builds phase snapshots, creates profile names/captured delegates, and calls `FrameProfiler.AddSample` around every registered system, including inactive systems. `AddSample` takes a lock, enqueues an entry, updates dictionaries, and prunes queues.

Acceptance on a matched idle-Battle capture:

- Closed-overlay `ProfilerSystem.Update` average and P95 are both at or below 0.05 ms.
- Open-overlay `ProfilerSystem.Update` P95 is below 0.25 ms with no periodic multi-millisecond refresh spike.
- Enabling detailed reporting increases CPU-busy average by less than 5% versus the same DEBUG scenario with detailed CPU scopes disabled.
- Session report totals, per-scene attribution, nested-scope semantics, GPU delayed attribution, and bounded memory behavior remain unchanged.

## Implementation Plan

### 1. Separate session accumulation from live-overlay aggregation

- Keep `CurrentCpuTotals`, session histograms, scene histograms, render cadence, CPU busy, workload, and GPU-session accumulation as the authoritative report path.
- Remove the per-sample `Entries` queue used by the overlay. Replace it with bounded rolling aggregates keyed by scope name.
- For CPU scopes, track per-render-frame total ticks and call counts. On frame completion, add the completed frame to rolling per-name totals and enqueue a compact frame contribution. When it leaves `WindowSeconds`, subtract it.
- For delayed GPU scopes, enqueue compact timestamped `(name, milliseconds, calls)` contributions and maintain matching per-name rolling totals as entries arrive and expire.
- Track the rolling rendered-frame denominator directly. `GetTopSamples` should sort only the scope aggregate rows, making refresh cost proportional to the number of scope names rather than the number of samples.
- Preserve call count, total time, average per call, and frame-average output. Add deterministic tests for expiration at the window boundary and delayed GPU samples arriving after their originating frame.

### 2. Gate live work on overlay visibility

- At the start of `ProfilerSystem.Update`, resolve whether a `ProfilerOverlay` exists and is open. When closed, skip graph queues, FPS formatting state, white-texture creation, top-list refresh, and live rolling aggregation.
- Add an explicit `FrameProfiler.SetLiveOverlayEnabled(bool)` transition. Disabling clears only live rolling state; it must not reset session/report accumulators. Enabling begins a fresh live window rather than reconstructing history by scanning session data.
- Continue collecting session histograms while the live overlay is closed so Shift+Escape reports remain complete.

### 3. Remove per-system timing allocations

- Change `SystemManager` phase storage to keep a registration record containing the `System` and a profile name computed once at registration.
- Cache each phase's iteration array and invalidate it only when systems are added or removed. Preserve registration order and the existing protection against modification during enumeration.
- Replace captured `FrameProfiler.Measure(..., () => system.Update(gameTime))` calls with a struct timing scope around a direct `system.Update(gameTime)` call. Inactive systems retain their current near-zero samples unless a separate report-format decision is made later.
- Apply the same direct-scope pattern to the hottest repeated profiling fan-outs where captured lambdas are currently created each frame. Do not mechanically rewrite cold scene-preparation closures.

### 4. Make collection levels explicit

- Add a DEBUG runtime collection level with `Session` and `Detailed` modes. `Session` retains frame cadence, CPU busy, GPU frame time, and top-level update/draw scopes; `Detailed` enables per-system and child display scopes.
- Keep the current developer profiling command/report path in `Detailed` mode. Allow the deterministic performance benchmarks to run both modes so profiler overhead is measurable.
- Include collection level and live-overlay state in the report header. Do not change Release behavior or introduce a production telemetry dependency.

## Interfaces and Tests

- **New diagnostic API:** live-overlay enable/disable and DEBUG collection level.
- **Internal SystemManager change:** cached registration/profile records and phase snapshots; public update order remains unchanged.
- Unit-test rolling CPU aggregation, call counts, eviction, disabled-live behavior, GPU delayed aggregation, scene/session histogram preservation, and dynamic system registration/removal after a cached phase snapshot exists.
- Add a profiler-overhead benchmark that runs the same deterministic scene with live overlay closed and open, emits allocation counts if available, and compares CPU-busy/`ProfilerSystem.Update` distributions.
- Verify Shift+Escape still writes a valid report after scene transitions and after toggling the overlay repeatedly.

## Dependencies and Boundaries

- Preserve `docs/adr/0001-performance-report-accounting.md`: scope rows remain non-additive, fixed histograms remain bounded, and GPU timing remains asynchronous.
- Do not remove useful scopes merely to improve the benchmark. Reduce their collection overhead or make the collection level explicit.
- `scene-module-composition-plan.md` may eventually centralize draw-scope invocation; this plan must work with both the current and proposed scene composition.

