#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.ScarletWraps)]
public static partial class ScarletWrapsEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.ScarletWraps, "scarlet_wraps", "Scarlet Wraps", "", "Red wrappings that mark the hand before it marks the enemy.",
        EquipmentSlotId.Arms, RuleCardColor.Red, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
