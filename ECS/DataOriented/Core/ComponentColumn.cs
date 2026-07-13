#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Crusaders30XX.ECS.DataOriented.Core;

internal interface IComponentColumn
{
    int TypeId { get; }

    void Clear(int row);

    void CopyTo(int sourceRow, IComponentColumn destination, int destinationRow);

    void CopyWithin(int sourceRow, int destinationRow);

    void SetBytes(int row, ReadOnlySpan<byte> bytes);
}

internal sealed class ComponentColumn<T> : IComponentColumn
    where T : unmanaged, IComponent
{
    private readonly T[] values;

    public ComponentColumn(int typeId, int capacity)
    {
        TypeId = typeId;
        values = new T[capacity];
    }

    public int TypeId { get; }

    public ref T Get(int row) => ref values[row];

    public Span<T> AsSpan(int count) => values.AsSpan(0, count);

    public void Clear(int row)
    {
        values[row] = default;
    }

    public void CopyTo(int sourceRow, IComponentColumn destination, int destinationRow)
    {
        if (destination is not ComponentColumn<T> typedDestination)
        {
            throw new InvalidOperationException($"Component column type mismatch for type ID {TypeId}.");
        }

        typedDestination.values[destinationRow] = values[sourceRow];
    }

    public void CopyWithin(int sourceRow, int destinationRow)
    {
        values[destinationRow] = values[sourceRow];
    }

    public void SetBytes(int row, ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length != Unsafe.SizeOf<T>())
        {
            throw new InvalidOperationException($"Invalid serialized size for component type ID {TypeId}.");
        }

        values[row] = MemoryMarshal.Read<T>(bytes);
    }
}
