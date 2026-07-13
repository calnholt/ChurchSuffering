#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StChristopher)]
public static partial class StChristopherMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StChristopher, "st_christopher", "St. Christopher", "Your brittle cards have +1 block.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.None, MedalTriggerFilter.None, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        default, default, default,
        default, default, default,
        MedalDefinitionParts.Provider(MedalProviderKind.BrittleBlockModifier, 1), RuleVisualEffectRecipeIds.None);

    public static CardStatModifierResult GetCardStatModifier(ProviderSource source, in CardStatQuery query) =>
        MedalProviderRules.GetCardStatModifier(source, in query);
}
