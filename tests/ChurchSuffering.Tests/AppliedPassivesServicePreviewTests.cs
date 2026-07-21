using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class AppliedPassivesServicePreviewTests : IDisposable
{
    public AppliedPassivesServicePreviewTests()
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
    public void Preview_fully_blocked_when_damage_within_guard()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(enemy, 5);

        int preview = PreviewAttackDamage(player, enemy, 3);

        Assert.Equal(0, preview);
    }

    [Fact]
    public void Preview_partially_blocked_when_damage_exceeds_guard()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(enemy, 3);

        int preview = PreviewAttackDamage(player, enemy, 10);

        Assert.Equal(7, preview);
    }

    [Fact]
    public void Preview_guard_runs_before_armor()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(enemy, 5);
        EventManager.Publish(new ApplyPassiveEvent { Target = enemy, Type = AppliedPassiveType.Armor, Delta = 2 });

        int preview = PreviewAttackDamage(player, enemy, 8);

        Assert.Equal(1, preview);
    }

    [Fact]
    public void Preview_matches_runtime_hp_delta()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        ApplyGuard(enemy, 5);
        EventManager.Publish(new ApplyPassiveEvent { Target = enemy, Type = AppliedPassiveType.Armor, Delta = 2 });

        int preview = PreviewAttackDamage(player, enemy, 8);
        int hpBefore = enemy.GetComponent<HP>().Current;
        PublishAttackDamage(player, enemy, 8);
        int hpAfter = enemy.GetComponent<HP>().Current;

        Assert.Equal(hpBefore - hpAfter, preview);
    }

    [Fact]
    public void Preview_without_guard_uses_passive_delta_only()
    {
        var (entityManager, player, enemy) = BuildCombatWorld();
        EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Might, Delta = 2 });
        EventManager.Publish(new ApplyPassiveEvent { Target = enemy, Type = AppliedPassiveType.Armor, Delta = 1 });

        int preview = PreviewAttackDamage(player, enemy, 5);

        Assert.Equal(6, preview);
    }

    [Fact]
    public void GetGuardAbsorption_returns_zero_without_guard()
    {
        var (_, _, enemy) = BuildCombatWorld();

        Assert.Equal(0, AppliedPassivesService.GetGuardAbsorption(enemy, 5));
    }

    [Fact]
    public void GetGuardAbsorption_caps_at_raw_damage()
    {
        var (_, _, enemy) = BuildCombatWorld();
        ApplyGuard(enemy, 10);

        Assert.Equal(3, AppliedPassivesService.GetGuardAbsorption(enemy, 3));
    }

    private static int PreviewAttackDamage(Entity player, Entity enemy, int rawDamage)
    {
        var preview = new ModifyHpRequestEvent
        {
            Source = player,
            Target = enemy,
            DamageType = ModifyTypeEnum.Attack
        };
        return AppliedPassivesService.GetPreviewAttackDamage(preview, rawDamage, ReadOnly: true);
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

    private static void ApplyGuard(Entity enemy, int amount)
    {
        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = enemy,
            Type = AppliedPassiveType.Guard,
            Delta = amount
        });
    }

    private static void PublishAttackDamage(Entity source, Entity target, int damage)
    {
        EventManager.Publish(new ModifyHpRequestEvent
        {
            Source = source,
            Target = target,
            Delta = -damage,
            DamageType = ModifyTypeEnum.Attack
        });
    }
}
