#nullable enable

using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;

namespace Crusaders30XX.ECS.DataOriented.Rules;

public enum RuleTriggerKind : byte
{
    None = 0,
    PhaseChanged = 1,
    Card = 2,
    Passive = 3,
    HpRequested = 4,
    Tracking = 5,
    EncounterReward = 6,
    DrawPileEmpty = 7,
    Mill = 8,
    Replacement = 9,
}

public enum RulePhase : byte
{
    None = 0,
    StartBattle = 1,
    PlayerStart = 2,
    Action = 3,
    PlayerEnd = 4,
    EnemyStart = 5,
    EnemyAction = 6,
    EnemyEnd = 7,
    EndBattle = 8,
}

[System.Flags]
public enum RulePhaseMask : ushort
{
    None = 0,
    StartBattle = 1 << 0,
    PlayerStart = 1 << 1,
    Action = 1 << 2,
    PlayerEnd = 1 << 3,
    EnemyStart = 1 << 4,
    EnemyAction = 1 << 5,
    EnemyEnd = 1 << 6,
    EndBattle = 1 << 7,
}

public enum RuleCardEventKind : byte
{
    None = 0,
    Played = 1,
    Blocked = 2,
    Pledged = 3,
}

[System.Flags]
public enum RuleCardTraits : ushort
{
    None = 0,
    Attack = 1 << 0,
    Block = 1 << 1,
    Weapon = 1 << 2,
    Token = 1 << 3,
    Brittle = 1 << 4,
    Scorched = 1 << 5,
    Thorned = 1 << 6,
    Frozen = 1 << 7,
    Curse = 1 << 8,
}

public enum RuleDamageKind : byte
{
    None = 0,
    Attack = 1,
    Effect = 2,
    HpLoss = 3,
}

public enum RuleReplacementKind : byte
{
    None = 0,
    EffectThresholdDamage = 1,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PhaseChangedTriggerPayload(
    RulePhase Previous,
    RulePhase Current,
    int BattleEpoch);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardTriggerPayload(
    EntityId Card,
    EntityId Owner,
    CardId Definition,
    RuleCardEventKind EventKind,
    RuleCardColor Color,
    RuleCardTraits Traits);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PassiveTriggerPayload(
    EntityId Source,
    EntityId Target,
    EffectId Effect,
    int Delta);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct HpRequestedTriggerPayload(
    EntityId Source,
    EntityId Target,
    EntityId Card,
    RuleDamageKind DamageKind,
    int RawDelta,
    int PreviewDelta);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TrackingTriggerPayload(
    EntityId Subject,
    ConditionId Tracking,
    int Delta);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EncounterRewardTriggerPayload(
    EntityId Reward,
    byte IsEncounterReward)
{
    public bool IsEncounter => IsEncounterReward != 0;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DrawPileEmptyTriggerPayload(
    EntityId Deck,
    int EligibleDiscardCount);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MillTriggerPayload(
    EntityId Deck,
    EntityId Card);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ReplacementTriggerPayload(
    RuleReplacementKind Kind,
    EntityId OriginalSource,
    EntityId OriginalTarget,
    EntityId PrimaryEnemy,
    EffectId Effect,
    RuleDamageKind DamageKind,
    int OriginalDelta,
    byte PrimaryEnemyHasRequiredState)
{
    public bool HasEligiblePrimaryEnemy => PrimaryEnemyHasRequiredState != 0 && !PrimaryEnemy.IsNull;
}

[StructLayout(LayoutKind.Explicit, Size = 64)]
public struct RuleTriggerPayload
{
    [FieldOffset(0)] public PhaseChangedTriggerPayload PhaseChanged;
    [FieldOffset(0)] public CardTriggerPayload Card;
    [FieldOffset(0)] public PassiveTriggerPayload Passive;
    [FieldOffset(0)] public HpRequestedTriggerPayload HpRequested;
    [FieldOffset(0)] public TrackingTriggerPayload Tracking;
    [FieldOffset(0)] public EncounterRewardTriggerPayload EncounterReward;
    [FieldOffset(0)] public DrawPileEmptyTriggerPayload DrawPileEmpty;
    [FieldOffset(0)] public MillTriggerPayload Mill;
    [FieldOffset(0)] public ReplacementTriggerPayload Replacement;

    public static RuleTriggerPayload From(in PhaseChangedTriggerPayload value) => new() { PhaseChanged = value };
    public static RuleTriggerPayload From(in CardTriggerPayload value) => new() { Card = value };
    public static RuleTriggerPayload From(in PassiveTriggerPayload value) => new() { Passive = value };
    public static RuleTriggerPayload From(in HpRequestedTriggerPayload value) => new() { HpRequested = value };
    public static RuleTriggerPayload From(in TrackingTriggerPayload value) => new() { Tracking = value };
    public static RuleTriggerPayload From(in EncounterRewardTriggerPayload value) => new() { EncounterReward = value };
    public static RuleTriggerPayload From(in DrawPileEmptyTriggerPayload value) => new() { DrawPileEmpty = value };
    public static RuleTriggerPayload From(in MillTriggerPayload value) => new() { Mill = value };
    public static RuleTriggerPayload From(in ReplacementTriggerPayload value) => new() { Replacement = value };
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RuleTriggerEnvelope(
    RuleTriggerKind Kind,
    TriggerId SemanticTrigger,
    RuleTriggerPayload Payload);
