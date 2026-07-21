using System;
using System.Collections.Generic;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using Xunit;

namespace ChurchSuffering.Tests;

public class FrameProfilerTests
{
    [Fact]
    public void BoundedHistogram_UsesDocumentedBucketEdgesAndExactCounters()
    {
        var histogram = new BoundedHistogram();
        histogram.Add(0.01);
        histogram.Add(19.99);
        histogram.Add(20.01);
        histogram.Add(99.99);
        histogram.Add(100.01);
        histogram.Add(499.99);
        histogram.Add(700);

        Assert.Equal(7, histogram.Count);
        Assert.Equal(6, histogram.SlowCount);
        Assert.Equal(0.05, histogram.Percentile(0.01), 3);
        Assert.Equal(700, histogram.Percentile(1), 3);
        Assert.Equal(700, histogram.Max, 3);
    }

    [Fact]
    public void GpuLaunchFlag_IsCaseInsensitiveAndStripped()
    {
        GpuProfilingRuntimeOptions.ConfigureFromArgs(new[] { "PROFILE-GPU", "test-fight" });
#if DEBUG
        Assert.True(GpuProfilingRuntimeOptions.Enabled);
#else
        Assert.False(GpuProfilingRuntimeOptions.Enabled);
#endif
        Assert.Equal(new[] { "test-fight" }, GpuProfilingRuntimeOptions.StripLaunchFlag(new[] { "PROFILE-GPU", "test-fight" }));
    }

    [Fact]
    public void GpuQueries_AreConsumedOnlyAfterBothTimestampsAreAvailable()
    {
        var backend = new FakeGpuBackend();
        using var profiler = new GpuQueryProfiler(backend);
        var token = profiler.Begin("CardList", 42, SceneId.Climb);
        token.End(default);

        int completed = 0;
        profiler.Poll((_, _, _, _, _) => completed++);
        Assert.Equal(0, completed);
        Assert.Equal(1, profiler.PendingCount);

        backend.Available = true;
        profiler.Poll((name, milliseconds, frame, scene, _) =>
        {
            completed++;
            Assert.Equal("CardList", name);
            Assert.Equal(1, milliseconds, 3);
            Assert.Equal(42, frame);
            Assert.Equal(SceneId.Climb, scene);
        });

        Assert.Equal(1, completed);
        Assert.Equal(0, profiler.PendingCount);
    }

    [Fact]
    public void GpuQueries_DropWithoutBlockingWhenPoolIsExhausted()
    {
        using var profiler = new GpuQueryProfiler(new FakeGpuBackend());
        var tokens = new List<GpuQueryProfiler.QueryToken>();
        for (int i = 0; i < GpuQueryProfiler.PairCapacity; i++)
        {
            var token = profiler.Begin("scope", i, SceneId.Battle);
            Assert.True(token.IsValid);
            tokens.Add(token);
        }

        Assert.False(profiler.Begin("overflow", 999, SceneId.Battle).IsValid);
        Assert.Equal(1, profiler.DroppedSamples);
        foreach (var token in tokens) token.End(default);
    }

    [Fact]
    public void GpuQueries_UnsupportedBackendRemainsSafeCountersOnly()
    {
        using var profiler = new GpuQueryProfiler(new FakeGpuBackend { Supported = false });
        Assert.False(profiler.IsSupported);
        Assert.False(profiler.Begin("scope", 1, SceneId.Battle).IsValid);
        profiler.Poll((_, _, _, _, _) => throw new InvalidOperationException());
        Assert.Equal(0, profiler.DroppedSamples);
    }

    [Fact]
    public void GpuQueries_PreserveNestedScopeOriginAndResetPendingQueries()
    {
        var backend = new FakeGpuBackend { Available = true };
        using var profiler = new GpuQueryProfiler(backend);
        var outer = profiler.Begin("outer", 7, SceneId.Battle);
        var inner = profiler.Begin("inner", 7, SceneId.Battle);
        inner.End(default);
        outer.End(default);

        var completed = new Dictionary<string, (double Milliseconds, long Frame, SceneId Scene)>();
        profiler.Poll((name, milliseconds, frame, scene, _) => completed[name] = (milliseconds, frame, scene));
        Assert.Equal(1, completed["inner"].Milliseconds, 3);
        Assert.Equal(3, completed["outer"].Milliseconds, 3);
        Assert.All(completed.Values, value =>
        {
            Assert.Equal(7, value.Frame);
            Assert.Equal(SceneId.Battle, value.Scene);
        });

        profiler.Begin("pending", 8, SceneId.Climb).End(default);
        profiler.Reset();
        Assert.Equal(0, profiler.PendingCount);
        var afterReset = profiler.Begin("after-reset", 9, SceneId.Climb);
        Assert.True(afterReset.IsValid);
        afterReset.End(default);
        Assert.True(backend.DeleteCalls > 0);
        Assert.True(backend.CreateCalls > 1);
    }

    private sealed class FakeGpuBackend : IGpuQueryBackend
    {
        private uint _nextId = 1;
        private ulong _nextTimestamp;
        private readonly Dictionary<uint, ulong> _timestamps = new();

        public bool Supported { get; init; } = true;
        public bool Available { get; set; }
        public string BackendName => "fake";
        public bool IsSupported => Supported;
        public string Status => Supported ? "available" : "unsupported";
        public int CreateCalls { get; private set; }
        public int DeleteCalls { get; private set; }

        public void CreateQueries(uint[] queryIds)
        {
            CreateCalls++;
            for (int i = 0; i < queryIds.Length; i++) queryIds[i] = _nextId++;
        }

        public void DeleteQueries(uint[] queryIds) => DeleteCalls++;

        public void WriteTimestamp(uint queryId)
        {
            _timestamps[queryId] = _nextTimestamp;
            _nextTimestamp += 1_000_000;
        }

        public bool IsResultAvailable(uint queryId) => Available;
        public ulong GetResultNanoseconds(uint queryId) => _timestamps[queryId];
        public void Dispose() { }
    }
}
