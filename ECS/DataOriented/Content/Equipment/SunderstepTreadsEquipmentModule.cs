#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.SunderstepTreads, Handler = nameof(BuildCommands))]
public static partial class SunderstepTreadsEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.SunderstepTreads, "sunderstep_treads", "Sunderstep Treads", "Remove all guard from the enemy.", "",
        EquipmentSlotId.Legs, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Remove(RuleEffectIds.Guard), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
