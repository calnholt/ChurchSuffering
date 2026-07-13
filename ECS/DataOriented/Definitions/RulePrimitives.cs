#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;

namespace Crusaders30XX.ECS.DataOriented.Definitions;

public readonly record struct StatId(ushort Value)
{
    public static StatId Null => default;
    public bool IsNull => Value == 0;
}

public readonly record struct EffectId(ushort Value)
{
    public static EffectId Null => default;
    public bool IsNull => Value == 0;
}

public readonly record struct ConditionId(ushort Value)
{
    public static ConditionId Null => default;
    public bool IsNull => Value == 0;
}

public readonly record struct TriggerId(ushort Value)
{
    public static TriggerId Null => default;
    public bool IsNull => Value == 0;
}

public readonly record struct RuleFactId(ushort Value)
{
    public static RuleFactId Null => default;
    public bool IsNull => Value == 0;
}

public readonly record struct RuleHandlerId(ushort Value)
{
    public static RuleHandlerId Null => default;
    public bool IsNull => Value == 0;
}

public readonly record struct MetaResourceId(ushort Value)
{
    public static MetaResourceId Null => default;
    public bool IsNull => Value == 0;
}

/// <summary>
/// Dependency on either a command-buffer result (null Result plus Sequence/Version) or
/// a handler result (non-null Result plus its writer Sequence). Stored sequences are
/// one-based so the all-zero value remains null.
/// </summary>
public readonly record struct RuleResultToken(RuleFactId Result, int Sequence, int Version)
{
    public static RuleResultToken Null => default;
    public bool IsNull => Result.IsNull && Sequence == 0 && Version == 0;
}

public readonly record struct RuleInvocationId(int Value)
{
    public static RuleInvocationId Null => default;
    public bool IsNull => Value == 0;
}

public enum TargetKind : byte
{
    None = 0,
    Entity = 1,
    Source = 2,
    Player = 3,
    PrimaryEnemy = 4,
    SelectionSlot = 5,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct TargetHandle(TargetKind Kind, EntityId Entity, int Slot)
{
    public static TargetHandle None => default;

    public static TargetHandle ForEntity(EntityId entity) =>
        entity.IsNull ? None : new(TargetKind.Entity, entity, 0);

    public static TargetHandle Source => new(TargetKind.Source, EntityId.Null, 0);

    public static TargetHandle Player => new(TargetKind.Player, EntityId.Null, 0);

    public static TargetHandle PrimaryEnemy => new(TargetKind.PrimaryEnemy, EntityId.Null, 0);

    public static TargetHandle SelectionSlot(int slot) =>
        slot < 0
            ? throw new ArgumentOutOfRangeException(nameof(slot))
            : new(TargetKind.SelectionSlot, EntityId.Null, slot);
}

public readonly record struct TargetCollectionHandle(DynamicBufferHandle<EntityId> Entities)
{
    public static TargetCollectionHandle Null => default;
    public bool IsNull => Entities.IsNull;
}

public enum ComparisonOperator : byte
{
    Always = 0,
    Equal = 1,
    NotEqual = 2,
    LessThan = 3,
    LessThanOrEqual = 4,
    GreaterThan = 5,
    GreaterThanOrEqual = 6,
}

[Flags]
public enum RuleValueFlags : ushort
{
    None = 0,
    Permanent = 1 << 0,
    Unpreventable = 1 << 1,
    PreviewOnly = 1 << 2,
    Upgraded = 1 << 3,
    BattleOnly = 1 << 4,
    QuestPersistent = 1 << 5,
    IgnoreAegis = 1 << 6,
    FreeAction = 1 << 7,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ConditionSpec(
    ConditionId Id,
    ComparisonOperator Operator,
    TargetHandle Subject,
    StatId Stat,
    int Threshold,
    RuleValueFlags Flags)
{
    public static ConditionSpec Always => default;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EffectSpec(
    EffectId Id,
    int Magnitude,
    int Duration,
    ConditionSpec Condition,
    RuleValueFlags Flags);

public enum DefinitionKind : byte
{
    None = 0,
    Card = 1,
    Enemy = 2,
    EnemyAttack = 3,
    Equipment = 4,
    Medal = 5,
}

[StructLayout(LayoutKind.Sequential, Pack = 2)]
public readonly record struct DefinitionReference(DefinitionKind Kind, ushort Value)
{
    public static DefinitionReference From(CardId value) => new(DefinitionKind.Card, (ushort)value);
    public static DefinitionReference From(EnemyId value) => new(DefinitionKind.Enemy, (ushort)value);
    public static DefinitionReference From(EnemyAttackId value) => new(DefinitionKind.EnemyAttack, (ushort)value);
    public static DefinitionReference From(EquipmentId value) => new(DefinitionKind.Equipment, (ushort)value);
    public static DefinitionReference From(MedalId value) => new(DefinitionKind.Medal, (ushort)value);
}

public enum CardZone : byte
{
    None = 0,
    MasterDeck = 1,
    DrawPile = 2,
    Hand = 3,
    DiscardPile = 4,
    ExhaustPile = 5,
    Removed = 6,
}

public enum RuleCardType : byte
{
    None = 0,
    Attack = 1,
    Prayer = 2,
    Block = 3,
    Relic = 4,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PresentationSpec(
    VisualEffectRecipeId VisualEffect,
    SoundId Sound,
    int DelayMilliseconds,
    RuleValueFlags Flags);
