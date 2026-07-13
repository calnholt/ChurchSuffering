#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StNicholas, Handler = nameof(BuildCommands))]
public static partial class StNicholasMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StNicholas, "st_nicholas", "St. Nicholas the Bishop", "When this is acquired, increase your max HP by 2 and 4 random cards from your deck become frozen.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.Acquired, MedalTriggerFilter.None, RuleTriggerIds.MedalAcquired, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        default, default, default,
        MedalDefinitionParts.MaxHealth(2), MedalDefinitionParts.Freeze(4), default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
