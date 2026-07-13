#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StGeorge)]
public static partial class StGeorgeMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StGeorge, "st_george", "St. George", "Your block cards can be played as 3 damage free action attacks.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.None, MedalTriggerFilter.None, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        default, default, default,
        default, default, default,
        MedalDefinitionParts.Provider(MedalProviderKind.AlternateBlockAttack, 3), RuleVisualEffectRecipeIds.None);

    public static AlternatePlayResult GetAlternatePlay(ProviderSource source, in AlternatePlayQuery query) =>
        MedalProviderRules.GetAlternatePlay(source, in query);
}
