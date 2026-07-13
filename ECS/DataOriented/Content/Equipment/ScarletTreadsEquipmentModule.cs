#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

[EquipmentDefinitionAttribute(EquipmentId.ScarletTreads)]
public static partial class ScarletTreadsEquipmentModule
{
    public static EquipmentDefinition Definition => new(
        EquipmentId.ScarletTreads, "scarlet_treads", "Scarlet Treads", "", "Red leather scuffed at the toe. Built for closing distance.",
        EquipmentSlotId.Legs, RuleCardColor.Red, 1,
        EquipmentDefinitionParts.Inert, RuleVisualEffectRecipeIds.None,
        default, default);
}
