#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.SanctifiedCirclet, Handler = nameof(BuildCommands))]
public static partial class SanctifiedCircletEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.SanctifiedCirclet, "sanctified_circlet", "Sanctified Circlet", "Gain 2 temperance.", "",
        EquipmentSlotId.Head, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Modify(RuleStatIds.Temperance, 2), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
