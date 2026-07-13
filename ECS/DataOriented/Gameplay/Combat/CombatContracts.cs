#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Combat;

public enum CombatPhase : byte
{
    None = 0,
    BattleStart = 1,
    Block = 2,
    ResolvingEnemyAttacks = 3,
    Action = 4,
    Draw = 5,
    EnemyTurn = 6,
    Victory = 7,
    Defeat = 8,
    PhaseTransition = 9,
}

[Flags]
public enum CombatFlags : ushort
{
    None = 0,
    BattleStarted = 1 << 0,
    AwaitingBlockConfirmation = 1 << 1,
    AwaitingPresentation = 1 << 2,
    FinalBattle = 1 << 3,
    EnemyDefeated = 1 << 4,
    PlayerDefeated = 1 << 5,
    PhaseTransitionPending = 1 << 6,
}

public enum CombatDamageKind : byte
{
    Attack = 0,
    Effect = 1,
    Passive = 2,
}

public enum CombatDefeatKind : byte
{
    None = 0,
    EnemyPhase = 1,
    Enemy = 2,
    Player = 3,
}

public enum CombatRuleKind : byte
{
    StartBattle = 1,
    PlanEnemyIntent = 2,
    BeginEnemyAttack = 3,
    ConfirmBlocks = 4,
    PresentEnemyAttack = 5,
    ResolveEnemyImpact = 6,
    CompleteEnemyAttack = 7,
    BeginActionPhase = 8,
    EndActionPhase = 9,
    BeginEnemyTurn = 10,
    AdvanceEnemyPhase = 11,
    CompleteVictory = 12,
    CompleteDefeat = 13,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct BattleInfo : IComponent
{
    public EntityId Player;
    public EntityId Enemy;
    public EntityId Deck;
    public ulong Seed;
    public int InvocationSequence;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct BattleStateInfo : IComponent
{
    public int Turn;
    public int BattleEpoch;
    public int AttacksResolved;
    public int CourageLost;
    public int DamageTakenThisTurn;
    public CombatFlags Flags;
    public DynamicBufferHandle<CombatPassive> PlayerPassives;
    public DynamicBufferHandle<CombatPassive> EnemyPassives;
    public DynamicBufferHandle<CombatTraceEntry> Trace;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Battlefield : IComponent
{
    public EntityId Player;
    public EntityId Enemy;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct PhaseState : IComponent
{
    public CombatPhase Current;
    public CombatPhase Previous;
    public int Sequence;
    public int Turn;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Enemy : IComponent
{
    public EnemyId Definition;
    public int Phase;
    public int PhaseTurn;
    public EnemyPlanningMemory PlanningMemory;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EnemyArsenal : IComponent
{
    public DynamicBufferHandle<EnemyAttackId> Attacks;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AttackIntent : IComponent
{
    public DynamicBufferHandle<EnemyAttackId> Attacks;
    public int CurrentIndex;
    public EnemyAttackId Current;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct NextTurnAttackIntent : IComponent
{
    public DynamicBufferHandle<EnemyAttackId> Attacks;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EnemyAttackProgress : IComponent
{
    public EnemyAttackId Attack;
    public int BaseDamage;
    public int AdditionalDamage;
    public int EffectiveDamage;
    public int AssignedBlock;
    public int AssignedBlockerCount;
    public int DistinctBlockerColors;
    public int DamageDealt;
    public RequirementKind Requirement;
    public RequirementKind ColorRequirement;
    public RuleCardColor RequiredColor;
    public int RequiredAmount;
    public byte AssignedColorMask;
    public byte Revealed;
    public byte Confirmed;
    public byte FullyPreventedBySpecial;
    public DynamicBufferHandle<BlockAssignmentEntry> Blocks;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AssignedBlockCard : IComponent
{
    public EntityId AttackOwner;
    public int AttackIndex;
    public int Block;
    public RuleCardColor Color;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AmbushState : IComponent
{
    public int RemainingMilliseconds;
    public int BaseMilliseconds;
    public byte Active;
    public byte Expired;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Tribulation : IComponent
{
    public int Level;
    public int DamageBonus;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct ModifiedBlock : IComponent
{
    public int Base;
    public int TemporaryDelta;
    public int QuestDelta;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CannotBlockThisAttack : IComponent
{
    public EnemyAttackId Attack;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AssignedBlockPresentation : IComponent
{
    public int RailIndex;
    public float Offset;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct AssignedBlockRailPresentation : IComponent
{
    public int Count;
    public float Width;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct EnemyAttackBannerPresentation : IComponent
{
    public EnemyAttackId Attack;
    public float Opacity;
    public byte Visible;
}

public struct CourageTooltipAnchor : ITag { }
public struct ThreatTooltipAnchor : ITag { }
public struct RelentlessStrikeBattleState : ITag { }
public struct EnemyAttackBannerAnchor : ITag { }
public struct ExhaustOnBlock : ITag { }
public struct AmbushTextAnchor : ITag { }
public struct AmbushTimerAnchor : ITag { }

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CombatPassive(
    EffectId Effect,
    int Stacks,
    int Duration,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct BlockAssignmentEntry(
    EntityId Card,
    int Block,
    RuleCardColor Color,
    byte IsEquipment,
    byte IsFrozen,
    byte IsSealed);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CombatTraceEntry(
    int Sequence,
    CombatPhase Phase,
    CombatRuleKind Rule,
    EnemyAttackId Attack,
    int Value0,
    int Value1);

public enum CombatCardCandidateKind : byte
{
    None,
    Hand,
    TopOfDrawPile,
}

public enum CombatCardCommandKind : byte
{
    None,
    ApplyEffect,
    RemoveEffect,
    Draw,
    Mill,
    ResolveMarkedDiscard,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CombatCardFacts(
    int FrozenInHand,
    int EligibleRedBlockers,
    int EligibleWhiteBlockers,
    int EligibleBlackBlockers,
    int EligibleColorlessBlockers,
    int EligibleRedEquipment,
    int EligibleWhiteEquipment,
    int EligibleBlackEquipment)
{
    public int CountEligible(RequirementKind restriction, RuleCardColor color)
    {
        int red = EligibleRedBlockers + EligibleRedEquipment;
        int white = EligibleWhiteBlockers + EligibleWhiteEquipment;
        int black = EligibleBlackBlockers + EligibleBlackEquipment;
        int colorless = EligibleColorlessBlockers;
        return restriction switch
        {
            RequirementKind.OnlyCardColor when color == RuleCardColor.Red => red,
            RequirementKind.OnlyCardColor when color == RuleCardColor.White => white,
            RequirementKind.OnlyCardColor when color == RuleCardColor.Black => black,
            RequirementKind.ExcludeCardColor when color == RuleCardColor.Red => white + black + colorless,
            RequirementKind.ExcludeCardColor when color == RuleCardColor.White => red + black + colorless,
            RequirementKind.ExcludeCardColor when color == RuleCardColor.Black => red + white + colorless,
            _ => red + white + black + colorless,
        };
    }
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CombatCardCommand(
    CombatCardCommandKind Kind,
    EntityId Battle,
    EntityId Deck,
    EntityId Card,
    EffectId Effect,
    int Amount,
    byte ConditionSucceeded = 0);

/// <summary>
/// Battle-scoped, root-composable seam between combat rules and card state. Implementations expose
/// read-only planning/selection facts and accept typed commands; combat never receives a card system.
/// </summary>
public interface ICombatCardBoundary
{
    CombatCardFacts ReadFacts(EntityId deck, EntityId player);
    int CopyCandidates(EntityId deck, CombatCardCandidateKind kind, Span<EntityId> destination);
    void Execute(in CombatCardCommand command, CommandBuffer structuralCommands);
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct CombatRuleState
{
    public CombatRuleKind Kind;
    public EntityId Battle;
    public EntityId Subject;
    public EnemyAttackId Attack;
    public int Stage;
    public int WaitFrames;
    public int Value0;
    public int Value1;
}

public readonly record struct AmbushTimerExpired(EntityId Battle);
public readonly record struct ChangeBattlePhaseEvent(EntityId Battle, CombatPhase Previous, CombatPhase Current, int Sequence);
public readonly record struct ProceedToNextPhase(EntityId Battle);
public readonly record struct ShowConfirmButtonEvent(EntityId Battle, byte Visible);
public readonly record struct AssignedBlockReturnCompleted(EntityId Battle, EntityId Card);
public readonly record struct BlockAssignmentAdded(EntityId Battle, EntityId Card, int Block, RuleCardColor Color);
public readonly record struct BlockAssignmentRemoved(EntityId Battle, EntityId Card);
public readonly record struct CardBlockedEvent(EntityId Card, EnemyAttackId Attack, int Block);
public readonly record struct MustBeBlockedEvent(EntityId Battle, RequirementKind Kind, int Amount);
public readonly record struct ApplyEffect(EntityId Source, EntityId Target, EffectSpec Effect);
public readonly record struct AttackResolved(EntityId Battle, EnemyAttackId Attack, int Damage);
public readonly record struct EnemyAbsorbComplete(EntityId Battle);
public readonly record struct EnemyAttackImpactNow(EntityId Battle, EnemyAttackId Attack);
public readonly record struct EnemyDamageAppliedEvent(EntityId Battle, int Damage);
public readonly record struct EnemyKilledEvent(EntityId Battle, EnemyId Enemy);
public readonly record struct EnemyPhaseLethalEvent(EntityId Battle, int Phase);
public readonly record struct EnemyPhaseResetEvent(EntityId Battle, int Phase);
public readonly record struct IntentPlanned(EntityId Battle, int Count);
public readonly record struct OnEnemyAttackHitEvent(EntityId Battle, EnemyAttackId Attack, int Damage);
public readonly record struct ResolveAttack(EntityId Battle, EnemyAttackId Attack);
public readonly record struct ResolvingEnemyDamageEvent(EntityId Battle, EnemyAttackId Attack, int Damage);
public readonly record struct ShowStunnedOverlay(EntityId Battle, byte Visible);
public readonly record struct TriggerEnemyAttackDisplayEvent(EntityId Battle, EnemyAttackId Attack);
public readonly record struct ApplyBattleMaxHpEvent(EntityId Target, int Maximum);
public readonly record struct FullyHealEvent(EntityId Target);
public readonly record struct HealEvent(EntityId Target, int Amount);
public readonly record struct IncreaseMaxHpEvent(EntityId Target, int Amount);
public readonly record struct ModifyHpEvent(EntityId Source, EntityId Target, int Delta, CombatDamageKind Kind);
public readonly record struct PlayerDied(EntityId Battle, EntityId Player);
public readonly record struct SetHpEvent(EntityId Target, int Current);
public readonly record struct ModifyThreatEvent(EntityId Player, int Delta);
public readonly record struct SetThreatEvent(EntityId Player, int Amount);
