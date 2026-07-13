#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Crusaders30XX.ECS.DataOriented.Core;

internal sealed class Archetype
{
    private const int TargetChunkBytes = 16 * 1024;
    private const int MaximumChunkCapacity = 1024;

    private readonly ComponentTypeRegistry registry;
    private readonly short[] columnIndexes = new short[ComponentSignature.MaximumTypeCount];
    private readonly List<Chunk> chunks = new();
    private readonly Stack<Chunk> emptyChunks = new();
    private readonly Archetype?[] addTransitions = new Archetype?[ComponentSignature.MaximumTypeCount];
    private readonly Archetype?[] removeTransitions = new Archetype?[ComponentSignature.MaximumTypeCount];

    public Archetype(ComponentSignature signature, ComponentTypeRegistry registry)
    {
        Signature = signature;
        this.registry = registry;
        Array.Fill(columnIndexes, (short)-1);

        var rowSize = Unsafe.SizeOf<EntityId>();
        var columnIndex = 0;
        for (var typeId = 0; typeId < ComponentSignature.MaximumTypeCount; typeId++)
        {
            if (!signature.Contains(typeId))
            {
                continue;
            }

            var metadata = registry.GetDescriptor(typeId).Metadata;
            if (!metadata.IsTag)
            {
                columnIndexes[typeId] = checked((short)columnIndex++);
                rowSize += metadata.Size;
            }
        }

        ComponentCount = columnIndex;
        ChunkCapacity = Math.Clamp(TargetChunkBytes / rowSize, 1, MaximumChunkCapacity);
    }

    public ComponentSignature Signature { get; }

    public int ComponentCount { get; }

    public int ChunkCapacity { get; }

    public IReadOnlyList<Chunk> Chunks => chunks;

    public int AllocatedChunkCount { get; private set; }

    public int ReusableChunkCount => emptyChunks.Count;

    public int GetColumnIndex(int typeId) => columnIndexes[typeId];

    public Archetype? GetTransition(int typeId, bool add) =>
        add ? addTransitions[typeId] : removeTransitions[typeId];

    public void SetTransition(int typeId, bool add, Archetype target)
    {
        if (add)
        {
            addTransitions[typeId] = target;
            target.removeTransitions[typeId] = this;
        }
        else
        {
            removeTransitions[typeId] = target;
            target.addTransitions[typeId] = this;
        }
    }

    public (Chunk Chunk, int Row) Add(EntityId entity, bool enabled)
    {
        Chunk? chunk = null;
        for (var index = chunks.Count - 1; index >= 0; index--)
        {
            if (chunks[index].HasCapacity)
            {
                chunk = chunks[index];
                break;
            }
        }

        if (chunk is null)
        {
            if (!emptyChunks.TryPop(out chunk))
            {
                chunk = new Chunk(this, ChunkCapacity, registry);
                AllocatedChunkCount++;
            }

            chunks.Add(chunk);
        }

        return (chunk, chunk.Add(entity, enabled));
    }

    public EntityId Remove(Chunk chunk, int row)
    {
        var moved = chunk.RemoveSwap(row);
        if (chunk.Count == 0)
        {
            chunks.Remove(chunk);
            emptyChunks.Push(chunk);
        }

        return moved;
    }
}
