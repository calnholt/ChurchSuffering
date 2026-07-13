#nullable enable

namespace Crusaders30XX.ECS.DataOriented.Core;

public readonly record struct EntityId(int Index, int Generation)
{
    public static EntityId Null => default;

    public bool IsNull => Index == 0;
}
