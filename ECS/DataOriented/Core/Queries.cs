#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Generated;

namespace Crusaders30XX.ECS.DataOriented.Core;

public sealed class Query<T1>
    where T1 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1> : IDisposable
    where T1 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1>
    where T1 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed class Query<T1, T2>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1, T2> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1, T2> : IDisposable
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1, T2> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1, T2>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
        Component2 = chunk.GetSpan<T2>(ComponentType<T2>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public Span<T2> Component2 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed class Query<T1, T2, T3>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1, T2, T3> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1, T2, T3> : IDisposable
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1, T2, T3> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1, T2, T3>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
        Component2 = chunk.GetSpan<T2>(ComponentType<T2>.Id);
        Component3 = chunk.GetSpan<T3>(ComponentType<T3>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public Span<T2> Component2 { get; }

    public Span<T3> Component3 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed class Query<T1, T2, T3, T4>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1, T2, T3, T4> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1, T2, T3, T4> : IDisposable
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1, T2, T3, T4> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1, T2, T3, T4>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
        Component2 = chunk.GetSpan<T2>(ComponentType<T2>.Id);
        Component3 = chunk.GetSpan<T3>(ComponentType<T3>.Id);
        Component4 = chunk.GetSpan<T4>(ComponentType<T4>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public Span<T2> Component2 { get; }

    public Span<T3> Component3 { get; }

    public Span<T4> Component4 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed class Query<T1, T2, T3, T4, T5>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1, T2, T3, T4, T5> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1, T2, T3, T4, T5> : IDisposable
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1, T2, T3, T4, T5> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1, T2, T3, T4, T5>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
        Component2 = chunk.GetSpan<T2>(ComponentType<T2>.Id);
        Component3 = chunk.GetSpan<T3>(ComponentType<T3>.Id);
        Component4 = chunk.GetSpan<T4>(ComponentType<T4>.Id);
        Component5 = chunk.GetSpan<T5>(ComponentType<T5>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public Span<T2> Component2 { get; }

    public Span<T3> Component3 { get; }

    public Span<T4> Component4 { get; }

    public Span<T5> Component5 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed class Query<T1, T2, T3, T4, T5, T6>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1, T2, T3, T4, T5, T6> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1, T2, T3, T4, T5, T6> : IDisposable
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1, T2, T3, T4, T5, T6> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1, T2, T3, T4, T5, T6>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
        Component2 = chunk.GetSpan<T2>(ComponentType<T2>.Id);
        Component3 = chunk.GetSpan<T3>(ComponentType<T3>.Id);
        Component4 = chunk.GetSpan<T4>(ComponentType<T4>.Id);
        Component5 = chunk.GetSpan<T5>(ComponentType<T5>.Id);
        Component6 = chunk.GetSpan<T6>(ComponentType<T6>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public Span<T2> Component2 { get; }

    public Span<T3> Component3 { get; }

    public Span<T4> Component4 { get; }

    public Span<T5> Component5 { get; }

    public Span<T6> Component6 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed class Query<T1, T2, T3, T4, T5, T6, T7>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
    where T7 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1, T2, T3, T4, T5, T6, T7> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1, T2, T3, T4, T5, T6, T7> : IDisposable
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
    where T7 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1, T2, T3, T4, T5, T6, T7> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1, T2, T3, T4, T5, T6, T7>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
    where T7 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
        Component2 = chunk.GetSpan<T2>(ComponentType<T2>.Id);
        Component3 = chunk.GetSpan<T3>(ComponentType<T3>.Id);
        Component4 = chunk.GetSpan<T4>(ComponentType<T4>.Id);
        Component5 = chunk.GetSpan<T5>(ComponentType<T5>.Id);
        Component6 = chunk.GetSpan<T6>(ComponentType<T6>.Id);
        Component7 = chunk.GetSpan<T7>(ComponentType<T7>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public Span<T2> Component2 { get; }

    public Span<T3> Component3 { get; }

    public Span<T4> Component4 { get; }

    public Span<T5> Component5 { get; }

    public Span<T6> Component6 { get; }

    public Span<T7> Component7 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed class Query<T1, T2, T3, T4, T5, T6, T7, T8>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
    where T7 : unmanaged, IComponent
    where T8 : unmanaged, IComponent
{
    private readonly QueryCache cache;

    internal Query(QueryCache cache)
    {
        this.cache = cache;
    }

    public QueryEnumerator<T1, T2, T3, T4, T5, T6, T7, T8> GetEnumerator() => new(cache);
}

public struct QueryEnumerator<T1, T2, T3, T4, T5, T6, T7, T8> : IDisposable
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
    where T7 : unmanaged, IComponent
    where T8 : unmanaged, IComponent
{
    private QueryChunkEnumeratorCore core;

    internal QueryEnumerator(QueryCache cache)
    {
        core = new QueryChunkEnumeratorCore(cache);
    }

    public QueryChunk<T1, T2, T3, T4, T5, T6, T7, T8> Current => new(core.Current!, core.IncludeDisabled);

    public bool MoveNext() => core.MoveNext();

    public void Dispose() => core.Dispose();
}

public readonly ref struct QueryChunk<T1, T2, T3, T4, T5, T6, T7, T8>
    where T1 : unmanaged, IComponent
    where T2 : unmanaged, IComponent
    where T3 : unmanaged, IComponent
    where T4 : unmanaged, IComponent
    where T5 : unmanaged, IComponent
    where T6 : unmanaged, IComponent
    where T7 : unmanaged, IComponent
    where T8 : unmanaged, IComponent
{
    private readonly ReadOnlySpan<bool> enabled;
    private readonly bool includeDisabled;

    internal QueryChunk(Chunk chunk, bool includeDisabled)
    {
        Entities = chunk.Entities;
        enabled = chunk.Enabled;
        this.includeDisabled = includeDisabled;
        Component1 = chunk.GetSpan<T1>(ComponentType<T1>.Id);
        Component2 = chunk.GetSpan<T2>(ComponentType<T2>.Id);
        Component3 = chunk.GetSpan<T3>(ComponentType<T3>.Id);
        Component4 = chunk.GetSpan<T4>(ComponentType<T4>.Id);
        Component5 = chunk.GetSpan<T5>(ComponentType<T5>.Id);
        Component6 = chunk.GetSpan<T6>(ComponentType<T6>.Id);
        Component7 = chunk.GetSpan<T7>(ComponentType<T7>.Id);
        Component8 = chunk.GetSpan<T8>(ComponentType<T8>.Id);
    }

    public ReadOnlySpan<EntityId> Entities { get; }

    public Span<T1> Component1 { get; }

    public Span<T2> Component2 { get; }

    public Span<T3> Component3 { get; }

    public Span<T4> Component4 { get; }

    public Span<T5> Component5 { get; }

    public Span<T6> Component6 { get; }

    public Span<T7> Component7 { get; }

    public Span<T8> Component8 { get; }

    public int Count => Entities.Length;

    public QueryRows Rows => new(enabled, includeDisabled);
}

public sealed partial class World
{
    public Query<T1> Query<T1>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1>(CreateQueryCache(in completeFilter));
    }

    public Query<T1> Query<T1>(in GeneratedQueryDescriptor<T1> descriptor)
        where T1 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1>(filter);
    }

    public Query<T1, T2> Query<T1, T2>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        AddReturnedType<T2>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1, T2>(CreateQueryCache(in completeFilter));
    }

    public Query<T1, T2> Query<T1, T2>(in GeneratedQueryDescriptor<T1, T2> descriptor)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1, T2>(filter);
    }

    public Query<T1, T2, T3> Query<T1, T2, T3>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        AddReturnedType<T2>(ref all);
        AddReturnedType<T3>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1, T2, T3>(CreateQueryCache(in completeFilter));
    }

    public Query<T1, T2, T3> Query<T1, T2, T3>(in GeneratedQueryDescriptor<T1, T2, T3> descriptor)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1, T2, T3>(filter);
    }

    public Query<T1, T2, T3, T4> Query<T1, T2, T3, T4>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        AddReturnedType<T2>(ref all);
        AddReturnedType<T3>(ref all);
        AddReturnedType<T4>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1, T2, T3, T4>(CreateQueryCache(in completeFilter));
    }

    public Query<T1, T2, T3, T4> Query<T1, T2, T3, T4>(in GeneratedQueryDescriptor<T1, T2, T3, T4> descriptor)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1, T2, T3, T4>(filter);
    }

    public Query<T1, T2, T3, T4, T5> Query<T1, T2, T3, T4, T5>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        AddReturnedType<T2>(ref all);
        AddReturnedType<T3>(ref all);
        AddReturnedType<T4>(ref all);
        AddReturnedType<T5>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1, T2, T3, T4, T5>(CreateQueryCache(in completeFilter));
    }

    public Query<T1, T2, T3, T4, T5> Query<T1, T2, T3, T4, T5>(in GeneratedQueryDescriptor<T1, T2, T3, T4, T5> descriptor)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1, T2, T3, T4, T5>(filter);
    }

    public Query<T1, T2, T3, T4, T5, T6> Query<T1, T2, T3, T4, T5, T6>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        AddReturnedType<T2>(ref all);
        AddReturnedType<T3>(ref all);
        AddReturnedType<T4>(ref all);
        AddReturnedType<T5>(ref all);
        AddReturnedType<T6>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1, T2, T3, T4, T5, T6>(CreateQueryCache(in completeFilter));
    }

    public Query<T1, T2, T3, T4, T5, T6> Query<T1, T2, T3, T4, T5, T6>(in GeneratedQueryDescriptor<T1, T2, T3, T4, T5, T6> descriptor)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1, T2, T3, T4, T5, T6>(filter);
    }

    public Query<T1, T2, T3, T4, T5, T6, T7> Query<T1, T2, T3, T4, T5, T6, T7>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        AddReturnedType<T2>(ref all);
        AddReturnedType<T3>(ref all);
        AddReturnedType<T4>(ref all);
        AddReturnedType<T5>(ref all);
        AddReturnedType<T6>(ref all);
        AddReturnedType<T7>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1, T2, T3, T4, T5, T6, T7>(CreateQueryCache(in completeFilter));
    }

    public Query<T1, T2, T3, T4, T5, T6, T7> Query<T1, T2, T3, T4, T5, T6, T7>(in GeneratedQueryDescriptor<T1, T2, T3, T4, T5, T6, T7> descriptor)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1, T2, T3, T4, T5, T6, T7>(filter);
    }

    public Query<T1, T2, T3, T4, T5, T6, T7, T8> Query<T1, T2, T3, T4, T5, T6, T7, T8>(QueryFilter filter = default)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
        where T8 : unmanaged, IComponent
    {
        var all = filter.All;
        AddReturnedType<T1>(ref all);
        AddReturnedType<T2>(ref all);
        AddReturnedType<T3>(ref all);
        AddReturnedType<T4>(ref all);
        AddReturnedType<T5>(ref all);
        AddReturnedType<T6>(ref all);
        AddReturnedType<T7>(ref all);
        AddReturnedType<T8>(ref all);
        var completeFilter = new QueryFilter(all, filter.Any, filter.None, filter.IncludeDisabled, filter.DebugName);
        return new Query<T1, T2, T3, T4, T5, T6, T7, T8>(CreateQueryCache(in completeFilter));
    }

    public Query<T1, T2, T3, T4, T5, T6, T7, T8> Query<T1, T2, T3, T4, T5, T6, T7, T8>(in GeneratedQueryDescriptor<T1, T2, T3, T4, T5, T6, T7, T8> descriptor)
        where T1 : unmanaged, IComponent
        where T2 : unmanaged, IComponent
        where T3 : unmanaged, IComponent
        where T4 : unmanaged, IComponent
        where T5 : unmanaged, IComponent
        where T6 : unmanaged, IComponent
        where T7 : unmanaged, IComponent
        where T8 : unmanaged, IComponent
    {
        var filter = new QueryFilter(
            descriptor.Required,
            descriptor.Any,
            descriptor.None,
            descriptor.IncludeDisabled,
            descriptor.StableId);
        return Query<T1, T2, T3, T4, T5, T6, T7, T8>(filter);
    }

    private void AddReturnedType<T>(ref ComponentSignature signature)
        where T : unmanaged, IComponent
    {
        var typeId = ComponentType<T>.Id;
        var descriptor = registry.GetDescriptor(typeId);
        if (descriptor.Metadata.IsTag)
        {
            throw new InvalidOperationException($"Returned query type {typeof(T).FullName} is registered as a tag.");
        }

        signature = signature.With(typeId);
    }
}
