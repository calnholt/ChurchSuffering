#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Storage;

/// <summary>
/// Tracks the single live entity carrying a unique tag contract.
/// </summary>
public sealed class UniqueEntityIndex<TTag> : IEntityDestructionListener
    where TTag : unmanaged, ITag
{
    private EntityId entity;

    public bool HasValue => !entity.IsNull;

    public EntityId GetRequired()
    {
        if (entity.IsNull)
        {
            throw new InvalidOperationException($"No entity is registered for unique tag {typeof(TTag).FullName}.");
        }

        return entity;
    }

    public bool TryGet(out EntityId value)
    {
        value = entity;
        return !value.IsNull;
    }

    public void Register(EntityId value)
    {
        if (value.IsNull)
        {
            throw new ArgumentException("A null entity cannot be registered as unique.", nameof(value));
        }

        if (!entity.IsNull)
        {
            throw new InvalidOperationException(
                $"Unique tag {typeof(TTag).FullName} is already registered to entity {entity}; entity {value} is a duplicate.");
        }

        entity = value;
    }

    public bool Remove(EntityId value)
    {
        if (entity != value)
        {
            return false;
        }

        entity = default;
        return true;
    }

    public void OnEntityDestroyed(EntityId value)
    {
        Remove(value);
    }
}
