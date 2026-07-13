#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.KunaiSheath, Handler = nameof(BuildCommands))]
public static partial class KunaiSheathEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.KunaiSheath, "kunai_sheath", "Kunai Sheath", "Add 1 Kunai to your hand.", "",
        EquipmentSlotId.Arms, RuleCardColor.Black, 1,
        EquipmentDefinitionParts.ActionActivation(), RuleVisualEffectRecipeIds.None,
        EquipmentDefinitionParts.SpawnKunai(1), default);

    public static void BuildCommands(ref EquipmentHandlerContext context) =>
        EquipmentRuleHandlers.BuildActivationCommands(ref context, Definition);
}
