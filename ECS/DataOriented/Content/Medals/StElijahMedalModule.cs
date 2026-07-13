#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StElijah, Handler = nameof(BuildCommands))]
public static partial class StElijahMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StElijah, "st_elijah", "St. Elijah", "The first time each battle you pledge a scorched card, the enemy gains 1 burn.", 1,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.PledgeAdded, MedalTriggerFilter.ScorchedCard, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Charge(), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Burn, 1, MedalEffectTarget.PrimaryEnemy), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
