using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class GuardDamageTests : IDisposable
{
    public GuardDamageTests()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
        EventQueue.Clear();
    }

    [Fact]
    public void Attack_fully_blocked_when_damage_within_guard()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 5);

        PublishAttackDamage(entityManager, player, enemy, 3);

        Assert.Equal(30, enemy.GetComponent<HP>().Current);
        Assert.False(HasGuard(enemy));
    }

    [Fact]
    public void Attack_partially_blocked_when_damage_exceeds_guard()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 3);

        PublishAttackDamage(entityManager, player, enemy, 10);

        Assert.Equal(23, enemy.GetComponent<HP>().Current);
        Assert.False(HasGuard(enemy));
    }

    [Fact]
    public void Any_attack_damage_removes_all_guard_not_partial()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 5);

        PublishAttackDamage(entityManager, player, enemy, 1);

        Assert.Equal(30, enemy.GetComponent<HP>().Current);
        Assert.False(HasGuard(enemy));
    }

    [Fact]
    public void Second_attack_deals_full_damage_after_guard_consumed()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 3);

        PublishAttackDamage(entityManager, player, enemy, 2);
        Assert.Equal(30, enemy.GetComponent<HP>().Current);
        Assert.False(HasGuard(enemy));

        PublishAttackDamage(entityManager, player, enemy, 2);
        Assert.Equal(28, enemy.GetComponent<HP>().Current);
    }

    [Fact]
    public void Stacked_guard_absorbs_up_to_total_value()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 3);
        ApplyGuard(entityManager, enemy, 4);

        PublishAttackDamage(entityManager, player, enemy, 5);

        Assert.Equal(30, enemy.GetComponent<HP>().Current);
        Assert.False(HasGuard(enemy));
    }

    [Fact]
    public void Guard_ignored_for_effect_damage()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 5);

        EventManager.Publish(new ModifyHpRequestEvent
        {
            Source = player,
            Target = enemy,
            Delta = -5,
            DamageType = ModifyTypeEnum.Effect
        });

        Assert.Equal(25, enemy.GetComponent<HP>().Current);
        Assert.Equal(5, GetGuard(enemy));
    }

    [Fact]
    public void Guard_runs_before_armor_on_enemy()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 5);
        EventManager.Publish(new ApplyPassiveEvent { Target = enemy, Type = AppliedPassiveType.Armor, Delta = 2 });

        PublishAttackDamage(entityManager, player, enemy, 8);

        Assert.Equal(29, enemy.GetComponent<HP>().Current);
        Assert.False(HasGuard(enemy));
    }

    [Fact]
    public void EnemyStart_removes_guard_when_not_consumed()
    {
        var (entityManager, _, enemy) = BuildCombatWorld();
        ApplyGuard(entityManager, enemy, 10);

        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });
        PumpEventQueue();

        Assert.Equal(0, GetGuard(enemy));
        Assert.Equal(0, GetPassive(enemy, AppliedPassiveType.Aggression));
    }

    [Fact]
    public void EnemyStart_no_op_when_no_guard()
    {
        var (entityManager, _, enemy) = BuildCombatWorld();

        EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });
        PumpEventQueue();

        Assert.Equal(0, GetPassive(enemy, AppliedPassiveType.Aggression));
    }

    private static (EntityManager EntityManager, Entity Player, Entity Enemy) BuildCombatWorld()
    {
        var entityManager = new EntityManager();
        _ = new HpManagementSystem(entityManager);
        _ = new AppliedPassivesManagementSystem(entityManager);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new HP { Max = 25, Current = 25 });
        entityManager.AddComponent(player, new AppliedPassives());

        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new HP { Max = 30, Current = 30 });
        entityManager.AddComponent(enemy, new AppliedPassives());

        return (entityManager, player, enemy);
    }

    private static void ApplyGuard(EntityManager entityManager, Entity enemy, int amount)
    {
        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = enemy,
            Type = AppliedPassiveType.Guard,
            Delta = amount
        });
    }

    private static void PublishAttackDamage(EntityManager entityManager, Entity source, Entity target, int damage)
    {
        EventManager.Publish(new ModifyHpRequestEvent
        {
            Source = source,
            Target = target,
            Delta = -damage,
            DamageType = ModifyTypeEnum.Attack
        });
    }

    private static void PumpEventQueue()
    {
        while (!EventQueue.IsIdle)
        {
            EventQueue.Update(AppliedPassivesManagementSystem.Duration + 0.1f);
        }
    }

    private static bool HasGuard(Entity enemy)
    {
        return GetGuard(enemy) > 0;
    }

    private static int GetGuard(Entity enemy)
    {
        return GetPassive(enemy, AppliedPassiveType.Guard);
    }

    private static int GetPassive(Entity owner, AppliedPassiveType type)
    {
        var passives = owner.GetComponent<AppliedPassives>()?.Passives;
        if (passives == null) return 0;
        return passives.TryGetValue(type, out int stacks) ? stacks : 0;
    }
}
