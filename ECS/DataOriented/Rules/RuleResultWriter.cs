#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Rules;

public enum RuleHandlerResultKind : byte
{
    None = 0,
    Allow = 1,
    Reject = 2,
    StatModifier = 3,
    Value = 4,
}

public enum RuleValidationDecision : byte
{
    Undecided = 0,
    Allowed = 1,
    Rejected = 2,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct RuleResultWriterState
{
    internal int Count;
    internal RuleValidationDecision Validation;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RuleHandlerResult(
    RuleHandlerResultKind Kind,
    int Sequence,
    RuleFactId Result,
    StatId Stat,
    StringId RejectionReason,
    int Value,
    int AuxiliaryValue,
    RuleValueFlags Flags)
{
    internal RuleHandlerResult WithSequence(int sequence) => this with { Sequence = sequence };
}

/// <summary>
/// Stack-only writer over caller-owned result storage. Validation, derived-stat, and
/// staged-resolution outputs remain separate from mutation commands.
/// </summary>
public ref struct RuleResultWriter
{
    private Span<RuleHandlerResult> destination;
    private Span<RuleResultWriterState> state;

    public RuleResultWriter(
        Span<RuleHandlerResult> destination,
        ref RuleResultWriterState state)
    {
        this.destination = destination;
        state = default;
        this.state = MemoryMarshal.CreateSpan(ref state, 1);
    }

    public readonly int Count => state.IsEmpty ? 0 : state[0].Count;

    public readonly int Capacity => destination.Length;

    public readonly int RemainingCapacity => destination.Length - Count;

    public readonly RuleValidationDecision Validation => state.IsEmpty
        ? RuleValidationDecision.Undecided
        : state[0].Validation;

    public readonly ReadOnlySpan<RuleHandlerResult> WrittenSpan => destination[..Count];

    public readonly ref readonly RuleHandlerResult this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return ref destination[index];
        }
    }

    public int Allow()
    {
        EnsureValidationUndecided();
        int index = Append(new RuleHandlerResult(
            RuleHandlerResultKind.Allow,
            Sequence: 0,
            RuleFactId.Null,
            StatId.Null,
            StringId.Null,
            Value: 1,
            AuxiliaryValue: 0,
            RuleValueFlags.None));
        state[0].Validation = RuleValidationDecision.Allowed;
        return index;
    }

    public int Reject(StringId reason)
    {
        EnsureValidationUndecided();
        int index = Append(new RuleHandlerResult(
            RuleHandlerResultKind.Reject,
            Sequence: 0,
            RuleFactId.Null,
            StatId.Null,
            reason,
            Value: 0,
            AuxiliaryValue: 0,
            RuleValueFlags.None));
        state[0].Validation = RuleValidationDecision.Rejected;
        return index;
    }

    public int ModifyStat(
        StatId stat,
        int amount,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (stat.IsNull)
            throw new ArgumentException("A stat result requires a concrete stat ID.", nameof(stat));

        return Append(new RuleHandlerResult(
            RuleHandlerResultKind.StatModifier,
            Sequence: 0,
            RuleFactId.Null,
            stat,
            StringId.Null,
            amount,
            AuxiliaryValue: 0,
            flags));
    }

    public int Record(
        RuleFactId result,
        int value,
        int auxiliaryValue = 0,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (result.IsNull)
            throw new ArgumentException("A recorded result requires a concrete result ID.", nameof(result));

        return Append(new RuleHandlerResult(
            RuleHandlerResultKind.Value,
            Sequence: 0,
            result,
            StatId.Null,
            StringId.Null,
            value,
            auxiliaryValue,
            flags));
    }

    public bool TryAppend(in RuleHandlerResult result, out int index)
    {
        if (result.Kind == RuleHandlerResultKind.None || state.IsEmpty || Count == destination.Length)
        {
            index = -1;
            return false;
        }

        index = state[0].Count;
        destination[index] = result.WithSequence(index);
        state[0].Count++;
        return true;
    }

    public int Append(in RuleHandlerResult result)
    {
        if (result.Kind == RuleHandlerResultKind.None)
            throw new ArgumentException("A handler result must have a concrete kind.", nameof(result));

        if (!TryAppend(in result, out int index))
        {
            throw new InvalidOperationException(
                $"Rule result capacity {destination.Length} was exceeded deterministically.");
        }

        return index;
    }

    private readonly void EnsureValidationUndecided()
    {
        if (state.IsEmpty)
            throw new InvalidOperationException("The rule result writer is not initialized.");

        if (Validation != RuleValidationDecision.Undecided)
        {
            throw new InvalidOperationException(
                "A handler may emit exactly one validation decision per result writer.");
        }
    }
}
