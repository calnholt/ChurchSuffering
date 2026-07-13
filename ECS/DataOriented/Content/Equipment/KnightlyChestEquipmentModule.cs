#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.KnightlyChest)]
public static partial class KnightlyChestEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.KnightlyChest, "knightly_chest", "Knightly Chest", "", "",
        EquipmentSlotId.Chest, RuleCardColor.Black, 2,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
