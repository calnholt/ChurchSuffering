#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rules;

public sealed class Ecs023ProviderContractTests
{
    [Fact]
    public void Trigger_provider_state_and_replacement_contracts_are_unmanaged()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleTriggerEnvelope>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PhaseChangedTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<CardTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<PassiveTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<HpRequestedTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TrackingTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EncounterRewardTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<DrawPileEmptyTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<MillTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ReplacementTriggerPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EquipmentActivationSpec>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EquipmentUsageState>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<MedalCounterSpec>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<MedalActivationSpec>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<MedalRuntimeState>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<AlternatePlayQuery>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<AlternatePlayResult>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<CardStatQuery>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<CardStatModifierResult>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ReplacementQuery>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ReplacementPlan>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ReplacementAction>());
    }

    [Fact]
    public void Trigger_payload_is_a_fixed_union_and_preserves_typed_payloads()
    {
        Assert.Equal(64, Marshal.SizeOf<RuleTriggerPayload>());
        foreach (FieldInfo field in typeof(RuleTriggerPayload).GetFields(BindingFlags.Public | BindingFlags.Instance))
            Assert.Equal(IntPtr.Zero, Marshal.OffsetOf<RuleTriggerPayload>(field.Name));

        var card = new CardTriggerPayload(
            new EntityId(10, 2),
            new EntityId(20, 3),
            CardId.Strike,
            RuleCardEventKind.Played,
            RuleCardColor.Red,
            RuleCardTraits.Attack | RuleCardTraits.Scorched);
        RuleTriggerPayload payload = RuleTriggerPayload.From(in card);
        var envelope = new RuleTriggerEnvelope(RuleTriggerKind.Card, new TriggerId(9), payload);

        Assert.Equal(card, envelope.Payload.Card);
        Assert.Equal(RuleTriggerKind.Card, envelope.Kind);
        Assert.Equal(new TriggerId(9), envelope.SemanticTrigger);
    }

    [Fact]
    public void Equipment_usage_resets_only_at_its_declared_lifetime_boundary()
    {
        var battleSpec = new EquipmentActivationSpec(
            RulePhaseMask.Action,
            TriggerId.Null,
            ConditionId.Null,
            MaxUses: 1,
            RuleResetPolicy.StartBattle,
            EquipmentUsageLifetime.Battle);
        var questSpec = battleSpec with
        {
            ResetPolicy = RuleResetPolicy.Never,
            Lifetime = EquipmentUsageLifetime.Quest,
        };
        var battleState = default(EquipmentUsageState);
        var questState = default(EquipmentUsageState);
        EquipmentMedalStateRules.Initialize(ref battleState, in battleSpec, battleEpoch: 1);
        EquipmentMedalStateRules.Initialize(ref questState, in questSpec, battleEpoch: 1);

        Assert.True(EquipmentMedalStateRules.CanActivate(
            in battleState, in battleSpec, RulePhaseMask.Action, availabilityConditionSatisfied: true));
        Assert.True(EquipmentMedalStateRules.TryMarkUsed(ref battleState, in battleSpec));
        Assert.True(EquipmentMedalStateRules.TryMarkUsed(ref questState, in questSpec));
        Assert.False(EquipmentMedalStateRules.CanActivate(
            in battleState, in battleSpec, RulePhaseMask.Action, availabilityConditionSatisfied: true));

        EquipmentMedalStateRules.RefreshForBattle(ref battleState, in battleSpec, battleEpoch: 2);
        EquipmentMedalStateRules.RefreshForBattle(ref questState, in questSpec, battleEpoch: 2);

        Assert.Equal((ushort)0, battleState.Uses);
        Assert.Equal((ushort)1, questState.Uses);
    }

    [Fact]
    public void Medal_counter_models_persistent_thresholds_and_per_battle_charges()
    {
        var repeating = new MedalCounterSpec(
            Threshold: 3,
            InitialCount: 0,
            RuleResetPolicy.Never,
            MedalCounterProgression.IncrementToThreshold,
            MedalCounterConsumePolicy.KeepRemainder);
        var charged = new MedalCounterSpec(
            Threshold: 1,
            InitialCount: 1,
            RuleResetPolicy.StartBattle,
            MedalCounterProgression.ConsumeCharge,
            MedalCounterConsumePolicy.StayAtZero);
        var repeatingState = default(MedalRuntimeState);
        var chargedState = default(MedalRuntimeState);
        EquipmentMedalStateRules.Initialize(ref repeatingState, in repeating, 1);
        EquipmentMedalStateRules.Initialize(ref chargedState, in charged, 1);

        Assert.True(EquipmentMedalStateRules.ObserveQualifyingTrigger(ref repeatingState, in repeating, amount: 5));
        Assert.Equal((ushort)2, repeatingState.Count);
        Assert.True(EquipmentMedalStateRules.ObserveQualifyingTrigger(ref chargedState, in charged));
        Assert.False(EquipmentMedalStateRules.ObserveQualifyingTrigger(ref chargedState, in charged));

        EquipmentMedalStateRules.RefreshForBattle(ref repeatingState, in repeating, 2);
        EquipmentMedalStateRules.RefreshForBattle(ref chargedState, in charged, 2);

        Assert.Equal((ushort)2, repeatingState.Count);
        Assert.Equal((ushort)1, chargedState.Count);
    }

    [Fact]
    public void Provider_precedence_selects_or_accumulates_by_stable_entity_id()
    {
        EntityId owner = new(50, 1);
        ProviderCandidateResult[] candidates =
        [
            Candidate(1, owner, MedalId.StGeorge, applies: true, active: false),
            Candidate(9, owner, MedalId.StGeorge, applies: true),
            Candidate(2, owner, MedalId.StOlaf, applies: true),
            Candidate(1, new EntityId(99, 1), MedalId.StLawrence, applies: true),
            Candidate(5, owner, MedalId.StChristopher, applies: false),
            Candidate(7, owner, MedalId.StLawrence, applies: true),
        ];

        Assert.True(ProviderPrecedence.TrySelectFirst(candidates, owner, out ProviderCandidateResult first));
        Assert.Equal(2, first.Source.Entity.Index);

        Span<ProviderCandidateResult> ordered = stackalloc ProviderCandidateResult[3];
        int count = ProviderPrecedence.CollectApplicableInOrder(candidates, owner, ordered);

        Assert.Equal(3, count);
        Assert.Equal(2, ordered[0].Source.Entity.Index);
        Assert.Equal(7, ordered[1].Source.Entity.Index);
        Assert.Equal(9, ordered[2].Source.Entity.Index);
        Assert.Equal(ProviderResolutionPolicy.FirstApplicableByStableEntityId, EquipmentMedalProviderPolicies.AlternatePlay);
        Assert.Equal(ProviderResolutionPolicy.FirstApplicableByStableEntityId, EquipmentMedalProviderPolicies.Replacement);
        Assert.Equal(ProviderResolutionPolicy.AccumulateByStableEntityId, EquipmentMedalProviderPolicies.CardStatModifiers);
    }

    [Fact]
    public void Replacement_plan_can_handle_and_suppress_with_zero_actions()
    {
        var source = new ProviderSource(
            new EntityId(4, 1),
            new EntityId(8, 1),
            MedalId.StOlaf,
            ProviderLifetime.WhileEquipped,
            IsActive: 1);
        Span<ReplacementAction> storage = stackalloc ReplacementAction[0];
        var writer = new ReplacementPlanWriter(storage);

        writer.MarkHandled(source);
        ReplacementPlan plan = writer.BuildPlan();

        Assert.True(plan.IsHandled);
        Assert.Equal(0, plan.ActionCount);
        Assert.Equal(MedalId.StOlaf, plan.HandlingProvider.Definition);
        Assert.Empty(writer.Actions.ToArray());
    }

    [Fact]
    public void Replacement_writer_is_bounded_and_never_overwrites_caller_storage()
    {
        Span<ReplacementAction> storage = stackalloc ReplacementAction[1];
        var writer = new ReplacementPlanWriter(storage);
        var provider = new ProviderSource(
            new EntityId(4, 1),
            new EntityId(8, 1),
            MedalId.StOlaf,
            ProviderLifetime.WhileEquipped,
            IsActive: 1);
        var action = new ReplacementAction(
            ReplacementActionKind.ModifyHp,
            new EntityId(1, 1),
            new EntityId(2, 1),
            StatId.Null,
            EffectId.Null,
            RuleDamageKind.Effect,
            Amount: -3,
            RuleValueFlags.None);

        Assert.False(writer.TryAppend(in action));
        writer.MarkHandled(provider);
        Assert.True(writer.TryAppend(in action));
        Assert.False(writer.TryAppend(in action));
        Assert.Equal(1, writer.BuildPlan().ActionCount);
        Assert.Equal(action, writer.Actions[0]);
    }

    [Fact]
    public void Warmed_precedence_state_and_replacement_operations_allocate_zero_bytes()
    {
        EntityId owner = new(20, 1);
        Span<ProviderCandidateResult> candidates = stackalloc ProviderCandidateResult[3]
        {
            Candidate(8, owner, MedalId.StGeorge, applies: true),
            Candidate(3, owner, MedalId.StOlaf, applies: true),
            Candidate(5, owner, MedalId.StChristopher, applies: true),
        };
        Span<ProviderCandidateResult> ordered = stackalloc ProviderCandidateResult[3];
        Span<ReplacementAction> actions = stackalloc ReplacementAction[1];
        var counterSpec = new MedalCounterSpec(
            2, 0, RuleResetPolicy.Never,
            MedalCounterProgression.IncrementToThreshold,
            MedalCounterConsumePolicy.ResetToZero);
        var state = default(MedalRuntimeState);
        EquipmentMedalStateRules.Initialize(ref state, in counterSpec, 1);
        RunHotPath(candidates, ordered, actions, owner, ref state, in counterSpec);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
            RunHotPath(candidates, ordered, actions, owner, ref state, in counterSpec);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void RunHotPath(
        ReadOnlySpan<ProviderCandidateResult> candidates,
        Span<ProviderCandidateResult> ordered,
        Span<ReplacementAction> actions,
        EntityId owner,
        ref MedalRuntimeState state,
        in MedalCounterSpec spec)
    {
        ProviderPrecedence.TrySelectFirst(candidates, owner, out ProviderCandidateResult selected);
        ProviderPrecedence.CollectApplicableInOrder(candidates, owner, ordered);
        EquipmentMedalStateRules.ObserveQualifyingTrigger(ref state, in spec);
        var writer = new ReplacementPlanWriter(actions);
        writer.MarkHandled(selected.Source);
        writer.BuildPlan();
    }

    private static ProviderCandidateResult Candidate(
        int entityIndex,
        EntityId owner,
        MedalId definition,
        bool applies,
        bool active = true) =>
        new(
            new ProviderSource(
                new EntityId(entityIndex, 1),
                owner,
                definition,
                ProviderLifetime.WhileEquipped,
                IsActive: active ? (byte)1 : (byte)0),
            Applies: applies ? (byte)1 : (byte)0);
}
