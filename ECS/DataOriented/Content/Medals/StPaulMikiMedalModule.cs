#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StPaulMiki, Handler = nameof(BuildCommands))]
public static partial class StPaulMikiMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StPaulMiki, "st_paul_miki", "St. Paul Miki", "The first time you block with a black card in a battle, add a Kunai to your hand.", 1,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.CardBlocked, MedalTriggerFilter.BlackCard, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Charge(), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.SpawnKunai(1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
