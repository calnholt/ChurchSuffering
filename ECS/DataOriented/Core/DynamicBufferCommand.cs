#nullable enable

namespace Crusaders30XX.ECS.DataOriented.Core;

public interface IDynamicBufferCommandHandler<TCommand>
    where TCommand : unmanaged
{
    void Playback(World world, in TCommand command);
}
