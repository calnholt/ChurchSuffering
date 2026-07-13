#nullable enable

using System;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Combat;

public sealed class CombatRuntimeTests
{
    [Fact]
    public void Battle_start_applies_enemy_lifecycle_and_plans_first_intent()
    {
        CombatSession session = Create(EnemyId.FireSkeleton);

        session.Process();

        Assert.Equal(CombatPhase.Block, session.World.Get<PhaseState>(session.Battle).Current);
        Assert.NotEqual(default, session.World.Get<AttackIntent>(session.Battle).Current);
        Assert.Equal(2, session.GetPassiveStacks(session.Enemy, RuleEffectIds.Armor));
        Assert.Equal(2, session.GetPassiveStacks(session.Player, RuleEffectIds.Enflamed));
        Assert.NotNull(session.EventHub);
        Assert.True(session.World.Events.LastBarrierEventCount > 0);
    }

    [Fact]
    public void Exact_block_requirement_rejects_confirmation_until_the_shape_is_valid()
    {
        CombatSession session = Create(EnemyId.SandGolem);
        session.Process();
        Assert.Equal(EnemyAttackId.SandPound, session.World.Get<EnemyAttackProgress>(session.Battle).Attack);

        session.ConfirmBlocks();
        session.Process();
        Assert.True((session.World.Get<BattleStateInfo>(session.Battle).Flags & CombatFlags.AwaitingBlockConfirmation) != 0);

        EntityId card = CreateBlockCard(session.World);
        Assert.True(session.AssignBlock(card, block: 6, RuleCardColor.Black));
        session.ConfirmBlocks();
        Assert.True(session.Process().IsWaiting);
        session.Process();

        Assert.Equal(CombatPhase.Action, session.World.Get<PhaseState>(session.Battle).Current);
        Assert.Equal(session.World.Get<HP>(session.Player).Max, session.World.Get<HP>(session.Player).Current);
        Assert.Equal(1, session.World.Get<EnemyAttackProgress>(session.Battle).AssignedBlockerCount);
        Assert.Equal(7, session.World.Get<EnemyAttackProgress>(session.Battle).AssignedBlock);
    }

    [Fact]
    public void Enemy_attack_resolution_resumes_after_a_multi_frame_presentation_wait()
    {
        CombatSession session = Create(EnemyId.TrainingDemon);
        session.Process();
        session.ConfirmBlocks();

        var waiting = session.Process();
        Assert.True(waiting.IsWaiting);
        Assert.Equal(CombatRuleKind.PresentEnemyAttack, (CombatRuleKind)waiting.WaitingRuleType.Value);
        Assert.Equal(30, session.World.Get<HP>(session.Player).Current);

        var completed = session.Process();
        Assert.False(completed.IsWaiting);
        Assert.Equal(21, session.World.Get<HP>(session.Player).Current);
        Assert.Equal(CombatPhase.Action, session.World.Get<PhaseState>(session.Battle).Current);
    }

    [Fact]
    public void Armor_guard_and_aegis_prevent_damage_in_legacy_order()
    {
        CombatSession session = Create(EnemyId.TrainingDemon);
        session.GrantPassive(session.Player, RuleEffectIds.Armor, 2);
        session.GrantPassive(session.Player, RuleEffectIds.Guard, 1);
        session.GrantPassive(session.Player, RuleEffectIds.Aegis, 3);
        session.Process();
        session.ConfirmBlocks();
        session.Process();
        session.Process();

        Assert.Equal(27, session.World.Get<HP>(session.Player).Current);
        Assert.Equal(2, session.GetPassiveStacks(session.Player, RuleEffectIds.Armor));
        Assert.Equal(0, session.GetPassiveStacks(session.Player, RuleEffectIds.Guard));
        Assert.Equal(0, session.GetPassiveStacks(session.Player, RuleEffectIds.Aegis));
    }

    [Fact]
    public void Block_colors_grant_resources_and_bleed_triggers_per_qualifying_color()
    {
        CombatSession session = Create(EnemyId.TrainingDemon, playerHealth: 40);
        session.GrantPassive(session.Player, RuleEffectIds.Bleed, 3);
        session.Process();
        session.AssignBlock(CreateBlockCard(session.World), 1, RuleCardColor.Red);
        session.AssignBlock(CreateBlockCard(session.World), 1, RuleCardColor.Red);
        session.AssignBlock(CreateBlockCard(session.World), 1, RuleCardColor.White);
        session.AssignBlock(CreateBlockCard(session.World), 1, RuleCardColor.White);
        session.ConfirmBlocks();
        session.Process();
        session.Process();

        Assert.Equal(2, session.World.Get<Courage>(session.Player).Amount);
        Assert.Equal(2, session.World.Get<Temperance>(session.Player).Amount);
        Assert.Equal(1, session.GetPassiveStacks(session.Player, RuleEffectIds.Bleed));
        Assert.Equal(33, session.World.Get<HP>(session.Player).Current);
    }

    [Fact]
    public void Ambush_expiry_forces_resolution_and_fear_makes_every_attack_an_ambush()
    {
        CombatSession session = Create(EnemyId.TrainingDemon);
        session.GrantPassive(session.Player, RuleEffectIds.Fear, 1);
        session.Process();
        Assert.Equal(1, session.World.Get<AmbushState>(session.Battle).Active);

        session.TickAmbush(20_000);
        Assert.Equal(1, session.World.Get<AmbushState>(session.Battle).Expired);
        Assert.True(session.Process().IsWaiting);
        session.Process();
        Assert.Equal(CombatPhase.Action, session.World.Get<PhaseState>(session.Battle).Current);
    }

    [Fact]
    public void Fallen_shepherd_lethal_damage_advances_phase_and_resets_phase_state()
    {
        CombatSession session = Create(EnemyId.FallenShepherd);
        session.Process();

        session.DamageEnemy(100);
        Assert.True(session.Process().IsWaiting);

        ref Enemy enemy = ref session.World.Get<Enemy>(session.Enemy);
        Assert.Equal(2, enemy.Phase);
        Assert.Equal(1, enemy.PhaseTurn);
        Assert.Equal(session.World.Get<HP>(session.Enemy).Max, session.World.Get<HP>(session.Enemy).Current);
        Assert.Equal(CombatPhase.PhaseTransition, session.World.Get<PhaseState>(session.Battle).Current);

        session.Process();
        Assert.Equal(EnemyAttackId.FallenShepherdPhase2, session.World.Get<EnemyAttackProgress>(session.Battle).Attack);
    }

    [Fact]
    public void Final_enemy_and_player_lethal_damage_take_distinct_defeat_flows()
    {
        CombatSession victory = Create(EnemyId.TrainingDemon);
        victory.Process();
        victory.DamageEnemy(100);
        victory.Process();
        Assert.Equal(CombatPhase.Victory, victory.World.Get<PhaseState>(victory.Battle).Current);

        CombatSession defeat = Create(EnemyId.TrainingDemon, playerHealth: 5);
        defeat.Process();
        defeat.ConfirmBlocks();
        defeat.Process();
        defeat.Process();
        Assert.Equal(CombatPhase.Defeat, defeat.World.Get<PhaseState>(defeat.Battle).Current);
    }

    [Fact]
    public void Full_fight_trace_is_deterministic_and_preserves_attack_order()
    {
        CombatTraceEntry[] first = RunTrainingFight(seed: 77);
        CombatTraceEntry[] second = RunTrainingFight(seed: 77);

        Assert.Equal(first, second);
        Assert.Equal(3, first.Count(entry => entry.Rule == CombatRuleKind.ResolveEnemyImpact));
        Assert.All(first.Where(entry => entry.Rule == CombatRuleKind.BeginEnemyAttack), entry =>
            Assert.Equal(EnemyAttackId.TrainingStrike, entry.Attack));
        Assert.Equal(CombatRuleKind.CompleteVictory, first[^1].Rule);
    }

    [Fact]
    public void Operational_composition_registers_only_non_noop_systems_with_access_metadata()
    {
        CombatSession session = Create(EnemyId.TrainingDemon);
        IGameSystem[] systems = CombatGameplayComposition.Create(session).Systems.ToArray();

        Assert.Equal(2, systems.Length);
        Assert.Equal(2, systems.Select(system => system.Descriptor.Id).Distinct().Count());
        Assert.All(systems, system => Assert.Equal(SceneGroup.Battle, system.Descriptor.SceneGroup));
        Assert.All(systems, system => Assert.Equal(system.GetType(), system.GetType()
            .GetMethod(nameof(IGameSystem.Update))!.DeclaringType));
        Assert.All(systems, system => Assert.False(
            system.Descriptor.ReadComponents.IsEmpty &&
            system.Descriptor.WriteComponents.IsEmpty &&
            system.Descriptor.ReadDynamicBufferTypes.IsEmpty &&
            system.Descriptor.WriteDynamicBufferTypes.IsEmpty));
        Assert.True(systems.OfType<AttackResolutionSystem>().Single().Descriptor.RecordsStructuralCommands);
        Assert.NotEmpty(systems.OfType<AttackResolutionSystem>().Single().Descriptor.EmittedEventTypeIds.ToArray());
    }

    [Fact]
    public void Scheduled_composition_runs_the_same_deterministic_phase_block_and_damage_trace()
    {
        CombatSession first = Create(EnemyId.TrainingDemon, playerHealth: 40, seed: 19);
        CombatSession second = Create(EnemyId.TrainingDemon, playerHealth: 40, seed: 19);

        RunScheduledOpening(first);
        RunScheduledOpening(second);

        Assert.Equal(first.Trace.ToArray(), second.Trace.ToArray());
        Assert.Contains(first.Trace.ToArray(), entry => entry.Rule == CombatRuleKind.ConfirmBlocks);
        Assert.Contains(first.Trace.ToArray(), entry => entry.Rule == CombatRuleKind.ResolveEnemyImpact);
        Assert.Equal(31, first.World.Get<HP>(first.Player).Current);
    }

    [Fact]
    public void Warmed_block_progress_loop_allocates_zero_bytes()
    {
        CombatSession session = Create(EnemyId.TrainingDemon);
        session.Process();
        for (var index = 0; index < 16; index++)
            session.AssignBlock(CreateBlockCard(session.World), 1, (RuleCardColor)(index % 3 + 1));
        CombatSession.RecalculateBlocks(session.World, session.Battle);

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 1_000; index++)
            CombatSession.RecalculateBlocks(session.World, session.Battle);
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    private static CombatTraceEntry[] RunTrainingFight(ulong seed)
    {
        CombatSession session = Create(EnemyId.TrainingDemon, playerHealth: 100, seed: seed);
        session.Process();
        while (session.World.Get<PhaseState>(session.Battle).Current != CombatPhase.Victory)
        {
            session.ConfirmBlocks();
            session.Process();
            session.Process();
            session.DamageEnemy(10);
            if ((session.World.Get<BattleStateInfo>(session.Battle).Flags & CombatFlags.EnemyDefeated) != 0)
            {
                session.Process();
                break;
            }
            session.EndActionPhase();
            session.Process();
        }
        return session.Trace.ToArray();
    }

    private static CombatSession Create(EnemyId id, int playerHealth = 30, ulong seed = 1)
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var hub = new CombatEventHub();
        var owned = new CombatOwnedEventConsumers(world);
        var runtime = new EventRuntime(new EventRoutingEndpoint(hub.BuildRoutes(owned.RegisterRoutes())));
        world.AttachEventRuntime(runtime);
        CombatSession session = CombatSession.Create(world, hub, id, playerHealth, seed);
        owned.Bind(session);
        return session;
    }

    private static void RunScheduledOpening(CombatSession session)
    {
        var scheduler = new SystemScheduler(session.World, session.World.Events) { ActiveScene = SceneGroup.Battle };
        foreach (IGameSystem system in CombatGameplayComposition.Create(session).Systems)
            scheduler.Register(system);
        scheduler.Build();
        scheduler.Update(TimeSpan.FromMilliseconds(16));
        session.ConfirmBlocks();
        scheduler.Update(TimeSpan.FromMilliseconds(16));
        scheduler.Update(TimeSpan.FromMilliseconds(16));
    }

    private static EntityId CreateBlockCard(World world)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new ModifiedBlock { Base = 1 });
        return world.Create(in bundle);
    }
}
