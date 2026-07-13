using Crusaders30XX.ECS.Benchmarks.Benchmarking;
using Xunit;

namespace Crusaders30XX.ECS.Benchmarks.Tests;

public sealed class BenchmarkRunnerTests
{
    [Fact]
    public void Reports_raw_empty_and_net_measurements_with_stable_checksum()
    {
        var options = new BenchmarkOptions(
            Seed: 1337,
            WarmupBatches: 1,
            SampleBatches: 5,
            IterationsPerBatch: 16,
            OutputPath: null,
            LegacyBaselinePath: null,
            AllowDebug: true);
        BenchmarkArtifact artifact = new BenchmarkRunner(options).Run(
            [new DeterministicBenchmark()],
            legacyBaseline: null);

        BenchmarkResult result = Assert.Single(artifact.Benchmarks);
        Assert.Equal(5, result.Raw.SampleCount);
        Assert.Equal(5, result.HarnessOverhead.SampleCount);
        Assert.Equal(5, result.Net.SampleCount);
        Assert.True(result.StableChecksum);
        Assert.Equal(16 * 32, result.OperationsPerSample);
        Assert.True(result.Raw.MedianNanoseconds >= 0d);
        Assert.True(result.HarnessOverhead.MedianAllocatedBytes >= 0);
        Assert.True(result.Net.MedianAllocatedBytes >= 0);
        Assert.Contains("raw - empty", artifact.Configuration.HarnessOverheadPolicy);
    }

    [Fact]
    public void Parses_repeatable_quick_configuration()
    {
        BenchmarkOptions options = BenchmarkOptions.Parse(
        [
            "--quick",
            "--seed", "7331",
            "--output", "result.json",
        ]);

        Assert.Equal(7331, options.Seed);
        Assert.Equal(2, options.WarmupBatches);
        Assert.Equal(5, options.SampleBatches);
        Assert.Equal(8, options.IterationsPerBatch);
        Assert.Equal("result.json", options.OutputPath);
    }

    private sealed class DeterministicBenchmark : IMicrobenchmark
    {
        public string Name => "deterministic";
        public string Category => "test";
        public int EntityCount => 32;
        public int ComponentCount => 64;
        public long OperationsPerIteration => 32;
        public long ProcessedRowsPerIteration => 32;

        public void Initialize(int seed)
        {
        }

        public long RunBatch(int iterations)
        {
            long checksum = 0;
            for (int iteration = 0; iteration < iterations; iteration++)
            {
                for (int row = 0; row < 32; row++)
                {
                    checksum += row;
                }
            }

            return checksum;
        }
    }
}
