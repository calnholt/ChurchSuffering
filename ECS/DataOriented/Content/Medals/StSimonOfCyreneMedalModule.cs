#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

[MedalDefinitionAttribute(MedalId.StSimonOfCyrene, Handler = nameof(BuildCommands))]
public static partial class StSimonOfCyreneMedalModule
{
    public static MedalDefinition Definition => new(
        MedalId.StSimonOfCyrene, "st_simon_of_cyrene", "St. Simon of Cyrene", "At the start of battle, the enemy gains 1 anathema.", 0,
        MedalDefinitionParts.Trigger(MedalReactiveEvent.BattlePhaseChanged, MedalTriggerFilter.StartBattle, RuleTriggerIds.BattleStart, MedalDefinitionParts.NoCounter, priority: 0, timing: RuleActivationTiming.QueuedAfterTrigger),
        MedalDefinitionParts.Apply(RuleEffectIds.Anathema, 1, MedalEffectTarget.PrimaryEnemy), default, default,
        default, default, default,
        default, RuleVisualEffectRecipeIds.None);

    public static void BuildCommands(ref MedalHandlerContext context) =>
        MedalRuleHandlers.BuildCommands(ref context, Definition);
}
