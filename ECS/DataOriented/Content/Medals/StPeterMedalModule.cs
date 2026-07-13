#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StPeter, Handler = nameof(BuildCommands))]
public static partial class StPeterMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StPeter, "st_peter", "St. Peter the Apostle", "Each time you block with 3 black cards this quest, resurrect 1.", 3,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.CardBlocked, MedalTriggerFilter.BlackCard, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Increment(3), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Resurrect(1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
