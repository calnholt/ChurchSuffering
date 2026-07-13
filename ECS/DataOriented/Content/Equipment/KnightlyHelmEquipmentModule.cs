#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.KnightlyHelm)]
public static partial class KnightlyHelmEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.KnightlyHelm, "knightly_helm", "Knightly Helm", "", "",
        EquipmentSlotId.Head, RuleCardColor.Black, 2,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
