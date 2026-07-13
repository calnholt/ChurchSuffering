#nullable enable

using System;
using System.Runtime.CompilerServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rules;

public sealed class Ecs023HandlerContextTests
{
    [Fact]
    public void Handler_inputs_results_and_planning_memory_are_unmanaged()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<CardHandlerInput>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EnemyHandlerInput>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EquipmentHandlerInput>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<MedalHandlerInput>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleHandlerResult>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleResultWriterState>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EnemyPlanningMemory>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EnemyPlanWriterState>());
    }

    [Fact]
    public void Card_context_exposes_snapshots_spans_facts_results_random_and_commands()
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer(initialCapacity: 2);
        RuleTriggerEnvelope trigger = Trigger(RuleTriggerIds.CardValidate);
        var input = CardInput(trigger);
        RuleFact[] facts =
        [
            new(RuleFactIds.Phase, (int)RulePhase.Action),
            new(RuleFactIds.Turn, 4),
        ];
        EntityId[] paymentCards = [new(20, 1), new(21, 1)];
        EntityId[] targets = [new(30, 1), new(31, 1)];
        var resultStorage = new RuleHandlerResult[3];
        var resultState = new RuleResultWriterState();
        RuleRandomState randomState = RuleRandomState.FromSeed(1234);
        var context = new CardHandlerContext(
            world.AsReadOnly(),
            commands.Writer,
            in input,
            facts,
            paymentCards,
            targets,
            resultStorage,
            ref resultState,
            ref randomState);

        context.Results.Reject(new StringId(77));
        context.Results.Record(RuleFactIds.ResultValue, 12, auxiliaryValue: 3);
        ulong beforeRandom = randomState.Value;
        _ = context.Random.NextInt(9);
        context.Append(RuleCommand.Damage(TargetHandle.Source, input.PrimaryTarget, 8));

        Assert.Equal(RuleTriggerIds.CardValidate, context.Stage);
        Assert.True(context.IsUpgraded);
        Assert.Equal(2, context.PaymentCards.Length);
        Assert.Equal(targets, context.CandidateTargets.ToArray());
        Assert.Equal(4, context.Facts.GetRequired(RuleFactIds.Turn));
        Assert.Equal(RuleValidationDecision.Rejected, context.Results.Validation);
        Assert.Equal(2, context.Results.Count);
        Assert.Equal(0, context.Results[0].Sequence);
        Assert.Equal(new StringId(77), context.Results[0].RejectionReason);
        Assert.Equal(12, context.Results[1].Value);
        Assert.NotEqual(beforeRandom, randomState.Value);
        Assert.Equal(RuleCommandKind.Damage, commands[0].Kind);
    }

    [Fact]
    public void Result_writer_has_shared_copy_state_and_deterministic_capacity_failure()
    {
        var storage = new RuleHandlerResult[1];
        var state = new RuleResultWriterState();
        var first = new RuleResultWriter(storage, ref state);
        RuleResultWriter second = first;

        first.Allow();

        Assert.Equal(1, second.Count);
        Assert.Equal(RuleValidationDecision.Allowed, second.Validation);
        Assert.False(second.TryAppend(second[0], out int rejectedIndex));
        Assert.Equal(-1, rejectedIndex);
        bool capacityThrew = false;
        try
        {
            second.Record(RuleFactIds.ResultValue, 1);
        }
        catch (InvalidOperationException)
        {
            capacityThrew = true;
        }

        Assert.True(capacityThrew);
    }

    [Fact]
    public void Enemy_context_writes_bounded_plan_and_persists_memory()
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer(initialCapacity: 1);
        var initialMemory = new EnemyPlanningMemory { Phase = 2, PhaseTurn = 3 };
        var input = new EnemyHandlerInput(
            new RuleInvocationId(9),
            new EntityId(40, 1),
            EnemyId.Skeleton,
            EnemyAttackId.BoneStrike,
            Trigger(RuleTriggerIds.DefinitionLifecycle),
            EnemyHandlerFlags.Planning,
            RulePhase.EnemyStart,
            Turn: 5,
            BattleEpoch: 7,
            new EnemyCombatSnapshot(6, 8, 0, 0, 0, 0, 0, 0, 0),
            initialMemory,
            TargetHandle.Player);
        RuleFact[] facts = [new(RuleFactIds.Turn, 5)];
        var results = new RuleHandlerResult[1];
        var attacks = new EnemyAttackId[2];
        var persistedMemory = default(EnemyPlanningMemory);
        var resultState = new RuleResultWriterState();
        var planState = new EnemyPlanWriterState();
        RuleRandomState randomState = RuleRandomState.FromSeed(22);
        var context = new EnemyHandlerContext(
            world.AsReadOnly(),
            commands.Writer,
            in input,
            facts,
            ReadOnlySpan<EntityId>.Empty,
            results,
            attacks,
            ref persistedMemory,
            ref resultState,
            ref planState,
            ref randomState);

        context.Plan.Append(EnemyAttackId.BoneStrike);
        context.Plan.Append(EnemyAttackId.Sweep);
        context.Plan.Remember(EnemyAttackId.Sweep);

        Assert.Equal(2, context.Plan.Count);
        Assert.Equal(EnemyAttackId.BoneStrike, context.Plan[0]);
        Assert.Equal(EnemyAttackId.Sweep, context.Plan[1]);
        Assert.True(persistedMemory.HasLastAttack);
        Assert.Equal(EnemyAttackId.Sweep, persistedMemory.LastAttack);
        Assert.Equal(1, persistedMemory.PlansGenerated);
        Assert.False(context.Plan.TryAppend(EnemyAttackId.Calcify));
        bool capacityThrew = false;
        try
        {
            context.Plan.Append(EnemyAttackId.Calcify);
        }
        catch (InvalidOperationException)
        {
            capacityThrew = true;
        }

        Assert.True(capacityThrew);
    }

    [Fact]
    public void Equipment_and_medal_contexts_carry_typed_trigger_and_runtime_state()
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer();
        var equipmentInput = new EquipmentHandlerInput(
            new RuleInvocationId(10),
            new EntityId(50, 1),
            new EntityId(51, 1),
            EquipmentId.WhetstoneGauntlets,
            Trigger(RuleTriggerIds.EquipmentActivated),
            EquipmentHandlerFlags.Equipped | EquipmentHandlerFlags.Activated,
            RulePhase.Action,
            new CombatResourceSnapshot(3, 4, 1, 2, 0, 0, 0),
            new DeckStateSnapshot(new EntityId(52, 1), EntityId.Null, 4, 3, 2, 1),
            new EquipmentUsageState { BattleEpoch = 7, Uses = 1, Initialized = 1 },
            TargetHandle.PrimaryEnemy);
        var medalInput = new MedalHandlerInput(
            new RuleInvocationId(11),
            new EntityId(60, 1),
            new EntityId(51, 1),
            MedalId.StGeorge,
            Trigger(RuleTriggerIds.MedalReactive),
            MedalHandlerFlags.Acquired,
            RulePhase.Action,
            equipmentInput.OwnerResources,
            equipmentInput.Deck,
            new MedalRuntimeState
            {
                BattleEpoch = 7,
                Count = 2,
                Flags = MedalRuntimeFlags.Initialized,
            },
            TargetHandle.Player);
        var equipmentResults = new RuleHandlerResult[1];
        var medalResults = new RuleHandlerResult[1];
        var equipmentResultState = new RuleResultWriterState();
        var medalResultState = new RuleResultWriterState();
        RuleRandomState equipmentRandom = RuleRandomState.FromSeed(1);
        RuleRandomState medalRandom = RuleRandomState.FromSeed(2);
        var equipment = new EquipmentHandlerContext(
            world.AsReadOnly(), commands.Writer, in equipmentInput,
            ReadOnlySpan<RuleFact>.Empty, ReadOnlySpan<EntityId>.Empty,
            equipmentResults, ref equipmentResultState, ref equipmentRandom);
        var medal = new MedalHandlerContext(
            world.AsReadOnly(), commands.Writer, in medalInput,
            ReadOnlySpan<RuleFact>.Empty, ReadOnlySpan<EntityId>.Empty,
            medalResults, ref medalResultState, ref medalRandom);

        equipment.Results.Allow();
        medal.Results.Record(RuleFactIds.ResultValue, medal.Input.State.Count);

        Assert.True(equipment.Input.IsEquipped);
        Assert.Equal((ushort)1, equipment.Input.State.Uses);
        Assert.Equal(RuleTriggerIds.EquipmentActivated, equipment.Trigger);
        Assert.True(medal.Input.IsAcquired);
        Assert.Equal((ushort)2, medal.Input.State.Count);
        Assert.Equal(1, equipment.Results.Count);
        Assert.Equal(1, medal.Results.Count);
    }

    [Fact]
    public void Warmed_context_construction_writer_use_and_random_advance_allocate_zero_bytes()
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer(initialCapacity: 1);
        CardHandlerInput input = CardInput(Trigger(RuleTriggerIds.CardResolvePlay));
        RuleFact[] facts = [new(RuleFactIds.Turn, 1)];
        var resultStorage = new RuleHandlerResult[1];
        var resultState = new RuleResultWriterState();
        RuleRandomState randomState = RuleRandomState.FromSeed(123);
        InvokeCard(world.AsReadOnly(), commands, in input, facts, resultStorage, ref resultState, ref randomState);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (int iteration = 0; iteration < 100; iteration++)
        {
            InvokeCard(world.AsReadOnly(), commands, in input, facts, resultStorage, ref resultState, ref randomState);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void InvokeCard(
        ReadOnlyWorld world,
        RuleCommandBuffer commands,
        in CardHandlerInput input,
        ReadOnlySpan<RuleFact> facts,
        Span<RuleHandlerResult> results,
        ref RuleResultWriterState resultState,
        ref RuleRandomState randomState)
    {
        commands.Clear();
        var context = new CardHandlerContext(
            world,
            commands.Writer,
            in input,
            facts,
            ReadOnlySpan<EntityId>.Empty,
            ReadOnlySpan<EntityId>.Empty,
            results,
            ref resultState,
            ref randomState);
        context.Results.Record(RuleFactIds.ResultValue, context.Random.NextInt(10));
        context.Append(RuleCommand.Damage(TargetHandle.Source, context.PrimaryTarget, 3));
    }

    private static CardHandlerInput CardInput(RuleTriggerEnvelope trigger) => new(
        new RuleInvocationId(8),
        new EntityId(10, 1),
        new EntityId(11, 1),
        CardId.Strike,
        trigger,
        CardHandlerFlags.Upgraded | CardHandlerFlags.FirstPlayThisBattle,
        new CardPhaseSnapshot(RulePhase.Action, Turn: 4, BattleEpoch: 7, ActionSequence: 2),
        new CardPaymentSnapshot(2, 1, 0, 1, 0, 1),
        new CombatResourceSnapshot(5, 4, 1, 2, 0, 1, 0),
        new CardBattleSnapshot(2, 4, 3, 1, 2, 1, 0),
        new DeckStateSnapshot(new EntityId(12, 1), new EntityId(13, 1), 8, 4, 3, 1),
        DerivedDamage: 9,
        ResolvedDamage: 7,
        TargetHandle.PrimaryEnemy);

    private static RuleTriggerEnvelope Trigger(TriggerId id) => new(
        RuleTriggerKind.Card,
        id,
        default);

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.Seal();
        return new World(registry);
    }
}
