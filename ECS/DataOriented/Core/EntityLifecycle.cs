#nullable enable

using System;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.DataOriented.Core;

public interface IEntityDestructionListener
{
    void OnEntityDestroyed(EntityId entity);
}

public sealed partial class World
{
    private readonly List<IEntityDestructionListener> destructionListeners = new();

    public void RegisterEntityDestructionListener(IEntityDestructionListener listener)
    {
        ArgumentNullException.ThrowIfNull(listener);
        for (var index = 0; index < destructionListeners.Count; index++)
        {
            if (ReferenceEquals(destructionListeners[index], listener))
            {
                throw new InvalidOperationException("The entity destruction listener is already registered.");
            }
        }

        destructionListeners.Add(listener);
    }

    partial void OnEntityDestroying(EntityId entity)
    {
        for (var index = 0; index < destructionListeners.Count; index++)
        {
            destructionListeners[index].OnEntityDestroyed(entity);
        }
    }
}
