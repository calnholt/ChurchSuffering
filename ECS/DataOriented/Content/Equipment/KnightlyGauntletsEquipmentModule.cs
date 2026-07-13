#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.KnightlyGauntlets)]
public static partial class KnightlyGauntletsEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.KnightlyGauntlets, "knightly_gauntlets", "Knightly Gauntlets", "", "",
        EquipmentSlotId.Arms, RuleCardColor.Black, 2,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
