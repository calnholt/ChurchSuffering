#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StBartholomew, Handler = nameof(BuildCommands))]
public static partial class StBartholomewMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StBartholomew, "st_bartholomew", "St. Bartholomew", "The first time you deal 8 or more damage to the enemy with a single attack, the enemy gains 1 wounded.", 1,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.HpRequested, MedalTriggerFilter.OwnerAttackEnemyForEightPreviewDamage, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Charge(), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Wounded, 1, MedalEffectTarget.PrimaryEnemy), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
