#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StRita, Handler = nameof(BuildCommands))]
public static partial class StRitaMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StRita, "st_rita", "St. Rita of Cascia", "The first time you remove a curse in battle, resurrect 2.", 1,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.Tracking, MedalTriggerFilter.PositiveCursesRemoved, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Charge(), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Resurrect(2), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
