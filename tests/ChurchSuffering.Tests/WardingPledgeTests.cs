using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class WardingPledgeTests : IDisposable
{
    public WardingPledgeTests()
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
    public void Pledged_scar_gain_becomes_aegis_and_decrements_remaining()
    {
        var (entityManager, player, card, warding) = BuildWorld(pledged: true);
        var hp = player.GetComponent<HP>();

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Scar,
            Delta = 1
        });

        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Scar));
        Assert.Equal(1, GetPassive(player, AppliedPassiveType.Aegis));
        Assert.Equal(20, hp.Max);
        Assert.Equal(20, hp.Current);
        Assert.Contains("(2 remaining)", warding.Text);
    }

    [Fact]
    public void Not_pledged_scar_applies_normally()
    {
        var (_, player, _, warding) = BuildWorld(pledged: false);
        var hp = player.GetComponent<HP>();

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Scar,
            Delta = 2
        });

        Assert.Equal(2, GetPassive(player, AppliedPassiveType.Scar));
        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Aegis));
        Assert.Equal(18, hp.Max);
        Assert.Equal(18, hp.Current);
        Assert.Contains("(3 remaining)", warding.Text);
    }

    [Fact]
    public void Multi_delta_scar_uses_one_charge_for_equal_aegis()
    {
        var (_, player, _, warding) = BuildWorld(pledged: true);

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Scar,
            Delta = 2
        });

        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Scar));
        Assert.Equal(2, GetPassive(player, AppliedPassiveType.Aegis));
        Assert.Contains("(2 remaining)", warding.Text);
    }

    [Fact]
    public void Exhausted_charges_allow_scar_to_apply()
    {
        var (_, player, _, warding) = BuildWorld(pledged: true);

        for (int i = 0; i < 3; i++)
        {
            EventManager.Publish(new ApplyPassiveEvent
            {
                Target = player,
                Type = AppliedPassiveType.Scar,
                Delta = 1
            });
        }

        Assert.Equal(0, GetPassive(player, AppliedPassiveType.Scar));
        Assert.Equal(3, GetPassive(player, AppliedPassiveType.Aegis));
        Assert.Contains("(0 remaining)", warding.Text);

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Scar,
            Delta = 1
        });

        Assert.Equal(1, GetPassive(player, AppliedPassiveType.Scar));
        Assert.Equal(3, GetPassive(player, AppliedPassiveType.Aegis));
        Assert.Equal(19, player.GetComponent<HP>().Max);
    }

    [Fact]
    public void Re_pledge_resets_remaining_to_three()
    {
        var (entityManager, player, card, warding) = BuildWorld(pledged: true);

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Scar,
            Delta = 1
        });
        Assert.Contains("(2 remaining)", warding.Text);

        warding.OnPledged(entityManager, card);
        Assert.Contains("(3 remaining)", warding.Text);

        EventManager.Publish(new ApplyPassiveEvent
        {
            Target = player,
            Type = AppliedPassiveType.Scar,
            Delta = 1
        });
        Assert.Contains("(2 remaining)", warding.Text);
        Assert.Equal(2, GetPassive(player, AppliedPassiveType.Aegis));
    }

    [Fact]
    public void CardFactory_includes_warding_pledge()
    {
        Assert.IsType<WardingPledge>(CardFactory.Create("warding_pledge"));
        Assert.Contains(ECS.Data.Ids.CardId.WardingPledge, CardFactory.GetAllCards().Keys);
    }

    [Fact]
    public void Upgrade_increases_block_by_one()
    {
        var entityManager = new EntityManager();
        var cardEntity = entityManager.CreateEntity("WardingPledge");
        var card = new WardingPledge { IsUpgraded = true };
        entityManager.AddComponent(cardEntity, new CardData { Card = card, Owner = cardEntity });
        card.Initialize(entityManager, cardEntity);
        Assert.Equal(4, card.Block);
    }

    private static (EntityManager entityManager, Entity player, Entity card, WardingPledge warding) BuildWorld(bool pledged)
    {
        var entityManager = new EntityManager();
        _ = new HpManagementSystem(entityManager);
        _ = new ReplacementEffectSystem(entityManager);
        _ = new AppliedPassivesManagementSystem(entityManager);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        entityManager.AddComponent(player, new HP { Max = 20, Current = 20, UnscarredMax = 20 });
        entityManager.AddComponent(player, new AppliedPassives());

        var warding = new WardingPledge();
        var card = entityManager.CreateEntity("WardingPledge");
        entityManager.AddComponent(card, new CardData { Card = warding, Owner = card });
        warding.Initialize(entityManager, card);

        if (pledged)
        {
            entityManager.AddComponent(card, new Pledge { Owner = card, CanPlay = false });
            warding.OnPledged?.Invoke(entityManager, card);
        }

        return (entityManager, player, card, warding);
    }

    private static int GetPassive(Entity owner, AppliedPassiveType type)
    {
        var passives = owner.GetComponent<AppliedPassives>()?.Passives;
        if (passives == null) return 0;
        return passives.TryGetValue(type, out var stacks) ? stacks : 0;
    }
}
