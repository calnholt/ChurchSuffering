#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Core;

public sealed partial class World
{
    private UniqueTagStore? uniqueTagStore;

    public void RegisterUnique<TTag>(EntityId entity)
        where TTag : unmanaged, ITag
    {
        ValidateEntity(entity);
        if (!ComponentType<TTag>.IsTag)
        {
            throw new InvalidOperationException($"Type {typeof(TTag).FullName} is not registered as a tag.");
        }

        if (!Has<TTag>(entity))
        {
            throw new InvalidOperationException(
                $"Entity {entity} cannot be registered as unique because it does not carry tag {typeof(TTag).FullName}.");
        }

        UniqueTags.Register<TTag>(entity);
    }

    public EntityId GetUnique<TTag>()
        where TTag : unmanaged, ITag
    {
        EntityId entity = UniqueTags.GetRequired<TTag>();
        ValidateEntity(entity);
        return entity;
    }

    public bool TryGetUnique<TTag>(out EntityId entity)
        where TTag : unmanaged, ITag
    {
        if (uniqueTagStore is null || !uniqueTagStore.TryGet<TTag>(out entity))
        {
            entity = default;
            return false;
        }

        return IsAlive(entity);
    }

    private UniqueTagStore UniqueTags
    {
        get
        {
            if (uniqueTagStore is not null)
            {
                return uniqueTagStore;
            }

            var created = new UniqueTagStore();
            RegisterEntityDestructionListener(created);
            uniqueTagStore = created;
            return created;
        }
    }
}
