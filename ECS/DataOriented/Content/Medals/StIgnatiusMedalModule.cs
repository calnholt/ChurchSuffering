#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StIgnatius, Handler = nameof(BuildCommands))]
public static partial class StIgnatiusMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StIgnatius, "st_ignatius", "St. Ignatius of Loyola", "The first time each battle you start your action phase with 5 or more courage, gain 2 aggression.", 1,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.BattlePhaseChanged, MedalTriggerFilter.ActionWithFiveCourage, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Charge(), priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Aggression, 2), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.HolySupport);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
