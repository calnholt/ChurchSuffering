using System.Diagnostics;
using System.Runtime.InteropServices;

namespace Crusaders30XX.ECS.Benchmarks.Benchmarking;

public sealed class BenchmarkRunner
{
    private readonly BenchmarkOptions options;
    private readonly EmptyHarnessBenchmark emptyHarness = new();

    public BenchmarkRunner(BenchmarkOptions options)
    {
        this.options = options;
    }

    public BenchmarkArtifact Run(
        IReadOnlyList<IMicrobenchmark> benchmarks,
        LegacyBaselineReference? legacyBaseline)
    {
        ValidateBuildConfiguration();
        var results = new List<BenchmarkResult>(benchmarks.Count);
        foreach (IMicrobenchmark benchmark in benchmarks)
        {
            results.Add(Measure(benchmark));
        }

        return new BenchmarkArtifact(
            SchemaVersion: 1,
            Runtime: "ecs-microbenchmark-harness",
            CapturedAtUtc: DateTimeOffset.UtcNow,
            BuildConfiguration,
            new BenchmarkRunConfiguration(
                options.Seed,
                options.WarmupBatches,
                options.SampleBatches,
                options.IterationsPerBatch,
                "Paired empty batches use the same outer-loop count; per-sample time and allocation are clamped(raw - empty, 0)."),
            new BenchmarkMachineMetadata(
                Environment.MachineName,
                RuntimeInformation.OSDescription,
                RuntimeInformation.OSArchitecture.ToString(),
                RuntimeInformation.ProcessArchitecture.ToString(),
                RuntimeInformation.FrameworkDescription,
                Environment.ProcessorCount,
                Stopwatch.Frequency),
            legacyBaseline,
            results);
    }

    private BenchmarkResult Measure(IMicrobenchmark benchmark)
    {
        benchmark.Initialize(options.Seed);
        emptyHarness.Initialize(options.Seed);
        for (int i = 0; i < options.WarmupBatches; i++)
        {
            _ = emptyHarness.RunBatch(options.IterationsPerBatch);
            _ = benchmark.RunBatch(options.IterationsPerBatch);
        }

        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();

        var raw = new Sample[options.SampleBatches];
        var overhead = new Sample[options.SampleBatches];
        var net = new Sample[options.SampleBatches];
        long? expectedChecksum = null;
        bool stableChecksum = true;

        for (int sampleIndex = 0; sampleIndex < options.SampleBatches; sampleIndex++)
        {
            // Alternate order to prevent a systematic first-measurement advantage.
            if ((sampleIndex & 1) == 0)
            {
                overhead[sampleIndex] = Capture(emptyHarness);
                raw[sampleIndex] = Capture(benchmark);
            }
            else
            {
                raw[sampleIndex] = Capture(benchmark);
                overhead[sampleIndex] = Capture(emptyHarness);
            }

            expectedChecksum ??= raw[sampleIndex].Checksum;
            stableChecksum &= expectedChecksum == raw[sampleIndex].Checksum;
            net[sampleIndex] = Sample.Subtract(raw[sampleIndex], overhead[sampleIndex]);
        }

        long operationsPerSample = checked(
            benchmark.OperationsPerIteration * options.IterationsPerBatch);
        return new BenchmarkResult(
            benchmark.Name,
            benchmark.Category,
            benchmark.EntityCount,
            benchmark.ComponentCount,
            operationsPerSample,
            checked(benchmark.ProcessedRowsPerIteration * options.IterationsPerBatch),
            stableChecksum,
            Summarize(raw, operationsPerSample),
            Summarize(overhead, operationsPerSample),
            Summarize(net, operationsPerSample));
    }

    private Sample Capture(IMicrobenchmark benchmark)
    {
        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        long started = Stopwatch.GetTimestamp();
        long checksum = benchmark.RunBatch(options.IterationsPerBatch);
        long elapsedTicks = Stopwatch.GetTimestamp() - started;
        return new Sample(
            elapsedTicks,
            GC.GetAllocatedBytesForCurrentThread() - allocatedBefore,
            GC.CollectionCount(0) - gen0Before,
            GC.CollectionCount(1) - gen1Before,
            GC.CollectionCount(2) - gen2Before,
            checksum);
    }

    private static MeasurementStatistics Summarize(Sample[] samples, long operationsPerSample)
    {
        long[] ticks = samples.Select(sample => sample.ElapsedTicks).Order().ToArray();
        long[] allocations = samples.Select(sample => sample.AllocatedBytes).Order().ToArray();
        double medianNanoseconds = ToNanoseconds(Percentile(ticks, 0.50));
        return new MeasurementStatistics(
            samples.Length,
            medianNanoseconds,
            ToNanoseconds(Percentile(ticks, 0.95)),
            ToNanoseconds(ticks[^1]),
            Percentile(allocations, 0.50),
            allocations[^1],
            samples.Sum(sample => sample.Gen0Collections),
            samples.Sum(sample => sample.Gen1Collections),
            samples.Sum(sample => sample.Gen2Collections),
            medianNanoseconds <= 0d
                ? 0d
                : operationsPerSample * 1_000_000_000d / medianNanoseconds);
    }

    private static long Percentile(long[] sortedValues, double percentile)
    {
        int index = Math.Clamp(
            (int)Math.Ceiling(sortedValues.Length * percentile) - 1,
            0,
            sortedValues.Length - 1);
        return sortedValues[index];
    }

    private static double ToNanoseconds(long ticks) =>
        ticks * (1_000_000_000d / Stopwatch.Frequency);

    private void ValidateBuildConfiguration()
    {
        if (!options.AllowDebug && BuildConfiguration != "Release")
        {
            throw new InvalidOperationException(
                "Microbenchmarks must run in Release. Pass --allow-debug only for harness development.");
        }
    }

    private static string BuildConfiguration
    {
        get
        {
#if DEBUG
            return "Debug";
#else
            return "Release";
#endif
        }
    }

    private readonly record struct Sample(
        long ElapsedTicks,
        long AllocatedBytes,
        int Gen0Collections,
        int Gen1Collections,
        int Gen2Collections,
        long Checksum)
    {
        public static Sample Subtract(Sample value, Sample overhead) => new(
            Math.Max(0, value.ElapsedTicks - overhead.ElapsedTicks),
            Math.Max(0, value.AllocatedBytes - overhead.AllocatedBytes),
            value.Gen0Collections,
            value.Gen1Collections,
            value.Gen2Collections,
            value.Checksum);
    }

    private sealed class EmptyHarnessBenchmark : IMicrobenchmark
    {
        private long state;

        public string Name => "empty-harness";
        public string Category => "harness-overhead";
        public int EntityCount => 0;
        public int ComponentCount => 0;
        public long OperationsPerIteration => 1;
        public long ProcessedRowsPerIteration => 0;

        public void Initialize(int seed)
        {
            state = seed;
        }

        public long RunBatch(int iterations)
        {
            long value = state;
            for (int i = 0; i < iterations; i++)
            {
                value = unchecked((value * 6364136223846793005L) + 1442695040888963407L);
            }

            state = value;
            return value;
        }
    }
}
