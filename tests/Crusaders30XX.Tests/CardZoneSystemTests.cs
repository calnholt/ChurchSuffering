using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class CardZoneSystemTests : IDisposable
{
    public CardZoneSystemTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Moving_assigned_block_to_discard_clears_hotkey()
    {
        var entityManager = new EntityManager();
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        var card = entityManager.CreateEntity("AssignedBlockCard");
        entityManager.AddComponent(card, new CardData { Card = new Tempest() });
        entityManager.AddComponent(card, new AssignedBlockCard());
        entityManager.AddComponent(card, new HotKey { Button = FaceButton.B, Position = HotKeyPosition.Top });
        entityManager.AddComponent(card, new UIElement { EventType = UIElementEventType.UnassignCardAsBlock });

        _ = new CardZoneSystem(entityManager);

        EventManager.Publish(new CardMoveRequested
        {
            Card = card,
            Deck = deckEntity,
            Destination = CardZoneType.DiscardPile,
            ContextId = "attack-1",
            Reason = "TestAssignedBlockResolution"
        });

        Assert.Contains(card, deck.DiscardPile);
        Assert.False(card.HasComponent<HotKey>());
    }
}
