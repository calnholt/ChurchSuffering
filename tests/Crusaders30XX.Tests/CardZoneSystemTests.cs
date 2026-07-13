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
        EventQueue.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
        EventQueue.Clear();
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
        Assert.False(card.HasComponent<AssignedBlockCard>());
    }

	[Fact]
	public void Reserve_assigned_block_return_adds_hand_layout_membership_without_finalizing_zone()
	{
		var entityManager = new EntityManager();
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var card = entityManager.CreateEntity("AssignedBlockCard");
		entityManager.AddComponent(card, new AssignedBlockCard { IsEquipment = false });
		entityManager.AddComponent(card, new AssignedBlockPresentation
		{
			Phase = AssignedBlockPresentation.PhaseState.Returning,
		});
		_ = new CardZoneSystem(entityManager);

		EventManager.Publish(new ReserveAssignedBlockReturnRequested { Card = card, Deck = deckEntity });

		Assert.Contains(card, deck.Hand);
		Assert.True(card.HasComponent<AssignedBlockCard>());
		Assert.True(card.HasComponent<AssignedBlockPresentation>());
	}

    [Fact]
    public void BeginDefeatPresentation_returns_unspent_assigned_block_to_hand()
    {
        var entityManager = BuildBattleWorld(SubPhase.Block, out var enemy, out var deck, out var deckEntity, out var card);

        _ = new CardZoneSystem(entityManager);

        EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

        Assert.Contains(card, deck.Hand);
        Assert.False(card.HasComponent<AssignedBlockCard>());
        Assert.DoesNotContain(card, deck.DiscardPile);
        Assert.DoesNotContain(card, deck.DrawPile);
    }

    [Fact]
    public void BeginDefeatPresentation_discards_spent_assigned_block()
    {
        var entityManager = BuildBattleWorld(SubPhase.EnemyAttack, out var enemy, out var deck, out var deckEntity, out var card);

        _ = new CardZoneSystem(entityManager);

        EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

        Assert.Contains(card, deck.DiscardPile);
        Assert.False(card.HasComponent<AssignedBlockCard>());
        Assert.DoesNotContain(card, deck.Hand);
    }

    [Fact]
    public void BeginDefeatPresentation_preview_does_not_resolve_assigned_blocks()
    {
        var entityManager = BuildBattleWorld(SubPhase.Block, out var enemy, out var deck, out var deckEntity, out var card);

        _ = new CardZoneSystem(entityManager);

        EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = true });

        Assert.True(card.HasComponent<AssignedBlockCard>());
        Assert.DoesNotContain(card, deck.Hand);
        Assert.DoesNotContain(card, deck.DiscardPile);
    }

    [Fact]
    public void QueuedDiscardAssignedBlocksEvent_resolves_immediately_without_current_attack()
    {
        var entityManager = new EntityManager();
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        var card = entityManager.CreateEntity("AssignedBlockCard");
        entityManager.AddComponent(card, new CardData { Card = new Tempest() });
		entityManager.AddComponent(card, new AssignedBlockCard());
		entityManager.AddComponent(card, new AssignedBlockPresentation { Phase = AssignedBlockPresentation.PhaseState.Idle });

        _ = new CardZoneSystem(entityManager);

        var queuedEvent = new QueuedDiscardAssignedBlocksEvent(entityManager);
        queuedEvent.StartResolving();

        Assert.Equal(EventQueue.EventState.Complete, queuedEvent.State);
        Assert.Empty(entityManager.GetEntitiesWithComponent<CardToDiscardFlight>());
        Assert.Contains(card, deck.Hand);
        Assert.False(card.HasComponent<AssignedBlockCard>());
    }

    private static EntityManager BuildBattleWorld(
        SubPhase subPhase,
        out Entity enemy,
        out Deck deck,
        out Entity deckEntity,
        out Entity card)
    {
        var entityManager = new EntityManager();

        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.EnemyTurn,
            Sub = subPhase,
            TurnNumber = 1,
        });

        enemy = entityManager.CreateEntity("Enemy");
        entityManager.AddComponent(enemy, new Enemy());

        deckEntity = entityManager.CreateEntity("Deck");
        deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        card = entityManager.CreateEntity("AssignedBlockCard");
        entityManager.AddComponent(card, new CardData { Card = new Tempest() });
		entityManager.AddComponent(card, new AssignedBlockCard());
		entityManager.AddComponent(card, new AssignedBlockPresentation { Phase = AssignedBlockPresentation.PhaseState.Idle });
        entityManager.AddComponent(card, new UIElement { EventType = UIElementEventType.UnassignCardAsBlock });

        return entityManager;
    }
}
