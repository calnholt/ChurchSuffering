#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Storage;

/// <summary>
/// World-scoped owner of every typed dynamic buffer. Register one store with its owning
/// <see cref="World"/> so entity destruction releases every buffer owned by that entity.
/// </summary>
public sealed class DynamicBufferStore : IEntityDestructionListener, IDisposable
{
    private readonly Dictionary<Type, IDynamicBufferPool> poolsByType = new();
    private readonly List<IDynamicBufferPool> pools = new();
    private bool disposed;

    public int BufferTypeCount => pools.Count;

    public int ActiveBufferCount
    {
        get
        {
            var count = 0;
            for (var index = 0; index < pools.Count; index++)
            {
                count += pools[index].ActiveCount;
            }

            return count;
        }
    }

    public DynamicBufferHandle<T> Create<T>(EntityId owner, int initialCapacity = 0)
        where T : unmanaged
    {
        ThrowIfDisposed();
        if (owner.IsNull)
        {
            throw new ArgumentException("A dynamic buffer must have a non-null owning entity.", nameof(owner));
        }

        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        return GetOrCreatePool<T>().Create(owner, initialCapacity);
    }

    public DynamicBuffer<T> Get<T>(DynamicBufferHandle<T> handle)
        where T : unmanaged
    {
        ThrowIfDisposed();
        return GetRequiredPool<T>().Get(handle);
    }

    public bool TryGet<T>(DynamicBufferHandle<T> handle, out DynamicBuffer<T> buffer)
        where T : unmanaged
    {
        if (disposed || !poolsByType.TryGetValue(typeof(T), out IDynamicBufferPool? untypedPool))
        {
            buffer = default;
            return false;
        }

        var pool = (DynamicBufferPool<T>)untypedPool;
        return pool.TryGet(handle, out buffer);
    }

    public void Release<T>(DynamicBufferHandle<T> handle)
        where T : unmanaged
    {
        ThrowIfDisposed();
        GetRequiredPool<T>().Release(handle);
    }

    public IDynamicBufferCommandHandler<DynamicBufferMutation<T>> GetMutationHandler<T>()
        where T : unmanaged
    {
        ThrowIfDisposed();
        return GetOrCreatePool<T>().MutationHandler;
    }

    public int ReleaseOwnedBy(EntityId owner)
    {
        ThrowIfDisposed();
        if (owner.IsNull)
        {
            return 0;
        }

        var released = 0;
        for (var index = 0; index < pools.Count; index++)
        {
            released += pools[index].ReleaseOwnedBy(owner);
        }

        return released;
    }

    public void OnEntityDestroyed(EntityId entity)
    {
        ReleaseOwnedBy(entity);
    }

    public bool TryGetDebugInfo<T>(DynamicBufferHandle<T> handle, out DynamicBufferDebugInfo info)
        where T : unmanaged
    {
        if (disposed || !poolsByType.TryGetValue(typeof(T), out IDynamicBufferPool? untypedPool))
        {
            info = default;
            return false;
        }

        return ((DynamicBufferPool<T>)untypedPool).TryGetDebugInfo(handle, out info);
    }

    /// <summary>
    /// Creates an allocating snapshot intended only for diagnostics and tooling.
    /// Runtime paths should use typed handles instead.
    /// </summary>
    public DynamicBufferDebugInfo[] GetDebugSnapshot()
    {
        ThrowIfDisposed();
        var result = new DynamicBufferDebugInfo[ActiveBufferCount];
        var written = 0;
        for (var index = 0; index < pools.Count; index++)
        {
            written += pools[index].CopyDebugInfo(result.AsSpan(written));
        }

        return result;
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        for (var index = 0; index < pools.Count; index++)
        {
            pools[index].Dispose();
        }

        pools.Clear();
        poolsByType.Clear();
        disposed = true;
    }

    private DynamicBufferPool<T> GetOrCreatePool<T>()
        where T : unmanaged
    {
        if (poolsByType.TryGetValue(typeof(T), out IDynamicBufferPool? existing))
        {
            return (DynamicBufferPool<T>)existing;
        }

        var created = new DynamicBufferPool<T>(this);
        poolsByType.Add(typeof(T), created);
        pools.Add(created);
        return created;
    }

    private DynamicBufferPool<T> GetRequiredPool<T>()
        where T : unmanaged
    {
        if (!poolsByType.TryGetValue(typeof(T), out IDynamicBufferPool? pool))
        {
            throw new InvalidOperationException(
                $"Dynamic buffer handle {typeof(T).FullName} has no pool in this world store.");
        }

        return (DynamicBufferPool<T>)pool;
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(disposed, this);
    }
}

internal interface IDynamicBufferPool : IDisposable
{
    int ActiveCount { get; }

    int ReleaseOwnedBy(EntityId owner);

    int CopyDebugInfo(Span<DynamicBufferDebugInfo> destination);
}

internal sealed class DynamicBufferPool<T> : IDynamicBufferPool
    where T : unmanaged
{
    private const int MinimumSlotCapacity = 16;
    private const int MinimumDataCapacity = 4;

    private BufferSlot[] slots = new BufferSlot[MinimumSlotCapacity + 1];
    private int[] freeIndexes = new int[MinimumSlotCapacity];
    private readonly List<T[]> returnedArrays = new(MinimumSlotCapacity);
    private int nextIndex = 1;
    private int freeIndexCount;

    public DynamicBufferPool(DynamicBufferStore store)
    {
        MutationHandler = new DynamicBufferMutationHandler<T>(store, this);
    }

    public IDynamicBufferCommandHandler<DynamicBufferMutation<T>> MutationHandler { get; }

    public int ActiveCount { get; private set; }

    public DynamicBufferHandle<T> Create(EntityId owner, int initialCapacity)
    {
        int index;
        if (freeIndexCount > 0)
        {
            index = freeIndexes[--freeIndexCount];
        }
        else
        {
            index = nextIndex++;
            EnsureSlotCapacity(index + 1);
        }

        ref BufferSlot slot = ref slots[index];
        if (slot.Generation == 0)
        {
            slot.Generation = 1;
        }

        slot.Items = initialCapacity == 0 ? Array.Empty<T>() : RentArray(initialCapacity);
        slot.Count = 0;
        slot.Owner = owner;
        slot.Active = true;
        ActiveCount++;
        return new DynamicBufferHandle<T>(index, slot.Generation);
    }

    public DynamicBuffer<T> Get(DynamicBufferHandle<T> handle)
    {
        Validate(handle);
        return new DynamicBuffer<T>(this, handle);
    }

    public bool TryGet(DynamicBufferHandle<T> handle, out DynamicBuffer<T> buffer)
    {
        if (!IsValid(handle))
        {
            buffer = default;
            return false;
        }

        buffer = new DynamicBuffer<T>(this, handle);
        return true;
    }

    public void Release(DynamicBufferHandle<T> handle)
    {
        ref BufferSlot slot = ref Validate(handle);
        Release(handle.Index, ref slot);
    }

    public int ReleaseOwnedBy(EntityId owner)
    {
        var released = 0;
        for (var index = 1; index < nextIndex; index++)
        {
            ref BufferSlot slot = ref slots[index];
            if (slot.Active && slot.Owner == owner)
            {
                Release(index, ref slot);
                released++;
            }
        }

        return released;
    }

    public int CopyDebugInfo(Span<DynamicBufferDebugInfo> destination)
    {
        var written = 0;
        for (var index = 1; index < nextIndex; index++)
        {
            ref BufferSlot slot = ref slots[index];
            if (!slot.Active)
            {
                continue;
            }

            destination[written++] = new DynamicBufferDebugInfo(
                typeof(T),
                index,
                slot.Generation,
                slot.Owner,
                slot.Count,
                slot.Items!.Length);
        }

        return written;
    }

    public bool TryGetDebugInfo(DynamicBufferHandle<T> handle, out DynamicBufferDebugInfo info)
    {
        if (!IsValid(handle))
        {
            info = default;
            return false;
        }

        ref BufferSlot slot = ref slots[handle.Index];
        info = new DynamicBufferDebugInfo(
            typeof(T),
            handle.Index,
            handle.Generation,
            slot.Owner,
            slot.Count,
            slot.Items!.Length);
        return true;
    }

    public int Count(DynamicBufferHandle<T> handle) => Validate(handle).Count;

    public int Capacity(DynamicBufferHandle<T> handle) => Validate(handle).Items!.Length;

    public ref T Item(DynamicBufferHandle<T> handle, int index)
    {
        ref BufferSlot slot = ref Validate(handle);
        if ((uint)index >= slot.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        return ref slot.Items![index];
    }

    public void Set(DynamicBufferHandle<T> handle, int index, in T value)
    {
        Item(handle, index) = value;
    }

    public Span<T> AsSpan(DynamicBufferHandle<T> handle)
    {
        ref BufferSlot slot = ref Validate(handle);
        return slot.Items.AsSpan(0, slot.Count);
    }

    public void Add(DynamicBufferHandle<T> handle, in T value)
    {
        ref BufferSlot slot = ref Validate(handle);
        EnsureDataCapacity(ref slot, slot.Count + 1);
        slot.Items![slot.Count++] = value;
    }

    public void AddRange(DynamicBufferHandle<T> handle, ReadOnlySpan<T> values)
    {
        ref BufferSlot slot = ref Validate(handle);
        EnsureDataCapacity(ref slot, checked(slot.Count + values.Length));
        values.CopyTo(slot.Items.AsSpan(slot.Count));
        slot.Count += values.Length;
    }

    public void Insert(DynamicBufferHandle<T> handle, int index, in T value)
    {
        ref BufferSlot slot = ref Validate(handle);
        if ((uint)index > slot.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        EnsureDataCapacity(ref slot, slot.Count + 1);
        slot.Items.AsSpan(index, slot.Count - index).CopyTo(slot.Items.AsSpan(index + 1));
        slot.Items![index] = value;
        slot.Count++;
    }

    public void RemoveAt(DynamicBufferHandle<T> handle, int index)
    {
        ref BufferSlot slot = ref Validate(handle);
        if ((uint)index >= slot.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        slot.Items.AsSpan(index + 1, slot.Count - index - 1).CopyTo(slot.Items.AsSpan(index));
        slot.Items![--slot.Count] = default;
    }

    public void RemoveAtSwapBack(DynamicBufferHandle<T> handle, int index)
    {
        ref BufferSlot slot = ref Validate(handle);
        if ((uint)index >= slot.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(index));
        }

        var last = --slot.Count;
        slot.Items![index] = slot.Items[last];
        slot.Items[last] = default;
    }

    public void Clear(DynamicBufferHandle<T> handle)
    {
        ref BufferSlot slot = ref Validate(handle);
        slot.Items.AsSpan(0, slot.Count).Clear();
        slot.Count = 0;
    }

    public void EnsureCapacity(DynamicBufferHandle<T> handle, int capacity)
    {
        if (capacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        ref BufferSlot slot = ref Validate(handle);
        EnsureDataCapacity(ref slot, capacity);
    }

    public void Resize(DynamicBufferHandle<T> handle, int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        ref BufferSlot slot = ref Validate(handle);
        EnsureDataCapacity(ref slot, count);
        if (count < slot.Count)
        {
            slot.Items.AsSpan(count, slot.Count - count).Clear();
        }
        else if (count > slot.Count)
        {
            slot.Items.AsSpan(slot.Count, count - slot.Count).Clear();
        }

        slot.Count = count;
    }

    public void Dispose()
    {
        for (var index = 1; index < nextIndex; index++)
        {
            ref BufferSlot slot = ref slots[index];
            if (slot.Active)
            {
                Release(index, ref slot);
            }
        }

        returnedArrays.Clear();
    }

    private ref BufferSlot Validate(DynamicBufferHandle<T> handle)
    {
        if (handle.IsNull)
        {
            throw new InvalidOperationException("A null dynamic buffer handle cannot be accessed.");
        }

        if (!IsValid(handle))
        {
            var currentGeneration = handle.Index > 0 && handle.Index < nextIndex
                ? slots[handle.Index].Generation
                : 0;
            throw new InvalidOperationException(
                $"Dynamic buffer handle {handle} is dead or stale; current generation is {currentGeneration}.");
        }

        return ref slots[handle.Index];
    }

    private bool IsValid(DynamicBufferHandle<T> handle)
    {
        return handle.Index > 0 &&
               handle.Index < nextIndex &&
               slots[handle.Index].Active &&
               slots[handle.Index].Generation == handle.Generation;
    }

    private void Release(int index, ref BufferSlot slot)
    {
        if (slot.Items!.Length > 0)
        {
            slot.Items.AsSpan(0, slot.Count).Clear();
            returnedArrays.Add(slot.Items);
        }

        slot.Items = null;
        slot.Count = 0;
        slot.Owner = default;
        slot.Active = false;
        slot.Generation = NextGeneration(slot.Generation);
        PushFreeIndex(index);
        ActiveCount--;
    }

    private void EnsureDataCapacity(ref BufferSlot slot, int required)
    {
        if (slot.Items!.Length >= required)
        {
            return;
        }

        var capacity = Math.Max(required, Math.Max(MinimumDataCapacity, slot.Items.Length * 2));
        T[] replacement = RentArray(capacity);
        slot.Items.AsSpan(0, slot.Count).CopyTo(replacement);
        if (slot.Items.Length > 0)
        {
            returnedArrays.Add(slot.Items);
        }

        slot.Items = replacement;
    }

    private T[] RentArray(int minimumCapacity)
    {
        var bestIndex = -1;
        var bestLength = int.MaxValue;
        for (var index = 0; index < returnedArrays.Count; index++)
        {
            int length = returnedArrays[index].Length;
            if (length >= minimumCapacity && length < bestLength)
            {
                bestIndex = index;
                bestLength = length;
            }
        }

        if (bestIndex < 0)
        {
            return new T[Math.Max(MinimumDataCapacity, minimumCapacity)];
        }

        T[] result = returnedArrays[bestIndex];
        int lastIndex = returnedArrays.Count - 1;
        returnedArrays[bestIndex] = returnedArrays[lastIndex];
        returnedArrays.RemoveAt(lastIndex);
        return result;
    }

    private void EnsureSlotCapacity(int required)
    {
        if (slots.Length >= required)
        {
            return;
        }

        var capacity = Math.Max(required, slots.Length * 2);
        Array.Resize(ref slots, capacity);
        Array.Resize(ref freeIndexes, capacity - 1);
    }

    private void PushFreeIndex(int index)
    {
        if (freeIndexCount == freeIndexes.Length)
        {
            Array.Resize(ref freeIndexes, freeIndexes.Length * 2);
        }

        freeIndexes[freeIndexCount++] = index;
    }

    private static int NextGeneration(int generation)
    {
        int next = unchecked(generation + 1);
        return next <= 0 ? 1 : next;
    }

    private struct BufferSlot
    {
        public T[]? Items;
        public int Count;
        public int Generation;
        public EntityId Owner;
        public bool Active;
    }
}

internal sealed class DynamicBufferMutationHandler<T> : IDynamicBufferCommandHandler<DynamicBufferMutation<T>>
    where T : unmanaged
{
    private readonly DynamicBufferStore store;
    private readonly DynamicBufferPool<T> pool;

    public DynamicBufferMutationHandler(DynamicBufferStore store, DynamicBufferPool<T> pool)
    {
        this.store = store;
        this.pool = pool;
    }

    public void Playback(World world, in DynamicBufferMutation<T> command)
    {
        ArgumentNullException.ThrowIfNull(world);
        if (!ReferenceEquals(world.DynamicBuffers, store))
        {
            throw new InvalidOperationException("A dynamic buffer mutation handler can only play back against its owning world.");
        }

        T value = command.Value;
        switch (command.Operation)
        {
            case DynamicBufferMutationOperation.Add:
                pool.Add(command.Handle, in value);
                break;
            case DynamicBufferMutationOperation.Insert:
                pool.Insert(command.Handle, command.Index, in value);
                break;
            case DynamicBufferMutationOperation.Set:
                pool.Set(command.Handle, command.Index, in value);
                break;
            case DynamicBufferMutationOperation.RemoveAt:
                pool.RemoveAt(command.Handle, command.Index);
                break;
            case DynamicBufferMutationOperation.RemoveAtSwapBack:
                pool.RemoveAtSwapBack(command.Handle, command.Index);
                break;
            case DynamicBufferMutationOperation.Clear:
                pool.Clear(command.Handle);
                break;
            case DynamicBufferMutationOperation.Resize:
                pool.Resize(command.Handle, command.Index);
                break;
            case DynamicBufferMutationOperation.EnsureCapacity:
                pool.EnsureCapacity(command.Handle, command.Index);
                break;
            default:
                throw new InvalidOperationException($"Unknown dynamic buffer mutation operation {command.Operation}.");
        }
    }
}
