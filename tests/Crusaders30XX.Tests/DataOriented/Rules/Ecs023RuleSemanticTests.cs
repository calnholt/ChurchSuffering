#nullable enable

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rules;

public sealed class Ecs023RuleSemanticTests
{
    [Fact]
    public void Semantic_ids_have_frozen_explicit_values()
    {
        Assert.Equal((ushort)1, RuleStatIds.Courage.Value);
        Assert.Equal((ushort)19, RuleStatIds.MetaProgress.Value);
        Assert.Equal((ushort)1, RuleEffectIds.Burn.Value);
        Assert.Equal((ushort)41, RuleEffectIds.SwordIntoShield.Value);
        Assert.Equal((ushort)51, RuleEffectIds.CannotBlockCurrentAttack.Value);
        Assert.Equal((ushort)8, RuleTriggerIds.CardLifecycle.Value);
        Assert.Equal((ushort)23, RuleTriggerIds.EnemyAttackProgressOverride.Value);
        Assert.Equal((ushort)30, RuleTriggerIds.DefinitionLifecycle.Value);
        Assert.Equal((ushort)21, RuleFactIds.CourageLostThisBattle.Value);
        Assert.Equal((ushort)29, RuleFactIds.CandidateCount.Value);
        Assert.Equal((ushort)13, RuleHandlerIds.ResolveDelayedRule.Value);
        Assert.Equal((ushort)99, RuleHandlerIds.LegacyCharacterization.Value);
        Assert.Equal(22, RuleVisualEffectRecipeIds.Whirlwind.Value);
        Assert.True(RuleHandlerIds.IsKnown(RuleHandlerIds.ResolveEnemyAttack));
        Assert.True(RuleHandlerIds.IsKnown(RuleHandlerIds.ResolveEndTurnRequest));
        Assert.False(RuleHandlerIds.IsKnown(new RuleHandlerId(98)));
        Assert.True(RuleMetaResourceIds.IsKnown(RuleMetaResourceIds.RedClimbResource));
        Assert.True(RuleMetaResourceIds.IsKnown(RuleMetaResourceIds.WhiteClimbResource));
        Assert.True(RuleMetaResourceIds.IsKnown(RuleMetaResourceIds.BlackClimbResource));
    }

    [Fact]
    public void Every_typed_command_is_unmanaged_and_fits_the_fixed_payload()
    {
        AssertUnmanagedAndFits<ResourceDeltaRuleCommand>();
        AssertUnmanagedAndFits<EffectRuleCommand>();
        AssertUnmanagedAndFits<RemoveEffectRuleCommand>();
        AssertUnmanagedAndFits<CardZoneRuleCommand>();
        AssertUnmanagedAndFits<SpawnDefinitionRuleCommand>();
        AssertUnmanagedAndFits<PresentationRuleCommand>();
        AssertUnmanagedAndFits<CustomRuleCommand>();
        AssertUnmanagedAndFits<RequirementRuleCommand>();
        AssertUnmanagedAndFits<ResolvedValueRuleCommand>();
        AssertUnmanagedAndFits<ScheduledRuleCommand>();
        AssertUnmanagedAndFits<DerivedDamageRuleCommand>();
        AssertUnmanagedAndFits<CardMutationRuleCommand>();
        AssertUnmanagedAndFits<RejectionRuleCommand>();
        AssertUnmanagedAndFits<RandomCardZoneRuleCommand>();
        AssertUnmanagedAndFits<SpawnCardRuleCommand>();
        AssertUnmanagedAndFits<RemovePledgeRuleCommand>();
        AssertUnmanagedAndFits<MaximumRuleCommand>();
        AssertUnmanagedAndFits<MetaResourceRuleCommand>();
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleRandomState>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleFact>());
        Assert.Equal(96, Marshal.SizeOf<RuleCommandPayload>());
    }

    [Fact]
    public void Extended_command_vocabulary_round_trips_and_preserves_append_order()
    {
        var buffer = new RuleCommandBuffer(initialCapacity: 16);
        RuleCommandIndex damage = buffer.Writer.Append(RuleCommand.Damage(
            TargetHandle.Source,
            TargetHandle.PrimaryEnemy,
            7));
        RuleResultToken result = RuleCommand.ResultOf(damage);
        RuleResultToken handlerResult = RuleCommand.ResultOf(RuleFactIds.ResultValue, 0);
        buffer.Writer.Append(RuleCommand.SetRequirement(
            TargetHandle.PrimaryEnemy,
            RequirementKind.MinimumBlockers,
            amount: 2));
        buffer.Writer.Append(RuleCommand.SetResolvedValue(
            TargetHandle.PrimaryEnemy,
            ResolvedValueKind.EffectiveBlock,
            6));
        buffer.Writer.Append(RuleCommand.Schedule(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleTriggerIds.ActionPhaseEnd,
            RuleHandlerIds.ResolveDelayedRule,
            delayMilliseconds: 250,
            dependsOn: result));
        buffer.Writer.Append(RuleCommand.DamageFromResult(
            TargetHandle.Source,
            TargetHandle.PrimaryEnemy,
            result,
            numerator: 2,
            offset: 1));
        buffer.Writer.Append(RuleCommand.MutateCard(
            new EntityId(4, 2),
            CardMutationKind.ModifyBlock,
            amount: -2,
            flags: RuleValueFlags.QuestPersistent));
        buffer.Writer.Append(RuleCommand.Reject(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleRejectionReason.InsufficientStat,
            new StringId(17)));
        buffer.Writer.Append(RuleCommand.RandomCardZone(
            new EntityId(5, 3),
            CardZone.DiscardPile,
            CardZone.Hand,
            RandomCardZoneOperation.Resurrect,
            count: 2,
            filter: RuleCardFilter.ExcludePledged));
        buffer.Writer.Append(RuleCommand.SpawnCard(
            TargetHandle.Source,
            TargetHandle.Player,
            new EntityId(5, 3),
            CardId.Kunai,
            CardZone.Hand,
            RuleCardColor.Black,
            isUpgraded: true));
        buffer.Writer.Append(RuleCommand.RemovePledge(
            new EntityId(4, 2),
            TargetHandle.Player,
            CardZone.DiscardPile,
            PledgeRemovalReason.Played));
        buffer.Writer.Append(RuleCommand.ModifyMaxHealth(
            TargetHandle.Player,
            -3,
            MaxHealthChangeKind.Permanent));
        buffer.Writer.Append(RuleCommand.ModifyMaxHandSize(
            TargetHandle.Player,
            1,
            MaxHandSizeChangeKind.Permanent));
        buffer.Writer.Append(RuleCommand.ModifyMetaResource(
            TargetHandle.Player,
            RuleMetaResourceIds.Gold,
            MetaResourceChangeKind.Add,
            10));

        RuleCommandKind[] expected =
        [
            RuleCommandKind.Damage,
            RuleCommandKind.SetRequirement,
            RuleCommandKind.SetResolvedValue,
            RuleCommandKind.Schedule,
            RuleCommandKind.DamageFromResult,
            RuleCommandKind.MutateCard,
            RuleCommandKind.Reject,
            RuleCommandKind.RandomCardZone,
            RuleCommandKind.SpawnCard,
            RuleCommandKind.RemovePledge,
            RuleCommandKind.ModifyMaxHealth,
            RuleCommandKind.ModifyMaxHandSize,
            RuleCommandKind.ModifyMetaResource,
        ];
        Assert.Equal(expected.Length, buffer.Count);
        for (var index = 0; index < expected.Length; index++)
        {
            Assert.Equal(expected[index], buffer[index].Kind);
            Assert.Equal(index, buffer[index].Sequence);
        }

        Assert.Equal(2, buffer[1].Payload.Requirement.Amount);
        Assert.Equal(result, buffer[3].Payload.Scheduled.DependsOn);
        Assert.Equal(RuleFactIds.ResultValue, handlerResult.Result);
        Assert.Equal(1, handlerResult.Sequence);
        Assert.Equal(2, buffer[4].Payload.DerivedDamage.Numerator);
        Assert.Equal(RuleCardColor.Black, buffer[8].Payload.SpawnCard.Color);
        Assert.True(buffer[8].Payload.SpawnCard.IsUpgraded);
        Assert.Equal(RuleMetaResourceIds.Gold, buffer[12].Payload.MetaResource.Resource);
    }

    [Fact]
    public void Remove_effect_has_explicit_remove_all_semantics()
    {
        RuleCommand all = RuleCommand.RemoveEffect(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleEffectIds.Burn);
        RuleCommand two = RuleCommand.RemoveEffect(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleEffectIds.Burn,
            stackCount: 2);

        Assert.Equal(RemoveEffectRuleCommand.AllStacks, all.Payload.RemoveEffect.StackCount);
        Assert.Equal(2, two.Payload.RemoveEffect.StackCount);
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleCommand.RemoveEffect(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleEffectIds.Burn,
            stackCount: 0));
    }

    [Fact]
    public void Custom_and_scheduled_routes_reject_unfrozen_handler_ids()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleCommand.Custom(
            TargetHandle.Source,
            TargetHandle.Player,
            new RuleHandlerId(98)));
        Assert.Throws<ArgumentOutOfRangeException>(() => RuleCommand.Schedule(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleTriggerIds.ActionPhaseEnd,
            new RuleHandlerId(98)));

        RuleCommand valid = RuleCommand.Custom(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleHandlerIds.ResolveReplacementEffect);
        Assert.Equal(RuleHandlerIds.ResolveReplacementEffect, valid.Payload.Custom.Handler);
    }

    [Fact]
    public void Fact_reader_requires_sorted_unique_ids_and_finds_values_without_allocation()
    {
        RuleFact[] facts =
        [
            new(RuleFactIds.Phase, 2),
            new(RuleFactIds.Turn, 7),
            new(RuleFactIds.DamageDealt, 9),
        ];
        var reader = new RuleFactReader(facts);

        Assert.Equal(3, reader.Count);
        Assert.True(reader.TryGet(RuleFactIds.Turn, out int turn));
        Assert.Equal(7, turn);
        Assert.Equal(9, reader.GetRequired(RuleFactIds.DamageDealt));
        Assert.Equal(-1, reader.GetOrDefault(RuleFactIds.Channel, -1));
        Assert.False(reader.Contains(RuleFactIds.Channel));
        Assert.Throws<ArgumentException>(CreateUnsortedFactReader);
        Assert.Throws<ArgumentException>(CreateDuplicateFactReader);

        _ = reader.TryGet(RuleFactIds.Turn, out _);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = reader.TryGet(RuleFactIds.DamageDealt, out _);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    [Fact]
    public void Caller_owned_random_stream_has_frozen_sequence_and_deterministic_shuffle()
    {
        RuleRandomState state = RuleRandomState.FromSeed(123456789UL);
        var random = new DeterministicRuleRandom(ref state);

        Assert.Equal(17131907776045769687UL, random.NextUInt64());
        Assert.Equal(9120621550721899595UL, random.NextUInt64());
        Assert.Equal(5237368999691878260UL, random.NextUInt64());
        Assert.Equal(2352804886863130741UL, random.NextUInt64());
        Assert.Equal(11490144281350267834UL, random.NextUInt64());

        RuleRandomState firstState = RuleRandomState.FromSeed(42);
        RuleRandomState secondState = RuleRandomState.FromSeed(42);
        Span<int> first = stackalloc int[] { 1, 2, 3, 4, 5, 6 };
        Span<int> second = stackalloc int[] { 1, 2, 3, 4, 5, 6 };
        var firstRandom = new DeterministicRuleRandom(ref firstState);
        var secondRandom = new DeterministicRuleRandom(ref secondState);
        firstRandom.Shuffle(first);
        secondRandom.Shuffle(second);

        Assert.True(first.SequenceEqual(second));
        Assert.Equal(firstState, secondState);
        Assert.NotEqual(RuleRandomState.FromSeed(42), firstState);

        _ = firstRandom.NextInt(100);
        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 1_000; iteration++)
        {
            _ = firstRandom.NextInt(100);
        }

        Assert.Equal(0, GC.GetAllocatedBytesForCurrentThread() - before);
    }

    private static void AssertUnmanagedAndFits<T>() where T : unmanaged
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<T>());
        Assert.InRange(Marshal.SizeOf<T>(), 1, 96);
    }

    private static void CreateUnsortedFactReader()
    {
        RuleFact[] facts =
        [
            new(RuleFactIds.Turn, 1),
            new(RuleFactIds.Phase, 1),
        ];
        _ = new RuleFactReader(facts);
    }

    private static void CreateDuplicateFactReader()
    {
        RuleFact[] facts =
        [
            new(RuleFactIds.Turn, 1),
            new(RuleFactIds.Turn, 2),
        ];
        _ = new RuleFactReader(facts);
    }
}
