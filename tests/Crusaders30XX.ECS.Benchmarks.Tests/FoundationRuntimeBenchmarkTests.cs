using Crusaders30XX.ECS.Benchmarks.Adapters;
using Crusaders30XX.ECS.Benchmarks.Benchmarking;
using Crusaders30XX.ECS.Benchmarks.Model;
using Crusaders30XX.ECS.Benchmarks.Workloads;
using Xunit;

namespace Crusaders30XX.ECS.Benchmarks.Tests;

public sealed class FoundationRuntimeBenchmarkTests
{
    [Theory]
    [InlineData(2)]
    [InlineData(4)]
    public void Cached_query_workload_allocates_zero_after_warmup(int componentCount)
    {
        var benchmark = new EcsCachedQueryUpdateBenchmark(componentCount);
        benchmark.Initialize(seed: 1337);
        _ = benchmark.RunBatch(iterations: 2);

        long before = GC.GetAllocatedBytesForCurrentThread();
        long checksum = benchmark.RunBatch(iterations: 8);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.NotEqual(0, checksum);
        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Release_artifact_reports_cached_query_results_and_zero_raw_allocations()
    {
        var options = new BenchmarkOptions(
            Seed: 1337,
            WarmupBatches: 2,
            SampleBatches: 5,
            IterationsPerBatch: 4,
            OutputPath: null,
            LegacyBaselinePath: null,
            AllowDebug: true);
        BenchmarkArtifact artifact = new BenchmarkRunner(options).Run(
        [
            new EcsCachedQueryUpdateBenchmark(2),
            new EcsCachedQueryUpdateBenchmark(4),
        ],
            legacyBaseline: null);

        Assert.Equal(2, artifact.Benchmarks.Count);
        Assert.All(artifact.Benchmarks, result =>
        {
            Assert.Equal("data-oriented-cached-query", result.Category);
            Assert.True(result.StableChecksum);
            Assert.Equal(10_000 * 4, result.ProcessedRowsPerSample);
            Assert.Equal(0, result.Raw.MaximumAllocatedBytes);
            Assert.Equal(0, result.Raw.Gen0Collections);
        });
    }

    [Fact]
    public void Real_adapter_advertises_and_exercises_all_foundation_capabilities()
    {
        var adapter = new DataOrientedWorldModelAdapter();
        Assert.Equal(ModelWorldCapabilities.All, adapter.Capabilities);
        ModelEntityHandle first = adapter.Create();
        ModelEntityHandle second = adapter.Create();
        adapter.AddComponent(first, ModelComponentKind.Position, new ModelComponentValue(1, 2));
        adapter.AddComponent(second, ModelComponentKind.Position, new ModelComponentValue(3, 4));
        adapter.AddTag(second, ModelTagKind.Primary);
        adapter.Disable(second);
        adapter.AppendBuffer(first, 10);
        adapter.Playback(
        [
            new ModelCommand(ModelCommandKind.AppendBuffer, first, BufferValueOrIndex: 20),
            new ModelCommand(ModelCommandKind.SetComponent, first, ModelComponentKind.Position, new ModelComponentValue(5, 6)),
        ]);

        ModelQuery enabledPosition = new(ModelTypeMask.Position, ModelTypeMask.None, ModelTypeMask.None);
        Assert.Equal([first], adapter.Query(enabledPosition));
        Assert.Equal([10, 20], adapter.Observe(first).BufferContents);
        Assert.Equal(new ModelComponentValue(5, 6), adapter.Observe(first).Position);

        ModelQuery includeDisabled = new(
            ModelTypeMask.Position,
            ModelTypeMask.None,
            ModelTypeMask.None,
            IncludeDisabled: true);
        Assert.Equal([first, second], adapter.Query(includeDisabled).OrderBy(entity => entity.Index));
    }
}
