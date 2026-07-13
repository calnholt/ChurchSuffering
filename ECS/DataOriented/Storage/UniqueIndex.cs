#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Storage;

public readonly record struct UniqueIndexDebugEntry<TKey>(TKey Key, EntityId Entity)
    where TKey : notnull;

/// <summary>
/// Bidirectional one-key-per-entity index with deterministic collision failures.
/// </summary>
public sealed class UniqueIndex<TKey> : IEntityDestructionListener
    where TKey : notnull
{
    private readonly Dictionary<TKey, EntityId> entitiesByKey;
    private readonly Dictionary<EntityId, TKey> keysByEntity;

    public UniqueIndex(int initialCapacity = 0, IEqualityComparer<TKey>? comparer = null)
    {
        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        entitiesByKey = new Dictionary<TKey, EntityId>(initialCapacity, comparer);
        keysByEntity = new Dictionary<EntityId, TKey>(initialCapacity);
    }

    public int Count => entitiesByKey.Count;

    public void Add(TKey key, EntityId entity)
    {
        if (entity.IsNull)
        {
            throw new ArgumentException("A null entity cannot be added to a unique index.", nameof(entity));
        }

        if (entitiesByKey.TryGetValue(key, out EntityId existing))
        {
            throw new InvalidOperationException(
                $"Unique index key '{key}' is already owned by entity {existing}; entity {entity} cannot also own it.");
        }

        if (keysByEntity.TryGetValue(entity, out TKey? existingKey))
        {
            throw new InvalidOperationException(
                $"Entity {entity} is already indexed by key '{existingKey}' and cannot also own key '{key}'.");
        }

        entitiesByKey.Add(key, entity);
        keysByEntity.Add(entity, key);
    }

    public EntityId GetRequired(TKey key)
    {
        if (!entitiesByKey.TryGetValue(key, out EntityId entity))
        {
            throw new KeyNotFoundException($"Unique index does not contain key '{key}'.");
        }

        return entity;
    }

    public bool TryGet(TKey key, out EntityId entity) => entitiesByKey.TryGetValue(key, out entity);

    public bool TryGetKey(EntityId entity, out TKey key) => keysByEntity.TryGetValue(entity, out key!);

    public bool Remove(TKey key)
    {
        if (!entitiesByKey.Remove(key, out EntityId entity))
        {
            return false;
        }

        keysByEntity.Remove(entity);
        return true;
    }

    public bool RemoveEntity(EntityId entity)
    {
        if (!keysByEntity.Remove(entity, out TKey? key))
        {
            return false;
        }

        entitiesByKey.Remove(key);
        return true;
    }

    public void OnEntityDestroyed(EntityId entity)
    {
        RemoveEntity(entity);
    }

    public void Clear()
    {
        entitiesByKey.Clear();
        keysByEntity.Clear();
    }

    public int CopyDebugEntries(Span<UniqueIndexDebugEntry<TKey>> destination)
    {
        if (destination.Length < Count)
        {
            throw new ArgumentException("Destination is smaller than the index count.", nameof(destination));
        }

        var written = 0;
        foreach (KeyValuePair<TKey, EntityId> pair in entitiesByKey)
        {
            destination[written++] = new UniqueIndexDebugEntry<TKey>(pair.Key, pair.Value);
        }

        return written;
    }

    public UniqueIndexDebugEntry<TKey>[] GetDebugSnapshot()
    {
        var result = new UniqueIndexDebugEntry<TKey>[Count];
        CopyDebugEntries(result);
        return result;
    }
}
