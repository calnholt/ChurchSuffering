#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

public enum MedalReactiveEvent : byte
{
    None,
    Acquired,
    BattlePhaseChanged,
    TemperanceTriggered,
    CardBlocked,
    EnemyKilled,
    EncounterReward,
    PledgeAdded,
    CardPlayed,
    PassiveApplied,
    DrawPileEmpty,
    HpRequested,
    Tracking,
    MilledCard,
}

public enum MedalTriggerFilter : byte
{
    None,
    StartBattle,
    PlayerStart,
    ActionWithFiveCourage,
    NonNullOwner,
    BlackCard,
    PlayerAtOneHealth,
    EncounterReward,
    ScorchedCard,
    WeaponCard,
    PositiveOwnerAggression,
    ThornedCard,
    EligibleDiscard,
    OwnerAttackEnemyForEightPreviewDamage,
    PositiveCursesRemoved,
    NonNullMilledCard,
}

public readonly record struct MedalTriggerDefinition(
    MedalReactiveEvent Event,
    MedalTriggerFilter Filter,
    MedalActivationSpec Activation);

public enum MedalEffectTarget : byte
{
    Owner,
    PrimaryEnemy,
}

public enum MedalRuleEffectKind : byte
{
    None,
    ApplyEffect,
    ModifyStat,
    ResurrectRandom,
    ModifyMaxHealth,
    FreezeRandomCards,
    SpawnKunai,
    ModifyMetaResource,
    Damage,
    ModifyMaxHandSize,
    MillTopCard,
    ShuffleRandomDiscardToDraw,
}

public readonly record struct MedalRuleEffect(
    MedalRuleEffectKind Kind,
    MedalEffectTarget Target,
    EffectId Effect,
    StatId Stat,
    MetaResourceId MetaResource,
    int Amount);

public enum MedalProviderKind : byte
{
    None,
    AlternateBlockAttack,
    BrittleBlockModifier,
    ScorchedPaymentDamageModifier,
    FrostbiteReplacement,
}

public readonly record struct MedalProviderDefinition(
    MedalProviderKind Kind,
    int Amount,
    ProviderLifetime Lifetime);

public readonly record struct MedalDefinition(
    MedalId Id,
    string LegacyId,
    string Name,
    string Text,
    int MaxCount,
    MedalTriggerDefinition Trigger,
    MedalRuleEffect ActivationEffect1,
    MedalRuleEffect ActivationEffect2,
    MedalRuleEffect ActivationEffect3,
    MedalRuleEffect AcquisitionEffect1,
    MedalRuleEffect AcquisitionEffect2,
    MedalRuleEffect AcquisitionEffect3,
    MedalProviderDefinition Provider,
    VisualEffectRecipeId VisualRecipe)
{
    public int ActivationEffectCount => Count(ActivationEffect1, ActivationEffect2, ActivationEffect3);
    public int AcquisitionEffectCount => Count(AcquisitionEffect1, AcquisitionEffect2, AcquisitionEffect3);

    public MedalRuleEffect GetActivationEffect(int index) =>
        Get(index, ActivationEffect1, ActivationEffect2, ActivationEffect3, ActivationEffectCount);

    public MedalRuleEffect GetAcquisitionEffect(int index) =>
        Get(index, AcquisitionEffect1, AcquisitionEffect2, AcquisitionEffect3, AcquisitionEffectCount);

    private static int Count(MedalRuleEffect first, MedalRuleEffect second, MedalRuleEffect third) =>
        third.Kind != MedalRuleEffectKind.None ? 3 : second.Kind != MedalRuleEffectKind.None ? 2 :
        first.Kind != MedalRuleEffectKind.None ? 1 : 0;

    private static MedalRuleEffect Get(
        int index,
        MedalRuleEffect first,
        MedalRuleEffect second,
        MedalRuleEffect third,
        int count) => index switch
    {
        0 when count > 0 => first,
        1 when count > 1 => second,
        2 when count > 2 => third,
        _ => throw new System.ArgumentOutOfRangeException(nameof(index)),
    };
}

public static class MedalDefinitionParts
{
    public static MedalCounterSpec NoCounter => default;

    public static MedalCounterSpec Increment(ushort threshold, RuleResetPolicy reset = RuleResetPolicy.Never) =>
        new(threshold, 0, reset, MedalCounterProgression.IncrementToThreshold,
            MedalCounterConsumePolicy.ResetToZero);

    public static MedalCounterSpec Charge() =>
        new(1, 1, RuleResetPolicy.StartBattle, MedalCounterProgression.ConsumeCharge,
            MedalCounterConsumePolicy.StayAtZero);

    public static MedalTriggerDefinition Trigger(
        MedalReactiveEvent eventKind,
        MedalTriggerFilter filter,
        TriggerId trigger,
        MedalCounterSpec counter = default,
        short priority = 0,
        RuleActivationTiming timing = RuleActivationTiming.QueuedAfterTrigger) =>
        new(eventKind, filter, new MedalActivationSpec(
            trigger, ConditionId.Null, counter, priority, timing));

    public static MedalRuleEffect Apply(EffectId effect, int amount, MedalEffectTarget target = MedalEffectTarget.Owner) =>
        new(MedalRuleEffectKind.ApplyEffect, target, effect, StatId.Null, MetaResourceId.Null, amount);

    public static MedalRuleEffect Modify(StatId stat, int amount) =>
        new(MedalRuleEffectKind.ModifyStat, MedalEffectTarget.Owner, EffectId.Null, stat, MetaResourceId.Null, amount);

    public static MedalRuleEffect Resurrect(int amount) =>
        new(MedalRuleEffectKind.ResurrectRandom, MedalEffectTarget.Owner, EffectId.Null, StatId.Null, MetaResourceId.Null, amount);

    public static MedalRuleEffect MaxHealth(int amount) =>
        new(MedalRuleEffectKind.ModifyMaxHealth, MedalEffectTarget.Owner, EffectId.Null, StatId.Null, MetaResourceId.Null, amount);

    public static MedalRuleEffect Freeze(int amount) =>
        new(MedalRuleEffectKind.FreezeRandomCards, MedalEffectTarget.Owner, RuleEffectIds.CardFrozen, StatId.Null, MetaResourceId.Null, amount);

    public static MedalRuleEffect SpawnKunai(int amount) =>
        new(MedalRuleEffectKind.SpawnKunai, MedalEffectTarget.Owner, EffectId.Null, StatId.Null, MetaResourceId.Null, amount);

    public static MedalRuleEffect Meta(MetaResourceId resource, int amount) =>
        new(MedalRuleEffectKind.ModifyMetaResource, MedalEffectTarget.Owner, EffectId.Null, StatId.Null, resource, amount);

    public static MedalRuleEffect Damage(int amount) =>
        new(MedalRuleEffectKind.Damage, MedalEffectTarget.PrimaryEnemy, EffectId.Null, StatId.Null, MetaResourceId.Null, amount);

    public static MedalRuleEffect MaxHand(int amount) =>
        new(MedalRuleEffectKind.ModifyMaxHandSize, MedalEffectTarget.Owner, EffectId.Null, RuleStatIds.MaxHandSize, MetaResourceId.Null, amount);

    public static MedalRuleEffect Mill(int amount) =>
        new(MedalRuleEffectKind.MillTopCard, MedalEffectTarget.Owner, EffectId.Null, StatId.Null, MetaResourceId.Null, amount);

    public static MedalRuleEffect ShuffleDiscard(int amount) =>
        new(MedalRuleEffectKind.ShuffleRandomDiscardToDraw, MedalEffectTarget.Owner, EffectId.Null, StatId.Null, MetaResourceId.Null, amount);

    public static MedalProviderDefinition Provider(MedalProviderKind kind, int amount) =>
        new(kind, amount, ProviderLifetime.WhileEquipped);
}
