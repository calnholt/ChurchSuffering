#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.ScarletVest)]
public static partial class ScarletVestEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.ScarletVest, "scarlet_vest", "Scarlet Vest", "", "Cut close and dyed deep. Worn by crusaders who prefer speed to ceremony.",
        EquipmentSlotId.Chest, RuleCardColor.Red, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
