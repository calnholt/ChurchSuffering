#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;

namespace Crusaders30XX.ECS.DataOriented.Rules;

[Flags]
public enum CardHandlerFlags : ushort
{
    None = 0,
    Upgraded = 1 << 0,
    Pledged = 1 << 1,
    Scorched = 1 << 2,
    Weapon = 1 << 3,
    FirstPlayThisBattle = 1 << 4,
    WeaponUsedThisAction = 1 << 5,
    HasPledgedCard = 1 << 6,
    FinalBattle = 1 << 7,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardPhaseSnapshot(
    RulePhase Phase,
    int Turn,
    int BattleEpoch,
    int ActionSequence);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardPaymentSnapshot(
    int Count,
    int RedCount,
    int WhiteCount,
    int BlackCount,
    int AnyCount,
    int ScorchedCount)
{
    public bool IsEmpty => Count == 0;
    public bool HasScorchedCard => ScorchedCount > 0;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CombatResourceSnapshot(
    int Courage,
    int Temperance,
    int ActionPoints,
    int Vigor,
    int Aggression,
    int Scar,
    int Frostbite);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardBattleSnapshot(
    int CardsMilled,
    int ActionAttackHits,
    int CourageLostThisPhase,
    int CursesRemovedThisClimb,
    int EnemyBurn,
    int EnemyGuard,
    int EnemyArmor);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DeckStateSnapshot(
    EntityId Deck,
    EntityId PreviousTurnPledgedCard,
    int DrawCount,
    int HandCount,
    int DiscardCount,
    int ExhaustCount);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardHandlerInput(
    RuleInvocationId Invocation,
    EntityId Card,
    EntityId Player,
    CardId Definition,
    RuleTriggerEnvelope Trigger,
    CardHandlerFlags Flags,
    CardPhaseSnapshot Phase,
    CardPaymentSnapshot Payment,
    CombatResourceSnapshot Resources,
    CardBattleSnapshot Battle,
    DeckStateSnapshot Deck,
    int DerivedDamage,
    int ResolvedDamage,
    TargetHandle PrimaryTarget)
{
    public bool IsUpgraded => (Flags & CardHandlerFlags.Upgraded) != 0;
}

[Flags]
public enum EnemyHandlerFlags : ushort
{
    None = 0,
    FinalBattle = 1 << 0,
    Planning = 1 << 1,
    AttackBlocked = 1 << 2,
    DamageThresholdMet = 1 << 3,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EnemyCombatSnapshot(
    int BaseDamage,
    int DerivedDamage,
    int ResolvedDamage,
    int DamageThreshold,
    int AssignedBlockerCount,
    int DistinctBlockerColors,
    int TotalAssignedBlock,
    int ChannelStacks,
    int RequiredChannelStacks);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EnemyHandlerInput(
    RuleInvocationId Invocation,
    EntityId Enemy,
    EnemyId Definition,
    EnemyAttackId Attack,
    RuleTriggerEnvelope Trigger,
    EnemyHandlerFlags Flags,
    RulePhase Phase,
    int Turn,
    int BattleEpoch,
    EnemyCombatSnapshot Combat,
    EnemyPlanningMemory PlanningMemory,
    TargetHandle PrimaryTarget);

[Flags]
public enum EquipmentHandlerFlags : byte
{
    None = 0,
    Equipped = 1 << 0,
    Activated = 1 << 1,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EquipmentHandlerInput(
    RuleInvocationId Invocation,
    EntityId Equipment,
    EntityId Owner,
    EquipmentId Definition,
    RuleTriggerEnvelope Trigger,
    EquipmentHandlerFlags Flags,
    RulePhase Phase,
    CombatResourceSnapshot OwnerResources,
    DeckStateSnapshot Deck,
    EquipmentUsageState State,
    TargetHandle PrimaryTarget)
{
    public bool IsEquipped => (Flags & EquipmentHandlerFlags.Equipped) != 0;
}

[Flags]
public enum MedalHandlerFlags : byte
{
    None = 0,
    Acquired = 1 << 0,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MedalHandlerInput(
    RuleInvocationId Invocation,
    EntityId Medal,
    EntityId Owner,
    MedalId Definition,
    RuleTriggerEnvelope Trigger,
    MedalHandlerFlags Flags,
    RulePhase Phase,
    CombatResourceSnapshot OwnerResources,
    DeckStateSnapshot Deck,
    MedalRuntimeState State,
    TargetHandle PrimaryTarget)
{
    public bool IsAcquired => (Flags & MedalHandlerFlags.Acquired) != 0;
}
