#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Events;

public sealed class EventStream<T>
    where T : unmanaged
{
    private T[] pending;
    private T[] current;
    private int pendingCount;
    private int currentCount;

    public EventStream(int initialCapacity = 16)
    {
        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        int capacity = Math.Max(1, initialCapacity);
        pending = new T[capacity];
        current = new T[capacity];
    }

    public int PendingCount => pendingCount;

    public void Publish(in T value)
    {
        EnsurePendingCapacity(pendingCount + 1);
        pending[pendingCount++] = value;
    }

    internal ReadOnlySpan<T> Current => current.AsSpan(0, currentCount);

    internal void BeginWave()
    {
        if (currentCount != 0)
        {
            throw new InvalidOperationException("An event stream wave is already active.");
        }

        (pending, current) = (current, pending);
        currentCount = pendingCount;
        pendingCount = 0;
    }

    internal void EndWave()
    {
        current.AsSpan(0, currentCount).Clear();
        currentCount = 0;
    }

    internal void DiscardAll()
    {
        EndWave();
        pending.AsSpan(0, pendingCount).Clear();
        pendingCount = 0;
    }

    private void EnsurePendingCapacity(int required)
    {
        if (pending.Length >= required)
        {
            return;
        }

        Array.Resize(ref pending, Math.Max(required, pending.Length * 2));
    }
}
