#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.IvoryVest)]
public static partial class IvoryVestEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.IvoryVest, "ivory_vest", "Ivory Vest", "", "Blessed linen, light enough to march in and strong enough to turn a glancing blow.",
        EquipmentSlotId.Chest, RuleCardColor.White, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
