#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Equipment;

public static class EquipmentRuleHandlers
{
    public static void BuildActivationCommands(ref EquipmentHandlerContext context, EquipmentDefinition definition)
    {
        TargetHandle source = TargetHandle.ForEntity(context.Equipment);
        TargetHandle owner = TargetHandle.ForEntity(context.Owner);
        if (!definition.VisualRecipe.IsNull)
        {
            var presentation = new PresentationSpec(
                definition.VisualRecipe, SoundId.Null, 0, RuleValueFlags.None);
            context.Append(RuleCommand.Present(source, owner, in presentation));
        }

        for (var index = 0; index < definition.EffectCount; index++)
            AppendEffect(ref context, definition.GetEffect(index), source, owner);
    }

    private static void AppendEffect(
        ref EquipmentHandlerContext context,
        EquipmentRuleEffect effect,
        TargetHandle source,
        TargetHandle owner)
    {
        switch (effect.Kind)
        {
            case EquipmentRuleEffectKind.ApplyEffect:
                var applied = new EffectSpec(
                    effect.Effect, effect.Amount, 0, ConditionSpec.Always, RuleValueFlags.None);
                context.Append(RuleCommand.ApplyEffect(source, owner, in applied));
                break;
            case EquipmentRuleEffectKind.ModifyStat:
                context.Append(RuleCommand.ModifyStat(source, owner, effect.Stat, effect.Amount));
                break;
            case EquipmentRuleEffectKind.ResurrectRandom:
                context.Append(RuleCommand.RandomCardZone(
                    Deck(ref context), CardZone.DiscardPile, CardZone.Hand,
                    RandomCardZoneOperation.Resurrect, effect.Amount));
                break;
            case EquipmentRuleEffectKind.SpawnKunai:
                context.Append(RuleCommand.SpawnCard(
                    source, owner, Deck(ref context), CardId.Kunai, CardZone.Hand,
                    RuleCardColor.White, count: effect.Amount));
                break;
            case EquipmentRuleEffectKind.RemovePriorTurnPledge:
                EntityId pledgedCard = context.HasUnifiedInput
                    ? context.Input.Deck.PreviousTurnPledgedCard
                    : context.Target.Entity;
                if (!pledgedCard.IsNull)
                {
                    context.Append(RuleCommand.RemovePledge(
                        pledgedCard, owner, CardZone.Hand, PledgeRemovalReason.Replaced));
                }
                break;
            case EquipmentRuleEffectKind.RemoveEffect:
                context.Append(RuleCommand.RemoveEffect(source, TargetHandle.PrimaryEnemy, effect.Effect));
                break;
        }
    }

    private static EntityId Deck(ref EquipmentHandlerContext context) =>
        context.HasUnifiedInput ? context.Input.Deck.Deck : EntityId.Null;
}
