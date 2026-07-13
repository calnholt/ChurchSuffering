#nullable enable

using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Definitions;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rules;
using Crusaders30XX.ECS.DataOriented.Storage;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Combat;

public sealed class Ecs052CombatAttackParityTests
{
    [Fact]
    public void Exact_block_count_fully_prevents_damage_and_exhausts_only_card_blockers()
    {
        CombatSession session = Create(EnemyId.SandGolem);
        StartAttack(session, EnemyAttackId.SandSlam);
        EntityId card = CreateBlocker(session.World);
        EntityId equipment = CreateBlocker(session.World);

        Assert.True(session.AssignBlock(card, 0, RuleCardColor.White));
        Assert.True(session.AssignBlock(equipment, 0, RuleCardColor.Red, equipment: true));
        ResolveCurrentAttack(session);

        Assert.Equal(30, session.World.Get<HP>(session.Player).Current);
        Assert.Equal(1, session.World.Get<EnemyAttackProgress>(session.Battle).FullyPreventedBySpecial);
        Assert.True(session.World.Has<ExhaustOnBlock>(card));
        Assert.False(session.World.Has<ExhaustOnBlock>(equipment));
    }

    [Fact]
    public void Fallen_shepherd_blocker_restrictions_apply_once_to_cards_and_skip_equipment()
    {
        CombatSession castOut = Create(EnemyId.FallenShepherd);
        StartAttack(castOut, EnemyAttackId.FallenShepherdPhase1);
        EntityId castOutCard = CreateBlocker(castOut.World);
        EntityId castOutEquipment = CreateBlocker(castOut.World);

        Assert.True(castOut.AssignBlock(castOutCard, 1, RuleCardColor.White));
        Assert.True(castOut.AssignBlock(castOutEquipment, 1, RuleCardColor.Red, equipment: true));
        Assert.True(castOut.World.Has<Colorless>(castOutCard));
        Assert.False(castOut.World.Has<Colorless>(castOutEquipment));
        Assert.False(castOut.AssignBlock(castOutCard, 1, RuleCardColor.White));

        CombatSession breakFaith = Create(EnemyId.FallenShepherd);
        StartAttack(breakFaith, EnemyAttackId.FallenShepherdBreakFaith);
        EntityId breakFaithCard = CreateBlocker(breakFaith.World);
        EntityId breakFaithEquipment = CreateBlocker(breakFaith.World);

        Assert.True(breakFaith.AssignBlock(breakFaithCard, 1, RuleCardColor.White));
        Assert.True(breakFaith.AssignBlock(breakFaithEquipment, 1, RuleCardColor.Red, equipment: true));
        Assert.True(breakFaith.World.Has<Brittle>(breakFaithCard));
        Assert.False(breakFaith.World.Has<Brittle>(breakFaithEquipment));
    }

    [Fact]
    public void Frost_eater_frozen_block_penalty_drives_final_damage_and_reverses_on_removal()
    {
        CombatSession session = Create(EnemyId.IceDemon);
        StartAttack(session, EnemyAttackId.FrostEater);
        EntityId frozen = CreateBlocker(session.World);

        Assert.True(session.AssignBlock(frozen, 2, RuleCardColor.White, frozen: true));
        Assert.Equal(1, session.World.Get<EnemyAttackProgress>(session.Battle).AssignedBlock);
        Assert.True(session.RemoveBlock(frozen));
        Assert.Equal(0, session.World.Get<EnemyAttackProgress>(session.Battle).AssignedBlock);
        Assert.True(session.AssignBlock(frozen, 2, RuleCardColor.White, frozen: true));

        ResolveCurrentAttack(session);

        Assert.Equal(22, session.World.Get<HP>(session.Player).Current);
        Assert.Equal(8, session.World.Get<EnemyAttackProgress>(session.Battle).DamageDealt);
    }

    [Fact]
    public void Damage_threshold_runs_below_block_threshold_only_when_final_damage_is_positive()
    {
        CombatSession triggered = Create(EnemyId.Mummy);
        StartAttack(triggered, EnemyAttackId.Mummify);
        int required = triggered.World.Get<EnemyAttackProgress>(triggered.Battle).RequiredAmount;
        Assert.True(triggered.AssignBlock(CreateBlocker(triggered.World), required - 1, RuleCardColor.White));
        ResolveCurrentAttack(triggered);
        Assert.Equal(2, triggered.GetPassiveStacks(triggered.Player, RuleEffectIds.Scar));

        CombatSession thresholdMet = Create(EnemyId.Mummy);
        StartAttack(thresholdMet, EnemyAttackId.Mummify);
        required = thresholdMet.World.Get<EnemyAttackProgress>(thresholdMet.Battle).RequiredAmount;
        Assert.True(thresholdMet.AssignBlock(CreateBlocker(thresholdMet.World), required, RuleCardColor.White));
        ResolveCurrentAttack(thresholdMet);
        Assert.Equal(0, thresholdMet.GetPassiveStacks(thresholdMet.Player, RuleEffectIds.Scar));

        CombatSession aegisPrevented = Create(EnemyId.Mummy);
        StartAttack(aegisPrevented, EnemyAttackId.Mummify);
        aegisPrevented.GrantPassive(aegisPrevented.Player, RuleEffectIds.Aegis, 20);
        ResolveCurrentAttack(aegisPrevented);
        Assert.Equal(0, aegisPrevented.World.Get<EnemyAttackProgress>(aegisPrevented.Battle).DamageDealt);
        Assert.Equal(0, aegisPrevented.GetPassiveStacks(aegisPrevented.Player, RuleEffectIds.Scar));
    }

    [Fact]
    public void Infernal_execution_channel_scales_burn_and_keeps_two_blocker_gate()
    {
        CombatSession session = Create(EnemyId.Demon);
        session.Process();
        session.GrantPassive(session.Enemy, RuleEffectIds.Channel, 3);
        StartAttack(session, EnemyAttackId.InfernalExecution, processOpening: false);

        ref EnemyAttackProgress progress = ref session.World.Get<EnemyAttackProgress>(session.Battle);
        Assert.Equal(RequirementKind.MinimumBlockers, progress.Requirement);
        Assert.Equal(2, progress.RequiredAmount);
        Assert.Equal(4, session.GetPassiveStacks(session.Player, RuleEffectIds.Burn));

        Assert.True(session.AssignBlock(CreateBlocker(session.World), 0, RuleCardColor.White));
        session.ConfirmBlocks();
        session.Process();
        Assert.True((session.World.Get<BattleStateInfo>(session.Battle).Flags & CombatFlags.AwaitingBlockConfirmation) != 0);
        Assert.True(session.AssignBlock(CreateBlocker(session.World), 0, RuleCardColor.Red));
        session.ConfirmBlocks();
        Assert.True(session.Process().IsWaiting);
    }

    [Fact]
    public void Fallen_shepherd_reveal_and_hit_hooks_apply_exact_passives_to_legacy_targets()
    {
        CombatSession purge = Create(EnemyId.FallenShepherd);
        StartAttack(purge, EnemyAttackId.FallenShepherdPurgeTheHeretic);
        Assert.Equal(1, purge.GetPassiveStacks(purge.Player, RuleEffectIds.Burn));
        Assert.Equal(0, purge.GetPassiveStacks(purge.Enemy, RuleEffectIds.Burn));

        CombatSession crooksScar = Create(EnemyId.FallenShepherd);
        StartAttack(crooksScar, EnemyAttackId.FallenShepherdCrooksScar);
        ResolveCurrentAttack(crooksScar);
        Assert.Equal(1, crooksScar.GetPassiveStacks(crooksScar.Player, RuleEffectIds.Scar));
        Assert.Equal(29, crooksScar.World.Get<HP>(crooksScar.Player).Max);
    }

    private static void StartAttack(
        CombatSession session,
        EnemyAttackId attack,
        bool processOpening = true)
    {
        if (processOpening) session.Process();
        ref AttackIntent intent = ref session.World.Get<AttackIntent>(session.Battle);
        DynamicBuffer<EnemyAttackId> attacks = session.World.GetDynamicBuffer(intent.Attacks);
        attacks.Clear();
        attacks.Add(attack);
        intent.CurrentIndex = -1;
        intent.Current = default;
        ref BattleStateInfo battle = ref session.World.Get<BattleStateInfo>(session.Battle);
        battle.Flags &= ~CombatFlags.AwaitingBlockConfirmation;
        session.EnqueueMandatory(CombatRuleKind.BeginEnemyAttack);
        session.Process();
        Assert.Equal(attack, session.World.Get<EnemyAttackProgress>(session.Battle).Attack);
    }

    private static void ResolveCurrentAttack(CombatSession session)
    {
        session.ConfirmBlocks();
        Assert.True(session.Process().IsWaiting);
        session.Process();
    }

    private static CombatSession Create(EnemyId enemy)
    {
        var world = new World(GeneratedComponentRegistry.Create());
        var hub = new CombatEventHub();
        var owned = new CombatOwnedEventConsumers(world);
        world.AttachEventRuntime(new EventRuntime(new EventRoutingEndpoint(hub.BuildRoutes(owned.RegisterRoutes()))));
        CombatSession session = CombatSession.Create(world, hub, enemy);
        owned.Bind(session);
        return session;
    }

    private static EntityId CreateBlocker(World world)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new ModifiedBlock { Base = 1 });
        return world.Create(in bundle);
    }
}
