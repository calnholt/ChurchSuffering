#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.PurgingBracers, Handler = nameof(BuildCommands))]
public static partial class PurgingBracersEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.PurgingBracers, "purging_bracers", "Purging Bracers", "Gain 2 aggression.", "",
        EquipmentSlotId.Arms, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.Apply(RuleEffectIds.Aggression, 2), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
