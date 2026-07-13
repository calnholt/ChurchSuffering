#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Global;

public readonly record struct HostCommandRequest(PlayerCommand Command, PlayerInputDevice Source);

/// <summary>
/// Fixed-capacity hand-off consumed by the host after the scheduler completes. The event
/// consumer never mutates ECS state or calls Game1.
/// </summary>
public sealed class HostCommandRequestQueue : IEventConsumer<PlayerCommandEvent>
{
    private readonly HostCommandRequest[] requests;
    private readonly bool snapshotMode;
    private int readIndex;
    private int count;

    public HostCommandRequestQueue(int capacity = 32, bool snapshotMode = false)
    {
        if (capacity <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        requests = new HostCommandRequest[capacity];
        this.snapshotMode = snapshotMode;
    }

    public int Count => count;

    public void Consume(in PlayerCommandEvent value, ref EventDispatchContext context)
    {
        if (!IsHostCommand(value.Command) ||
            (snapshotMode && value.Command != PlayerCommand.QuitApplication))
        {
            return;
        }

        if (count == requests.Length)
        {
            throw new InvalidOperationException("The host command request queue is full.");
        }

        int destination = (readIndex + count) % requests.Length;
        requests[destination] = new HostCommandRequest(value.Command, value.Source);
        count++;
    }

    public bool TryDequeue(out HostCommandRequest request)
    {
        if (count == 0)
        {
            request = default;
            return false;
        }

        request = requests[readIndex];
        requests[readIndex] = default;
        readIndex = (readIndex + 1) % requests.Length;
        count--;
        return true;
    }

    private static bool IsHostCommand(PlayerCommand command) => command is
        PlayerCommand.QuitApplication or
        PlayerCommand.ToggleFullScreen or
        PlayerCommand.ToggleDebugMenu or
        PlayerCommand.ToggleEntityList or
        PlayerCommand.DealDebugDamage or
        PlayerCommand.ToggleProfiler;
}
