#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.WhetstoneGauntlets, Handler = nameof(BuildCommands))]
public static partial class WhetstoneGauntletsEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.WhetstoneGauntlets, "whetstone_gauntlets", "Whetstone Gauntlets", "Gain sharpen 2.", "",
        EquipmentSlotId.Arms, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Apply(RuleEffectIds.Sharpen, 2), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
