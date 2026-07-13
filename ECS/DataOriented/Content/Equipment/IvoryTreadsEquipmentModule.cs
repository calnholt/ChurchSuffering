#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.IvoryTreads)]
public static partial class IvoryTreadsEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.IvoryTreads, "ivory_treads", "Ivory Treads", "", "Soft leather over hard miles. The faithful learn to keep walking.",
        EquipmentSlotId.Legs, RuleCardColor.White, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
