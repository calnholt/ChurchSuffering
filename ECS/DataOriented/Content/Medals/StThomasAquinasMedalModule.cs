#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StThomasAquinas, Handler = nameof(BuildCommands))]
public static partial class StThomasAquinasMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StThomasAquinas, "st_thomas_aquinas", "St. Thomas Aquinas", "Lose 10 max HP when acquired. Your max hand size is increased by 1.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.Acquired, MedalTriggerFilter.None, RuleTriggerIds.MedalAcquired, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        default, default, default,
        MedalDefinitionParts.MaxHealth(-10), MedalDefinitionParts.MaxHand(1), default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
