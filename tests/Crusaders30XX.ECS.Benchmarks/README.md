# Data-oriented ECS benchmark and model harness

This standalone .NET 8 project is the foundation measurement boundary. It has no game,
MonoGame, content, or third-party benchmark-framework dependency. It directly links the
CPU-only data-oriented foundation files so Release measurements exercise the exact landed
runtime without loading the game executable. Performance adapters implement
`IMicrobenchmark`; correctness adapters implement `IModelWorld`.

The built-in contiguous-array workloads are labeled `harness-validation`; they verify
measurement behavior and are **not** ECS results. `data-oriented-cached-query` workloads
are the canonical 10,000-row two- and four-component ECS-013 chunk/span loops. The older
direct-storage workloads remain labeled separately and are not query-throughput results.

## Release run and JSON contract

Run from the repository root:

```bash
dotnet run --project tests/Crusaders30XX.ECS.Benchmarks/Crusaders30XX.ECS.Benchmarks.csproj \
  -c Release -- \
  --legacy-baseline tests/PerformanceBaselines/legacy-ecs-initial.json \
  --output debug/performance/ecs-microbenchmarks.json
```

The runner rejects Debug by default, warms each workload, samples an odd count of batches,
disables tiered compilation, and alternates whether the workload or empty harness is measured first. Every result
reports paired `raw`, `harnessOverhead`, and `net` statistics. Net elapsed ticks and
allocated bytes are calculated per sample as `max(raw - empty, 0)` before percentiles are
computed. GC counts are reported but not subtracted. The output has a versioned JSON
schema, deterministic result ordering, fixed seed/configuration, machine/runtime/build
metadata, processed rows, operations, allocations, and legacy-baseline summary.

For a smoke check use `--quick`. Use at least the defaults (8 warm-up batches, 21 samples,
64 iterations) for retained results. Compare medians only between Release runs made on the
same machine with identical arguments. Do not treat capture timestamps or normal timing
noise as byte-for-byte output stability.

## Model tests

The randomized runner uses logical scenario entity IDs so an adapter may choose any valid
free-index reuse order. The real data-oriented adapter enables every foundation
capability. Long deterministic runs cover lifecycle and generation reuse, components,
tags, enable state, cached all/any/none queries, ordered command playback, and world-owned
dynamic buffers. State and query results are compared after every operation batch.
Failures include the seed, batch, operation number, and the last 32 operations; tests also
assert that every operation category was reached.

```bash
dotnet test tests/Crusaders30XX.ECS.Benchmarks.Tests/Crusaders30XX.ECS.Benchmarks.Tests.csproj \
  -c Release
```

## Optional hardware counters

Hardware-counter captures are not an ECS-012 gate; ECS-061 records the comparable legacy
and new captures. Always publish once, use the same binary, seed, workload arguments, and
machine for both sides, and retain the benchmark JSON beside the counter output.

Linux with `perf` (event availability varies by CPU/kernel):

```bash
dotnet publish tests/Crusaders30XX.ECS.Benchmarks/Crusaders30XX.ECS.Benchmarks.csproj \
  -c Release -o debug/performance/ecs-benchmark-publish

perf stat -r 5 \
  -e cycles,instructions,cache-references,cache-misses,L1-dcache-loads,L1-dcache-load-misses \
  -- debug/performance/ecs-benchmark-publish/Crusaders30XX.ECS.Benchmarks \
  --legacy-baseline tests/PerformanceBaselines/legacy-ecs-initial.json \
  --output debug/performance/ecs-microbenchmarks-perf.json \
  2> debug/performance/ecs-microbenchmarks-perf-stat.txt
```

macOS Instruments exposes templates installed by the current Xcode. List them first and
select the available CPU-counter template (commonly `Counters` or `CPU Counters`):

```bash
xcrun xctrace list templates

xcrun xctrace record \
  --template 'Counters' \
  --output debug/performance/ecs-microbenchmarks.trace \
  --launch -- debug/performance/ecs-benchmark-publish/Crusaders30XX.ECS.Benchmarks \
  --legacy-baseline tests/PerformanceBaselines/legacy-ecs-initial.json \
  --output debug/performance/ecs-microbenchmarks-counters.json
```

If the host does not expose data-cache events, record that limitation in the retained
artifact rather than substituting results from a different workload or machine.
