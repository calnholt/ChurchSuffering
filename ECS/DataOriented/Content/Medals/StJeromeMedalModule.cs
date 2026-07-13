#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StJerome, Handler = nameof(BuildCommands))]
public static partial class StJeromeMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StJerome, "st_jerome", "St. Jerome", "Whenever you gain aggression, gain 1 courage.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.PassiveApplied, MedalTriggerFilter.PositiveOwnerAggression, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Modify(RuleStatIds.Courage, 1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
