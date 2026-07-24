using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class PledgeAvailabilityServiceTests : IDisposable
{
    public PledgeAvailabilityServiceTests()
    {
        StateSingleton.IsPledgeEnabled = true;
    }

    public void Dispose()
    {
        StateSingleton.IsPledgeEnabled = true;
    }

    [Fact]
    public void Evaluate_returns_available_when_every_rule_is_satisfied()
    {
        var entityManager = BuildBattle(SubPhase.Action, out var deck, out _);
        AddCard(entityManager, deck, new CardBase());

        var result = PledgeAvailabilityService.Evaluate(entityManager);

        Assert.True(result.IsAvailable);
        Assert.Equal(PledgeAvailabilityFailure.None, result.Failure);
    }

    [Fact]
    public void Evaluate_returns_disabled_when_pledging_is_disabled()
    {
        var entityManager = BuildBattle(SubPhase.Action, out var deck, out _);
        AddCard(entityManager, deck, new CardBase());
        StateSingleton.IsPledgeEnabled = false;

        AssertFailure(entityManager, PledgeAvailabilityFailure.Disabled);
    }

    [Fact]
    public void Evaluate_requires_the_action_phase()
    {
        var entityManager = BuildBattle(SubPhase.Block, out var deck, out _);
        AddCard(entityManager, deck, new CardBase());

        AssertFailure(entityManager, PledgeAvailabilityFailure.NotActionPhase);
    }

    [Fact]
    public void Evaluate_rejects_a_consumed_action_phase_opportunity()
    {
        var entityManager = BuildBattle(SubPhase.Action, out var deck, out _);
        AddCard(entityManager, deck, new CardBase());
        SetPledgedThisActionPhase(entityManager);

        AssertFailure(entityManager, PledgeAvailabilityFailure.AlreadyPledgedThisActionPhase);
    }

    [Fact]
    public void Evaluate_rejects_any_existing_pledged_card()
    {
        var entityManager = BuildBattle(SubPhase.Action, out var deck, out _);
        AddCard(entityManager, deck, new CardBase());
        var pledgedCard = entityManager.CreateEntity("PledgedOutsideHand");
        entityManager.AddComponent(pledgedCard, new CardData { Card = new CardBase() });
        entityManager.AddComponent(pledgedCard, new Pledge());

        AssertFailure(entityManager, PledgeAvailabilityFailure.CardAlreadyPledged);
    }

    [Fact]
    public void Evaluate_requires_a_hand()
    {
        var entityManager = BuildBattle(SubPhase.Action, out var deck, out _);
        deck.Hand = null;

        AssertFailure(entityManager, PledgeAvailabilityFailure.MissingHand);
    }

    [Fact]
    public void Evaluate_requires_at_least_one_eligible_card()
    {
        var entityManager = BuildBattle(SubPhase.Action, out var deck, out _);
        var sealedCard = AddCard(entityManager, deck, new CardBase());
        entityManager.AddComponent(sealedCard, new Sealed());

        AssertFailure(entityManager, PledgeAvailabilityFailure.NoEligibleCard);
    }

    [Fact]
    public void EvaluateCard_covers_every_ineligibility_branch_and_message()
    {
        var entityManager = new EntityManager();

        AssertCardFailure(entityManager, null, PledgeCardEligibilityFailure.MissingCard, "");
        AssertCardFailure(
            entityManager,
            entityManager.CreateEntity("MissingCardData"),
            PledgeCardEligibilityFailure.MissingCardData,
            "");

        var pledged = CreateCard(entityManager, new CardBase());
        entityManager.AddComponent(pledged, new Pledge());
        AssertCardFailure(entityManager, pledged, PledgeCardEligibilityFailure.AlreadyPledged, "");

        var sealedCard = CreateCard(entityManager, new CardBase());
        entityManager.AddComponent(sealedCard, new Sealed());
        AssertCardFailure(
            entityManager,
            sealedCard,
            PledgeCardEligibilityFailure.Sealed,
            "Sealed cards cannot be pledged!");

        AssertCardFailure(
            entityManager,
            CreateCard(entityManager, new CardBase { IsWeapon = true }),
            PledgeCardEligibilityFailure.Weapon,
            "Can't pledge weapons!");
        AssertCardFailure(
            entityManager,
            CreateCard(entityManager, new CardBase { Type = CardType.Block }),
            PledgeCardEligibilityFailure.Block,
            "Can't pledge block cards!");
        AssertCardFailure(
            entityManager,
            CreateCard(entityManager, new CardBase { Type = CardType.Relic }),
            PledgeCardEligibilityFailure.Relic,
            "Can't pledge relics!");
        AssertCardFailure(
            entityManager,
            CreateCard(entityManager, new CardBase { IsToken = true }),
            PledgeCardEligibilityFailure.Token,
            "Can't pledge token cards!");

        Assert.True(PledgeAvailabilityService.EvaluateCard(
            entityManager,
            CreateCard(entityManager, new CardBase())).IsEligible);
    }

    private static EntityManager BuildBattle(
        SubPhase subPhase,
        out Deck deck,
        out PhaseState phase)
    {
        var entityManager = new EntityManager();
        var phaseEntity = entityManager.CreateEntity("PhaseState");
        phase = new PhaseState { Main = MainPhase.PlayerTurn, Sub = subPhase };
        entityManager.AddComponent(phaseEntity, phase);
        var deckEntity = entityManager.CreateEntity("Deck");
        deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        return entityManager;
    }

    private static void SetPledgedThisActionPhase(EntityManager entityManager)
    {
        var phaseEntity = entityManager.GetEntitiesWithComponent<PhaseState>().First();
        entityManager.AddComponent(phaseEntity, new PledgeAvailabilityState
        {
            Owner = phaseEntity,
            PledgedThisActionPhase = true,
        });
    }

    private static Entity AddCard(EntityManager entityManager, Deck deck, CardBase definition)
    {
        var card = CreateCard(entityManager, definition);
        deck.Hand.Add(card);
        return card;
    }

    private static Entity CreateCard(EntityManager entityManager, CardBase definition)
    {
        var card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new CardData { Card = definition });
        return card;
    }

    private static void AssertFailure(
        EntityManager entityManager,
        PledgeAvailabilityFailure expected)
    {
        var result = PledgeAvailabilityService.Evaluate(entityManager);
        Assert.False(result.IsAvailable);
        Assert.Equal(expected, result.Failure);
    }

    private static void AssertCardFailure(
        EntityManager entityManager,
        Entity card,
        PledgeCardEligibilityFailure expected,
        string message)
    {
        var result = PledgeAvailabilityService.EvaluateCard(entityManager, card);
        Assert.False(result.IsEligible);
        Assert.Equal(expected, result.Failure);
        Assert.Equal(message, result.RejectionMessage);
    }
}
