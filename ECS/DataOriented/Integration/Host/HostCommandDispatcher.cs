#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;

namespace Crusaders30XX.ECS.DataOriented.Integration.Host;

/// <summary>Host-owned effects for commands that cannot be executed inside gameplay systems.</summary>
public interface IHostCommandTarget
{
    void QuitApplication();
    void ToggleFullScreen();
    void ToggleDebugMenu();
    void ToggleEntityList();
    void DealDebugDamage();
    void ToggleProfiler();
}

/// <summary>Drains root command requests in publication order and executes each request once.</summary>
public sealed class HostCommandDispatcher
{
    private readonly bool snapshotMode;

    public HostCommandDispatcher(bool snapshotMode = false) => this.snapshotMode = snapshotMode;

    public int Drain(HostCommandRequestQueue requests, IHostCommandTarget target)
    {
        ArgumentNullException.ThrowIfNull(requests);
        ArgumentNullException.ThrowIfNull(target);
        var dispatched = 0;
        while (requests.TryDequeue(out HostCommandRequest request))
        {
            if (snapshotMode && request.Command != PlayerCommand.QuitApplication) continue;
            Dispatch(request.Command, target);
            dispatched++;
        }
        return dispatched;
    }

    private static void Dispatch(PlayerCommand command, IHostCommandTarget target)
    {
        switch (command)
        {
            case PlayerCommand.QuitApplication: target.QuitApplication(); break;
            case PlayerCommand.ToggleFullScreen: target.ToggleFullScreen(); break;
            case PlayerCommand.ToggleDebugMenu: target.ToggleDebugMenu(); break;
            case PlayerCommand.ToggleEntityList: target.ToggleEntityList(); break;
            case PlayerCommand.DealDebugDamage: target.DealDebugDamage(); break;
            case PlayerCommand.ToggleProfiler: target.ToggleProfiler(); break;
            default: throw new ArgumentOutOfRangeException(nameof(command), command, "Not a host command.");
        }
    }
}
