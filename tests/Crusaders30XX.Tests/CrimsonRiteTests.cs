using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CrimsonRiteTests : IDisposable
{
    public CrimsonRiteTests()
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
    public void Heals_for_full_damage_including_aggression_and_might()
    {
        var (entityManager, player, enemy, cardEntity) = BuildCombatWithCrimsonRite(isUpgraded: false);
        EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Might, Delta = 1 });
        EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aggression, Delta = 3 });

        int playerHpBefore = player.GetComponent<HP>().Current;
        int enemyHpBefore = enemy.GetComponent<HP>().Current;

        var card = cardEntity.GetComponent<CardData>().Card;
        card.OnPlay(entityManager, cardEntity);

        Assert.Equal(playerHpBefore + 7, player.GetComponent<HP>().Current);
        Assert.Equal(enemyHpBefore - 7, enemy.GetComponent<HP>().Current);
    }

    [Fact]
    public void Upgraded_grants_aegis_equal_to_full_damage_dealt()
    {
        var (entityManager, player, enemy, cardEntity) = BuildCombatWithCrimsonRite(isUpgraded: true);
        EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Might, Delta = 1 });
        EventManager.Publish(new ApplyPassiveEvent { Target = player, Type = AppliedPassiveType.Aggression, Delta = 3 });

        var card = cardEntity.GetComponent<CardData>().Card;
        card.OnPlay(entityManager, cardEntity);

        var passives = player.GetComponent<AppliedPassives>().Passives;
        Assert.True(passives.TryGetValue(AppliedPassiveType.Aegis, out int aegis));
        Assert.Equal(7, aegis);
        Assert.Equal(23, enemy.GetComponent<HP>().Current);
    }

    private static (EntityManager EntityManager, Entity Player, Entity Enemy, Entity CardEntity) BuildCombatWithCrimsonRite(bool isUpgraded)
    {
        var entityManager = new EntityManager();
        _ = new HpManagementSystem(entityManager);
        _ = new AppliedPassivesManagementSystem(entityManager);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new HP { Max = 25, Current = 10 });
        entityManager.AddComponent(player, new AppliedPassives());

        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());
        entityManager.AddComponent(enemy, new HP { Max = 30, Current = 30 });
        entityManager.AddComponent(enemy, new AppliedPassives());

        var card = new CrimsonRite { IsUpgraded = isUpgraded };
        var cardEntity = entityManager.CreateEntity("crimson_rite");
        entityManager.AddComponent(cardEntity, new CardData { Card = card });
        entityManager.AddComponent(cardEntity, new ModifiedDamage());

        return (entityManager, player, enemy, cardEntity);
    }
}
