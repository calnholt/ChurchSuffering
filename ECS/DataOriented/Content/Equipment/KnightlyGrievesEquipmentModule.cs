#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.KnightlyGrieves)]
public static partial class KnightlyGrievesEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.KnightlyGrieves, "knightly_grieves", "Knightly Grieves", "", "Standard issue of the order. Built to hold the line when the march grows long.",
        EquipmentSlotId.Legs, RuleCardColor.Black, 2,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
