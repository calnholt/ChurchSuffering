#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.FleetfootGreaves, Handler = nameof(BuildCommands))]
public static partial class FleetfootGreavesEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.FleetfootGreaves, "fleetfoot_greaves", "Fleetfoot Greaves", "Gain 1 action point.", "",
        EquipmentSlotId.Legs, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Modify(RuleStatIds.ActionPoints, 1), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
