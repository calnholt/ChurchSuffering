using System.Collections.Generic;
using Crusaders30XX.ECS.Rendering;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class WeightedLruCacheTests
{
    [Fact]
    public void Add_evicts_least_recently_used_entries_to_stay_within_budget()
    {
        var disposed = new List<string>();
        using var cache = new WeightedLruCache<int, string>(10, disposed.Add);
        cache.Add(1, "one", 4);
        cache.Add(2, "two", 4);
        Assert.True(cache.TryGet(1, out _));

        cache.Add(3, "three", 4);

        Assert.False(cache.TryGet(2, out _));
        Assert.True(cache.TryGet(1, out _));
        Assert.True(cache.TryGet(3, out _));
        Assert.Equal(new[] { "two" }, disposed);
        Assert.Equal(8, cache.Weight);
    }

    [Fact]
    public void Clear_disposes_every_entry_and_resets_weight()
    {
        var disposed = new List<string>();
        using var cache = new WeightedLruCache<int, string>(10, disposed.Add);
        cache.Add(1, "one", 2);
        cache.Add(2, "two", 3);

        cache.Clear();

        Assert.Equal(2, disposed.Count);
        Assert.Equal(0, cache.Count);
        Assert.Equal(0, cache.Weight);
    }
}
