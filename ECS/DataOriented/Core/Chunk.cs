#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Core;

internal sealed class Chunk
{
    private readonly EntityId[] entities;
    private readonly bool[] enabled;
    private readonly IComponentColumn[] columns;

    public Chunk(Archetype archetype, int capacity, ComponentTypeRegistry registry)
    {
        Archetype = archetype;
        Capacity = capacity;
        entities = new EntityId[capacity];
        enabled = new bool[capacity];
        columns = new IComponentColumn[archetype.ComponentCount];

        var columnIndex = 0;
        for (var typeId = 0; typeId < ComponentSignature.MaximumTypeCount; typeId++)
        {
            if (!archetype.Signature.Contains(typeId))
            {
                continue;
            }

            var descriptor = registry.GetDescriptor(typeId);
            if (!descriptor.Metadata.IsTag)
            {
                columns[columnIndex++] = descriptor.CreateColumn(capacity);
            }
        }
    }

    public Archetype Archetype { get; }

    public int Capacity { get; }

    public int Count { get; private set; }

    public bool HasCapacity => Count < Capacity;

    public ReadOnlySpan<EntityId> Entities => entities.AsSpan(0, Count);

    public ReadOnlySpan<bool> Enabled => enabled.AsSpan(0, Count);

    public int Add(EntityId entity, bool isEnabled)
    {
        if (!HasCapacity)
        {
            throw new InvalidOperationException("Cannot add an entity to a full chunk.");
        }

        var row = Count++;
        entities[row] = entity;
        enabled[row] = isEnabled;
        return row;
    }

    public EntityId GetEntity(int row) => entities[row];

    public bool IsEnabled(int row) => enabled[row];

    public void SetEnabled(int row, bool value)
    {
        enabled[row] = value;
    }

    public ComponentColumn<T> GetColumn<T>(int typeId)
        where T : unmanaged, IComponent
    {
        var columnIndex = Archetype.GetColumnIndex(typeId);
        if (columnIndex < 0 || columns[columnIndex] is not ComponentColumn<T> column)
        {
            throw new InvalidOperationException($"Archetype does not contain component {typeof(T).FullName}.");
        }

        return column;
    }

    public Span<T> GetSpan<T>(int typeId)
        where T : unmanaged, IComponent =>
        GetColumn<T>(typeId).AsSpan(Count);

    public IComponentColumn GetColumn(int typeId)
    {
        var columnIndex = Archetype.GetColumnIndex(typeId);
        if (columnIndex < 0)
        {
            throw new InvalidOperationException($"Archetype does not contain component type ID {typeId}.");
        }

        return columns[columnIndex];
    }

    public void CopyRetainedComponentsTo(int sourceRow, Chunk destination, int destinationRow)
    {
        foreach (var sourceColumn in columns)
        {
            var destinationColumnIndex = destination.Archetype.GetColumnIndex(sourceColumn.TypeId);
            if (destinationColumnIndex >= 0)
            {
                sourceColumn.CopyTo(sourceRow, destination.columns[destinationColumnIndex], destinationRow);
            }
        }
    }

    public EntityId RemoveSwap(int row)
    {
        if ((uint)row >= Count)
        {
            throw new ArgumentOutOfRangeException(nameof(row));
        }

        var lastRow = Count - 1;
        var moved = EntityId.Null;
        if (row != lastRow)
        {
            moved = entities[lastRow];
            entities[row] = moved;
            enabled[row] = enabled[lastRow];

            foreach (var column in columns)
            {
                column.CopyWithin(lastRow, row);
            }
        }

        entities[lastRow] = default;
        enabled[lastRow] = false;
        foreach (var column in columns)
        {
            column.Clear(lastRow);
        }

        Count--;
        return moved;
    }
}
