#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;

namespace Crusaders30XX.ECS.DataOriented.Events;

public readonly record struct RuleTypeId(int Value)
{
    public bool IsValid => Value > 0;
}

public enum RuleExecutionStatus : byte
{
    Completed = 0,
    Pending = 1,
}

public enum RuleLane : byte
{
    None = 0,
    Mandatory = 1,
    ReactiveTrigger = 2,
}

public readonly record struct RuleProcessingResult(
    int CompletedCount,
    bool IsWaiting,
    RuleLane WaitingLane,
    RuleTypeId WaitingRuleType);

public interface IRuleRoutingEndpoint<TState>
    where TState : unmanaged
{
    RuleExecutionStatus Execute(
        RuleTypeId ruleType,
        ref TState state,
        ref RuleExecutionContext<TState> context);
}

public readonly ref struct RuleExecutionContext<TState>
    where TState : unmanaged
{
    internal RuleExecutionContext(
        World world,
        CommandBuffer commands,
        EventRuntime events,
        QueuedRuleRuntime<TState> rules)
    {
        World = world;
        Commands = commands;
        Events = events;
        Rules = rules;
    }

    public World World { get; }

    public CommandBuffer Commands { get; }

    public EventRuntime Events { get; }

    public QueuedRuleRuntime<TState> Rules { get; }
}

/// <summary>
/// Deterministic two-lane queue. Mandatory rules always take precedence over reactive
/// triggers. A pending handler keeps its explicit unmanaged state at the head and resumes
/// on a later Process call.
/// </summary>
public sealed class QueuedRuleRuntime<TState>
    where TState : unmanaged
{
    private readonly IRuleRoutingEndpoint<TState> endpoint;
    private readonly RuleQueue<TState> mandatory;
    private readonly RuleQueue<TState> reactive;
    private readonly int maximumCompletedPerProcess;

    public QueuedRuleRuntime(
        IRuleRoutingEndpoint<TState> endpoint,
        int initialCapacityPerLane = 16,
        int maximumCompletedPerProcess = 100_000)
    {
        ArgumentNullException.ThrowIfNull(endpoint);
        if (initialCapacityPerLane < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacityPerLane));
        }
        if (maximumCompletedPerProcess <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumCompletedPerProcess));
        }

        this.endpoint = endpoint;
        mandatory = new RuleQueue<TState>(initialCapacityPerLane);
        reactive = new RuleQueue<TState>(initialCapacityPerLane);
        this.maximumCompletedPerProcess = maximumCompletedPerProcess;
    }

    public int MandatoryCount => mandatory.Count;

    public int ReactiveTriggerCount => reactive.Count;

    public int Count => mandatory.Count + reactive.Count;

    public void EnqueueMandatory(RuleTypeId ruleType, in TState state) =>
        Enqueue(mandatory, ruleType, in state);

    public void EnqueueReactiveTrigger(RuleTypeId ruleType, in TState state) =>
        Enqueue(reactive, ruleType, in state);

    public RuleProcessingResult Process(
        World world,
        CommandBuffer commands,
        EventRuntime events)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(commands);
        ArgumentNullException.ThrowIfNull(events);
        var context = new RuleExecutionContext<TState>(world, commands, events, this);
        var completed = 0;
        while (Count > 0)
        {
            RuleLane lane = mandatory.Count > 0 ? RuleLane.Mandatory : RuleLane.ReactiveTrigger;
            RuleQueue<TState> queue = lane == RuleLane.Mandatory ? mandatory : reactive;
            RuleQueueEntry<TState> entry = queue.Peek();
            TState state = entry.State;
            RuleExecutionStatus status = endpoint.Execute(
                entry.RuleType,
                ref state,
                ref context);
            if (!Enum.IsDefined(status))
            {
                throw new InvalidOperationException(
                    $"Rule endpoint returned invalid status {(byte)status} for rule {entry.RuleType.Value}.");
            }

            if (status == RuleExecutionStatus.Pending)
            {
                queue.SetHeadState(in state);
                return new RuleProcessingResult(completed, true, lane, entry.RuleType);
            }

            queue.Dequeue();
            completed++;
            if (completed > maximumCompletedPerProcess)
            {
                throw new InvalidOperationException(
                    $"Queued rule processing exceeded {maximumCompletedPerProcess} completed rules in one call.");
            }
        }

        return new RuleProcessingResult(completed, false, RuleLane.None, default);
    }

    private static void Enqueue(
        RuleQueue<TState> queue,
        RuleTypeId ruleType,
        in TState state)
    {
        if (!ruleType.IsValid)
        {
            throw new ArgumentOutOfRangeException(nameof(ruleType), "Generated rule type IDs must be positive.");
        }

        queue.Enqueue(new RuleQueueEntry<TState>(ruleType, state));
    }
}

internal readonly record struct RuleQueueEntry<TState>(RuleTypeId RuleType, TState State)
    where TState : unmanaged;

internal sealed class RuleQueue<TState>
    where TState : unmanaged
{
    private RuleQueueEntry<TState>[] entries;
    private int head;

    public RuleQueue(int initialCapacity)
    {
        entries = new RuleQueueEntry<TState>[Math.Max(1, initialCapacity)];
    }

    public int Count { get; private set; }

    public void Enqueue(in RuleQueueEntry<TState> entry)
    {
        EnsureCapacity(Count + 1);
        entries[(head + Count) % entries.Length] = entry;
        Count++;
    }

    public RuleQueueEntry<TState> Peek()
    {
        if (Count == 0)
        {
            throw new InvalidOperationException("A queued rule lane is empty.");
        }

        return entries[head];
    }

    public void SetHeadState(in TState state)
    {
        RuleQueueEntry<TState> current = Peek();
        entries[head] = current with { State = state };
    }

    public void Dequeue()
    {
        _ = Peek();
        entries[head] = default;
        head = (head + 1) % entries.Length;
        Count--;
        if (Count == 0) head = 0;
    }

    private void EnsureCapacity(int required)
    {
        if (entries.Length >= required)
        {
            return;
        }

        var expanded = new RuleQueueEntry<TState>[Math.Max(required, entries.Length * 2)];
        for (var index = 0; index < Count; index++)
        {
            expanded[index] = entries[(head + index) % entries.Length];
        }

        entries = expanded;
        head = 0;
    }
}
