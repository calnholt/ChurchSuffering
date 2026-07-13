#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StMonica, Handler = nameof(BuildCommands))]
public static partial class StMonicaMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StMonica, "st_monica", "St. Monica", "Whenever you trigger your temperance ability, resurrect 1.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.TemperanceTriggered, MedalTriggerFilter.NonNullOwner, RuleTriggerIds.TemperanceTriggered, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Resurrect(1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
