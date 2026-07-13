#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

public enum EquipmentSlotId : byte
{
    Head,
    Chest,
    Arms,
    Legs,
}

public enum EquipmentRuleEffectKind : byte
{
    None,
    ApplyEffect,
    ModifyStat,
    ResurrectRandom,
    SpawnKunai,
    RemovePriorTurnPledge,
    RemoveEffect,
}

public readonly record struct EquipmentRuleEffect(
    EquipmentRuleEffectKind Kind,
    EffectId Effect,
    StatId Stat,
    int Amount);

public readonly record struct EquipmentDefinition(
    EquipmentId Id,
    string LegacyId,
    string Name,
    string Text,
    string FlavorText,
    EquipmentSlotId Slot,
    RuleCardColor Color,
    int Block,
    EquipmentActivationSpec Activation,
    VisualEffectRecipeId VisualRecipe,
    EquipmentRuleEffect Effect1,
    EquipmentRuleEffect Effect2)
{
    public int EffectCount => Effect2.Kind != EquipmentRuleEffectKind.None
        ? 2
        : Effect1.Kind != EquipmentRuleEffectKind.None ? 1 : 0;

    public EquipmentRuleEffect GetEffect(int index) => index switch
    {
        0 when EffectCount > 0 => Effect1,
        1 when EffectCount > 1 => Effect2,
        _ => throw new System.ArgumentOutOfRangeException(nameof(index)),
    };
}

public static class EquipmentDefinitionParts
{
    public static EquipmentActivationSpec Inert => default;

    public static EquipmentActivationSpec ActionActivation(ConditionId availability = default) => new(
        RulePhaseMask.Action,
        RuleTriggerIds.EquipmentActivated,
        availability,
        MaxUses: 1,
        RuleResetPolicy.StartBattle,
        EquipmentUsageLifetime.Battle);

    public static EquipmentRuleEffect Apply(EffectId effect, int amount) =>
        new(EquipmentRuleEffectKind.ApplyEffect, effect, StatId.Null, amount);

    public static EquipmentRuleEffect Modify(StatId stat, int amount) =>
        new(EquipmentRuleEffectKind.ModifyStat, EffectId.Null, stat, amount);

    public static EquipmentRuleEffect Resurrect(int amount) =>
        new(EquipmentRuleEffectKind.ResurrectRandom, EffectId.Null, StatId.Null, amount);

    public static EquipmentRuleEffect SpawnKunai(int amount) =>
        new(EquipmentRuleEffectKind.SpawnKunai, EffectId.Null, StatId.Null, amount);

    public static EquipmentRuleEffect RemovePledge() =>
        new(EquipmentRuleEffectKind.RemovePriorTurnPledge, RuleEffectIds.Pledged, StatId.Null, 1);

    public static EquipmentRuleEffect Remove(EffectId effect) =>
        new(EquipmentRuleEffectKind.RemoveEffect, effect, StatId.Null, 0);
}
