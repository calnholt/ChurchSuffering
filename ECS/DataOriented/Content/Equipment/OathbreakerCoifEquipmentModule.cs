#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.OathbreakerCoif, Handler = nameof(BuildCommands))]
public static partial class OathbreakerCoifEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.OathbreakerCoif, "oathbreaker_coif", "Oathbreaker Coif", "Unpledge your pledged card.", "",
        EquipmentSlotId.Head, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(RuleConditionIds.HasPledge), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.RemovePledge(), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
