#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StLazarus, Handler = nameof(BuildCommands))]
public static partial class StLazarusMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StLazarus, "st_lazarus", "St. Lazarus", "Whenever you mill 2 cards, resurrect 1.", 2,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.MilledCard, MedalTriggerFilter.NonNullMilledCard, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Increment(2), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Resurrect(1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
