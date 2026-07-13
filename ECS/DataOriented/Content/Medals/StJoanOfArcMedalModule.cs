#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StJoanOfArc, Handler = nameof(BuildCommands))]
public static partial class StJoanOfArcMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StJoanOfArc, "st_joan_of_arc", "St. Joan of Arc", "Whenever you attack with your weapon, gain 1 might.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.CardPlayed, MedalTriggerFilter.WeaponCard, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Might, 1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
