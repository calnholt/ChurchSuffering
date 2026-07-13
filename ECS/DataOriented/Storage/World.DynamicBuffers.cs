#nullable enable

using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Core;

public sealed partial class World
{
    private DynamicBufferStore? dynamicBufferStore;

    internal DynamicBufferStore DynamicBuffers
    {
        get
        {
            if (dynamicBufferStore is not null)
            {
                return dynamicBufferStore;
            }

            var created = new DynamicBufferStore();
            RegisterEntityDestructionListener(created);
            dynamicBufferStore = created;
            return created;
        }
    }

    public DynamicBufferHandle<T> CreateDynamicBuffer<T>(EntityId owner, int initialCapacity = 0)
        where T : unmanaged
    {
        ValidateEntity(owner);
        return DynamicBuffers.Create<T>(owner, initialCapacity);
    }

    public DynamicBuffer<T> GetDynamicBuffer<T>(DynamicBufferHandle<T> handle)
        where T : unmanaged
    {
        return DynamicBuffers.Get(handle);
    }

    public bool TryGetDynamicBuffer<T>(DynamicBufferHandle<T> handle, out DynamicBuffer<T> buffer)
        where T : unmanaged
    {
        return DynamicBuffers.TryGet(handle, out buffer);
    }

    public void ReleaseDynamicBuffer<T>(DynamicBufferHandle<T> handle)
        where T : unmanaged
    {
        DynamicBuffers.Release(handle);
    }

    public IDynamicBufferCommandHandler<DynamicBufferMutation<T>> GetDynamicBufferMutationHandler<T>()
        where T : unmanaged
    {
        return DynamicBuffers.GetMutationHandler<T>();
    }

    public DynamicBufferDebugInfo[] GetDynamicBufferDebugSnapshot()
    {
        return DynamicBuffers.GetDebugSnapshot();
    }
}
