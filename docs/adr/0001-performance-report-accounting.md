# Performance report uses render cadence and asynchronous GPU timing

Shift+Escape writes a DEBUG-only session report (`logs/performance-report.txt`) from `FrameProfiler`. The former report treated `GameTime.ElapsedGameTime` as measured frame duration and subtracted nested leaf scopes to produce “unaccounted” time. With a fixed timestep, that value primarily represented simulation cadence rather than actual CPU or GPU work.

**Decision:** render cadence is measured between `Game.Draw` starts with `Stopwatch.GetTimestamp`. CPU busy is the wall-clock interval from the first update iteration after a draw through completion of the next draw. Outside/pacing/driver time is the following render cadence minus that preceding CPU busy interval, clamped at zero. Update iterations and rendered frames are counted separately. Scope rows remain intentionally non-additive because scopes can nest.

Session distributions use fixed histograms: 0–20 ms in 0.05 ms buckets, 20–100 ms in 0.5 ms buckets, 100–500 ms in 5 ms buckets, and overflow. Min, max, average, and slow counts remain exact; P95 is the upper edge of the bucket containing the nearest-rank sample. This bounds profiler memory for sessions of any length. Display snapshot runs skip session accumulation.

The DEBUG-only `profile-gpu` flag enables MonoGame `GraphicsDevice.Metrics` deltas and paired DesktopGL `GL_TIMESTAMP` queries. A reusable 512-pair pool is polled on later render frames; the game never waits for a result and drops timing samples when the pool is exhausted. Unsupported backends retain counters-only reporting. GPU samples keep their originating render-frame and scene identity, and unresolved samples are reported rather than drained during shutdown.
