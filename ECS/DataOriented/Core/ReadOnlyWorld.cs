#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Core;

/// <summary>
/// Handler-facing world view. It exposes component values by copy and dynamic buffers as
/// read-only spans; structural and component writes remain available only through systems.
/// </summary>
public readonly struct ReadOnlyWorld
{
    private readonly World? world;

    internal ReadOnlyWorld(World world)
    {
        this.world = world;
    }

    public int EntityCount => RequiredWorld.EntityCount;

    public bool IsAlive(EntityId entity) => RequiredWorld.IsAlive(entity);

    public bool IsEnabled(EntityId entity) => RequiredWorld.IsEnabled(entity);

    public ComponentSignature GetSignature(EntityId entity) => RequiredWorld.GetSignature(entity);

    public bool Has<T>(EntityId entity) => RequiredWorld.Has<T>(entity);

    public T Get<T>(EntityId entity)
        where T : unmanaged, IComponent => RequiredWorld.Get<T>(entity);

    public bool TryGet<T>(EntityId entity, out T value)
        where T : unmanaged, IComponent => RequiredWorld.TryGet(entity, out value);

    public EntityId GetUnique<TTag>()
        where TTag : unmanaged, ITag => RequiredWorld.GetUnique<TTag>();

    public bool TryGetUnique<TTag>(out EntityId entity)
        where TTag : unmanaged, ITag => RequiredWorld.TryGetUnique<TTag>(out entity);

    public ReadOnlyDynamicBuffer<T> GetDynamicBuffer<T>(DynamicBufferHandle<T> handle)
        where T : unmanaged => new(RequiredWorld.GetDynamicBuffer(handle));

    public bool TryGetDynamicBuffer<T>(
        DynamicBufferHandle<T> handle,
        out ReadOnlyDynamicBuffer<T> buffer)
        where T : unmanaged
    {
        if (RequiredWorld.TryGetDynamicBuffer(handle, out DynamicBuffer<T> mutable))
        {
            buffer = new ReadOnlyDynamicBuffer<T>(mutable);
            return true;
        }

        buffer = default;
        return false;
    }

    private World RequiredWorld => world ??
        throw new InvalidOperationException("A default read-only world view cannot be accessed.");
}

public readonly struct ReadOnlyDynamicBuffer<T>
    where T : unmanaged
{
    private readonly DynamicBuffer<T> buffer;

    internal ReadOnlyDynamicBuffer(DynamicBuffer<T> buffer)
    {
        this.buffer = buffer;
    }

    public DynamicBufferHandle<T> Handle => buffer.Handle;

    public int Count => buffer.Count;

    public int Capacity => buffer.Capacity;

    public T this[int index] => buffer[index];

    public ReadOnlySpan<T> AsReadOnlySpan() => buffer.AsReadOnlySpan();
}

public sealed partial class World
{
    public ReadOnlyWorld AsReadOnly() => new(this);
}
