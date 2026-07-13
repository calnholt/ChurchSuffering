#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.IvoryCoif)]
public static partial class IvoryCoifEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.IvoryCoif, "ivory_coif", "Ivory Coif", "", "Woven for the long vigil. Keeps the sun from your eyes and doubt from your thoughts.",
        EquipmentSlotId.Head, RuleCardColor.White, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
