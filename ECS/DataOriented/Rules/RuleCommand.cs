#nullable enable

using System;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Rules;

public enum RuleCommandKind : byte
{
    None = 0,
    Damage = 1,
    Heal = 2,
    GainBlock = 3,
    ModifyStat = 4,
    ApplyEffect = 5,
    RemoveEffect = 6,
    MoveCard = 7,
    SpawnDefinition = 8,
    Present = 9,
    Custom = 10,
    SetRequirement = 11,
    SetResolvedValue = 12,
    Schedule = 13,
    DamageFromResult = 14,
    MutateCard = 15,
    Reject = 16,
    RandomCardZone = 17,
    SpawnCard = 18,
    RemovePledge = 19,
    ModifyMaxHealth = 20,
    ModifyMaxHandSize = 21,
    ModifyMetaResource = 22,
}

public enum RuleCardColor : byte
{
    None = 0,
    Red = 1,
    White = 2,
    Black = 3,
}

public enum RequirementKind : byte
{
    None = 0,
    MinimumBlockers = 1,
    ExactBlockers = 2,
    OnlyCardColor = 3,
    ExcludeCardColor = 4,
    MinimumStat = 5,
    MaximumStat = 6,
    MinimumCardCount = 7,
    WeaponAttackThisTurn = 8,
    EnemyPhase = 9,
    Predicate = 10,
}

public enum ResolvedValueKind : byte
{
    None = 0,
    Damage = 1,
    AdditionalDamage = 2,
    EffectiveBlock = 3,
    GuardConversion = 4,
    Cost = 5,
    Count = 6,
    Boolean = 7,
}

public enum CardMutationKind : byte
{
    None = 0,
    SetColor = 1,
    ModifyBlock = 2,
    SetBlock = 3,
    ModifyCost = 4,
    SetCost = 5,
    SetUpgraded = 6,
    SetExhaust = 7,
    SetFreeAction = 8,
    SetWeapon = 9,
    SetPledgeEligibility = 10,
    ModifySeals = 11,
    SetSeals = 12,
    ModifyDamage = 13,
    SetDamage = 14,
    SetType = 15,
    ClearDisplayText = 16,
}

public enum RuleRejectionReason : byte
{
    None = 0,
    InsufficientStat = 1,
    InvalidPhase = 2,
    MissingRequiredCard = 3,
    MissingWeaponAttack = 4,
    InvalidTarget = 5,
    InvalidCardCount = 6,
    AlreadyUsed = 7,
    PledgeUnavailable = 8,
    RequirementFailed = 9,
}

public enum RandomCardZoneOperation : byte
{
    None = 0,
    MoveTop = 1,
    MoveBottom = 2,
    MoveRandom = 3,
    ShuffleInto = 4,
    Mill = 5,
    Resurrect = 6,
}

[Flags]
public enum RuleCardFilter : ushort
{
    None = 0,
    Red = 1 << 0,
    White = 1 << 1,
    Black = 1 << 2,
    ExcludeColorless = 1 << 3,
    ExcludePledged = 1 << 4,
    ExcludeWeapon = 1 << 5,
    ExcludeEquipment = 1 << 6,
    Playable = 1 << 7,
    HasCardApplication = 1 << 8,
    Sealed = 1 << 9,
}

public enum PledgeRemovalReason : byte
{
    None = 0,
    Played = 1,
    DiscardedForCost = 2,
    Purged = 3,
    BattleEnded = 4,
    Replaced = 5,
}

public enum MaxHealthChangeKind : byte
{
    None = 0,
    Permanent = 1,
    BattleOnly = 2,
    RecalculateFromScar = 3,
}

public enum MaxHandSizeChangeKind : byte
{
    None = 0,
    Permanent = 1,
    BattleOnly = 2,
    Set = 3,
}

public enum MetaResourceChangeKind : byte
{
    None = 0,
    Add = 1,
    Spend = 2,
    Set = 3,
}

[Flags]
public enum MetaResourceScope : byte
{
    None = 0,
    Runtime = 1 << 0,
    PendingReward = 1 << 1,
    EventPayload = 1 << 2,
    Persist = 1 << 3,
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ResourceDeltaRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    StatId Stat,
    int Amount,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct EffectRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    EffectSpec Effect);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RemoveEffectRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    EffectId Effect,
    int StackCount,
    RuleValueFlags Flags)
{
    /// <summary>Sentinel requesting removal of every stack and the effect entry itself.</summary>
    public const int AllStacks = -1;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardZoneRuleCommand(
    EntityId Card,
    EntityId Deck,
    CardZone SourceZone,
    CardZone DestinationZone,
    int DestinationIndex,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SpawnDefinitionRuleCommand(
    TargetHandle Source,
    TargetHandle Owner,
    DefinitionReference Definition,
    int Count,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct PresentationRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    PresentationSpec Presentation);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CustomRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    RuleHandlerId Handler,
    int Value1,
    int Value2,
    int Value3,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RequirementRuleCommand(
    TargetHandle Subject,
    RequirementKind Kind,
    RuleCardColor Color,
    StatId Stat,
    ConditionId Predicate,
    int Amount,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ResolvedValueRuleCommand(
    TargetHandle Subject,
    ResolvedValueKind Kind,
    int Value,
    RuleValueFlags Flags);

/// <summary>
/// Schedules a frozen handler route at a trigger or elapsed delay. <see cref="DependsOn"/>
/// is null for unconditional work; otherwise the scheduler waits for that result token.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct ScheduledRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    TriggerId Trigger,
    RuleHandlerId Handler,
    int DelayMilliseconds,
    RuleResultToken DependsOn,
    int Value1,
    int Value2,
    int Value3,
    RuleValueFlags Flags);

/// <summary>
/// Produces damage from a prior result using
/// <c>Clamp((input * Numerator / Denominator) + Offset, Minimum, Maximum)</c>.
/// A minimum greater than a maximum disables that bound pair.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct DerivedDamageRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    RuleResultToken Input,
    int Numerator,
    int Denominator,
    int Offset,
    int Minimum,
    int Maximum,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct CardMutationRuleCommand(
    EntityId Card,
    CardMutationKind Kind,
    RuleCardColor Color,
    RuleCardType CardType,
    EffectId Effect,
    int Amount,
    int Duration,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RejectionRuleCommand(
    TargetHandle Source,
    TargetHandle Target,
    RuleRejectionReason Reason,
    StringId Message,
    RuleValueFlags Flags);

/// <summary>
/// Requests deterministic zone selection. Mill means source to discard; resurrect means
/// discard to hand. Selection consumes the invocation's caller-owned random stream.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RandomCardZoneRuleCommand(
    EntityId Deck,
    CardZone SourceZone,
    CardZone DestinationZone,
    RandomCardZoneOperation Operation,
    RuleCardFilter Filter,
    int Count,
    int DestinationIndex,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct SpawnCardRuleCommand(
    TargetHandle Source,
    TargetHandle Owner,
    EntityId Deck,
    CardId Card,
    CardZone DestinationZone,
    RuleCardColor Color,
    bool IsUpgraded,
    int Count,
    int DestinationIndex,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RemovePledgeRuleCommand(
    EntityId Card,
    TargetHandle Owner,
    CardZone DestinationZone,
    PledgeRemovalReason Reason,
    RuleValueFlags Flags);

/// <summary>
/// Shared fixed-layout payload for maximum changes. <see cref="RuleCommandKind.ModifyMaxHealth"/>
/// interprets <see cref="ChangeKind"/> as <see cref="MaxHealthChangeKind"/>;
/// <see cref="RuleCommandKind.ModifyMaxHandSize"/> interprets it as
/// <see cref="MaxHandSizeChangeKind"/>. Additive modes treat <see cref="Amount"/> as a delta.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MaximumRuleCommand(
    TargetHandle Target,
    int Amount,
    byte ChangeKind,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct MetaResourceRuleCommand(
    TargetHandle Owner,
    MetaResourceId Resource,
    MetaResourceChangeKind Change,
    int Amount,
    MetaResourceScope Scope,
    RuleValueFlags Flags);

[StructLayout(LayoutKind.Explicit, Size = 96)]
public struct RuleCommandPayload
{
    [FieldOffset(0)] public ResourceDeltaRuleCommand ResourceDelta;
    [FieldOffset(0)] public EffectRuleCommand Effect;
    [FieldOffset(0)] public RemoveEffectRuleCommand RemoveEffect;
    [FieldOffset(0)] public CardZoneRuleCommand CardZone;
    [FieldOffset(0)] public SpawnDefinitionRuleCommand SpawnDefinition;
    [FieldOffset(0)] public PresentationRuleCommand Presentation;
    [FieldOffset(0)] public CustomRuleCommand Custom;
    [FieldOffset(0)] public RequirementRuleCommand Requirement;
    [FieldOffset(0)] public ResolvedValueRuleCommand ResolvedValue;
    [FieldOffset(0)] public ScheduledRuleCommand Scheduled;
    [FieldOffset(0)] public DerivedDamageRuleCommand DerivedDamage;
    [FieldOffset(0)] public CardMutationRuleCommand CardMutation;
    [FieldOffset(0)] public RejectionRuleCommand Rejection;
    [FieldOffset(0)] public RandomCardZoneRuleCommand RandomCardZone;
    [FieldOffset(0)] public SpawnCardRuleCommand SpawnCard;
    [FieldOffset(0)] public RemovePledgeRuleCommand RemovePledge;
    [FieldOffset(0)] public MaximumRuleCommand Maximum;
    [FieldOffset(0)] public MetaResourceRuleCommand MetaResource;
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RuleCommand(
    RuleCommandKind Kind,
    int Sequence,
    RuleCommandPayload Payload)
{
    public static RuleCommand Damage(
        TargetHandle source,
        TargetHandle target,
        int amount,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Resource(RuleCommandKind.Damage, source, target, StatId.Null, amount, flags);

    public static RuleCommand Heal(
        TargetHandle source,
        TargetHandle target,
        int amount,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Resource(RuleCommandKind.Heal, source, target, StatId.Null, amount, flags);

    public static RuleCommand GainBlock(
        TargetHandle source,
        TargetHandle target,
        int amount,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Resource(RuleCommandKind.GainBlock, source, target, StatId.Null, amount, flags);

    public static RuleCommand ModifyStat(
        TargetHandle source,
        TargetHandle target,
        StatId stat,
        int amount,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Resource(RuleCommandKind.ModifyStat, source, target, stat, amount, flags);

    public static RuleCommand ApplyEffect(
        TargetHandle source,
        TargetHandle target,
        in EffectSpec effect)
    {
        var payload = new RuleCommandPayload
        {
            Effect = new EffectRuleCommand(source, target, effect),
        };
        return new RuleCommand(RuleCommandKind.ApplyEffect, 0, payload);
    }

    /// <summary>
    /// Removes all stacks by default. A positive stack count removes at most that many;
    /// zero and values below <see cref="RemoveEffectRuleCommand.AllStacks"/> are invalid.
    /// </summary>
    public static RuleCommand RemoveEffect(
        TargetHandle source,
        TargetHandle target,
        EffectId effect,
        int stackCount = RemoveEffectRuleCommand.AllStacks,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (stackCount == 0 || stackCount < RemoveEffectRuleCommand.AllStacks)
        {
            throw new ArgumentOutOfRangeException(nameof(stackCount));
        }

        var payload = new RuleCommandPayload
        {
            RemoveEffect = new RemoveEffectRuleCommand(source, target, effect, stackCount, flags),
        };
        return new RuleCommand(RuleCommandKind.RemoveEffect, 0, payload);
    }

    public static RuleCommand MoveCard(
        EntityId card,
        EntityId deck,
        CardZone source,
        CardZone destination,
        int destinationIndex = -1,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        var payload = new RuleCommandPayload
        {
            CardZone = new CardZoneRuleCommand(card, deck, source, destination, destinationIndex, flags),
        };
        return new RuleCommand(RuleCommandKind.MoveCard, 0, payload);
    }

    public static RuleCommand SpawnDefinition(
        TargetHandle source,
        TargetHandle owner,
        DefinitionReference definition,
        int count = 1,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        RequirePositive(count, nameof(count));
        var payload = new RuleCommandPayload
        {
            SpawnDefinition = new SpawnDefinitionRuleCommand(source, owner, definition, count, flags),
        };
        return new RuleCommand(RuleCommandKind.SpawnDefinition, 0, payload);
    }

    public static RuleCommand Present(
        TargetHandle source,
        TargetHandle target,
        in PresentationSpec presentation)
    {
        var payload = new RuleCommandPayload
        {
            Presentation = new PresentationRuleCommand(source, target, presentation),
        };
        return new RuleCommand(RuleCommandKind.Present, 0, payload);
    }

    public static RuleCommand Custom(
        TargetHandle source,
        TargetHandle target,
        RuleHandlerId handler,
        int value1 = 0,
        int value2 = 0,
        int value3 = 0,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        RequireKnownHandler(handler);
        var payload = new RuleCommandPayload
        {
            Custom = new CustomRuleCommand(source, target, handler, value1, value2, value3, flags),
        };
        return new RuleCommand(RuleCommandKind.Custom, 0, payload);
    }

    [Obsolete("Use the RuleHandlerId overload. Arbitrary integer handlers are rejected.")]
    public static RuleCommand Custom(
        TargetHandle source,
        TargetHandle target,
        int handlerId,
        int value1 = 0,
        int value2 = 0,
        int value3 = 0,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Custom(source, target, new RuleHandlerId(checked((ushort)handlerId)), value1, value2, value3, flags);

    public static RuleCommand SetRequirement(
        TargetHandle subject,
        RequirementKind kind,
        int amount = 0,
        StatId stat = default,
        RuleCardColor color = RuleCardColor.None,
        ConditionId predicate = default,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (kind == RequirementKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var payload = new RuleCommandPayload
        {
            Requirement = new RequirementRuleCommand(subject, kind, color, stat, predicate, amount, flags),
        };
        return new RuleCommand(RuleCommandKind.SetRequirement, 0, payload);
    }

    public static RuleCommand SetResolvedValue(
        TargetHandle subject,
        ResolvedValueKind kind,
        int value,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (kind == ResolvedValueKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var payload = new RuleCommandPayload
        {
            ResolvedValue = new ResolvedValueRuleCommand(subject, kind, value, flags),
        };
        return new RuleCommand(RuleCommandKind.SetResolvedValue, 0, payload);
    }

    public static RuleCommand Schedule(
        TargetHandle source,
        TargetHandle target,
        TriggerId trigger,
        RuleHandlerId handler,
        int delayMilliseconds = 0,
        RuleResultToken dependsOn = default,
        int value1 = 0,
        int value2 = 0,
        int value3 = 0,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        RequireKnownHandler(handler);
        if (trigger.IsNull && delayMilliseconds <= 0)
        {
            throw new ArgumentException("A scheduled rule requires a trigger or a positive delay.");
        }

        if (delayMilliseconds < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(delayMilliseconds));
        }

        var payload = new RuleCommandPayload
        {
            Scheduled = new ScheduledRuleCommand(
                source, target, trigger, handler, delayMilliseconds, dependsOn, value1, value2, value3, flags),
        };
        return new RuleCommand(RuleCommandKind.Schedule, 0, payload);
    }

    public static RuleCommand DamageFromResult(
        TargetHandle source,
        TargetHandle target,
        RuleResultToken input,
        int numerator = 1,
        int denominator = 1,
        int offset = 0,
        int minimum = 0,
        int maximum = int.MaxValue,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (input.IsNull)
        {
            throw new ArgumentException("Derived damage requires a result token.", nameof(input));
        }

        if (denominator == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(denominator));
        }

        var payload = new RuleCommandPayload
        {
            DerivedDamage = new DerivedDamageRuleCommand(
                source, target, input, numerator, denominator, offset, minimum, maximum, flags),
        };
        return new RuleCommand(RuleCommandKind.DamageFromResult, 0, payload);
    }

    public static RuleCommand MutateCard(
        EntityId card,
        CardMutationKind kind,
        int amount = 0,
        RuleCardColor color = RuleCardColor.None,
        EffectId effect = default,
        int duration = 0,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (kind == CardMutationKind.None)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        var payload = new RuleCommandPayload
        {
            CardMutation = new CardMutationRuleCommand(
                card, kind, color, RuleCardType.None, effect, amount, duration, flags),
        };
        return new RuleCommand(RuleCommandKind.MutateCard, 0, payload);
    }

    public static RuleCommand SetCardType(
        EntityId card,
        RuleCardType type,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (type == RuleCardType.None)
        {
            throw new ArgumentOutOfRangeException(nameof(type));
        }

        var payload = new RuleCommandPayload
        {
            CardMutation = new CardMutationRuleCommand(
                card,
                CardMutationKind.SetType,
                RuleCardColor.None,
                type,
                EffectId.Null,
                0,
                0,
                flags),
        };
        return new RuleCommand(RuleCommandKind.MutateCard, 0, payload);
    }

    public static RuleCommand RequestEndTurn(
        TargetHandle source,
        int delayMilliseconds = 0,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Schedule(
            source,
            TargetHandle.None,
            RuleTriggerIds.ActionPhaseEnd,
            RuleHandlerIds.ResolveEndTurnRequest,
            delayMilliseconds,
            flags: flags);

    public static RuleCommand Reject(
        TargetHandle source,
        TargetHandle target,
        RuleRejectionReason reason,
        StringId message,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (reason == RuleRejectionReason.None || message.IsNull)
        {
            throw new ArgumentException("A rejection requires a semantic reason and compact message ID.");
        }

        var payload = new RuleCommandPayload
        {
            Rejection = new RejectionRuleCommand(source, target, reason, message, flags),
        };
        return new RuleCommand(RuleCommandKind.Reject, 0, payload);
    }

    public static RuleCommand RandomCardZone(
        EntityId deck,
        CardZone source,
        CardZone destination,
        RandomCardZoneOperation operation,
        int count = 1,
        RuleCardFilter filter = RuleCardFilter.None,
        int destinationIndex = -1,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (operation == RandomCardZoneOperation.None)
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (operation == RandomCardZoneOperation.Mill &&
            (source != CardZone.DrawPile || destination != CardZone.DiscardPile))
        {
            throw new ArgumentException("Mill moves cards from the draw pile to the discard pile.");
        }

        if (operation == RandomCardZoneOperation.Resurrect &&
            (source != CardZone.DiscardPile || destination != CardZone.Hand))
        {
            throw new ArgumentException("Resurrect moves cards from the discard pile to the hand.");
        }

        RequirePositive(count, nameof(count));
        var payload = new RuleCommandPayload
        {
            RandomCardZone = new RandomCardZoneRuleCommand(
                deck, source, destination, operation, filter, count, destinationIndex, flags),
        };
        return new RuleCommand(RuleCommandKind.RandomCardZone, 0, payload);
    }

    public static RuleCommand SpawnCard(
        TargetHandle source,
        TargetHandle owner,
        EntityId deck,
        CardId card,
        CardZone destination,
        RuleCardColor color,
        bool isUpgraded = false,
        int count = 1,
        int destinationIndex = -1,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        RequirePositive(count, nameof(count));
        if (destination == CardZone.None || color == RuleCardColor.None)
        {
            throw new ArgumentException("A spawned card requires an explicit destination zone and printed color.");
        }

        var payload = new RuleCommandPayload
        {
            SpawnCard = new SpawnCardRuleCommand(
                source, owner, deck, card, destination, color, isUpgraded, count, destinationIndex, flags),
        };
        return new RuleCommand(RuleCommandKind.SpawnCard, 0, payload);
    }

    public static RuleCommand RemovePledge(
        EntityId card,
        TargetHandle owner,
        CardZone destination,
        PledgeRemovalReason reason,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (reason == PledgeRemovalReason.None)
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        var payload = new RuleCommandPayload
        {
            RemovePledge = new RemovePledgeRuleCommand(card, owner, destination, reason, flags),
        };
        return new RuleCommand(RuleCommandKind.RemovePledge, 0, payload);
    }

    public static RuleCommand ModifyMaxHealth(
        TargetHandle target,
        int amount,
        MaxHealthChangeKind change,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Maximum(RuleCommandKind.ModifyMaxHealth, target, amount, (byte)change, flags);

    public static RuleCommand ModifyMaxHandSize(
        TargetHandle target,
        int amount,
        MaxHandSizeChangeKind change,
        RuleValueFlags flags = RuleValueFlags.None) =>
        Maximum(RuleCommandKind.ModifyMaxHandSize, target, amount, (byte)change, flags);

    public static RuleCommand ModifyMetaResource(
        TargetHandle owner,
        MetaResourceId resource,
        MetaResourceChangeKind change,
        int amount,
        RuleValueFlags flags = RuleValueFlags.None) =>
        ModifyMetaResource(owner, resource, change, amount, MetaResourceScope.Runtime, flags);

    public static RuleCommand ModifyMetaResource(
        TargetHandle owner,
        MetaResourceId resource,
        MetaResourceChangeKind change,
        int amount,
        MetaResourceScope scope,
        RuleValueFlags flags = RuleValueFlags.None)
    {
        if (!RuleMetaResourceIds.IsKnown(resource) ||
            change == MetaResourceChangeKind.None ||
            scope == MetaResourceScope.None)
        {
            throw new ArgumentException("A meta-resource change requires a resource, operation, and scope.");
        }

        var payload = new RuleCommandPayload
        {
            MetaResource = new MetaResourceRuleCommand(owner, resource, change, amount, scope, flags),
        };
        return new RuleCommand(RuleCommandKind.ModifyMetaResource, 0, payload);
    }

    public static RuleResultToken ResultOf(RuleCommandIndex index) =>
        new(RuleFactId.Null, index.Index + 1, index.Version);

    public static RuleResultToken ResultOf(RuleFactId result, int resultSequence)
    {
        if (result.IsNull || resultSequence < 0)
        {
            throw new ArgumentException("A handler-result dependency requires a result fact and non-negative sequence.");
        }

        return new RuleResultToken(result, resultSequence + 1, 0);
    }

    internal RuleCommand WithSequence(int sequence) => this with { Sequence = sequence };

    private static RuleCommand Resource(
        RuleCommandKind kind,
        TargetHandle source,
        TargetHandle target,
        StatId stat,
        int amount,
        RuleValueFlags flags)
    {
        var payload = new RuleCommandPayload
        {
            ResourceDelta = new ResourceDeltaRuleCommand(source, target, stat, amount, flags),
        };
        return new RuleCommand(kind, 0, payload);
    }

    private static RuleCommand Maximum(
        RuleCommandKind kind,
        TargetHandle target,
        int amount,
        byte change,
        RuleValueFlags flags)
    {
        if (change == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(change));
        }

        var payload = new RuleCommandPayload
        {
            Maximum = new MaximumRuleCommand(target, amount, change, flags),
        };
        return new RuleCommand(kind, 0, payload);
    }

    private static void RequireKnownHandler(RuleHandlerId handler)
    {
        if (!RuleHandlerIds.IsKnown(handler))
        {
            throw new ArgumentOutOfRangeException(nameof(handler), handler.Value, "Handler ID is not frozen.");
        }
    }

    private static void RequirePositive(int value, string parameterName)
    {
        if (value <= 0)
        {
            throw new ArgumentOutOfRangeException(parameterName);
        }
    }
}
