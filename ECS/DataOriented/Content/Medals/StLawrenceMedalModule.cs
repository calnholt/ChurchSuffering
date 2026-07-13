#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StLawrence)]
public static partial class StLawrenceMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StLawrence, "st_lawrence", "St. Lawrence", "Your scorched cards deal +X damage, where X is the number of cards discarded to play it.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.None, MedalTriggerFilter.None, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        default, default, default,
        default, default, default,
        MedalDefinitionParts.Provider(MedalProviderKind.ScorchedPaymentDamageModifier, 0), RuleVisualEffectRecipeIds.None);

    public static CardStatModifierResult GetCardStatModifier(ProviderSource source, in CardStatQuery query) =>
        MedalProviderRules.GetCardStatModifier(source, in query);
}
