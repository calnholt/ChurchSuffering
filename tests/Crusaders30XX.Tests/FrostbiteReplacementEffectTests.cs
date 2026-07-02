using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Medals;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class FrostbiteReplacementEffectTests : IDisposable
{
    public FrostbiteReplacementEffectTests()
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
    public void Gain_two_frostbite_does_not_trigger_damage()
    {
        var entityManager = BuildWorld(out var player, out _, equipOlaf: false);

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Frostbite,
            Delta = 2
        });

        Assert.Equal(20, player.GetComponent<HP>().Current);
        Assert.Equal(2, GetPassive(player, AppliedPassiveType.Frostbite));
    }

    [Fact]
    public void Gain_three_frostbite_triggers_self_damage_and_removes_stacks()
    {
        var entityManager = BuildWorld(out var player, out _, equipOlaf: false);

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Frostbite,
            Delta = 3
        });

        Assert.Equal(17, player.GetComponent<HP>().Current);
        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Frostbite));
    }

    [Fact]
    public void Gain_six_frostbite_triggers_twice()
    {
        var entityManager = BuildWorld(out var player, out _, equipOlaf: false);
        int frostbiteTriggers = 0;
        EventManager.Subscribe<FrostbiteTriggered>(evt =>
        {
            if (evt.Target == player)
            {
                frostbiteTriggers++;
                Assert.Equal(2, evt.TriggerCount);
            }
        });

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Frostbite,
            Delta = 6
        });

        Assert.Equal(2, frostbiteTriggers);
        Assert.Equal(14, player.GetComponent<HP>().Current);
        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Frostbite));
    }

    [Fact]
    public void Existing_two_plus_four_frostbite_triggers_twice()
    {
        var entityManager = BuildWorld(out var player, out _, equipOlaf: false);
        player.GetComponent<AppliedPassives>().Passives[AppliedPassiveType.Frostbite] = 2;

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Frostbite,
            Delta = 4
        });

        Assert.Equal(14, player.GetComponent<HP>().Current);
        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Frostbite));
    }

    [Fact]
    public void StOlaf_replaces_frostbite_self_damage_with_enemy_effect_damage()
    {
        var entityManager = BuildWorld(out var player, out var enemy, equipOlaf: true);
        int frostbiteTriggers = 0;
        int medalTriggers = 0;
        EventManager.Subscribe<FrostbiteTriggered>(evt =>
        {
            if (evt.Target == player) frostbiteTriggers++;
        });
        EventManager.Subscribe<MedalTriggered>(evt =>
        {
            if (evt.MedalId == "st_olaf") medalTriggers++;
        });

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Frostbite,
            Delta = 6
        });

        Assert.Equal(2, frostbiteTriggers);
        Assert.Equal(2, medalTriggers);
        Assert.Equal(20, player.GetComponent<HP>().Current);
        Assert.Equal(24, enemy.GetComponent<HP>().Current);
        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Frostbite));
    }

    [Fact]
    public void StOlaf_prevents_frostbite_damage_when_no_enemy_exists()
    {
        var entityManager = BuildWorld(out var player, out _, equipOlaf: true, createEnemy: false);
        int medalTriggers = 0;
        EventManager.Subscribe<MedalTriggered>(evt =>
        {
            if (evt.MedalId == "st_olaf") medalTriggers++;
        });

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Frostbite,
            Delta = 3
        });

        Assert.Equal(1, medalTriggers);
        Assert.Equal(20, player.GetComponent<HP>().Current);
        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Frostbite));
    }

    [Fact]
    public void MedalFactory_includes_st_olaf()
    {
        Assert.IsType<StOlaf>(MedalFactory.Create("st_olaf"));
        Assert.Contains(MedalId.StOlaf, MedalFactory.GetAllMedals().Keys);
    }

    private static EntityManager BuildWorld(
        out Entity player,
        out Entity enemy,
        bool equipOlaf,
        bool createEnemy = true)
    {
        var entityManager = new EntityManager();
        _ = new HpManagementSystem(entityManager);
        _ = new ReplacementEffectSystem(entityManager);
        _ = new AppliedPassivesManagementSystem(entityManager);

        player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new HP { Max = 20, Current = 20 });
        entityManager.AddComponent(player, new AppliedPassives());

        enemy = null;
        if (createEnemy)
        {
            enemy = entityManager.CreateEntity("Enemy");
            entityManager.AddComponent(enemy, new Enemy());
            entityManager.AddComponent(enemy, new HP { Max = 30, Current = 30 });
            entityManager.AddComponent(enemy, new AppliedPassives());
        }

        if (equipOlaf)
        {
            var medalEntity = entityManager.CreateEntity("Medal_st_olaf");
            var medal = new StOlaf();
            medal.Initialize(entityManager, medalEntity);
            entityManager.AddComponent(medalEntity, new EquippedMedal
            {
                EquippedOwner = player,
                Medal = medal
            });
        }

        return entityManager;
    }

    private static int GetPassive(Entity owner, AppliedPassiveType type)
    {
        var passives = owner.GetComponent<AppliedPassives>()?.Passives;
        if (passives == null) return 0;
        return passives.TryGetValue(type, out var stacks) ? stacks : 0;
    }
}
