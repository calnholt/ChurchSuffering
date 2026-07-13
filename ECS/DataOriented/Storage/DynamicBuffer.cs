#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Storage;

public readonly struct DynamicBuffer<T>
    where T : unmanaged
{
    private readonly DynamicBufferPool<T>? pool;
    private readonly DynamicBufferHandle<T> handle;

    internal DynamicBuffer(DynamicBufferPool<T> pool, DynamicBufferHandle<T> handle)
    {
        this.pool = pool;
        this.handle = handle;
    }

    public DynamicBufferHandle<T> Handle => handle;

    public int Count => RequiredPool.Count(handle);

    public int Capacity => RequiredPool.Capacity(handle);

    public ref T this[int index] => ref RequiredPool.Item(handle, index);

    public Span<T> AsSpan() => RequiredPool.AsSpan(handle);

    public ReadOnlySpan<T> AsReadOnlySpan() => RequiredPool.AsSpan(handle);

    public void Add(in T value) => RequiredPool.Add(handle, in value);

    public void AddRange(ReadOnlySpan<T> values) => RequiredPool.AddRange(handle, values);

    public void Insert(int index, in T value) => RequiredPool.Insert(handle, index, in value);

    public void RemoveAt(int index) => RequiredPool.RemoveAt(handle, index);

    public void RemoveAtSwapBack(int index) => RequiredPool.RemoveAtSwapBack(handle, index);

    public void Clear() => RequiredPool.Clear(handle);

    public void EnsureCapacity(int capacity) => RequiredPool.EnsureCapacity(handle, capacity);

    public void Resize(int count) => RequiredPool.Resize(handle, count);

    private DynamicBufferPool<T> RequiredPool => pool ??
        throw new InvalidOperationException("A default dynamic buffer view cannot be accessed.");
}
