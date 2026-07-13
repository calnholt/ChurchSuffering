#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StAnthonyOfPadua, Handler = nameof(BuildCommands))]
public static partial class StAnthonyOfPaduaMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StAnthonyOfPadua, "st_anthony_of_padua", "St. Anthony of Padua", "The first time each battle you try to draw and your deck is empty, shuffle 4 random cards from your discard pile back into your deck.", 1,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.DrawPileEmpty, MedalTriggerFilter.EligibleDiscard, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Charge(), priority: 0, timing: RuleActivationTiming.SynchronousBeforeOriginContinues),
        MedalDefinitionParts.ShuffleDiscard(4), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
