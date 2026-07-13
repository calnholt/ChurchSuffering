#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StBenedict, Handler = nameof(BuildCommands))]
public static partial class StBenedictMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StBenedict, "st_benedict", "St. Benedict", "Whenever you pledge 3 cards, gain 1 vigor.", 3,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.PledgeAdded, MedalTriggerFilter.None, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Increment(3), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Vigor, 1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
