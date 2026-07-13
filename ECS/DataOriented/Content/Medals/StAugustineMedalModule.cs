#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StAugustine, Handler = nameof(BuildCommands))]
public static partial class StAugustineMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StAugustine, "st_augustine", "St. Augustine of Hippo", "At the start of battle, mill 1 card. Increase your max HP by 1 when this is acquired.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.BattlePhaseChanged, MedalTriggerFilter.StartBattle, RuleTriggerIds.BattleStart, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Mill(1), default, default,
        MedalDefinitionParts.MaxHealth(1), default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
