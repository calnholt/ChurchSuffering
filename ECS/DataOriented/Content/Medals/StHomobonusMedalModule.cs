#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StHomobonus, Handler = nameof(BuildCommands))]
public static partial class StHomobonusMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StHomobonus, "st_homobonus", "St. Homobonus", "After 3 encounters, gain 1 red, 1 white, and 1 black resource.", 3,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.EncounterReward, MedalTriggerFilter.EncounterReward, RuleTriggerIds.MedalReactive, MedalDefinitionParts.Increment(3), priority: 1, timing: RuleActivationTiming.SynchronousBeforeOriginContinues),
        MedalDefinitionParts.Meta(RuleMetaResourceIds.RedClimbResource, 1), MedalDefinitionParts.Meta(RuleMetaResourceIds.WhiteClimbResource, 1), MedalDefinitionParts.Meta(RuleMetaResourceIds.BlackClimbResource, 1),
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
