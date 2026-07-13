#nullable enable

using System;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.DataOriented.Core;

public sealed partial class World
{
    private const int MinimumEntityCapacity = 16;

    private readonly ComponentTypeRegistry registry;
    private readonly Dictionary<ComponentSignature, Archetype> archetypeLookup = new();
    private readonly List<Archetype> archetypes = new();
    private Archetype?[] entityArchetypes;
    private Chunk?[] entityChunks;
    private int[] entityRows;
    private int[] generations;
    private bool[] alive;
    private bool[] enabled;
    private int[] freeIndexes;
    private int freeIndexCount;
    private int nextEntityIndex = 1;
    private int activeIterationCount;
    private string? activeIterationName;

    public World(ComponentTypeRegistry registry, int initialEntityCapacity = 128)
    {
        ArgumentNullException.ThrowIfNull(registry);
        if (!registry.IsSealed)
        {
            throw new InvalidOperationException("The component type registry must be sealed before creating a world.");
        }

        if (initialEntityCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(initialEntityCapacity));
        }

        this.registry = registry;
        var capacity = Math.Max(MinimumEntityCapacity, initialEntityCapacity + 1);
        entityArchetypes = new Archetype?[capacity];
        entityChunks = new Chunk?[capacity];
        entityRows = new int[capacity];
        generations = new int[capacity];
        alive = new bool[capacity];
        enabled = new bool[capacity];
        freeIndexes = new int[capacity];

        GetOrCreateArchetype(ComponentSignature.Empty);
    }

    public int EntityCount { get; private set; }

    public int ArchetypeCount => archetypes.Count;

    public long StructuralMoveCount { get; private set; }

    internal IReadOnlyList<Archetype> Archetypes => archetypes;

    internal event Action<Archetype>? ArchetypeCreated;


    public EntityId Create(in SpawnBundle bundle)
    {
        EnsureStructuralWriteAllowed(nameof(Create));
        registry.ValidateSignature(bundle.Signature);

        var index = AcquireEntityIndex();
        var generation = generations[index];
        if (generation == 0)
        {
            generation = 1;
            generations[index] = generation;
        }

        var entity = new EntityId(index, generation);
        var archetype = GetOrCreateArchetype(bundle.Signature);
        var (chunk, row) = archetype.Add(entity, enabled: true);

        try
        {
            InitializeComponents(archetype, chunk, row, in bundle);
        }
        catch
        {
            archetype.Remove(chunk, row);
            ReleaseUncreatedIndex(index);
            throw;
        }

        entityArchetypes[index] = archetype;
        entityChunks[index] = chunk;
        entityRows[index] = row;
        alive[index] = true;
        enabled[index] = true;
        EntityCount++;
        return entity;
    }

    public bool IsAlive(EntityId entity) =>
        entity.Index > 0 &&
        entity.Index < nextEntityIndex &&
        alive[entity.Index] &&
        generations[entity.Index] == entity.Generation;

    public bool IsEnabled(EntityId entity)
    {
        ValidateEntity(entity);
        return enabled[entity.Index];
    }

    public ComponentSignature GetSignature(EntityId entity)
    {
        ValidateEntity(entity);
        return entityArchetypes[entity.Index]!.Signature;
    }

    public bool Has<T>(EntityId entity)
    {
        ValidateEntity(entity);
        var typeId = ComponentType<T>.Id;
        registry.GetDescriptor(typeId);
        return entityArchetypes[entity.Index]!.Signature.Contains(typeId);
    }

    public ref T Get<T>(EntityId entity)
        where T : unmanaged, IComponent
    {
        ValidateEntity(entity);
        var typeId = ComponentType<T>.Id;
        registry.GetDescriptor(typeId);
        return ref entityChunks[entity.Index]!
            .GetColumn<T>(typeId)
            .Get(entityRows[entity.Index]);
    }

    public bool TryGet<T>(EntityId entity, out T value)
        where T : unmanaged, IComponent
    {
        if (!IsAlive(entity))
        {
            value = default;
            return false;
        }

        var typeId = ComponentType<T>.Id;
        registry.GetDescriptor(typeId);
        if (!entityArchetypes[entity.Index]!.Signature.Contains(typeId))
        {
            value = default;
            return false;
        }

        value = entityChunks[entity.Index]!
            .GetColumn<T>(typeId)
            .Get(entityRows[entity.Index]);
        return true;
    }

    public void Set<T>(EntityId entity, in T value)
        where T : unmanaged, IComponent
    {
        Get<T>(entity) = value;
    }

    internal void SetComponentBytes(EntityId entity, int typeId, ReadOnlySpan<byte> bytes)
    {
        ValidateEntity(entity);
        var descriptor = registry.GetDescriptor(typeId);
        if (descriptor.Metadata.IsTag)
        {
            throw new InvalidOperationException($"Cannot set tag {descriptor.Metadata.DebugName} as a component.");
        }

        if (!entityArchetypes[entity.Index]!.Signature.Contains(typeId))
        {
            throw new InvalidOperationException(
                $"Entity {entity} does not have component {descriptor.Metadata.DebugName}.");
        }

        entityChunks[entity.Index]!.GetColumn(typeId).SetBytes(entityRows[entity.Index], bytes);
    }

    internal void AddComponentBytes(EntityId entity, int typeId, ReadOnlySpan<byte> bytes)
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(Add));
        var descriptor = registry.GetDescriptor(typeId);
        if (descriptor.Metadata.IsTag)
        {
            throw new InvalidOperationException($"Type {descriptor.Metadata.DebugName} is registered as a tag.");
        }

        var source = entityArchetypes[entity.Index]!;
        if (source.Signature.Contains(typeId))
        {
            throw new InvalidOperationException(
                $"Entity {entity} already has component {descriptor.Metadata.DebugName}.");
        }

        if (bytes.Length != descriptor.Metadata.Size)
        {
            throw new InvalidOperationException($"Invalid serialized size for {descriptor.Metadata.DebugName}.");
        }

        var target = GetTransitionArchetype(source, typeId, add: true);
        MoveEntityWithBytes(entity, source, target, typeId, bytes);
    }

    internal void AddTag(EntityId entity, int typeId)
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(AddTag));
        var descriptor = registry.GetDescriptor(typeId);
        if (!descriptor.Metadata.IsTag)
        {
            throw new InvalidOperationException($"Type {descriptor.Metadata.DebugName} is registered as a component.");
        }

        var source = entityArchetypes[entity.Index]!;
        if (source.Signature.Contains(typeId))
        {
            throw new InvalidOperationException($"Entity {entity} already has tag {descriptor.Metadata.DebugName}.");
        }

        var target = GetTransitionArchetype(source, typeId, add: true);
        MoveEntity(entity, source, target, additions: null);
    }

    internal void RemoveType(EntityId entity, int typeId)
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(Remove));
        var descriptor = registry.GetDescriptor(typeId);
        var source = entityArchetypes[entity.Index]!;
        if (!source.Signature.Contains(typeId))
        {
            throw new InvalidOperationException($"Entity {entity} does not have type {descriptor.Metadata.DebugName}.");
        }

        var target = GetTransitionArchetype(source, typeId, add: false);
        MoveEntity(entity, source, target, additions: null);
    }

    public void Add<T>(EntityId entity, in T value)
        where T : unmanaged, IComponent
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(Add));
        var typeId = ComponentType<T>.Id;
        var descriptor = registry.GetDescriptor(typeId);
        if (descriptor.Metadata.IsTag)
        {
            throw new InvalidOperationException($"Type {typeof(T).FullName} is registered as a tag.");
        }

        if (entityArchetypes[entity.Index]!.Signature.Contains(typeId))
        {
            throw new InvalidOperationException($"Entity {entity} already has component {typeof(T).FullName}.");
        }

        var source = entityArchetypes[entity.Index]!;
        var target = GetTransitionArchetype(source, typeId, add: true);
        MoveEntityWithComponent(entity, source, target, typeId, in value);
    }

    public void AddTag<T>(EntityId entity)
        where T : unmanaged, ITag
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(AddTag));
        var typeId = ComponentType<T>.Id;
        var descriptor = registry.GetDescriptor(typeId);
        if (!descriptor.Metadata.IsTag)
        {
            throw new InvalidOperationException($"Type {typeof(T).FullName} is registered as a component.");
        }

        if (entityArchetypes[entity.Index]!.Signature.Contains(typeId))
        {
            throw new InvalidOperationException($"Entity {entity} already has tag {typeof(T).FullName}.");
        }

        var source = entityArchetypes[entity.Index]!;
        var target = GetTransitionArchetype(source, typeId, add: true);
        MoveEntity(entity, source, target, additions: null);
    }

    public void Remove<T>(EntityId entity)
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(Remove));
        var typeId = ComponentType<T>.Id;
        registry.GetDescriptor(typeId);
        var source = entityArchetypes[entity.Index]!;
        if (!source.Signature.Contains(typeId))
        {
            throw new InvalidOperationException($"Entity {entity} does not have type {typeof(T).FullName}.");
        }

        var target = GetTransitionArchetype(source, typeId, add: false);
        MoveEntity(entity, source, target, additions: null);
    }

    public void Transition(
        EntityId entity,
        in SpawnBundle additions,
        in ComponentSignature removals)
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(Transition));
        registry.ValidateSignature(additions.Signature);
        registry.ValidateSignature(removals);

        var source = entityArchetypes[entity.Index]!;
        if (!source.Signature.ContainsAll(removals))
        {
            throw new InvalidOperationException($"Entity {entity} does not contain every requested removal type.");
        }

        if (source.Signature.Intersects(additions.Signature))
        {
            throw new InvalidOperationException($"Entity {entity} already contains one or more requested addition types.");
        }

        if (removals.Intersects(additions.Signature))
        {
            throw new InvalidOperationException("A batch transition cannot add and remove the same type.");
        }

        var targetSignature = source.Signature.Except(removals) | additions.Signature;
        if (targetSignature == source.Signature)
        {
            return;
        }

        var target = GetOrCreateArchetype(targetSignature);
        MoveEntity(entity, source, target, additions);
    }

    public void Enable(EntityId entity)
    {
        SetEnabled(entity, value: true, nameof(Enable));
    }

    public void Disable(EntityId entity)
    {
        SetEnabled(entity, value: false, nameof(Disable));
    }

    public void Destroy(EntityId entity)
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(nameof(Destroy));
        DestroyValidated(entity);
    }

    public int Destroy(ReadOnlySpan<EntityId> entities)
    {
        EnsureStructuralWriteAllowed(nameof(Destroy));
        for (var index = 0; index < entities.Length; index++)
        {
            ValidateEntity(entities[index]);
            for (var previous = 0; previous < index; previous++)
            {
                if (entities[previous] == entities[index])
                {
                    throw new InvalidOperationException(
                        $"Bulk destruction contains duplicate entity {entities[index]}.");
                }
            }
        }

        for (var index = 0; index < entities.Length; index++)
        {
            DestroyValidated(entities[index]);
        }

        return entities.Length;
    }

    public int DestroyAll()
    {
        EnsureStructuralWriteAllowed(nameof(DestroyAll));
        var destroyed = 0;
        for (var index = 1; index < nextEntityIndex; index++)
        {
            if (!alive[index])
            {
                continue;
            }

            DestroyValidated(new EntityId(index, generations[index]));
            destroyed++;
        }

        return destroyed;
    }

    public int DestroyAll(in ComponentSignature requiredTypes)
    {
        EnsureStructuralWriteAllowed(nameof(DestroyAll));
        registry.ValidateSignature(requiredTypes);
        var destroyed = 0;
        for (var index = 1; index < nextEntityIndex; index++)
        {
            if (!alive[index] || !entityArchetypes[index]!.Signature.ContainsAll(requiredTypes))
            {
                continue;
            }

            DestroyValidated(new EntityId(index, generations[index]));
            destroyed++;
        }

        return destroyed;
    }

    internal void BeginQueryIteration(string name)
    {
        activeIterationCount++;
        activeIterationName ??= name;
    }

    internal void EndQueryIteration()
    {
        if (activeIterationCount <= 0)
        {
            throw new InvalidOperationException("No active query iteration exists.");
        }

        activeIterationCount--;
        if (activeIterationCount == 0)
        {
            activeIterationName = null;
        }
    }

    private void SetEnabled(EntityId entity, bool value, string operation)
    {
        ValidateEntity(entity);
        EnsureStructuralWriteAllowed(operation);
        if (enabled[entity.Index] == value)
        {
            return;
        }

        enabled[entity.Index] = value;
        entityChunks[entity.Index]!.SetEnabled(entityRows[entity.Index], value);
    }

    private void MoveEntity(
        EntityId entity,
        Archetype source,
        Archetype target,
        SpawnBundle? additions)
    {
        var sourceChunk = entityChunks[entity.Index]!;
        var sourceRow = entityRows[entity.Index];
        var (targetChunk, targetRow) = target.Add(entity, enabled[entity.Index]);

        sourceChunk.CopyRetainedComponentsTo(sourceRow, targetChunk, targetRow);
        if (additions.HasValue)
        {
            InitializeAddedComponents(target, targetChunk, targetRow, additions.Value);
        }

        entityArchetypes[entity.Index] = target;
        entityChunks[entity.Index] = targetChunk;
        entityRows[entity.Index] = targetRow;

        var moved = source.Remove(sourceChunk, sourceRow);
        if (!moved.IsNull)
        {
            entityRows[moved.Index] = sourceRow;
        }

        StructuralMoveCount++;
    }

    private void MoveEntityWithComponent<T>(
        EntityId entity,
        Archetype source,
        Archetype target,
        int addedTypeId,
        in T value)
        where T : unmanaged, IComponent
    {
        var sourceChunk = entityChunks[entity.Index]!;
        var sourceRow = entityRows[entity.Index];
        var (targetChunk, targetRow) = target.Add(entity, enabled[entity.Index]);

        sourceChunk.CopyRetainedComponentsTo(sourceRow, targetChunk, targetRow);
        targetChunk.GetColumn<T>(addedTypeId).Get(targetRow) = value;

        entityArchetypes[entity.Index] = target;
        entityChunks[entity.Index] = targetChunk;
        entityRows[entity.Index] = targetRow;

        var moved = source.Remove(sourceChunk, sourceRow);
        if (!moved.IsNull)
        {
            entityRows[moved.Index] = sourceRow;
        }

        StructuralMoveCount++;
    }

    private void MoveEntityWithBytes(
        EntityId entity,
        Archetype source,
        Archetype target,
        int addedTypeId,
        ReadOnlySpan<byte> bytes)
    {
        var sourceChunk = entityChunks[entity.Index]!;
        var sourceRow = entityRows[entity.Index];
        var (targetChunk, targetRow) = target.Add(entity, enabled[entity.Index]);

        sourceChunk.CopyRetainedComponentsTo(sourceRow, targetChunk, targetRow);
        targetChunk.GetColumn(addedTypeId).SetBytes(targetRow, bytes);

        entityArchetypes[entity.Index] = target;
        entityChunks[entity.Index] = targetChunk;
        entityRows[entity.Index] = targetRow;

        var moved = source.Remove(sourceChunk, sourceRow);
        if (!moved.IsNull)
        {
            entityRows[moved.Index] = sourceRow;
        }

        StructuralMoveCount++;
    }

    private void InitializeComponents(
        Archetype archetype,
        Chunk chunk,
        int row,
        in SpawnBundle bundle)
    {
        for (var typeId = 0; typeId < ComponentSignature.MaximumTypeCount; typeId++)
        {
            if (!archetype.Signature.Contains(typeId))
            {
                continue;
            }

            var descriptor = registry.GetDescriptor(typeId);
            if (descriptor.Metadata.IsTag)
            {
                continue;
            }

            if (!bundle.TryGetComponentBytes(typeId, out var bytes))
            {
                throw new InvalidOperationException(
                    $"Spawn bundle omitted a value for component {descriptor.Metadata.DebugName}.");
            }

            chunk.GetColumn(typeId).SetBytes(row, bytes);
        }
    }

    private void InitializeAddedComponents(
        Archetype target,
        Chunk chunk,
        int row,
        in SpawnBundle additions)
    {
        for (var typeId = 0; typeId < ComponentSignature.MaximumTypeCount; typeId++)
        {
            if (!additions.Signature.Contains(typeId))
            {
                continue;
            }

            var descriptor = registry.GetDescriptor(typeId);
            if (descriptor.Metadata.IsTag)
            {
                continue;
            }

            if (!target.Signature.Contains(typeId) ||
                !additions.TryGetComponentBytes(typeId, out var bytes))
            {
                throw new InvalidOperationException(
                    $"Batch transition omitted a value for component {descriptor.Metadata.DebugName}.");
            }

            chunk.GetColumn(typeId).SetBytes(row, bytes);
        }
    }

    private Archetype GetTransitionArchetype(Archetype source, int typeId, bool add)
    {
        var cached = source.GetTransition(typeId, add);
        if (cached is not null)
        {
            return cached;
        }

        var signature = add ? source.Signature.With(typeId) : source.Signature.Without(typeId);
        var target = GetOrCreateArchetype(signature);
        source.SetTransition(typeId, add, target);
        return target;
    }

    private Archetype GetOrCreateArchetype(ComponentSignature signature)
    {
        if (archetypeLookup.TryGetValue(signature, out var archetype))
        {
            return archetype;
        }

        registry.ValidateSignature(signature);
        archetype = new Archetype(signature, registry);
        archetypeLookup.Add(signature, archetype);
        archetypes.Add(archetype);
        OnArchetypeCreated(archetype);
        ArchetypeCreated?.Invoke(archetype);
        return archetype;
    }

    private void DestroyValidated(EntityId entity)
    {
        OnEntityDestroying(entity);

        var index = entity.Index;
        var archetype = entityArchetypes[index]!;
        var chunk = entityChunks[index]!;
        var row = entityRows[index];
        var moved = archetype.Remove(chunk, row);
        if (!moved.IsNull)
        {
            entityRows[moved.Index] = row;
        }

        entityArchetypes[index] = null;
        entityChunks[index] = null;
        entityRows[index] = 0;
        alive[index] = false;
        enabled[index] = false;
        generations[index] = NextGeneration(generations[index]);
        PushFreeIndex(index);
        EntityCount--;
    }

    private int AcquireEntityIndex()
    {
        if (freeIndexCount > 0)
        {
            return freeIndexes[--freeIndexCount];
        }

        var index = nextEntityIndex++;
        EnsureEntityCapacity(index + 1);
        return index;
    }

    private void ReleaseUncreatedIndex(int index)
    {
        generations[index] = NextGeneration(generations[index]);
        PushFreeIndex(index);
    }

    private void PushFreeIndex(int index)
    {
        if (freeIndexCount == freeIndexes.Length)
        {
            Array.Resize(ref freeIndexes, freeIndexes.Length * 2);
        }

        freeIndexes[freeIndexCount++] = index;
    }

    private void EnsureEntityCapacity(int required)
    {
        if (entityArchetypes.Length >= required)
        {
            return;
        }

        var capacity = Math.Max(required, entityArchetypes.Length * 2);
        Array.Resize(ref entityArchetypes, capacity);
        Array.Resize(ref entityChunks, capacity);
        Array.Resize(ref entityRows, capacity);
        Array.Resize(ref generations, capacity);
        Array.Resize(ref alive, capacity);
        Array.Resize(ref enabled, capacity);
    }

    private void ValidateEntity(EntityId entity)
    {
        if (entity.IsNull)
        {
            throw new InvalidOperationException("EntityId.Null is not a live entity handle.");
        }

        if (!IsAlive(entity))
        {
            var currentGeneration = entity.Index > 0 && entity.Index < nextEntityIndex
                ? generations[entity.Index]
                : 0;
            throw new InvalidOperationException(
                $"Entity handle {entity} is dead or stale; current generation is {currentGeneration}.");
        }
    }

    private void EnsureStructuralWriteAllowed(string operation)
    {
        if (activeIterationCount > 0)
        {
            throw new InvalidOperationException(
                $"Structural operation {operation} is not allowed while query '{activeIterationName}' is active.");
        }
    }

    private static int NextGeneration(int generation)
    {
        var next = unchecked(generation + 1);
        return next <= 0 ? 1 : next;
    }

    partial void OnArchetypeCreated(Archetype archetype);

    partial void OnEntityDestroying(EntityId entity);
}
