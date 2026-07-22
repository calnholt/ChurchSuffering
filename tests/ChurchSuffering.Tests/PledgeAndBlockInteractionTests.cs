using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class PledgeAndBlockInteractionTests : IDisposable
{
    public PledgeAndBlockInteractionTests()
    {
        EventManager.Clear();
        StateSingleton.IsActive = false;
        StateSingleton.PreventClicking = false;
        StateSingleton.IsTutorialActive = false;
        StateSingleton.IsPledgeEnabled = true;
    }

    public void Dispose()
    {
        EventManager.Clear();
        StateSingleton.IsActive = false;
        StateSingleton.PreventClicking = false;
        StateSingleton.IsTutorialActive = false;
        StateSingleton.IsPledgeEnabled = true;
    }

    [Theory]
    [InlineData("sealed", "Sealed cards cannot be pledged!")]
    [InlineData("weapon", "Can't pledge weapons!")]
    [InlineData("block", "Can't pledge block cards!")]
    [InlineData("relic", "Can't pledge relics!")]
    [InlineData("token", "Can't pledge token cards!")]
    public void Invalid_pledge_attempt_preserves_card_specific_message(
        string invalidKind,
        string expectedMessage)
    {
        var entityManager = BuildActionHand(out var deck, out _);
        var card = AddCard(entityManager, deck, CreateDefinition(invalidKind));
        if (invalidKind == "sealed")
        {
            entityManager.AddComponent(card, new Sealed());
        }
        _ = new PledgeManagementSystem(entityManager);
        var messages = new List<string>();
        EventManager.Subscribe<CantPlayCardMessage>(evt => messages.Add(evt.Message));

        EventManager.Publish(new PledgeCardRequested { Card = card });

        Assert.Equal([expectedMessage], messages);
        Assert.False(card.HasComponent<Pledge>());
    }

    [Fact]
    public void Pledge_attempt_preserves_once_per_action_phase_message()
    {
        var entityManager = BuildActionHand(out var deck, out _);
        var card = AddCard(entityManager, deck, new CardBase());
        SetPledgedThisActionPhase(entityManager);
        _ = new PledgeManagementSystem(entityManager);
        string message = null;
        EventManager.Subscribe<CantPlayCardMessage>(evt => message = evt.Message);

        EventManager.Publish(new PledgeCardRequested { Card = card });

        Assert.Equal("You can only pledge one card per action phase!", message);
    }

    [Fact]
    public void Pledge_attempt_preserves_existing_pledge_message()
    {
        var entityManager = BuildActionHand(out var deck, out _);
        var candidate = AddCard(entityManager, deck, new CardBase());
        var pledged = AddCard(entityManager, deck, new CardBase());
        entityManager.AddComponent(pledged, new Pledge());
        _ = new PledgeManagementSystem(entityManager);
        string message = null;
        EventManager.Subscribe<CantPlayCardMessage>(evt => message = evt.Message);

        EventManager.Publish(new PledgeCardRequested { Card = candidate });

        Assert.Equal("You already have a pledged card in hand!", message);
    }

    [Fact]
    public void Removing_a_pledged_card_does_not_restore_the_same_phase_opportunity()
    {
        var entityManager = BuildActionHand(out var deck, out var deckEntity);
        var first = AddCard(entityManager, deck, new CardBase());
        AddCard(entityManager, deck, new CardBase());
        _ = new PledgeManagementSystem(entityManager);

        EventManager.Publish(new PledgeCardRequested { Card = first });
        EventManager.Publish(new CardMoved
        {
            Card = first,
            Deck = deckEntity,
            From = CardZoneType.Hand,
            To = CardZoneType.DiscardPile,
        });

        var result = PledgeAvailabilityService.Evaluate(entityManager);
        Assert.False(result.IsAvailable);
        Assert.Equal(PledgeAvailabilityFailure.AlreadyPledgedThisActionPhase, result.Failure);
    }

    [Fact]
    public void Apply_pledge_request_adds_pledge_consumes_phase_and_runs_triggers()
    {
        var entityManager = BuildActionHand(out var deck, out _);
        var cardDefinition = new CountingPledgeCard();
        var card = AddCard(entityManager, deck, cardDefinition);
        _ = new PledgeManagementSystem(entityManager);
        Entity pledgedEventCard = null;
        EventManager.Subscribe<PledgeAddedEvent>(evt => pledgedEventCard = evt.Card);

        EventManager.Publish(new ApplyPledgeToCardRequested { Card = card });

        var pledge = card.GetComponent<Pledge>();
        Assert.NotNull(pledge);
        Assert.Same(card, pledge.Owner);
        Assert.False(pledge.CanPlay);
        Assert.Same(card, pledgedEventCard);
        Assert.Equal(1, cardDefinition.PledgedCount);
        Assert.True(entityManager.GetEntitiesWithComponent<PhaseState>()
            .First()
            .GetComponent<PledgeAvailabilityState>()
            .PledgedThisActionPhase);
    }

    [Fact]
    public void Remove_pledge_request_removes_pledge()
    {
        var entityManager = BuildActionHand(out var deck, out _);
        var card = AddCard(entityManager, deck, new CardBase());
        entityManager.AddComponent(card, new Pledge { Owner = card, CanPlay = true });
        _ = new PledgeManagementSystem(entityManager);

        EventManager.Publish(new RemovePledgeFromCardRequested { Card = card });

        Assert.False(card.HasComponent<Pledge>());
    }

    [Fact]
    public void Enemy_phase_reset_event_clears_pledge_state_in_pledge_management_system()
    {
        var entityManager = BuildActionHand(out var deck, out _);
        var pledged = AddCard(entityManager, deck, new CardBase());
        var preview = AddCard(entityManager, deck, new CardBase());
        entityManager.AddComponent(pledged, new Pledge { Owner = pledged, CanPlay = false });
        entityManager.AddComponent(preview, new PledgePreview { Owner = preview });
        SetPledgedThisActionPhase(entityManager);
        _ = new PledgeManagementSystem(entityManager);

        EventManager.Publish(new EnemyPhaseResetEvent());

        Assert.False(pledged.HasComponent<Pledge>());
        Assert.False(preview.HasComponent<PledgePreview>());
        Assert.False(entityManager.GetEntitiesWithComponent<PhaseState>()
            .First()
            .GetComponent<PledgeAvailabilityState>()
            .PledgedThisActionPhase);
    }

    [Fact]
    public void Hand_block_assignment_uses_typed_request_and_ignores_IsClicked_polling()
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.EnemyTurn,
            Sub = SubPhase.Block,
        });
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        var card = AddCard(entityManager, deck, new CardBase { Block = 3 });
        entityManager.AddComponent(card, new UIElement { IsInteractable = true, IsClicked = true });
        entityManager.AddComponent(card, new Transform { Position = new Vector2(100, 200) });
        var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned = [new PlannedAttack { AttackDefinition = new EnemyAttackBase() }],
		});
        var system = new HandBlockInteractionSystem(entityManager);
        int moves = 0;
        int assignments = 0;
        EventManager.Subscribe<CardMoveRequested>(evt =>
        {
            Assert.Same(card, evt.Card);
            moves++;
        });
        EventManager.Subscribe<BlockAssignmentAdded>(evt =>
        {
            Assert.Same(card, evt.Card);
            assignments++;
        });

        system.Update(new GameTime());
        Assert.Equal(0, moves);
        Assert.Equal(0, assignments);

        EventManager.Publish(new AssignCardAsBlockRequested { Card = card });

        Assert.Equal(1, moves);
        Assert.Equal(1, assignments);
    }

    private static EntityManager BuildActionHand(out Deck deck, out Entity deckEntity)
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.PlayerTurn,
            Sub = SubPhase.Action,
        });
        deckEntity = entityManager.CreateEntity("Deck");
        deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        return entityManager;
    }

    private static void SetPledgedThisActionPhase(EntityManager entityManager)
    {
        var phaseEntity = entityManager.GetEntitiesWithComponent<PhaseState>().First();
        var state = phaseEntity.GetComponent<PledgeAvailabilityState>();
        if (state == null)
        {
            state = new PledgeAvailabilityState { Owner = phaseEntity };
            entityManager.AddComponent(phaseEntity, state);
        }

        state.PledgedThisActionPhase = true;
    }

    private static Entity AddCard(EntityManager entityManager, Deck deck, CardBase definition)
    {
        var card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new CardData { Card = definition });
        deck.Hand.Add(card);
        return card;
    }

    private static CardBase CreateDefinition(string invalidKind)
    {
        return invalidKind switch
        {
            "weapon" => new CardBase { IsWeapon = true },
            "block" => new CardBase { Type = CardType.Block },
            "relic" => new CardBase { Type = CardType.Relic },
            "token" => new CardBase { IsToken = true },
            _ => new CardBase(),
        };
    }

    private sealed class CountingPledgeCard : CardBase
    {
        public int PledgedCount { get; private set; }

        public CountingPledgeCard()
        {
            OnPledged = (_, _) => PledgedCount++;
        }
    }
}
