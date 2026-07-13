#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;

namespace Crusaders30XX.ECS.DataOriented.Rules;

[Flags]
public enum EnemyPlanningMemoryFlags : byte
{
    None = 0,
    HasLastAttack = 1 << 0,
    HasPreviousAttack = 1 << 1,
    PhaseInitialized = 1 << 2,
}

/// <summary>
/// Compact state persisted by the enemy-planning system between invocations.
/// Named counters cover common planning history; four definition-owned values are
/// available for exceptional finite-state planners without managed state objects.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EnemyPlanningMemory
{
    public EnemyAttackId LastAttack;
    public EnemyAttackId PreviousAttack;
    public EnemyPlanningMemoryFlags Flags;
    public int PlansGenerated;
    public int RepetitionCount;
    public int Phase;
    public int PhaseTurn;
    public int Value0;
    public int Value1;
    public int Value2;
    public int Value3;

    public readonly bool HasLastAttack =>
        (Flags & EnemyPlanningMemoryFlags.HasLastAttack) != 0;

    public readonly bool HasPreviousAttack =>
        (Flags & EnemyPlanningMemoryFlags.HasPreviousAttack) != 0;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EnemyPlanWriterState
{
    internal int Count;
}

/// <summary>Bounded writer over caller-owned attack-plan and persisted-memory storage.</summary>
public ref struct EnemyPlanWriter
{
    private Span<EnemyAttackId> destination;
    private Span<EnemyPlanningMemory> memory;
    private Span<EnemyPlanWriterState> state;

    public EnemyPlanWriter(
        Span<EnemyAttackId> destination,
        ref EnemyPlanningMemory persistedMemory,
        ref EnemyPlanWriterState state)
    {
        this.destination = destination;
        memory = MemoryMarshal.CreateSpan(ref persistedMemory, 1);
        state = default;
        this.state = MemoryMarshal.CreateSpan(ref state, 1);
    }

    public readonly int Count => state.IsEmpty ? 0 : state[0].Count;

    public readonly int Capacity => destination.Length;

    public readonly int RemainingCapacity => destination.Length - Count;

    public readonly ReadOnlySpan<EnemyAttackId> WrittenSpan => destination[..Count];

    public EnemyPlanningMemory Memory
    {
        readonly get => memory[0];
        set => memory[0] = value;
    }

    public readonly EnemyAttackId this[int index]
    {
        get
        {
            if ((uint)index >= (uint)Count)
                throw new ArgumentOutOfRangeException(nameof(index));
            return destination[index];
        }
    }

    public bool TryAppend(EnemyAttackId attack)
    {
        if (state.IsEmpty || Count == destination.Length)
            return false;

        destination[state[0].Count++] = attack;
        return true;
    }

    public int Append(EnemyAttackId attack)
    {
        if (!TryAppend(attack))
        {
            throw new InvalidOperationException(
                $"Enemy plan capacity {destination.Length} was exceeded deterministically.");
        }

        return Count - 1;
    }

    public void AppendRange(ReadOnlySpan<EnemyAttackId> attacks)
    {
        if (attacks.Length > RemainingCapacity)
        {
            throw new InvalidOperationException(
                $"Enemy plan capacity {destination.Length} cannot append {attacks.Length} more attacks.");
        }

        attacks.CopyTo(destination[Count..]);
        state[0].Count += attacks.Length;
    }

    public void Remember(EnemyAttackId attack)
    {
        EnemyPlanningMemory next = Memory;
        if (next.HasLastAttack)
        {
            next.PreviousAttack = next.LastAttack;
            next.Flags |= EnemyPlanningMemoryFlags.HasPreviousAttack;
            next.RepetitionCount = next.LastAttack.Equals(attack)
                ? next.RepetitionCount + 1
                : 1;
        }
        else
        {
            next.RepetitionCount = 1;
        }

        next.LastAttack = attack;
        next.Flags |= EnemyPlanningMemoryFlags.HasLastAttack;
        next.PlansGenerated++;
        Memory = next;
    }
}
