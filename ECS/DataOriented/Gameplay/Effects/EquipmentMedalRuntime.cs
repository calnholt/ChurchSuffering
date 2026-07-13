#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Content.Equipment;
using Crusaders30XX.ECS.DataOriented.Content.Medals;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Effects;

public readonly record struct EquippedMedalProvider(EntityId Entity, EquippedMedal Medal)
{
    public ProviderSource Source
    {
        get
        {
            ref readonly MedalDefinition definition = ref GeneratedMedalCatalog.GetDefinition(Medal.Definition);
            return new ProviderSource(Entity, Medal.Owner, Medal.Definition, definition.Provider.Lifetime, Medal.Active);
        }
    }
}

/// <summary>Allocation-free provider resolution over caller-owned equipped-medal snapshots.</summary>
public static class EquipmentMedalProviderRuntime
{
    public static bool TryResolveAlternate(
        ReadOnlySpan<EquippedMedalProvider> providers,
        in AlternatePlayQuery query,
        out AlternatePlayResult result)
    {
        result = default;
        bool found = false;
        for (var index = 0; index < providers.Length; index++)
        {
            ProviderSource source = providers[index].Source;
            if (!Eligible(source, query.Owner))
                continue;
            AlternatePlayResult candidate = MedalProviderRules.GetAlternatePlay(source, in query);
            if (!candidate.IsApplicable || found && Compare(candidate.Source.Entity, result.Source.Entity) >= 0)
                continue;
            result = candidate;
            found = true;
        }
        return found;
    }

    public static int AggregateCardStat(
        ReadOnlySpan<EquippedMedalProvider> providers,
        in CardStatQuery query,
        out int modifierCount)
    {
        int delta = 0;
        modifierCount = 0;
        for (var index = 0; index < providers.Length; index++)
        {
            ProviderSource source = providers[index].Source;
            if (!Eligible(source, query.Owner))
                continue;
            CardStatModifierResult candidate = MedalProviderRules.GetCardStatModifier(source, in query);
            if (!candidate.IsApplicable)
                continue;
            delta = checked(delta + candidate.Delta);
            modifierCount++;
        }
        return checked(query.BaseValue + delta);
    }

    public static ReplacementPlan ResolveReplacement(
        ReadOnlySpan<EquippedMedalProvider> providers,
        EntityId queryOwner,
        in ReplacementQuery query,
        Span<ReplacementAction> actions)
    {
        ProviderSource selected = default;
        bool found = false;
        Span<ReplacementAction> scratchActions = stackalloc ReplacementAction[1];
        for (var index = 0; index < providers.Length; index++)
        {
            ProviderSource source = providers[index].Source;
            if (!Eligible(source, queryOwner))
                continue;
            scratchActions.Clear();
            var scratch = new ReplacementPlanWriter(scratchActions);
            if (!MedalProviderRules.TryBuildReplacement(source, in query, ref scratch) ||
                found && Compare(source.Entity, selected.Entity) >= 0)
                continue;
            selected = source;
            found = true;
        }

        var writer = new ReplacementPlanWriter(actions);
        if (found)
            MedalProviderRules.TryBuildReplacement(selected, in query, ref writer);
        return writer.BuildPlan();
    }

    private static bool Eligible(in ProviderSource source, EntityId owner) =>
        source.Active && source.EquippedOwner == owner &&
        GeneratedMedalCatalog.GetDefinition(source.Definition).Provider.Kind != MedalProviderKind.None;

    private static int Compare(EntityId left, EntityId right)
    {
        int index = left.Index.CompareTo(right.Index);
        return index != 0 ? index : left.Generation.CompareTo(right.Generation);
    }
}

public static class EquipmentActivationRuntime
{
    public static bool TryActivate(
        ReadOnlyWorld world,
        EntityId entity,
        ref EquippedEquipment equipment,
        in EquipmentHandlerInput suppliedInput,
        bool availabilityConditionSatisfied,
        int battleEpoch,
        RuleCommandBuffer commands,
        ReadOnlySpan<RuleFact> facts,
        ReadOnlySpan<EntityId> targets,
        Span<RuleHandlerResult> results,
        ref RuleResultWriterState resultState)
    {
        ref readonly EquipmentDefinition definition = ref GeneratedEquipmentCatalog.GetDefinition(equipment.Definition);
        EquipmentActivationSpec activation = definition.Activation;
        EquipmentUsageState usage = equipment.Usage;
        EquipmentMedalStateRules.RefreshForBattle(ref usage, in activation, battleEpoch);
        equipment.Usage = usage;
        if (equipment.Active == 0 || !EquipmentMedalStateRules.CanActivate(
                in usage, in activation, ToMask(suppliedInput.Phase), availabilityConditionSatisfied))
            return false;

        EquipmentHandlerInput input = suppliedInput with
        {
            Equipment = entity,
            Owner = equipment.Owner,
            Definition = equipment.Definition,
            State = equipment.Usage,
            Flags = suppliedInput.Flags | EquipmentHandlerFlags.Equipped | EquipmentHandlerFlags.Activated,
        };
        var context = new EquipmentHandlerContext(
            world, commands.Writer, in input, facts, targets, results, ref resultState, ref equipment.Random);
        if (!GeneratedEquipmentCatalog.Dispatch(equipment.Definition, ref context))
            return false;
        bool marked = EquipmentMedalStateRules.TryMarkUsed(ref usage, in activation);
        equipment.Usage = usage;
        return marked;
    }

    public static void RefreshForBattle(ref EquippedEquipment equipment, int battleEpoch)
    {
        ref readonly EquipmentDefinition definition = ref GeneratedEquipmentCatalog.GetDefinition(equipment.Definition);
        EquipmentActivationSpec activation = definition.Activation;
        EquipmentUsageState usage = equipment.Usage;
        EquipmentMedalStateRules.RefreshForBattle(ref usage, in activation, battleEpoch);
        equipment.Usage = usage;
    }

    private static RulePhaseMask ToMask(RulePhase phase) => phase == RulePhase.None
        ? RulePhaseMask.None
        : (RulePhaseMask)(1 << ((int)phase - 1));
}

public static class MedalActivationRuntime
{
    public static void RefreshForBattle(ref EquippedMedal medal, int battleEpoch)
    {
        ref readonly MedalDefinition definition = ref GeneratedMedalCatalog.GetDefinition(medal.Definition);
        MedalCounterSpec counter = definition.Trigger.Activation.Counter;
        MedalRuntimeState state = medal.State;
        EquipmentMedalStateRules.RefreshForBattle(ref state, in counter, battleEpoch);
        medal.State = state;
    }

    public static bool TryObserve(
        ReadOnlyWorld world,
        EntityId entity,
        ref EquippedMedal medal,
        in MedalHandlerInput suppliedInput,
        int battleEpoch,
        RuleCommandBuffer commands,
        ReadOnlySpan<RuleFact> facts,
        ReadOnlySpan<EntityId> targets,
        Span<RuleHandlerResult> results,
        ref RuleResultWriterState resultState,
        ushort amount = 1)
    {
        ref readonly MedalDefinition definition = ref GeneratedMedalCatalog.GetDefinition(medal.Definition);
        MedalCounterSpec counter = definition.Trigger.Activation.Counter;
        MedalRuntimeState state = medal.State;
        EquipmentMedalStateRules.RefreshForBattle(ref state, in counter, battleEpoch);
        medal.State = state;
        if (medal.Active == 0 || suppliedInput.Trigger.SemanticTrigger != definition.Trigger.Activation.Trigger ||
            !Qualifies(definition.Trigger.Filter, in suppliedInput))
            return false;

        bool activates = CounterWouldActivate(in state, in counter, amount);
        if (activates)
        {
            MedalHandlerInput input = suppliedInput with
            {
                Medal = entity,
                Owner = medal.Owner,
                Definition = medal.Definition,
                State = state,
            };
            var context = new MedalHandlerContext(
                world, commands.Writer, in input, facts, targets, results, ref resultState, ref medal.Random);
            GeneratedMedalCatalog.Dispatch(medal.Definition, ref context);
        }

        bool observed = EquipmentMedalStateRules.ObserveQualifyingTrigger(
            ref state, in counter, amount);
        medal.State = state;
        return activates && observed;
    }

    public static bool Qualifies(MedalTriggerFilter filter, in MedalHandlerInput input)
    {
        RuleTriggerEnvelope envelope = input.Trigger;
        return filter switch
        {
            MedalTriggerFilter.None => true,
            MedalTriggerFilter.StartBattle => envelope.Kind == RuleTriggerKind.PhaseChanged && envelope.Payload.PhaseChanged.Current == RulePhase.StartBattle,
            MedalTriggerFilter.PlayerStart => envelope.Kind == RuleTriggerKind.PhaseChanged && envelope.Payload.PhaseChanged.Current == RulePhase.PlayerStart,
            MedalTriggerFilter.ActionWithFiveCourage => envelope.Kind == RuleTriggerKind.PhaseChanged && envelope.Payload.PhaseChanged.Current == RulePhase.Action && input.OwnerResources.Courage >= 5,
            MedalTriggerFilter.NonNullOwner => !input.Owner.IsNull && envelope.SemanticTrigger == RuleTriggerIds.TemperanceTriggered,
            MedalTriggerFilter.BlackCard => envelope.Kind == RuleTriggerKind.Card && envelope.Payload.Card.Color == RuleCardColor.Black,
            MedalTriggerFilter.PlayerAtOneHealth => envelope.SemanticTrigger == RuleTriggerIds.MedalReactive,
            MedalTriggerFilter.EncounterReward => envelope.Kind == RuleTriggerKind.EncounterReward && envelope.Payload.EncounterReward.IsEncounter,
            MedalTriggerFilter.ScorchedCard => envelope.Kind == RuleTriggerKind.Card && (envelope.Payload.Card.Traits & RuleCardTraits.Scorched) != 0,
            MedalTriggerFilter.WeaponCard => envelope.Kind == RuleTriggerKind.Card && (envelope.Payload.Card.Traits & RuleCardTraits.Weapon) != 0,
            MedalTriggerFilter.PositiveOwnerAggression => envelope.Kind == RuleTriggerKind.Passive && envelope.Payload.Passive.Target == input.Owner && envelope.Payload.Passive.Effect == RuleEffectIds.Aggression && envelope.Payload.Passive.Delta > 0,
            MedalTriggerFilter.ThornedCard => envelope.Kind == RuleTriggerKind.Card && (envelope.Payload.Card.Traits & RuleCardTraits.Thorned) != 0,
            MedalTriggerFilter.EligibleDiscard => envelope.Kind == RuleTriggerKind.DrawPileEmpty && envelope.Payload.DrawPileEmpty.EligibleDiscardCount > 0,
            MedalTriggerFilter.OwnerAttackEnemyForEightPreviewDamage => envelope.Kind == RuleTriggerKind.HpRequested && envelope.Payload.HpRequested.Source == input.Owner && (input.PrimaryTarget.Kind == TargetKind.PrimaryEnemy || envelope.Payload.HpRequested.Target == input.PrimaryTarget.Entity) && envelope.Payload.HpRequested.DamageKind == RuleDamageKind.Attack && Math.Abs(envelope.Payload.HpRequested.PreviewDelta) >= 8,
            MedalTriggerFilter.PositiveCursesRemoved => envelope.Kind == RuleTriggerKind.Tracking && envelope.Payload.Tracking.Delta > 0,
            MedalTriggerFilter.NonNullMilledCard => envelope.Kind == RuleTriggerKind.Mill && !envelope.Payload.Mill.Card.IsNull,
            _ => false,
        };
    }

    private static bool CounterWouldActivate(in MedalRuntimeState state, in MedalCounterSpec spec, ushort amount) =>
        spec.Progression switch
        {
            MedalCounterProgression.None => true,
            MedalCounterProgression.IncrementToThreshold => (uint)state.Count + amount >= spec.Threshold,
            MedalCounterProgression.ConsumeCharge => state.Count > 0 && amount > 0,
            _ => false,
        };
}
