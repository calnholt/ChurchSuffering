#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.IvoryWraps)]
public static partial class IvoryWrapsEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.IvoryWraps, "ivory_wraps", "Ivory Wraps", "", "Cloth bindings worn thin on the road. They still hold when the strike comes.",
        EquipmentSlotId.Arms, RuleCardColor.White, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
