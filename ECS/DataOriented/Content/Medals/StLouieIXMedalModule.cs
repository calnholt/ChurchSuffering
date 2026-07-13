#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StLouieIX, Handler = nameof(BuildCommands))]
public static partial class StLouieIXMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StLouieIX, "st_louie_ix", "St. Louie IX, King of France", "At the start of every third turn, gain 1 might.", 3,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.BattlePhaseChanged, MedalTriggerFilter.PlayerStart, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Increment(3), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Might, 1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
