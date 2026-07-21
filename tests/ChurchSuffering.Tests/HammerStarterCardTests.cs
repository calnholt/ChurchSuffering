using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class HammerStarterCardTests : IDisposable
{
	public HammerStarterCardTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Battering_blow_gains_courage_when_no_cards_were_discarded()
	{
		var entityManager = BuildPlayerWithCourage(0);
		_ = new CourageManagerSystem(entityManager);

		var card = new BatteringBlow();
		var cardEntity = entityManager.CreateEntity("BatteringBlow");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });
		entityManager.AddComponent(cardEntity, new ModifiedDamage());

		card.OnPlay(entityManager, cardEntity);

		var player = entityManager.GetEntity("Player");
		Assert.Equal(3, player.GetComponent<Courage>().Amount);
	}

	[Fact]
	public void Battering_blow_does_not_gain_courage_when_cards_were_discarded()
	{
		var entityManager = BuildPlayerWithCourage(0);
		_ = new CourageManagerSystem(entityManager);

		var cacheEntity = entityManager.CreateEntity("LastPaymentCache");
		entityManager.AddComponent(cacheEntity, new LastPaymentCache
		{
			PaymentCards = [entityManager.CreateEntity("Payment")]
		});

		var card = new BatteringBlow();
		var cardEntity = entityManager.CreateEntity("BatteringBlow");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });
		entityManager.AddComponent(cardEntity, new ModifiedDamage());

		card.OnPlay(entityManager, cardEntity);

		var player = entityManager.GetEntity("Player");
		Assert.Equal(0, player.GetComponent<Courage>().Amount);
	}

	[Fact]
	public void Unburdened_strike_gains_bonus_damage_when_no_cards_were_discarded()
	{
		var entityManager = new EntityManager();
		var card = new UnburdenedStrike();
		var cardEntity = entityManager.CreateEntity("UnburdenedStrike");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });
		entityManager.AddComponent(cardEntity, new ModifiedDamage());
		var damageRequests = new List<ModifyHpRequestEvent>();
		EventManager.Subscribe<ModifyHpRequestEvent>(damageRequests.Add);

		entityManager.AddComponent(cardEntity, new CardPlayStatContext
		{
			Owner = cardEntity,
			PaymentCards = []
		});
		card.OnPlay(entityManager, cardEntity);
		Assert.Equal(-11, Assert.Single(damageRequests).Delta);
		damageRequests.Clear();

		entityManager.RemoveComponent<CardPlayStatContext>(cardEntity);
		entityManager.AddComponent(cardEntity, new CardPlayStatContext
		{
			Owner = cardEntity,
			PaymentCards = [entityManager.CreateEntity("Payment")]
		});

		card.OnPlay(entityManager, cardEntity);
		Assert.Equal(-8, Assert.Single(damageRequests).Delta);
	}

	[Theory]
	[InlineData(2, 1)]
	[InlineData(4, 2)]
	[InlineData(6, 3)]
	[InlineData(7, 3)]
	public void Stoke_the_furnace_repeats_up_to_three_times_based_on_courage(int startingCourage, int expectedVigor)
	{
		var entityManager = BuildPlayerWithCourage(startingCourage);
		_ = new CourageManagerSystem(entityManager);
		_ = new AppliedPassivesManagementSystem(entityManager);

		var card = new StokeTheFurnace();
		var cardEntity = entityManager.CreateEntity("StokeTheFurnace");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });
		entityManager.AddComponent(cardEntity, new ModifiedDamage());

		card.OnPlay(entityManager, cardEntity);

		var player = entityManager.GetEntity("Player");
		Assert.Equal(startingCourage - (expectedVigor * 2), player.GetComponent<Courage>().Amount);
		Assert.Equal(expectedVigor, GetVigor(player));
	}

	[Fact]
	public void Renounce_and_hone_rejects_pledged_this_turn_card()
	{
		var entityManager = BuildActionHand(out var deck, out _);
		var pledged = AddCard(entityManager, deck, new CardBase());
		entityManager.AddComponent(pledged, new Pledge { CanPlay = false });

		var card = new RenounceAndHone();
		var cardEntity = entityManager.CreateEntity("RenounceAndHone");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });

		Assert.False(card.CanPlay(entityManager, cardEntity));
	}

	[Fact]
	public void Renounce_and_hone_accepts_prior_turn_pledged_card()
	{
		var entityManager = BuildActionHand(out var deck, out _);
		var pledged = AddCard(entityManager, deck, new CardBase());
		entityManager.AddComponent(pledged, new Pledge { CanPlay = true });

		var card = new RenounceAndHone();
		var cardEntity = entityManager.CreateEntity("RenounceAndHone");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });

		Assert.True(card.CanPlay(entityManager, cardEntity));
	}

	[Fact]
	public void Hammer_starter_pool_cards_are_registered()
	{
		foreach (var cardId in StartingDeckGeneratorService.GetHammerStarterCardPool()
			.Concat(StartingDeckGeneratorService.GetHammerSingleCopyStarterCardPool()))
		{
			Assert.NotNull(CardFactory.Create(cardId));
		}
	}

	private static EntityManager BuildPlayerWithCourage(int courage)
	{
		var entityManager = new EntityManager();
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new Courage { Amount = courage });
		entityManager.AddComponent(player, new AppliedPassives());
		return entityManager;
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

	private static Entity AddCard(EntityManager entityManager, Deck deck, CardBase definition)
	{
		var card = entityManager.CreateEntity("Card");
		entityManager.AddComponent(card, new CardData { Card = definition });
		deck.Hand.Add(card);
		return card;
	}

	private static int GetVigor(Entity player)
	{
		var passives = player.GetComponent<AppliedPassives>()?.Passives;
		if (passives == null) return 0;
		return passives.TryGetValue(AppliedPassiveType.Vigor, out int stacks) ? stacks : 0;
	}
}
