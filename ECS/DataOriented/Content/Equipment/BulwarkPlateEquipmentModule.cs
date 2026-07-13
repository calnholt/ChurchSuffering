#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.BulwarkPlate, Handler = nameof(BuildCommands))]
public static partial class BulwarkPlateEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.BulwarkPlate, "bulwark_plate", "Bulwark Plate", "Gain 2 aegis.", "",
        EquipmentSlotId.Chest, RuleCardColor.White, 0,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Apply(RuleEffectIds.Aegis, 2), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
