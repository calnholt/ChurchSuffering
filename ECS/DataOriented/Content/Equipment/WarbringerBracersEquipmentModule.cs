#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.WarbringerBracers, Handler = nameof(BuildCommands))]
public static partial class WarbringerBracersEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.WarbringerBracers, "warbringer_bracers", "Warbringer Bracers", "Gain 1 might.", "",
        EquipmentSlotId.Arms, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Apply(RuleEffectIds.Might, 1), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
