#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Cards;

[CardDefinition(CardId.Absolution, Handler = nameof(Handle))]
public static partial class AbsolutionCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Absolution, "absolution", "Absolution", "",
        Damage: 10, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Starter,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.Pledged);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "When this is pledged, gain 2 courage.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Absolution(ref context);
}

[CardDefinition(CardId.ArkOfTheCovenant, Handler = nameof(Handle))]
public static partial class ArkOfTheCovenantCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.ArkOfTheCovenant, "ark_of_the_covenant", "Ark of the Covenant", "When this card is discarded to pay for a card cost, heal 2 HP.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Block, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.DiscardedForCost);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "When this card is discarded to pay for a card cost, heal 3 HP.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.ArkOfTheCovenant(ref context);
}

[CardDefinition(CardId.BatteringBlow, Handler = nameof(Handle))]
public static partial class BatteringBlowCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.BatteringBlow, "battering_blow", "Battering Blow", "If no cards were discarded to play this, gain 3 courage.",
        Damage: 6, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.White),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(AddFlags: CardDefinitionFlags.FreeAction);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.BatteringBlow(ref context);
}

[CardDefinition(CardId.BattleScars, Handler = nameof(Handle))]
public static partial class BattleScarsCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.BattleScars, "battle_scars", "Battle Scars", "If you have 2 or more scars, gain 2 vigor.",
        Damage: 7, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "If you have 2 or more scars, gain 3 vigor.", AddFlags: CardDefinitionFlags.FreeAction);
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Vigor, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.BattleScars(ref context);
}

[CardDefinition(CardId.BloodPrice, Handler = nameof(Handle))]
public static partial class BloodPriceCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.BloodPrice, "blood_price", "Blood Price", "Deals damage equal to twice the number of scars you have (max 10).",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain 1 scar. Deals damage equal to twice the number of scars you have (max 10).");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Scar, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.BloodPrice(ref context);
}

[CardDefinition(CardId.Burn, Handler = nameof(Handle))]
public static partial class BurnCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Burn, "burn", "Burn", "If the enemy has burn, the enemy gains 2 burn, otherwise the enemy gains 1 burn.",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Apply 2 burn to the enemy. If this is scorched, apply 3 burn instead.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Burn, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Burn(ref context);
}

[CardDefinition(CardId.CarpeDiem, Handler = nameof(Handle))]
public static partial class CarpeDiemCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.CarpeDiem, "carpe_diem", "Carpe Diem", "Gain 5 courage. At the end of the turn, lose all courage.",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain 5 courage and 1 might. At the end of the turn, lose all courage.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.CarpeDiem, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.CarpeDiem(ref context);
}

[CardDefinition(CardId.Colorless3Block, Handler = nameof(Handle))]
public static partial class Colorless3BlockCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Colorless3Block, "colorless_3_block", "Protect", "",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Block, CardDefinitionTarget.None, CardDefinitionRarity.Common,
        CardPrintedColors.None, CardDefinitionAuthoring.Flags(false, false, false, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual(""), CardHookStages.None);
    public static CardUpgradeDelta Upgrade => CardUpgradeDelta.None;
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Colorless3Block(ref context);
}

[CardDefinition(CardId.Consecrate, Handler = nameof(Handle))]
public static partial class ConsecrateCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Consecrate, "consecrate", "Consecrate", "If this card is pledged when played, it gains +2 damage and gain 1 courage.",
        Damage: 6, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Black),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Any));
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Might, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Consecrate(ref context);
}

[CardDefinition(CardId.Courageous, Handler = nameof(Handle))]
public static partial class CourageousCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Courageous, "courageous", "Courageous", "Gain 3 courage. End your turn.",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Starter,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain 4 courage. End your turn.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Courageous(ref context);
}

[CardDefinition(CardId.CrimsonRite, Handler = nameof(Handle))]
public static partial class CrimsonRiteCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.CrimsonRite, "crimson_rite", "Crimson Rite", "Heal X HP where X is the damage dealt from this attack.",
        Damage: 3, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain X aegis where X is the damage dealt from this attack.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aegis, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.CrimsonRite(ref context);
}

[CardDefinition(CardId.Crusade, Handler = nameof(Handle))]
public static partial class CrusadeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Crusade, "crusade", "Crusade", "If this card is pledged when played, gain 1 action point and 2 might.",
        Damage: 5, Block: 2, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: -2, ReplacementCost: CardDefinitionAuthoring.Cost(), TextOverride: "If this card is pledged when played, gain 1AP and 2 might.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Might, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Crusade(ref context);
}

[CardDefinition(CardId.Curse, Handler = nameof(Handle))]
public static partial class CurseCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Curse, "curse", "Curse", "Remove the curse from this card.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.None, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, false, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual(""), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => CardUpgradeDelta.None;
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Cursed, -1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Curse(ref context);
}

[CardDefinition(CardId.Dagger, Handler = nameof(Handle))]
public static partial class DaggerCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Dagger, "dagger", "Dagger", "As an additional cost, lose 2 courage.",
        Damage: 2, Block: 0, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, true, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => CardUpgradeDelta.None;
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Dagger(ref context);
}

[CardDefinition(CardId.DeusVult, Handler = nameof(Handle))]
public static partial class DeusVultCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.DeusVult, "deus_vult", "Deus Vult", "You can't play this if you have not used your weapon this turn. Gain 1 courage. This gains +X damage, where X is your courage",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.None, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage | CardHookStages.Reactive | CardHookStages.Lifecycle);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain 1 courage. This gains +X damage, where X is your courage");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.DeusVult(ref context);
}

[CardDefinition(CardId.DivineProtection, Handler = nameof(Handle))]
public static partial class DivineProtectionCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.DivineProtection, "divine_protection", "Divine Protection", "Gain 4 aegis.",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("defensive_guard"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain 5 aegis.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aegis, 4)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.DivineProtection(ref context);
}

[CardDefinition(CardId.DowseWithHolyWater, Handler = nameof(Handle))]
public static partial class DowseWithHolyWaterCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.DowseWithHolyWater, "dowse_with_holy_water", "Douse with Holy Water", "If you have 5+ courage, gain 3 might.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("holy_support"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "If you have 5+ courage, gain 4 might.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Might, 3)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.DowseWithHolyWater(ref context);
}

[CardDefinition(CardId.EmberHarvest, Handler = nameof(Handle))]
public static partial class EmberHarvestCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.EmberHarvest, "ember_harvest", "Ember Harvest", "If a scorched card was discarded to play this, gain 2 might.",
        Damage: 7, Block: 2, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 1, BlockDelta: 1, TextOverride: "If a scorched card was discarded to play this, gain 3 might.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Might, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.EmberHarvest(ref context);
}

[CardDefinition(CardId.Exaltation, Handler = nameof(Handle))]
public static partial class ExaltationCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Exaltation, "exaltation", "Exaltation", "As an additional cost, lose 3 courage.",
        Damage: 7, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Uncommon,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "As an additional cost, lose 2 courage.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Exaltation(ref context);
}

[CardDefinition(CardId.Excavate, Handler = nameof(Handle))]
public static partial class ExcavateCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Excavate, "excavate", "Excavate", "If you have milled 2 or more cards this battle, this attack gains +3 damage.",
        Damage: 9, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Black, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any), TextOverride: "If you have milled 2 or more cards this battle, this attack gains +5 damage.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Excavate(ref context);
}

[CardDefinition(CardId.Fervor, Handler = nameof(Handle))]
public static partial class FervorCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Fervor, "fervor", "Fervor", "If you have 5+ courage, this attack gains +3 damage.",
        Damage: 6, Block: 2, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Red),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(BlockDelta: 1, ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Any));
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Fervor(ref context);
}

[CardDefinition(CardId.ForgeStrike, Handler = nameof(Handle))]
public static partial class ForgeStrikeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.ForgeStrike, "forge_strike", "Forge Strike", "",
        Damage: 7, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Starter,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("heavy_hammer"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain 2 might.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Might, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.ForgeStrike(ref context);
}

[CardDefinition(CardId.Fury, Handler = nameof(Handle))]
public static partial class FuryCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Fury, "fury", "Fury", "Gain 1 aggression, then double your aggression.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Lose all courage. Gain X aggression, where X is the number of courage you lost, then double your aggression.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aggression, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Fury(ref context);
}

[CardDefinition(CardId.Graveward, Handler = nameof(Handle))]
public static partial class GravewardCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Graveward, "graveward", "Graveward", "Whenever you mill a card while this card is in your hand, gain 2 aegis.",
        Damage: 6, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.Reactive | CardHookStages.Lifecycle);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Whenever you mill a card while this card is in your hand, gain 4 aegis.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aegis, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Graveward(ref context);
}

[CardDefinition(CardId.HoldTheLine, Handler = nameof(Handle))]
public static partial class HoldTheLineCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.HoldTheLine, "hold_the_line", "Hold the Line", "Gain 1 courage.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Block, CardDefinitionTarget.None, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual(""), CardHookStages.ResolveBlock);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(BlockDelta: 1);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.HoldTheLine(ref context);
}

[CardDefinition(CardId.Hammer, Handler = nameof(Handle))]
public static partial class HammerCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Hammer, "hammer", "Hammer", "Gain 1 vigor.",
        Damage: 3, Block: 0, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Black, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, true, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("heavy_hammer"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => CardUpgradeDelta.None;
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Vigor, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Hammer(ref context);
}

[CardDefinition(CardId.HiddenKunai, Handler = nameof(Handle))]
public static partial class HiddenKunaiCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.HiddenKunai, "hidden_kunai", "Hidden Kunai", "Add 1 Kunai to your hand.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Block, CardDefinitionTarget.None, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual(""), CardHookStages.ResolveBlock);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Add 1 Kunai+ to your hand.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.HiddenKunai(ref context);
}

[CardDefinition(CardId.Impale, Handler = nameof(Handle))]
public static partial class ImpaleCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Impale, "impale", "Impale", "As an additional cost, lose 3 courage.",
        Damage: 6, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 1);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Impale(ref context);
}

[CardDefinition(CardId.IncreaseFaith, Handler = nameof(Handle))]
public static partial class IncreaseFaithCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.IncreaseFaith, "increase_faith", "Increase Faith", "Gain 1 power.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Uncommon,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay | CardHookStages.Pledged);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "When this is pledged, gain 2 aegis.\n\nGain 1 power.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Power, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.IncreaseFaith(ref context);
}

[CardDefinition(CardId.IronCovenant, Handler = nameof(Handle))]
public static partial class IronCovenantCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.IronCovenant, "iron_covenant", "Iron Covenant", "When this is pledged, gain 1 vigor.",
        Damage: 15, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Red, CardCostColor.Black, CardCostColor.Any, CardCostColor.Any, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.Pledged);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 6, ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Red, CardCostColor.Black, CardCostColor.Any, CardCostColor.Any, CardCostColor.Any, CardCostColor.Any));
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Vigor, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.IronCovenant(ref context);
}

[CardDefinition(CardId.Kunai, Handler = nameof(Handle))]
public static partial class KunaiCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Kunai, "kunai", "Kunai", "Wounds the enemy if you have dealt attack damage 4 times this action phase. Exhaust on play or at the end of your turn",
        Damage: 1, Block: 0, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, true, false, true, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Wounds the enemy if you have dealt attack damage 3 times this action phase. Exhaust on play or at the end of your turn");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Wounded, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Kunai(ref context);
}

[CardDefinition(CardId.Lacerate, Handler = nameof(Handle))]
public static partial class LacerateCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Lacerate, "lacerate", "Lacerate", "If this attack deals 7 or more damage, the enemy gains 1 wounded.",
        Damage: 4, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(BlockDelta: 1);
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Wounded, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Lacerate(ref context);
}

[CardDefinition(CardId.LitanyOfWrath, Handler = nameof(Handle))]
public static partial class LitanyOfWrathCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.LitanyOfWrath, "litany_of_wrath", "Litany of Wrath", "Gain 3 aggression.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Starter,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.White), TextOverride: "Gain 8 aggression.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aggression, 3)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.LitanyOfWrath(ref context);
}

[CardDefinition(CardId.Mantlet, Handler = nameof(Handle))]
public static partial class MantletCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Mantlet, "mantlet", "Mantlet", "",
        Damage: 0, Block: 4, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Block, CardDefinitionTarget.None, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual(""), CardHookStages.DiscardedForCost);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "When this card is discarded to pay for a card cost, gain 1 aegis.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aegis, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Mantlet(ref context);
}

[CardDefinition(CardId.MaleficRite, Handler = nameof(Handle))]
public static partial class MaleficRiteCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.MaleficRite, "malefic_rite", "Malefic Rite", "Gain 4 + X aggression, where X is twice the number of curses you have removed this climb.\n\nA random card in your deck becomes cursed. (You have removed 0 curses)",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay | CardHookStages.Reactive | CardHookStages.Lifecycle);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(BlockDelta: 1, TextOverride: "Gain 4 + X aggression, where X is thrice the number of curses you have removed this climb.\n\nA random card in your deck becomes cursed. (You have removed 0 curses)");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aggression, 4)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.MaleficRite(ref context);
}

[CardDefinition(CardId.QuickWit, Handler = nameof(Handle))]
public static partial class QuickWitCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.QuickWit, "quick_wit", "Quick Wit", "As an additional cost, lose 1 courage. Resurrect 1.",
        Damage: 2, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "As an additional cost, lose 2 courage. Resurrect 2.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.QuickWit(ref context);
}

[CardDefinition(CardId.RallyTheFaithful, Handler = nameof(Handle))]
public static partial class RallyTheFaithfulCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.RallyTheFaithful, "rally_the_faithful", "Rally the Faithful", "Gain 1 might.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Gain 1 might and 1 courage.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Might, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.RallyTheFaithful(ref context);
}

[CardDefinition(CardId.RelentlessStrike, Handler = nameof(Handle))]
public static partial class RelentlessStrikeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.RelentlessStrike, "relentless_strike", "Relentless Strike", "The first time you play this each battle, it goes to the bottom of your deck. It gains +4 damage for the rest of the battle.",
        Damage: 9, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.White, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.Lifecycle);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "The first time you play this each battle, it goes to the bottom of your deck. It gains +8 damage for the rest of the battle.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.RelentlessStrike(ref context);
}

[CardDefinition(CardId.PierceThrough, Handler = nameof(Handle))]
public static partial class PierceThroughCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.PierceThrough, "pierce_through", "Pierce Through", "Remove all guard from the enemy.",
        Damage: 8, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Remove all guard and armor from the enemy.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Guard, -1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.PierceThrough(ref context);
}

[CardDefinition(CardId.PouchOfKunai, Handler = nameof(Handle))]
public static partial class PouchOfKunaiCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.PouchOfKunai, "pouch_of_kunai", "Pouch of Kunai", "Put 2 to 4 Kunai cards in your hand.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Black),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Put 3 to 4 Kunai+ cards in your hand.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.PouchOfKunai(ref context);
}

[CardDefinition(CardId.Purge, Handler = nameof(Handle))]
public static partial class PurgeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Purge, "purge", "Purge", "While this card is in your hand, when you pledge a card, remove all location modifiers from that card.",
        Damage: 3, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.Reactive | CardHookStages.Lifecycle);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "While this card is in your hand, when you pledge a card, remove all location modifiers from that card.\n\nGain 1 might for each modification removed.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Purge(ref context);
}

[CardDefinition(CardId.Ravage, Handler = nameof(Handle))]
public static partial class RavageCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Ravage, "ravage", "Ravage", "As an additional cost, mill 1 cards.",
        Damage: 8, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 3, TextOverride: "As an additional cost, mill 4 cards.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Ravage(ref context);
}

[CardDefinition(CardId.RazorStorm, Handler = nameof(Handle))]
public static partial class RazorStormCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.RazorStorm, "razor_storm", "Razor Storm", "Attacks 2 times.",
        Damage: 1, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Uncommon,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 2, FirstHitDelayMilliseconds: 500, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Attacks 3 times.", ReplacementMultiHitCount: 3);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.RazorStorm(ref context);
}

[CardDefinition(CardId.Reckoning, Handler = nameof(Handle))]
public static partial class ReckoningCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Reckoning, "reckoning", "Reckoning", "",
        Damage: 8, Block: 2, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Starter,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 1, BlockDelta: 1, ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Red, CardCostColor.Any));
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Reckoning(ref context);
}

[CardDefinition(CardId.Reap, Handler = nameof(Handle))]
public static partial class ReapCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Reap, "reap", "Reap", "If two red cards are discarded to play this, this gains +2 damage.",
        Damage: 8, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "If two red cards are discarded to play this, this gains +2 damage and gain 2 courage.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Reap(ref context);
}

[CardDefinition(CardId.RenounceAndHone, Handler = nameof(Handle))]
public static partial class RenounceAndHoneCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.RenounceAndHone, "renounce_and_hone", "Renounce and Hone", "As an additional cost, discard your pledged card that was not pledged this turn. Gain 2 vigor and 2 courage.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "As an additional cost, discard your pledged card that was not pledged this turn. Gain 2 vigor and 4 courage.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Vigor, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.RenounceAndHone(ref context);
}

[CardDefinition(CardId.Sacrifice, Handler = nameof(Handle))]
public static partial class SacrificeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Sacrifice, "sacrifice", "Sacrifice", "Gain 1 scar, 1 temperance, and resurrect 2.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => CardUpgradeDelta.None;
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Scar, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Sacrifice(ref context);
}

[CardDefinition(CardId.SerpentCrush, Handler = nameof(Handle))]
public static partial class SerpentCrushCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.SerpentCrush, "serpent_crush", "Serpent Crush", "As an additional cost, lose 2 courage. Gain 1 action point and resurrect 1.",
        Damage: 3, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "As an additional cost, lose 1 courage. Gain 1 action point and resurrect 1.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.SerpentCrush(ref context);
}

[CardDefinition(CardId.Seize, Handler = nameof(Handle))]
public static partial class SeizeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Seize, "seize", "Seize", "If you have lost courage during this action phase, this gains +2 damage.",
        Damage: 2, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(AddFlags: CardDefinitionFlags.FreeAction);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Seize(ref context);
}

[CardDefinition(CardId.ShieldOfFaith, Handler = nameof(Handle))]
public static partial class ShieldOfFaithCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.ShieldOfFaith, "shield_of_faith", "Shield of Faith", "Gain 8 aegis.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.White), TextOverride: "Gain 12 aegis.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aegis, 8)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.ShieldOfFaith(ref context);
}

[CardDefinition(CardId.Smite, Handler = nameof(Handle))]
public static partial class SmiteCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Smite, "smite", "Smite", "",
        Damage: 3, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Starter,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("holy_strike"), CardHookStages.ResolvePlay | CardHookStages.Pledged);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "When this is pledged, gain 1 temperance.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Smite(ref context);
}

[CardDefinition(CardId.Stab, Handler = nameof(Handle))]
public static partial class StabCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Stab, "stab", "Stab", "As an additional cost, lose 2 courage.",
        Damage: 5, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "As an additional cost, lose 1 courage.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Stab(ref context);
}

[CardDefinition(CardId.SteadfastResolve, Handler = nameof(Handle))]
public static partial class SteadfastResolveCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.SteadfastResolve, "steadfast_resolve", "Steadfast Resolve", "Gain 1 vigor.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Any, CardCostColor.Any), TextOverride: "Gain 4 vigor.", AddFlags: CardDefinitionFlags.FreeAction);
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Vigor, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.SteadfastResolve(ref context);
}

[CardDefinition(CardId.Stalwart, Handler = nameof(Handle))]
public static partial class StalwartCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Stalwart, "stalwart", "Stalwart", "As an additional cost, lose 1 courage.",
        Damage: 0, Block: 7, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Block, CardDefinitionTarget.None, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual(""), CardHookStages.Validate | CardHookStages.ResolveBlock);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Stalwart(ref context);
}

[CardDefinition(CardId.SteelTheSpirit, Handler = nameof(Handle))]
public static partial class SteelTheSpiritCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.SteelTheSpirit, "steel_the_spirit", "Steel the Spirit", "As an additional cost, lose 3 courage. Gain 2 vigor.",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "As an additional cost, lose 2 courage. Gain 2 vigor.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Vigor, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.SteelTheSpirit(ref context);
}

[CardDefinition(CardId.StokedAssault, Handler = nameof(Handle))]
public static partial class StokedAssaultCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.StokedAssault, "stoked_assault", "Stoked Assault", "You can't play this if you don't have 2 vigor.",
        Damage: 4, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.Validate | CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 1);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.StokedAssault(ref context);
}

[CardDefinition(CardId.Strike, Handler = nameof(Handle))]
public static partial class StrikeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Strike, "strike", "Strike", "50% chance to gain 2 courage.",
        Damage: 3, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("light_slash"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(AddFlags: CardDefinitionFlags.FreeAction);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Strike(ref context);
}

[CardDefinition(CardId.SuddenThrust, Handler = nameof(Handle))]
public static partial class SuddenThrustCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.SuddenThrust, "sudden_thrust", "Sudden Thrust", "Gain 1 courage.",
        Damage: 2, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 1);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.SuddenThrust(ref context);
}

[CardDefinition(CardId.StokeTheFurnace, Handler = nameof(Handle))]
public static partial class StokeTheFurnaceCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.StokeTheFurnace, "stoke_the_furnace", "Stoke the Furnace", "Lose 2 courage, gain 1 vigor. Repeat up to 3 times if possible.",
        Damage: 2, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(AddFlags: CardDefinitionFlags.FreeAction);
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Vigor, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.StokeTheFurnace(ref context);
}

[CardDefinition(CardId.Sword, Handler = nameof(Handle))]
public static partial class SwordCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Sword, "sword", "Sword", "Gain 1 courage.",
        Damage: 5, Block: 0, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Black, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, true, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("light_slash"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => CardUpgradeDelta.None;
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Sword(ref context);
}

[CardDefinition(CardId.SwordIntoShield, Handler = nameof(Handle))]
public static partial class SwordIntoShieldCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.SwordIntoShield, "sword_into_shield", "Sword Into Shield", "The next non-weapon attack card you play this turn gains +1 damage this climb, then this becomes a textless block card.",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => CardUpgradeDelta.None;
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.SwordIntoShield, 1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.SwordIntoShield(ref context);
}

[CardDefinition(CardId.TemperTheBlade, Handler = nameof(Handle))]
public static partial class TemperTheBladeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.TemperTheBlade, "temper_the_blade", "Temper the Blade", "Gain sharpen 4.",
        Damage: 0, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(BlockDelta: 1);
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Sharpen, 4)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.TemperTheBlade(ref context);
}

[CardDefinition(CardId.Tempest, Handler = nameof(Handle))]
public static partial class TempestCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Tempest, "tempest", "Tempest", "Gain 5 temperance.",
        Damage: 2, Block: 2, Cost: CardDefinitionAuthoring.Cost(CardCostColor.White),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Any));
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Tempest(ref context);
}

[CardDefinition(CardId.Thaw, Handler = nameof(Handle))]
public static partial class ThawCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Thaw, "thaw", "Thaw", "Lose all frostbite, then gain X temperance where X is the amount of frostbite lost.",
        Damage: 3, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(TextOverride: "Lose all frostbite, then gain X temperance and X courage where X is the amount of frostbite lost.");
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Frostbite, -1)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Thaw(ref context);
}

[CardDefinition(CardId.UnburdenedStrike, Handler = nameof(Handle))]
public static partial class UnburdenedStrikeCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.UnburdenedStrike, "unburdened_strike", "Unburdened Strike", "If no cards were discarded to play this, this gains +3 damage.",
        Damage: 8, Block: 2, Cost: CardDefinitionAuthoring.Cost(CardCostColor.White, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Uncommon,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(BlockDelta: 1, TextOverride: "If no cards were discarded to play this, this gains +4 damage.");
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.UnburdenedStrike(ref context);
}

[CardDefinition(CardId.VanguardsPromise, Handler = nameof(Handle))]
public static partial class VanguardsPromiseCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.VanguardsPromise, "vanguards_promise", "Vanguard's Promise", "If you have no pledged card, pledge a random card from your discard pile.",
        Damage: 2, Block: 2, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 1);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.VanguardsPromise(ref context);
}

[CardDefinition(CardId.Vindicate, Handler = nameof(Handle))]
public static partial class VindicateCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Vindicate, "vindicate", "Vindicate", "If you have 5 or more courage, this attack gains +7 damage and lose all courage.",
        Damage: 8, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Red, CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay | CardHookStages.ConditionalDamage);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 3);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    private static readonly ConditionSpec[] s_conditions = [CardDefinitionAuthoring.Condition(RuleConditionIds.StatThreshold, TargetHandle.Player)];
    public static ReadOnlySpan<ConditionSpec> Conditions => s_conditions;
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Vindicate(ref context);
}

[CardDefinition(CardId.Whirlwind, Handler = nameof(Handle))]
public static partial class WhirlwindCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.Whirlwind, "whirlwind", "Whirlwind", "Attacks 2 times.",
        Damage: 3, Block: 3, Cost: CardDefinitionAuthoring.Cost(CardCostColor.Any),
        CardDefinitionType.Attack, CardDefinitionTarget.Enemy, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(false, false, true, false, false, false),
        MultiHitCount: 2, FirstHitDelayMilliseconds: 500, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_attack"), CardHookStages.ResolvePlay);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(ReplacementCost: CardDefinitionAuthoring.Cost(CardCostColor.Red), TextOverride: "Attacks 3 times.", ReplacementMultiHitCount: 3);
    public static ReadOnlySpan<EffectSpec> Effects => [];
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.Whirlwind(ref context);
}

[CardDefinition(CardId.ZealousVow, Handler = nameof(Handle))]
public static partial class ZealousVowCardDefinition
{
    public static CardDefinitionData Definition => new(
        CardId.ZealousVow, "zealous_vow", "Zealous Vow", "Gain 2 aggression.\n\nWhen this is pledged, gain 2 sharpen. ",
        Damage: 0, Block: 3, Cost: CardDefinitionAuthoring.Cost(),
        CardDefinitionType.Prayer, CardDefinitionTarget.Player, CardDefinitionRarity.Common,
        CardPrintedColors.All, CardDefinitionAuthoring.Flags(true, false, true, false, false, false),
        MultiHitCount: 1, FirstHitDelayMilliseconds: 0, HitIntervalMilliseconds: 500,
        CardDefinitionAuthoring.Visual("player_buff"), CardHookStages.ResolvePlay | CardHookStages.Pledged);
    public static CardUpgradeDelta Upgrade => new CardUpgradeDelta(DamageDelta: 1, ReplacementType: CardDefinitionType.Attack);
    private static readonly EffectSpec[] s_effects = [CardDefinitionAuthoring.Effect(RuleEffectIds.Aggression, 2)];
    public static ReadOnlySpan<EffectSpec> Effects => s_effects;
    public static ReadOnlySpan<ConditionSpec> Conditions => [];
    public static void Handle(ref CardHandlerContext context) => CardBehaviorHandlers.ZealousVow(ref context);
}
