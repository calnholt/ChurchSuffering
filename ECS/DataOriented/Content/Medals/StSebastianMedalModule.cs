#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StSebastian, Handler = nameof(BuildCommands))]
public static partial class StSebastianMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StSebastian, "st_sebastian", "St. Sebastian", "Whenever you win a battle with 1 HP remaining, increase your max HP by 1.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.EnemyKilled, MedalTriggerFilter.PlayerAtOneHealth, RuleTriggerIds.MedalReactive, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.MaxHealth(1), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
