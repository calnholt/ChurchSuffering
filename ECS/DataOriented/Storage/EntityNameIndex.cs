#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Storage;

public sealed class EntityNameIndex : IEntityDestructionListener
{
    private readonly StringTable strings;
    private readonly UniqueIndex<StringId> entities;

    public EntityNameIndex(StringTable strings, int initialCapacity = 0)
    {
        ArgumentNullException.ThrowIfNull(strings);
        this.strings = strings;
        entities = new UniqueIndex<StringId>(initialCapacity);
    }

    public int Count => entities.Count;

    public StringId Add(EntityId entity, string name)
    {
        StringId id = strings.Intern(name);
        entities.Add(id, entity);
        return id;
    }

    public bool TryGet(string name, out EntityId entity)
    {
        if (!strings.TryFind(name, out StringId id))
        {
            entity = default;
            return false;
        }

        return entities.TryGet(id, out entity);
    }

    public bool TryGet(StringId name, out EntityId entity) => entities.TryGet(name, out entity);

    public EntityId GetRequired(string name)
    {
        if (!TryGet(name, out EntityId entity))
        {
            throw new InvalidOperationException($"No entity is indexed with name '{name}'.");
        }

        return entity;
    }

    public bool TryGetName(EntityId entity, out StringId name) => entities.TryGetKey(entity, out name);

    public bool Remove(EntityId entity) => entities.RemoveEntity(entity);

    public void OnEntityDestroyed(EntityId entity)
    {
        Remove(entity);
    }

    public UniqueIndexDebugEntry<StringId>[] GetDebugSnapshot() => entities.GetDebugSnapshot();
}
