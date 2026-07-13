#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StLuke, Handler = nameof(BuildCommands))]
public static partial class StLukeMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StLuke, "st_luke", "St. Luke the Evangelist", "At the start of battle, gain 1 aegis.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.BattlePhaseChanged, MedalTriggerFilter.StartBattle, RuleTriggerIds.BattleStart, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Aegis, 1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.HolySupport);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
