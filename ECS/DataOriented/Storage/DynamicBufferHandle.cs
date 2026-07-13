#nullable enable

namespace Crusaders30XX.ECS.DataOriented.Storage;

public readonly record struct DynamicBufferHandle<T>(int Index, int Generation)
    where T : unmanaged
{
    public static DynamicBufferHandle<T> Null => default;

    public bool IsNull => Index == 0;
}
