#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Content.Cards;

public enum CardDefinitionType : byte
{
    Attack,
    Prayer,
    Block,
    Relic,
}

public enum CardDefinitionRarity : byte
{
    Starter,
    Common,
    Uncommon,
    Rare,
}

public enum CardDefinitionTarget : byte
{
    None,
    Player,
    Enemy,
}

public enum CardCostColor : byte
{
    Red,
    White,
    Black,
    Any,
}

[Flags]
public enum CardPrintedColors : byte
{
    None = 0,
    Red = 1 << 0,
    White = 1 << 1,
    Black = 1 << 2,
    All = Red | White | Black,
}

[Flags]
public enum CardDefinitionFlags : ushort
{
    None = 0,
    FreeAction = 1 << 0,
    ExhaustsOnEndTurn = 1 << 1,
    CanAddToLoadout = 1 << 2,
    Token = 1 << 3,
    Weapon = 1 << 4,
    Starter = 1 << 5,
}

[Flags]
public enum CardHookStages : ushort
{
    None = 0,
    Validate = 1 << 0,
    ResolvePlay = 1 << 1,
    ResolveBlock = 1 << 2,
    DiscardedForCost = 1 << 3,
    Pledged = 1 << 4,
    ConditionalDamage = 1 << 5,
    Reactive = 1 << 6,
    Lifecycle = 1 << 7,
}

public sealed class CardCost
{
    private readonly CardCostColor[] colors;
    private readonly IReadOnlyList<CardCostColor> view;

    public CardCost(params CardCostColor[] colors)
    {
        this.colors = colors is null ? [] : (CardCostColor[])colors.Clone();
        view = Array.AsReadOnly(this.colors);
    }

    public static CardCost Free { get; } = new();

    public IReadOnlyList<CardCostColor> Colors => view;

    public int Count => colors.Length;

    public CardCostColor this[int index] => colors[index];

    public ReadOnlySpan<CardCostColor> AsSpan() => colors;
}

public sealed record CardDefinitionData(
    CardId Id,
    string Key,
    string Name,
    string Text,
    int Damage,
    int Block,
    CardCost Cost,
    CardDefinitionType Type,
    CardDefinitionTarget Target,
    CardDefinitionRarity Rarity,
    CardPrintedColors PrintedColors,
    CardDefinitionFlags Flags,
    int MultiHitCount,
    int FirstHitDelayMilliseconds,
    int HitIntervalMilliseconds,
    VisualEffectRecipeId VisualEffect,
    CardHookStages Hooks);

public sealed record CardUpgradeDelta(
    int DamageDelta = 0,
    int BlockDelta = 0,
    CardCost? ReplacementCost = null,
    CardDefinitionType? ReplacementType = null,
    CardDefinitionTarget? ReplacementTarget = null,
    string? TextOverride = null,
    CardDefinitionFlags AddFlags = CardDefinitionFlags.None,
    CardDefinitionFlags RemoveFlags = CardDefinitionFlags.None,
    int? ReplacementMultiHitCount = null,
    int? ReplacementFirstHitDelayMilliseconds = null,
    int? ReplacementHitIntervalMilliseconds = null)
{
    public static CardUpgradeDelta None { get; } = new();

    public CardDefinitionData Apply(CardDefinitionData definition) => definition with
    {
        Damage = definition.Damage + DamageDelta,
        Block = definition.Block + BlockDelta,
        Cost = ReplacementCost ?? definition.Cost,
        Type = ReplacementType ?? definition.Type,
        Target = ReplacementTarget ?? definition.Target,
        Text = TextOverride ?? definition.Text,
        Flags = (definition.Flags | AddFlags) & ~RemoveFlags,
        MultiHitCount = ReplacementMultiHitCount ?? definition.MultiHitCount,
        FirstHitDelayMilliseconds = ReplacementFirstHitDelayMilliseconds ?? definition.FirstHitDelayMilliseconds,
        HitIntervalMilliseconds = ReplacementHitIntervalMilliseconds ?? definition.HitIntervalMilliseconds,
    };
}

internal static class CardDefinitionAuthoring
{
    public static CardCost Cost(params CardCostColor[] colors) =>
        colors.Length == 0 ? CardCost.Free : new CardCost(colors);

    public static CardDefinitionFlags Flags(
        bool free,
        bool exhaust,
        bool loadout,
        bool token,
        bool weapon,
        bool starter)
    {
        CardDefinitionFlags result = CardDefinitionFlags.None;
        if (free) result |= CardDefinitionFlags.FreeAction;
        if (exhaust) result |= CardDefinitionFlags.ExhaustsOnEndTurn;
        if (loadout) result |= CardDefinitionFlags.CanAddToLoadout;
        if (token) result |= CardDefinitionFlags.Token;
        if (weapon) result |= CardDefinitionFlags.Weapon;
        if (starter) result |= CardDefinitionFlags.Starter;
        return result;
    }

    public static VisualEffectRecipeId Visual(string legacyKey) => legacyKey switch
    {
        "player_attack" => RuleVisualEffectRecipeIds.PlayerAttack,
        "player_buff" => RuleVisualEffectRecipeIds.PlayerBuff,
        "light_slash" => RuleVisualEffectRecipeIds.LightSlash,
        "heavy_hammer" => RuleVisualEffectRecipeIds.HeavyHammer,
        "holy_strike" => RuleVisualEffectRecipeIds.HolyStrike,
        "holy_support" => RuleVisualEffectRecipeIds.HolySupport,
        "defensive_guard" => RuleVisualEffectRecipeIds.DefensiveGuard,
        _ => RuleVisualEffectRecipeIds.None,
    };

    public static ConditionSpec Condition(
        ConditionId id,
        TargetHandle subject,
        StatId stat = default,
        int threshold = 0,
        ComparisonOperator comparison = ComparisonOperator.Always) =>
        new(id, comparison, subject, stat, threshold, RuleValueFlags.None);

    public static EffectSpec Effect(
        EffectId id,
        int magnitude,
        ConditionSpec condition = default,
        RuleValueFlags flags = RuleValueFlags.None) =>
        new(id, magnitude, Duration: 0, condition, flags);

}
