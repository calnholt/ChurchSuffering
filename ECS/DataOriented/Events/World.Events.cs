#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Core;

public sealed partial class World
{
    private EventRuntime? eventRuntime;

    public bool HasEventRuntime => eventRuntime is not null;

    public EventRuntime Events => eventRuntime ??
        throw new InvalidOperationException("This world does not have an attached event runtime.");

    public void AttachEventRuntime(EventRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        if (eventRuntime is not null && !ReferenceEquals(eventRuntime, runtime))
        {
            throw new InvalidOperationException(
                "This world already owns a different event runtime; event runtimes cannot be replaced or shared.");
        }

        runtime.AttachToWorld(this);
        eventRuntime = runtime;
    }
}
