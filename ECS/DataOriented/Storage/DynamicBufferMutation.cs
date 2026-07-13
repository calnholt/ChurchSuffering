#nullable enable

namespace Crusaders30XX.ECS.DataOriented.Storage;

public enum DynamicBufferMutationOperation : byte
{
    Add = 1,
    Insert = 2,
    Set = 3,
    RemoveAt = 4,
    RemoveAtSwapBack = 5,
    Clear = 6,
    Resize = 7,
    EnsureCapacity = 8,
}

/// <summary>
/// Unmanaged command payload used by structural command buffers to defer buffer writes.
/// </summary>
public readonly record struct DynamicBufferMutation<T>(
    DynamicBufferHandle<T> Handle,
    DynamicBufferMutationOperation Operation,
    int Index,
    T Value)
    where T : unmanaged
{
    public static DynamicBufferMutation<T> Add(DynamicBufferHandle<T> handle, in T value) =>
        new(handle, DynamicBufferMutationOperation.Add, 0, value);

    public static DynamicBufferMutation<T> Insert(DynamicBufferHandle<T> handle, int index, in T value) =>
        new(handle, DynamicBufferMutationOperation.Insert, index, value);

    public static DynamicBufferMutation<T> Set(DynamicBufferHandle<T> handle, int index, in T value) =>
        new(handle, DynamicBufferMutationOperation.Set, index, value);

    public static DynamicBufferMutation<T> RemoveAt(DynamicBufferHandle<T> handle, int index) =>
        new(handle, DynamicBufferMutationOperation.RemoveAt, index, default);

    public static DynamicBufferMutation<T> RemoveAtSwapBack(DynamicBufferHandle<T> handle, int index) =>
        new(handle, DynamicBufferMutationOperation.RemoveAtSwapBack, index, default);

    public static DynamicBufferMutation<T> Clear(DynamicBufferHandle<T> handle) =>
        new(handle, DynamicBufferMutationOperation.Clear, 0, default);

    public static DynamicBufferMutation<T> Resize(DynamicBufferHandle<T> handle, int count) =>
        new(handle, DynamicBufferMutationOperation.Resize, count, default);

    public static DynamicBufferMutation<T> EnsureCapacity(DynamicBufferHandle<T> handle, int capacity) =>
        new(handle, DynamicBufferMutationOperation.EnsureCapacity, capacity, default);
}
