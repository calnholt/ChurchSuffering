#nullable enable

using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Rules;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Rules;

public sealed class Ecs022RuleContractTests
{
    [Fact]
    public void Rule_commands_and_declarative_primitives_are_unmanaged()
    {
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleCommand>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<RuleCommandPayload>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<EffectSpec>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<ConditionSpec>());
        Assert.False(RuntimeHelpers.IsReferenceOrContainsReferences<TargetHandle>());
        Assert.Equal(96, Marshal.SizeOf<RuleCommandPayload>());
    }

    [Fact]
    public void Rule_command_round_trips_through_raw_in_memory_representation()
    {
        var condition = new ConditionSpec(
            new ConditionId(7),
            ComparisonOperator.GreaterThanOrEqual,
            TargetHandle.Player,
            new StatId(3),
            12,
            RuleValueFlags.Upgraded);
        var effect = new EffectSpec(
            new EffectId(11),
            Magnitude: 9,
            Duration: 2,
            Condition: condition,
            Flags: RuleValueFlags.Unpreventable);
        var commands = new RuleCommandBuffer();
        commands.Writer.Append(RuleCommand.ApplyEffect(
            TargetHandle.Source,
            TargetHandle.ForEntity(new EntityId(42, 3)),
            in effect));
        RuleCommand recorded = commands[0];
        var bytes = new byte[Marshal.SizeOf<RuleCommand>()];

        MemoryMarshal.Write(bytes.AsSpan(), in recorded);
        RuleCommand restored = MemoryMarshal.Read<RuleCommand>(bytes);

        Assert.Equal(RuleCommandKind.ApplyEffect, restored.Kind);
        Assert.Equal(0, restored.Sequence);
        Assert.Equal(new EffectId(11), restored.Payload.Effect.Effect.Id);
        Assert.Equal(9, restored.Payload.Effect.Effect.Magnitude);
        Assert.Equal(condition, restored.Payload.Effect.Effect.Condition);
        Assert.Equal(new EntityId(42, 3), restored.Payload.Effect.Target.Entity);
    }

    [Fact]
    public void Append_writer_preserves_order_and_stamps_deterministic_sequences()
    {
        var commands = new RuleCommandBuffer(initialCapacity: 4);
        RuleCommandWriter writer = commands.Writer;
        RuleCommandIndex first = writer.Append(RuleCommand.Damage(
            TargetHandle.Source,
            TargetHandle.PrimaryEnemy,
            8));
        RuleCommandIndex second = writer.Append(RuleCommand.Heal(
            TargetHandle.Source,
            TargetHandle.Player,
            3));
        RuleCommandIndex third = writer.Append(RuleCommand.Custom(
            TargetHandle.Source,
            TargetHandle.Player,
            RuleHandlerIds.LegacyCharacterization));

        Assert.Equal(RuleCommandKind.Damage, commands[0].Kind);
        Assert.Equal(RuleCommandKind.Heal, commands[1].Kind);
        Assert.Equal(RuleCommandKind.Custom, commands[2].Kind);
        Assert.Equal(0, commands[0].Sequence);
        Assert.Equal(1, commands[1].Sequence);
        Assert.Equal(2, commands[2].Sequence);
        Assert.Equal(0, first.Index);
        Assert.Equal(1, second.Index);
        Assert.Equal(2, third.Index);
        Assert.Equal(first.Version, third.Version);

        int oldVersion = commands.Version;
        commands.Clear();
        RuleCommandIndex afterClear = commands.Writer.Append(RuleCommand.GainBlock(
            TargetHandle.Source,
            TargetHandle.Player,
            4));
        Assert.NotEqual(oldVersion, afterClear.Version);
        Assert.Equal(0, commands[0].Sequence);
    }

    [Fact]
    public void Read_only_world_returns_copies_and_exposes_no_direct_write_surface()
    {
        World world = CreateWorld();
        var bundle = new SpawnBundle(1);
        bundle.Add(new TestComponent { Value = 5 });
        EntityId entity = world.Create(in bundle);
        var handle = world.CreateDynamicBuffer<int>(entity, initialCapacity: 2);
        world.GetDynamicBuffer(handle).Add(10);
        ReadOnlyWorld readOnly = world.AsReadOnly();

        TestComponent copy = readOnly.Get<TestComponent>(entity);
        copy.Value = 99;
        ReadOnlyDynamicBuffer<int> buffer = readOnly.GetDynamicBuffer(handle);

        Assert.Equal(5, world.Get<TestComponent>(entity).Value);
        Assert.Equal(10, buffer[0]);
        Assert.Equal([10], buffer.AsReadOnlySpan().ToArray());
        AssertNoPublicMethod(nameof(World.Create));
        AssertNoPublicMethod(nameof(World.Destroy));
        AssertNoPublicMethod(nameof(World.Set));
        AssertNoPublicMethod(nameof(World.Add));
        AssertNoPublicMethod(nameof(World.Remove));
        AssertNoPublicMethod(nameof(World.Enable));
        AssertNoPublicMethod(nameof(World.Disable));
        AssertNoPublicMethod(nameof(World.Query));
        AssertNoPublicMethod(nameof(World.GetDynamicBufferMutationHandler));
        Assert.Null(typeof(ReadOnlyWorld).GetProperty("World", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Domain_handler_contexts_carry_stable_ids_targets_and_append_only_writer()
    {
        World world = CreateWorld();
        var commands = new RuleCommandBuffer();
        ReadOnlyWorld readOnly = world.AsReadOnly();
        var invocation = new RuleInvocationId(44);
        var source = new EntityId(10, 2);
        var owner = new EntityId(11, 3);
        TargetHandle target = TargetHandle.ForEntity(new EntityId(12, 4));
        var card = new CardPlayContext(
            readOnly,
            commands.Writer,
            invocation,
            source,
            owner,
            CardId.Strike,
            isUpgraded: true,
            primaryTarget: target);
        var enemy = new EnemyAttackContext(
            readOnly,
            commands.Writer,
            invocation,
            source,
            EnemyId.Skeleton,
            EnemyAttackId.BoneStrike,
            phase: 2,
            target: target);
        var equipment = new EquipmentHandlerContext(
            readOnly,
            commands.Writer,
            invocation,
            source,
            owner,
            EquipmentId.WhetstoneGauntlets,
            new TriggerId(3),
            target: target);
        var medal = new MedalHandlerContext(
            readOnly,
            commands.Writer,
            invocation,
            source,
            owner,
            MedalId.StGeorge,
            new TriggerId(4),
            target: target);

        Assert.Equal(CardId.Strike, card.Definition);
        Assert.True(card.IsUpgraded);
        Assert.Equal(EnemyAttackId.BoneStrike, enemy.AttackDefinition);
        Assert.Equal(EquipmentId.WhetstoneGauntlets, equipment.Definition);
        Assert.Equal(MedalId.StGeorge, medal.Definition);
        Assert.Equal(target, card.PrimaryTarget);
        Assert.Equal(target, enemy.Target);
        Assert.Equal(invocation, medal.Invocation);
    }

    [Fact]
    public void Warmed_static_handler_invocation_and_append_allocate_zero_bytes()
    {
        World world = CreateWorld();
        var bundle = new SpawnBundle(1);
        bundle.Add(new TestComponent { Value = 7 });
        EntityId card = world.Create(in bundle);
        var commands = new RuleCommandBuffer(initialCapacity: 4);
        InvokeCardHandler(world.AsReadOnly(), commands.Writer, card);
        commands.Clear();

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var iteration = 0; iteration < 100; iteration++)
        {
            var context = new CardPlayContext(
                world.AsReadOnly(),
                commands.Writer,
                new RuleInvocationId(iteration + 1),
                card,
                new EntityId(2, 1),
                CardId.Strike,
                isUpgraded: false,
                primaryTarget: TargetHandle.PrimaryEnemy);
            BuildCardCommands(ref context);
            commands.Clear();
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static void InvokeCardHandler(
        ReadOnlyWorld world,
        RuleCommandWriter writer,
        EntityId card)
    {
        var context = new CardPlayContext(
            world,
            writer,
            new RuleInvocationId(1),
            card,
            new EntityId(2, 1),
            CardId.Strike,
            isUpgraded: false,
            primaryTarget: TargetHandle.PrimaryEnemy);
        BuildCardCommands(ref context);
    }

    private static void BuildCardCommands(ref CardPlayContext context)
    {
        int amount = context.World.Get<TestComponent>(context.Card).Value;
        context.Append(RuleCommand.Damage(
            TargetHandle.ForEntity(context.Card),
            context.PrimaryTarget,
            amount));
        context.Append(RuleCommand.Present(
            TargetHandle.ForEntity(context.Card),
            context.PrimaryTarget,
            new PresentationSpec(
                new VisualEffectRecipeId(1),
                new SoundId(2),
                0,
                RuleValueFlags.None)));
    }

    private static void AssertNoPublicMethod(string name)
    {
        MethodInfo? method = typeof(ReadOnlyWorld).GetMethod(name, BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(method);
    }

    private static World CreateWorld()
    {
        var registry = new ComponentTypeRegistry();
        registry.RegisterComponent<TestComponent>(470);
        registry.Seal();
        return new World(registry);
    }

    private struct TestComponent : IComponent
    {
        public int Value;
    }
}
