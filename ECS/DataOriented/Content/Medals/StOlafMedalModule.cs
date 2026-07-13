#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StOlaf)]
public static partial class StOlafMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StOlaf, "st_olaf", "St. Olaf", "Each time frostbite triggers, the enemy takes 3 damage instead of you.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.None, MedalTriggerFilter.None, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        default, default, default,
        default, default, default,
        MedalDefinitionParts.Provider(MedalProviderKind.FrostbiteReplacement, 3), RuleVisualEffectRecipeIds.None);

    public static bool TryBuildReplacement(
        ProviderSource source, in ReplacementQuery query, ref ReplacementPlanWriter writer) =>
        MedalProviderRules.TryBuildReplacement(source, in query, ref writer);
}
