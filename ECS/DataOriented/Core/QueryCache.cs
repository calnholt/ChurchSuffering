#nullable enable

using System;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.DataOriented.Core;

public readonly record struct QueryFilter(
    ComponentSignature All = default,
    ComponentSignature Any = default,
    ComponentSignature None = default,
    bool IncludeDisabled = false,
    string? DebugName = null);

internal sealed class QueryCache
{
    private readonly World world;
    private readonly QueryFilter filter;
    private Archetype[] matches = new Archetype[4];

    public QueryCache(World world, in QueryFilter filter, IReadOnlyList<Archetype> archetypes)
    {
        this.world = world;
        this.filter = filter;
        for (var index = 0; index < archetypes.Count; index++)
        {
            TryAdd(archetypes[index]);
        }
    }

    public int Count { get; private set; }

    public bool IncludeDisabled => filter.IncludeDisabled;

    public string DebugName => filter.DebugName ?? "Query";

    public Archetype this[int index] => matches[index];

    public void TryAdd(Archetype archetype)
    {
        var signature = archetype.Signature;
        if (!signature.ContainsAll(filter.All) ||
            (!filter.Any.IsEmpty && !signature.Intersects(filter.Any)) ||
            signature.Intersects(filter.None))
        {
            return;
        }

        if (Count == matches.Length)
        {
            Array.Resize(ref matches, matches.Length * 2);
        }

        matches[Count++] = archetype;
    }

    public void BeginIteration() => world.BeginQueryIteration(DebugName);

    public void EndIteration() => world.EndQueryIteration();
}

public readonly ref struct QueryRows
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryRows(ReadOnlySpan<bool> enabled, bool includeDisabled)
    {
        this.enabled = enabled;
        this.includeDisabled = includeDisabled;
    }

    public Enumerator GetEnumerator() => new(enabled, includeDisabled);

    public ref struct Enumerator
    {
        private readonly ReadOnlySpan<bool> enabled;
        private readonly bool includeDisabled;
        private int row;

        internal Enumerator(ReadOnlySpan<bool> enabled, bool includeDisabled)
        {
            this.enabled = enabled;
            this.includeDisabled = includeDisabled;
            row = -1;
        }

        public int Current => row;

        public bool MoveNext()
        {
            while (++row < enabled.Length)
            {
                if (includeDisabled || enabled[row])
                {
                    return true;
                }
            }

            return false;
        }
    }
}

internal struct QueryChunkEnumeratorCore
{
    private QueryCache? cache;
    private int archetypeIndex;
    private int chunkIndex;
    private bool active;

    public QueryChunkEnumeratorCore(QueryCache cache)
    {
        this.cache = cache;
        archetypeIndex = 0;
        chunkIndex = -1;
        active = true;
        Current = null;
        cache.BeginIteration();
    }

    public Chunk? Current { get; private set; }

    public bool IncludeDisabled => cache!.IncludeDisabled;

    public bool MoveNext()
    {
        var currentCache = cache!;
        while (archetypeIndex < currentCache.Count)
        {
            var chunks = currentCache[archetypeIndex].Chunks;
            if (++chunkIndex < chunks.Count)
            {
                Current = chunks[chunkIndex];
                return true;
            }

            archetypeIndex++;
            chunkIndex = -1;
        }

        Current = null;
        return false;
    }

    public void Dispose()
    {
        if (!active)
        {
            return;
        }

        active = false;
        cache!.EndIteration();
        cache = null;
        Current = null;
    }
}

public sealed partial class World
{
    private readonly List<QueryCache> queryCaches = new();

    internal QueryCache CreateQueryCache(in QueryFilter filter)
    {
        registry.ValidateSignature(filter.All);
        registry.ValidateSignature(filter.Any);
        registry.ValidateSignature(filter.None);
        var cache = new QueryCache(this, in filter, archetypes);
        queryCaches.Add(cache);
        return cache;
    }

    partial void OnArchetypeCreated(Archetype archetype)
    {
        for (var index = 0; index < queryCaches.Count; index++)
        {
            queryCaches[index].TryAdd(archetype);
        }
    }
}
