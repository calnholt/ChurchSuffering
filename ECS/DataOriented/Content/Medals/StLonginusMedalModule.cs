#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StLonginus, Handler = nameof(BuildCommands))]
public static partial class StLonginusMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StLonginus, "st_longinus", "St. Longinus", "Whenever you pledge a thorned card, add a Kunai to your hand.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.PledgeAdded, MedalTriggerFilter.ThornedCard, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.SpawnKunai(1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
