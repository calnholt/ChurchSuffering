#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StMichael, Handler = nameof(BuildCommands))]
public static partial class StMichaelMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StMichael, "st_michael", "St. Michael the Archangel", "At the start of battle, gain 1 courage.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.BattlePhaseChanged, MedalTriggerFilter.StartBattle, RuleTriggerIds.BattleStart, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Modify(RuleStatIds.Courage, 1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
