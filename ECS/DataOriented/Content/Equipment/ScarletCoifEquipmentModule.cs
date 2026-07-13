#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.ScarletCoif)]
public static partial class ScarletCoifEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.ScarletCoif, "scarlet_coif", "Scarlet Coif", "", "Dyed for the field. A lighter hood for those who mean to press the attack.",
        EquipmentSlotId.Head, RuleCardColor.Red, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
