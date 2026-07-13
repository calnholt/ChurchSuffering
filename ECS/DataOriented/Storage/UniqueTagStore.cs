#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Storage;

/// <summary>
/// World-owned unique-tag slots addressed by generated component/tag ID.
/// </summary>
internal sealed class UniqueTagStore : IEntityDestructionListener
{
    private readonly IUniqueTagSlot?[] slotsByTypeId =
        new IUniqueTagSlot?[ComponentSignature.MaximumTypeCount];
    private readonly List<IUniqueTagSlot> activeSlots = new();

    public void Register<TTag>(EntityId entity)
        where TTag : unmanaged, ITag
    {
        GetOrCreateSlot<TTag>().Register(entity);
    }

    public EntityId GetRequired<TTag>()
        where TTag : unmanaged, ITag
    {
        int typeId = ComponentType<TTag>.Id;
        if (slotsByTypeId[typeId] is not UniqueTagSlot<TTag> slot)
        {
            throw new InvalidOperationException(
                $"No entity is registered for unique tag {typeof(TTag).FullName}.");
        }

        return slot.GetRequired();
    }

    public bool TryGet<TTag>(out EntityId entity)
        where TTag : unmanaged, ITag
    {
        int typeId = ComponentType<TTag>.Id;
        if (slotsByTypeId[typeId] is UniqueTagSlot<TTag> slot)
        {
            return slot.TryGet(out entity);
        }

        entity = default;
        return false;
    }

    public void OnEntityDestroyed(EntityId entity)
    {
        for (var index = 0; index < activeSlots.Count; index++)
        {
            activeSlots[index].Remove(entity);
        }
    }

    private UniqueTagSlot<TTag> GetOrCreateSlot<TTag>()
        where TTag : unmanaged, ITag
    {
        int typeId = ComponentType<TTag>.Id;
        if (slotsByTypeId[typeId] is UniqueTagSlot<TTag> existing)
        {
            return existing;
        }

        if (slotsByTypeId[typeId] is not null)
        {
            throw new InvalidOperationException(
                $"Unique tag type ID {typeId} is already assigned to another runtime type.");
        }

        var created = new UniqueTagSlot<TTag>();
        slotsByTypeId[typeId] = created;
        activeSlots.Add(created);
        return created;
    }
}

internal interface IUniqueTagSlot
{
    void Remove(EntityId entity);
}

internal sealed class UniqueTagSlot<TTag> : IUniqueTagSlot
    where TTag : unmanaged, ITag
{
    private readonly UniqueEntityIndex<TTag> index = new();

    public void Register(EntityId entity)
    {
        index.Register(entity);
    }

    public EntityId GetRequired() => index.GetRequired();

    public bool TryGet(out EntityId entity) => index.TryGet(out entity);

    public void Remove(EntityId entity)
    {
        index.Remove(entity);
    }
}
