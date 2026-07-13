#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using RuleCardColor = Crusaders30XX.ECS.DataOriented.Rules.RuleCardColor;

namespace Crusaders30XX.ECS.DataOriented.Content.Medals;

public static class MedalRuleHandlers
{
    public static void BuildCommands(ref MedalHandlerContext context, MedalDefinition definition)
    {
        bool acquiring = context.Trigger == RuleTriggerIds.MedalAcquired;
        if (!acquiring && (!Qualifies(ref context, definition.Trigger) ||
                           !CounterWouldActivate(context.Input.State, definition.Trigger.Activation.Counter)))
            return;

        TargetHandle source = TargetHandle.ForEntity(context.Medal);
        TargetHandle owner = TargetHandle.ForEntity(context.Owner);
        if (!acquiring && !definition.VisualRecipe.IsNull)
        {
            var presentation = new PresentationSpec(
                definition.VisualRecipe, SoundId.Null, 0, RuleValueFlags.None);
            context.Append(RuleCommand.Present(source, owner, in presentation));
        }

        int count = acquiring ? definition.AcquisitionEffectCount : definition.ActivationEffectCount;
        for (var index = 0; index < count; index++)
        {
            MedalRuleEffect effect = acquiring
                ? definition.GetAcquisitionEffect(index)
                : definition.GetActivationEffect(index);
            AppendEffect(ref context, effect, source, owner);
        }
    }

    private static bool Qualifies(ref MedalHandlerContext context, MedalTriggerDefinition trigger)
    {
        RuleTriggerEnvelope envelope = context.TriggerEnvelope;
        return trigger.Filter switch
        {
            MedalTriggerFilter.None => true,
            MedalTriggerFilter.StartBattle => envelope.Kind == RuleTriggerKind.PhaseChanged &&
                envelope.Payload.PhaseChanged.Current == RulePhase.StartBattle,
            MedalTriggerFilter.PlayerStart => envelope.Kind == RuleTriggerKind.PhaseChanged &&
                envelope.Payload.PhaseChanged.Current == RulePhase.PlayerStart,
            MedalTriggerFilter.ActionWithFiveCourage => envelope.Kind == RuleTriggerKind.PhaseChanged &&
                envelope.Payload.PhaseChanged.Current == RulePhase.Action &&
                context.Input.OwnerResources.Courage >= 5,
            MedalTriggerFilter.NonNullOwner => context.Trigger == RuleTriggerIds.TemperanceTriggered,
            MedalTriggerFilter.BlackCard => envelope.Kind == RuleTriggerKind.Card &&
                envelope.Payload.Card.Color == RuleCardColor.Black,
            // Enemy death and current-health qualification are resolved by the trigger mapper before dispatch.
            MedalTriggerFilter.PlayerAtOneHealth => context.Trigger == RuleTriggerIds.MedalReactive,
            MedalTriggerFilter.EncounterReward => envelope.Kind == RuleTriggerKind.EncounterReward &&
                envelope.Payload.EncounterReward.IsEncounter,
            MedalTriggerFilter.ScorchedCard => envelope.Kind == RuleTriggerKind.Card &&
                (envelope.Payload.Card.Traits & RuleCardTraits.Scorched) != 0,
            MedalTriggerFilter.WeaponCard => envelope.Kind == RuleTriggerKind.Card &&
                (envelope.Payload.Card.Traits & RuleCardTraits.Weapon) != 0,
            MedalTriggerFilter.PositiveOwnerAggression => envelope.Kind == RuleTriggerKind.Passive &&
                envelope.Payload.Passive.Target == context.Owner &&
                envelope.Payload.Passive.Effect == RuleEffectIds.Aggression &&
                envelope.Payload.Passive.Delta > 0,
            MedalTriggerFilter.ThornedCard => envelope.Kind == RuleTriggerKind.Card &&
                (envelope.Payload.Card.Traits & RuleCardTraits.Thorned) != 0,
            MedalTriggerFilter.EligibleDiscard => envelope.Kind == RuleTriggerKind.DrawPileEmpty &&
                envelope.Payload.DrawPileEmpty.EligibleDiscardCount > 0,
            MedalTriggerFilter.OwnerAttackEnemyForEightPreviewDamage => envelope.Kind == RuleTriggerKind.HpRequested &&
                envelope.Payload.HpRequested.Source == context.Owner &&
                (context.Target.Kind == TargetKind.PrimaryEnemy ||
                 envelope.Payload.HpRequested.Target == context.Target.Entity) &&
                envelope.Payload.HpRequested.DamageKind == RuleDamageKind.Attack &&
                Math.Abs(envelope.Payload.HpRequested.PreviewDelta) >= 8,
            MedalTriggerFilter.PositiveCursesRemoved => envelope.Kind == RuleTriggerKind.Tracking &&
                envelope.Payload.Tracking.Delta > 0,
            MedalTriggerFilter.NonNullMilledCard => envelope.Kind == RuleTriggerKind.Mill &&
                !envelope.Payload.Mill.Card.IsNull,
            _ => false,
        };
    }

    private static bool CounterWouldActivate(MedalRuntimeState state, MedalCounterSpec spec) =>
        spec.Progression switch
        {
            MedalCounterProgression.None => true,
            MedalCounterProgression.IncrementToThreshold => state.Count + 1 >= spec.Threshold,
            MedalCounterProgression.ConsumeCharge => state.Count > 0,
            _ => false,
        };

    private static void AppendEffect(
        ref MedalHandlerContext context,
        MedalRuleEffect effect,
        TargetHandle source,
        TargetHandle owner)
    {
        TargetHandle target = effect.Target == MedalEffectTarget.PrimaryEnemy
            ? TargetHandle.PrimaryEnemy
            : owner;
        switch (effect.Kind)
        {
            case MedalRuleEffectKind.ApplyEffect:
                var applied = new EffectSpec(
                    effect.Effect, effect.Amount, 0, ConditionSpec.Always, RuleValueFlags.None);
                context.Append(RuleCommand.ApplyEffect(source, target, in applied));
                break;
            case MedalRuleEffectKind.ModifyStat:
                context.Append(RuleCommand.ModifyStat(source, target, effect.Stat, effect.Amount));
                break;
            case MedalRuleEffectKind.ResurrectRandom:
                context.Append(RuleCommand.RandomCardZone(
                    Deck(ref context), CardZone.DiscardPile, CardZone.Hand,
                    RandomCardZoneOperation.Resurrect, effect.Amount));
                break;
            case MedalRuleEffectKind.ModifyMaxHealth:
                context.Append(RuleCommand.ModifyMaxHealth(
                    target, effect.Amount, MaxHealthChangeKind.Permanent, RuleValueFlags.Permanent));
                break;
            case MedalRuleEffectKind.FreezeRandomCards:
                AppendFrozenCards(ref context, effect.Amount);
                break;
            case MedalRuleEffectKind.SpawnKunai:
                context.Append(RuleCommand.SpawnCard(
                    source, owner, Deck(ref context), CardId.Kunai, CardZone.Hand,
                    RuleCardColor.White, count: effect.Amount));
                break;
            case MedalRuleEffectKind.ModifyMetaResource:
                const MetaResourceScope scope = MetaResourceScope.Runtime | MetaResourceScope.PendingReward |
                    MetaResourceScope.EventPayload | MetaResourceScope.Persist;
                context.Append(RuleCommand.ModifyMetaResource(
                    owner, effect.MetaResource, MetaResourceChangeKind.Add, effect.Amount, scope,
                    RuleValueFlags.QuestPersistent));
                break;
            case MedalRuleEffectKind.Damage:
                context.Append(RuleCommand.Damage(source, target, effect.Amount));
                break;
            case MedalRuleEffectKind.ModifyMaxHandSize:
                context.Append(RuleCommand.ModifyMaxHandSize(
                    target, effect.Amount, MaxHandSizeChangeKind.Permanent, RuleValueFlags.Permanent));
                break;
            case MedalRuleEffectKind.MillTopCard:
                context.Append(RuleCommand.RandomCardZone(
                    Deck(ref context), CardZone.DrawPile, CardZone.DiscardPile,
                    RandomCardZoneOperation.Mill, effect.Amount));
                break;
            case MedalRuleEffectKind.ShuffleRandomDiscardToDraw:
                context.Append(RuleCommand.RandomCardZone(
                    Deck(ref context), CardZone.DiscardPile, CardZone.DrawPile,
                    RandomCardZoneOperation.ShuffleInto, effect.Amount,
                    RuleCardFilter.ExcludeWeapon));
                break;
        }
    }

    private static void AppendFrozenCards(ref MedalHandlerContext context, int requested)
    {
        int count = Math.Min(Math.Min(requested, context.Targets.Length), 4);
        Span<EntityId> selected = stackalloc EntityId[4];
        var random = context.Random;
        for (var selectionIndex = 0; selectionIndex < count; selectionIndex++)
        {
            int remainingRank = random.NextInt(context.Targets.Length - selectionIndex);
            EntityId selectedCard = default;
            for (var candidateIndex = 0; candidateIndex < context.Targets.Length; candidateIndex++)
            {
                EntityId candidate = context.Targets[candidateIndex];
                if (Contains(selected[..selectionIndex], candidate))
                    continue;
                if (remainingRank-- == 0)
                {
                    selectedCard = candidate;
                    break;
                }
            }

            selected[selectionIndex] = selectedCard;
            var frozen = new EffectSpec(
                RuleEffectIds.CardFrozen, 1, 0, ConditionSpec.Always, RuleValueFlags.Permanent);
            context.Append(RuleCommand.ApplyEffect(
                TargetHandle.ForEntity(context.Medal), TargetHandle.ForEntity(selectedCard), in frozen));
        }
    }

    private static bool Contains(ReadOnlySpan<EntityId> values, EntityId candidate)
    {
        for (var index = 0; index < values.Length; index++)
        {
            if (values[index] == candidate)
                return true;
        }
        return false;
    }

    private static EntityId Deck(ref MedalHandlerContext context) =>
        context.HasUnifiedInput ? context.Input.Deck.Deck : EntityId.Null;
}
