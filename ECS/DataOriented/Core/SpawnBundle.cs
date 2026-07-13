#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Crusaders30XX.ECS.DataOriented.Core;

public struct SpawnBundle
{
    private SpawnBundleEntry[]? entries;
    private byte[]? data;
    private int entryCount;
    private int dataLength;
    private ComponentSignature signature;

    public SpawnBundle(int componentCapacity, int byteCapacity = 64)
    {
        if (componentCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(componentCapacity));
        }

        if (byteCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(byteCapacity));
        }

        entries = componentCapacity == 0 ? null : new SpawnBundleEntry[componentCapacity];
        data = byteCapacity == 0 ? null : new byte[byteCapacity];
        entryCount = 0;
        dataLength = 0;
        signature = default;
    }

    public ComponentSignature Signature => signature;

    public int ComponentCount => entryCount;

    public void Add<T>(in T value)
        where T : unmanaged, IComponent
    {
        var typeId = ComponentType<T>.Id;
        EnsureNotPresent(typeId);

        var size = Unsafe.SizeOf<T>();
        EnsureEntryCapacity(entryCount + 1);
        EnsureDataCapacity(dataLength + size);

        var copy = value;
        MemoryMarshal.Write(data.AsSpan(dataLength, size), in copy);
        entries![entryCount++] = new SpawnBundleEntry(typeId, dataLength, size);
        dataLength += size;
        signature = signature.With(typeId);
    }

    public void AddTag<T>()
        where T : unmanaged, ITag
    {
        var typeId = ComponentType<T>.Id;
        EnsureNotPresent(typeId);
        signature = signature.With(typeId);
    }

    public void Clear()
    {
        if (dataLength > 0)
        {
            data.AsSpan(0, dataLength).Clear();
        }

        if (entryCount > 0)
        {
            entries.AsSpan(0, entryCount).Clear();
        }

        entryCount = 0;
        dataLength = 0;
        signature = default;
    }

    internal bool TryGetComponentBytes(int typeId, out ReadOnlySpan<byte> bytes)
    {
        for (var index = 0; index < entryCount; index++)
        {
            ref readonly var entry = ref entries![index];
            if (entry.TypeId == typeId)
            {
                bytes = data.AsSpan(entry.Offset, entry.Size);
                return true;
            }
        }

        bytes = default;
        return false;
    }

    internal void CopyTo(ref SpawnBundle destination)
    {
        destination.EnsureEntryCapacity(entryCount);
        destination.EnsureDataCapacity(dataLength);
        if (entryCount > 0)
        {
            entries.AsSpan(0, entryCount).CopyTo(destination.entries!);
        }

        if (dataLength > 0)
        {
            data.AsSpan(0, dataLength).CopyTo(destination.data!);
        }

        destination.entryCount = entryCount;
        destination.dataLength = dataLength;
        destination.signature = signature;
    }

    private void EnsureNotPresent(int typeId)
    {
        if (signature.Contains(typeId))
        {
            throw new InvalidOperationException($"Type ID {typeId} already exists in this spawn bundle.");
        }
    }

    private void EnsureEntryCapacity(int required)
    {
        if (entries is not null && entries.Length >= required)
        {
            return;
        }

        var capacity = Math.Max(required, entries is null ? 4 : entries.Length * 2);
        Array.Resize(ref entries, capacity);
    }

    private void EnsureDataCapacity(int required)
    {
        if (data is not null && data.Length >= required)
        {
            return;
        }

        var capacity = Math.Max(required, data is null ? 64 : data.Length * 2);
        Array.Resize(ref data, capacity);
    }

    private readonly record struct SpawnBundleEntry(int TypeId, int Offset, int Size);
}
