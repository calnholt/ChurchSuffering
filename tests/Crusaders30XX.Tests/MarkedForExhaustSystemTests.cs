using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class MarkedForExhaustSystemTests : System.IDisposable
{
    public MarkedForExhaustSystemTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void BeginDefeatPresentation_exhausts_hand_card_with_MarkedForExhaust()
    {
        var entityManager = BuildWorld(out var enemy, out var deck, out var kunai);
        entityManager.AddComponent(kunai, new MarkedForExhaust { Owner = kunai });
        _ = new MarkedForExhaustSystem(entityManager);

        EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

        AssertCardExhausted(entityManager, kunai, deck);
    }

    [Fact]
    public void BeginDefeatPresentation_exhausts_unplayed_ExhaustsOnEndTurn_card()
    {
        var entityManager = BuildWorld(out var enemy, out var deck, out var kunai);
        _ = new MarkedForExhaustSystem(entityManager);

        EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

        AssertCardExhausted(entityManager, kunai, deck);
    }

    [Fact]
    public void BeginDefeatPresentation_finalizes_deferred_exhaust_animation()
    {
        var entityManager = BuildWorld(out var enemy, out var deck, out var kunai);
        entityManager.AddComponent(kunai, new AnimatingHandToZone
        {
            Owner = kunai,
            Destination = CardZoneType.ExhaustPile
        });
        _ = new MarkedForExhaustSystem(entityManager);

        EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

        AssertCardExhausted(entityManager, kunai, deck);
    }

    [Fact]
    public void BeginDefeatPresentation_preview_does_not_exhaust_cards()
    {
        var entityManager = BuildWorld(out var enemy, out var deck, out var kunai);
        entityManager.AddComponent(kunai, new MarkedForExhaust { Owner = kunai });
        _ = new MarkedForExhaustSystem(entityManager);

        EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = true });

        Assert.Contains(kunai, deck.Hand);
        Assert.NotNull(entityManager.GetEntity(kunai.Id));
    }

    private static void AssertCardExhausted(EntityManager entityManager, Entity card, Deck deck)
    {
        Assert.Null(entityManager.GetEntity(card.Id));
        Assert.DoesNotContain(card, deck.Hand);
        Assert.DoesNotContain(card, deck.DrawPile);
        Assert.DoesNotContain(card, deck.DiscardPile);
        Assert.DoesNotContain(card, deck.ExhaustPile);
        Assert.DoesNotContain(card, deck.Cards);
    }

    private static EntityManager BuildWorld(out Entity enemy, out Deck deck, out Entity kunai)
    {
        var entityManager = new EntityManager();

        enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());

        var deckEntity = entityManager.CreateEntity("Deck");
        deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        kunai = EntityFactory.CreateCardFromDefinition(entityManager, "kunai", CardData.CardColor.White, false, 0);
        deck.Hand.Add(kunai);

        return entityManager;
    }
}
