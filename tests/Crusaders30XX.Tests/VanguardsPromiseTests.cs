using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class VanguardsPromiseTests : IDisposable
{
    public VanguardsPromiseTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Card_text_describes_random_discard_pledge()
    {
        var card = new VanguardsPromise();

        Assert.Equal(
            "If you have no pledged card, pledge a random card from your discard pile.",
            card.Text);
    }

    [Fact]
    public void On_play_requests_random_discard_pledge_when_none_exists()
    {
        var entityManager = BuildBattle(out _, out _);
        var requests = 0;
        EventManager.Subscribe<PledgeRandomCardFromDiscardRequested>(_ => requests++);

        PlayCard(entityManager);

        Assert.Equal(1, requests);
    }

    [Fact]
    public void On_play_does_not_request_random_discard_pledge_when_one_exists()
    {
        var entityManager = BuildBattle(out var deck, out _);
        var pledgedCard = AddCard(entityManager, deck.Hand, "Pledged");
        entityManager.AddComponent(pledgedCard, new Pledge { CanPlay = true });
        var requests = 0;
        EventManager.Subscribe<PledgeRandomCardFromDiscardRequested>(_ => requests++);

        PlayCard(entityManager);

        Assert.Equal(0, requests);
    }

    [Fact]
    public void Random_discard_pledge_moves_and_pledges_the_only_discard_card()
    {
        var entityManager = BuildBattle(out var deck, out _);
        var discardedCard = AddCard(entityManager, deck.DiscardPile, "Discarded");
        AddPledgeSystems(entityManager);

        EventManager.Publish(new PledgeRandomCardFromDiscardRequested());

        Assert.DoesNotContain(discardedCard, deck.DiscardPile);
        Assert.Contains(discardedCard, deck.Hand);
        Assert.NotNull(discardedCard.GetComponent<Pledge>());
        Assert.True(entityManager.GetEntitiesWithComponent<PhaseState>()
            .Single()
            .GetComponent<PledgeAvailabilityState>()
            .PledgedThisActionPhase);
    }

    [Fact]
    public void Random_discard_pledge_selects_exactly_one_discard_card()
    {
        var entityManager = BuildBattle(out var deck, out _);
        var discardedCards = new[]
        {
            AddCard(entityManager, deck.DiscardPile, "Discarded1"),
            AddCard(entityManager, deck.DiscardPile, "Discarded2"),
            AddCard(entityManager, deck.DiscardPile, "Discarded3")
        };
        AddPledgeSystems(entityManager);

        EventManager.Publish(new PledgeRandomCardFromDiscardRequested());

        var pledgedCard = Assert.Single(discardedCards, card => card.HasComponent<Pledge>());
        Assert.Contains(pledgedCard, deck.Hand);
        Assert.Equal(2, deck.DiscardPile.Count);
    }

    [Fact]
    public void Random_discard_pledge_does_nothing_when_discard_is_empty()
    {
        var entityManager = BuildBattle(out var deck, out _);
        AddPledgeSystems(entityManager);

        EventManager.Publish(new PledgeRandomCardFromDiscardRequested());

        Assert.Empty(deck.Hand);
        Assert.Empty(entityManager.GetEntitiesWithComponent<Pledge>());
    }

    [Fact]
    public void Random_discard_pledge_does_nothing_when_a_pledge_exists()
    {
        var entityManager = BuildBattle(out var deck, out _);
        var pledgedCard = AddCard(entityManager, deck.Hand, "Pledged");
        entityManager.AddComponent(pledgedCard, new Pledge { CanPlay = true });
        var discardedCard = AddCard(entityManager, deck.DiscardPile, "Discarded");
        AddPledgeSystems(entityManager);

        EventManager.Publish(new PledgeRandomCardFromDiscardRequested());

        Assert.Contains(discardedCard, deck.DiscardPile);
        Assert.DoesNotContain(discardedCard, deck.Hand);
        Assert.False(discardedCard.HasComponent<Pledge>());
    }

    private static EntityManager BuildBattle(out Deck deck, out Entity deckEntity)
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.PlayerTurn,
            Sub = SubPhase.Action
        });

        deckEntity = entityManager.CreateEntity("Deck");
        deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());
        var enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());

        return entityManager;
    }

    private static Entity AddCard(EntityManager entityManager, ICollection<Entity> zone, string name)
    {
        var card = entityManager.CreateEntity(name);
        entityManager.AddComponent(card, new CardData { Card = new Strike() });
        zone.Add(card);
        return card;
    }

    private static void AddPledgeSystems(EntityManager entityManager)
    {
        _ = new CardZoneSystem(entityManager);
        _ = new PledgeManagementSystem(entityManager);
    }

    private static void PlayCard(EntityManager entityManager)
    {
        var card = new VanguardsPromise();
        var cardEntity = entityManager.CreateEntity("VanguardsPromise");
        entityManager.AddComponent(cardEntity, new CardData { Card = card });
        entityManager.AddComponent(cardEntity, new ModifiedDamage());

        card.OnPlay(entityManager, cardEntity);
    }
}
