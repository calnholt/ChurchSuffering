#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Definitions;

public enum RuleResetPolicy : byte
{
    Never = 0,
    OnAcquire = 1,
    StartBattle = 2,
}

public enum EquipmentUsageLifetime : byte
{
    Battle = 0,
    Quest = 1,
    Climb = 2,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EquipmentActivationSpec(
    RulePhaseMask AllowedPhases,
    TriggerId ActivationTrigger,
    ConditionId AvailabilityCondition,
    byte MaxUses,
    RuleResetPolicy ResetPolicy,
    EquipmentUsageLifetime Lifetime)
{
    public bool IsActive => MaxUses > 0 && AllowedPhases != RulePhaseMask.None;
}

public enum RuleActivationTiming : byte
{
    QueuedAfterTrigger = 0,
    SynchronousBeforeOriginContinues = 1,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EquipmentUsageState
{
    public int BattleEpoch;
    public ushort Uses;
    public byte Initialized;

    public readonly bool IsInitialized => Initialized != 0;
}

public enum MedalCounterProgression : byte
{
    None = 0,
    IncrementToThreshold = 1,
    ConsumeCharge = 2,
}

public enum MedalCounterConsumePolicy : byte
{
    ResetToZero = 0,
    KeepRemainder = 1,
    StayAtZero = 2,
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly record struct MedalCounterSpec(
    ushort Threshold,
    ushort InitialCount,
    RuleResetPolicy ResetPolicy,
    MedalCounterProgression Progression,
    MedalCounterConsumePolicy ConsumePolicy)
{
    public bool UsesCounter => Progression != MedalCounterProgression.None;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MedalActivationSpec(
    TriggerId Trigger,
    ConditionId QualificationCondition,
    MedalCounterSpec Counter,
    short EventPriority,
    RuleActivationTiming Timing);

[System.Flags]
public enum MedalRuntimeFlags : byte
{
    None = 0,
    Initialized = 1 << 0,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct MedalRuntimeState
{
    public int BattleEpoch;
    public ushort Count;
    public MedalRuntimeFlags Flags;

    public readonly bool IsInitialized => (Flags & MedalRuntimeFlags.Initialized) != 0;
}

public static class EquipmentMedalStateRules
{
    public static void Initialize(
        ref EquipmentUsageState state,
        in EquipmentActivationSpec spec,
        int battleEpoch)
    {
        state.BattleEpoch = battleEpoch;
        state.Uses = 0;
        state.Initialized = 1;
    }

    public static void RefreshForBattle(
        ref EquipmentUsageState state,
        in EquipmentActivationSpec spec,
        int battleEpoch)
    {
        if (!state.IsInitialized)
        {
            Initialize(ref state, in spec, battleEpoch);
            return;
        }

        if (state.BattleEpoch == battleEpoch)
            return;

        state.BattleEpoch = battleEpoch;
        if (spec.ResetPolicy == RuleResetPolicy.StartBattle)
            state.Uses = 0;
    }

    public static bool CanActivate(
        in EquipmentUsageState state,
        in EquipmentActivationSpec spec,
        RulePhaseMask phase,
        bool availabilityConditionSatisfied)
    {
        if (!state.IsInitialized || !spec.IsActive || !availabilityConditionSatisfied)
            return false;
        if ((spec.AllowedPhases & phase) == 0)
            return false;
        return state.Uses < spec.MaxUses;
    }

    public static bool TryMarkUsed(ref EquipmentUsageState state, in EquipmentActivationSpec spec)
    {
        if (!state.IsInitialized || state.Uses >= spec.MaxUses)
            return false;
        state.Uses++;
        return true;
    }

    public static void Initialize(
        ref MedalRuntimeState state,
        in MedalCounterSpec spec,
        int battleEpoch)
    {
        state.BattleEpoch = battleEpoch;
        state.Count = spec.InitialCount;
        state.Flags = MedalRuntimeFlags.Initialized;
    }

    public static void RefreshForBattle(
        ref MedalRuntimeState state,
        in MedalCounterSpec spec,
        int battleEpoch)
    {
        if (!state.IsInitialized)
        {
            Initialize(ref state, in spec, battleEpoch);
            return;
        }

        if (state.BattleEpoch == battleEpoch)
            return;

        state.BattleEpoch = battleEpoch;
        if (spec.ResetPolicy == RuleResetPolicy.StartBattle)
            state.Count = spec.InitialCount;
    }

    public static bool ObserveQualifyingTrigger(
        ref MedalRuntimeState state,
        in MedalCounterSpec spec,
        ushort amount = 1)
    {
        if (!state.IsInitialized)
            throw new InvalidOperationException("Medal runtime state must be initialized before observing triggers.");

        return spec.Progression switch
        {
            MedalCounterProgression.None => true,
            MedalCounterProgression.IncrementToThreshold => Increment(ref state, in spec, amount),
            MedalCounterProgression.ConsumeCharge => ConsumeCharge(ref state, amount),
            _ => throw new ArgumentOutOfRangeException(nameof(spec)),
        };
    }

    private static bool Increment(
        ref MedalRuntimeState state,
        in MedalCounterSpec spec,
        ushort amount)
    {
        if (spec.Threshold == 0)
            return true;

        uint total = (uint)state.Count + amount;
        if (total < spec.Threshold)
        {
            state.Count = (ushort)Math.Min(total, ushort.MaxValue);
            return false;
        }

        state.Count = spec.ConsumePolicy switch
        {
            MedalCounterConsumePolicy.ResetToZero => 0,
            MedalCounterConsumePolicy.KeepRemainder => (ushort)Math.Min(total % spec.Threshold, ushort.MaxValue),
            MedalCounterConsumePolicy.StayAtZero => 0,
            _ => throw new ArgumentOutOfRangeException(nameof(spec)),
        };
        return true;
    }

    private static bool ConsumeCharge(ref MedalRuntimeState state, ushort amount)
    {
        if (amount == 0 || state.Count == 0)
            return false;
        state.Count = (ushort)Math.Max(0, state.Count - amount);
        return true;
    }
}
