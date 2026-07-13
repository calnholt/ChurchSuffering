#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.HelmOfSeeing, Handler = nameof(BuildCommands))]
public static partial class HelmOfSeeingEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.HelmOfSeeing, "helm_of_seeing", "Helm of Seeing", "Resurrect 1.", "",
        EquipmentSlotId.Head, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.DefensiveGuard,
        EquipmentDefinitionParts.Resurrect(1), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
