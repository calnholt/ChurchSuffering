#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.HeartforgeCuirass, Handler = nameof(BuildCommands))]
public static partial class HeartforgeCuirassEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.HeartforgeCuirass, "heartforge_cuirass", "Heartforge Cuirass", "Gain 1 vigor.", "",
        EquipmentSlotId.Chest, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Apply(RuleEffectIds.Vigor, 1), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
