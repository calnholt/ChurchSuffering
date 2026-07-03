using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class CardInputRoutingTests : IDisposable
{
    public CardInputRoutingTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Primary_hand_card_click_in_block_publishes_typed_assignment_only()
    {
        var entityManager = BuildHand(SubPhase.Block, out var card);
        int assignments = 0;
        int plays = 0;
        EventManager.Subscribe<AssignCardAsBlockRequested>(evt =>
        {
            Assert.Same(card, evt.Card);
            assignments++;
        });
        EventManager.Subscribe<PlayCardRequested>(_ => plays++);

        UIElementEventDelegateService.HandleEvent(
            UIElementEventType.CardClicked,
            card,
            entityManager);

        Assert.Equal(1, assignments);
        Assert.Equal(0, plays);
    }

    [Fact]
    public void Primary_hand_card_click_in_action_publishes_play_only()
    {
        var entityManager = BuildHand(SubPhase.Action, out var card);
        int assignments = 0;
        int plays = 0;
        EventManager.Subscribe<AssignCardAsBlockRequested>(_ => assignments++);
        EventManager.Subscribe<PlayCardRequested>(evt =>
        {
            Assert.Same(card, evt.Card);
            plays++;
        });

        UIElementEventDelegateService.HandleEvent(
            UIElementEventType.CardClicked,
            card,
            entityManager);

        Assert.Equal(0, assignments);
        Assert.Equal(1, plays);
    }

    [Fact]
    public void Secondary_hand_card_action_routes_only_during_action_phase()
    {
        var entityManager = BuildHand(SubPhase.Action, out var card);
        var phase = entityManager.GetEntitiesWithComponent<PhaseState>()
            .Single()
            .GetComponent<PhaseState>();
        int pledges = 0;
        EventManager.Subscribe<PledgeCardRequested>(evt =>
        {
            Assert.Same(card, evt.Card);
            pledges++;
        });

        UIElementEventDelegateService.HandleEvent(
            UIElementEventType.PledgeCard,
            card,
            entityManager);
        phase.Sub = SubPhase.Block;
        UIElementEventDelegateService.HandleEvent(
            UIElementEventType.PledgeCard,
            card,
            entityManager);

        Assert.Equal(1, pledges);
    }

    [Fact]
    public void Secondary_action_does_not_route_for_a_card_outside_the_hand()
    {
        var entityManager = BuildHand(SubPhase.Action, out _);
        var outsideCard = entityManager.CreateEntity("OutsideCard");
        entityManager.AddComponent(outsideCard, new CardData { Card = new CardBase() });
        int pledges = 0;
        EventManager.Subscribe<PledgeCardRequested>(_ => pledges++);

        UIElementEventDelegateService.HandleEvent(
            UIElementEventType.PledgeCard,
            outsideCard,
            entityManager);

        Assert.Equal(0, pledges);
    }

    [Fact]
    public void Display_only_card_list_modal_click_does_not_publish_card_selection()
    {
        var entityManager = new EntityManager();
        var card = entityManager.CreateEntity("DisplayOnlyCard");
        entityManager.AddComponent(card, new CardData { Card = new CardBase { CardId = "smite" } });
        var modalEntity = entityManager.CreateEntity("CardListModal");
        entityManager.AddComponent(modalEntity, new CardListModal
        {
            IsOpen = true,
            IsSelectable = false,
            Cards = new System.Collections.Generic.List<Entity> { card },
        });
        int selections = 0;
        EventManager.Subscribe<CardListModalCardSelectedEvent>(_ => selections++);

        UIElementEventDelegateService.HandleEvent(
            UIElementEventType.CardClicked,
            card,
            entityManager);

        Assert.Equal(0, selections);
    }

    private static EntityManager BuildHand(SubPhase subPhase, out Entity card)
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = subPhase == SubPhase.Action ? MainPhase.PlayerTurn : MainPhase.EnemyTurn,
            Sub = subPhase,
        });
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new CardData { Card = new CardBase() });
        deck.Hand.Add(card);
        return entityManager;
    }
}
