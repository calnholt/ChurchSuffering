using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Enemies;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class AzureWardenTests
{
    [Fact]
    public void Azure_warden_has_a_single_ten_damage_seal_attack()
    {
        var enemy = Assert.IsType<AzureWarden>(EnemyFactory.Create(EnemyId.AzureWarden));
        var attacks = enemy.GetAttackIds(new EntityManager(), 1);
        var attack = EnemyAttackFactory.Create(EnemyAttackId.WardenSeal);

        Assert.Equal(30, enemy.HP);
        Assert.Equal([EnemyAttackId.WardenSeal], attacks);
        Assert.Equal(10, attack.Damage);
        Assert.Equal("Seal", attack.Name);
        Assert.Equal("On reveal - Seal a random card from your hand.", attack.Text);
    }

    [Fact]
    public void Seal_reveal_requests_two_stacks_on_one_hand_card()
    {
        EventManager.Clear();
        try
        {
            ApplyCardApplicationEvent applied = null;
            EventManager.Subscribe<ApplyCardApplicationEvent>(evt => applied = evt);

            var attack = EnemyAttackFactory.Create(EnemyAttackId.WardenSeal);
            attack.OnAttackReveal(new EntityManager());

            Assert.NotNull(applied);
            Assert.Equal(1, applied.Amount);
            Assert.Equal(2, applied.StacksPerCard);
            Assert.Equal(CardApplicationType.Sealed, applied.Type);
            Assert.Equal(CardApplicationTarget.Hand, applied.Target);
        }
        finally
        {
            EventManager.Clear();
        }
    }

    [Fact]
    public void Sealed_card_loses_one_stack_per_block_and_is_freed_at_zero()
    {
        EventManager.Clear();
        try
        {
            var entityManager = new EntityManager();
            var card = entityManager.CreateEntity("SealedCard");
            entityManager.AddComponent(card, new Sealed { Owner = card, Seals = 2 });
            _ = new SealManagementSystem(entityManager);

            EventManager.Publish(new CardBlockedEvent { Card = card });
            Assert.Equal(1, card.GetComponent<Sealed>()?.Seals);

            EventManager.Publish(new CardBlockedEvent { Card = card });
            Assert.Null(card.GetComponent<Sealed>());
        }
        finally
        {
            EventManager.Clear();
        }
    }
}
