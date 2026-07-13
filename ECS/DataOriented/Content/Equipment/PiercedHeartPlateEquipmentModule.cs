#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.PiercedHeartPlate, Handler = nameof(BuildCommands))]
public static partial class PiercedHeartPlateEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.PiercedHeartPlate, "pierced_heart_plate", "Pierced Heart Plate", "Gain 2 courage. Gain 1 bleed.", "",
        EquipmentSlotId.Chest, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Modify(RuleStatIds.Courage, 2), EquipmentDefinitionParts.Apply(RuleEffectIds.Bleed, 1));

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
