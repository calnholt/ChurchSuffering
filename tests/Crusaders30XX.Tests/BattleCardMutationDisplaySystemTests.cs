using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class BattleCardMutationDisplaySystemTests : IDisposable
{
	public BattleCardMutationDisplaySystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
		BattleMutationTestSupport.ResetSettings();
	}

	[Fact]
	public void Hand_application_applies_immediately_without_animation_or_input_gate()
	{
		var entityManager = new EntityManager();
		var deck = CreateDeck(entityManager);
		var card = AddCard(entityManager, deck, deck.Hand, new Tempest());
		var mutationSystem = BattleMutationTestSupport.CreateBattleMutationPipeline(entityManager);

		Assert.False(card.HasComponent<Frozen>());

		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = card,
			Type = CardApplicationType.Frozen,
		});

		Assert.False(mutationSystem.IsBusy);
		Assert.True(card.HasComponent<Frozen>());
		Assert.False(card.HasComponent<SuppressCardZoneRender>());
		Assert.False(StateSingleton.PreventClicking);
		Assert.False(entityManager.GetEntity("PhaseState")?.GetComponent<PhaseState>()?.BattleAnimationActive ?? true);
	}

	[Fact]
	public void Hand_application_keeps_restriction_sfx()
	{
		var entityManager = new EntityManager();
		var deck = CreateDeck(entityManager);
		var card = AddCard(entityManager, deck, deck.Hand, new Tempest());
		var mutationSystem = BattleMutationTestSupport.CreateBattleMutationPipeline(entityManager);
		var played = new List<SfxTrack>();
		EventManager.Subscribe<PlaySfxEvent>(evt => played.Add(evt.Track));

		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = card,
			Type = CardApplicationType.Brittle,
		});

		Assert.False(mutationSystem.IsBusy);
		Assert.Contains(SfxTrack.ApplyBrittle, played);
	}

	[Fact]
	public void Hand_seal_application_adds_stacks_without_animation()
	{
		var entityManager = new EntityManager();
		var deck = CreateDeck(entityManager);
		var card = AddCard(entityManager, deck, deck.Hand, new Tempest());
		var mutationSystem = BattleMutationTestSupport.CreateBattleMutationPipeline(entityManager);

		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = card,
			Type = CardApplicationType.Sealed,
			StacksPerCard = 2,
		});
		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = card,
			Type = CardApplicationType.Sealed,
			StacksPerCard = 3,
		});

		Assert.False(mutationSystem.IsBusy);
		Assert.Equal(5, card.GetComponent<Sealed>()?.Seals);
		Assert.False(card.HasComponent<SuppressCardZoneRender>());
		Assert.False(StateSingleton.PreventClicking);
	}

	[Fact]
	public void Queued_applications_run_sequentially_and_keep_input_blocked_until_done()
	{
		var entityManager = new EntityManager();
		var deck = CreateDeck(entityManager);
		var first = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
		var second = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
		var mutationSystem = BattleMutationTestSupport.CreateBattleMutationPipeline(entityManager);

		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = first,
			Type = CardApplicationType.Frozen,
		});
		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = second,
			Type = CardApplicationType.Scorched,
		});

		Assert.True(StateSingleton.PreventClicking);
		BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);

		Assert.True(first.HasComponent<Frozen>());
		Assert.True(second.HasComponent<Scorched>());
		Assert.False(mutationSystem.IsBusy);
		Assert.False(StateSingleton.PreventClicking);
	}

	[Fact]
	public void Queued_application_applies_immediately_if_card_enters_hand_before_starting()
	{
		var entityManager = new EntityManager();
		var deck = CreateDeck(entityManager);
		var first = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
		var second = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
		var mutationSystem = BattleMutationTestSupport.CreateBattleMutationPipeline(entityManager);

		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = first,
			Type = CardApplicationType.Frozen,
		});
		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = second,
			Type = CardApplicationType.Scorched,
		});
		deck.DrawPile.Remove(second);
		deck.Hand.Add(second);

		BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);

		Assert.True(first.HasComponent<Frozen>());
		Assert.True(second.HasComponent<Scorched>());
		Assert.False(second.HasComponent<SuppressCardZoneRender>());
		Assert.False(mutationSystem.IsBusy);
		Assert.False(StateSingleton.PreventClicking);
	}

	[Fact]
	public void DeleteCachesEvent_clears_queue_and_unblocks_input()
	{
		var entityManager = new EntityManager();
		var deck = CreateDeck(entityManager);
		var card = AddCard(entityManager, deck, deck.DrawPile, new Tempest());
		var mutationSystem = BattleMutationTestSupport.CreateBattleMutationPipeline(entityManager);

		EventManager.Publish(new CardRestrictionMutationAnimationRequested
		{
			TargetCard = card,
			Type = CardApplicationType.Thorned,
		});
		Assert.True(mutationSystem.IsBusy);

		EventManager.Publish(new DeleteCachesEvent());
		BattleMutationTestSupport.CompleteMutations(entityManager, mutationSystem);

		Assert.False(mutationSystem.IsBusy);
		Assert.False(card.HasComponent<Thorned>());
		Assert.False(card.HasComponent<SuppressCardZoneRender>());
		Assert.False(StateSingleton.PreventClicking);
	}

	[Fact]
	public void Hand_display_skips_suppressed_cards()
	{
		var entityManager = new EntityManager();
		var deck = CreateDeck(entityManager);
		var card = AddCard(entityManager, deck, deck.Hand, new Tempest());
		entityManager.AddComponent(card, new SuppressCardZoneRender { Owner = card });

		Assert.False(InvokeShouldDrawInHand(card));
	}

	private static bool InvokeShouldDrawInHand(Entity card)
	{
		var method = typeof(HandDisplaySystem).GetMethod(
			"ShouldDrawInHand",
			System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
		Assert.NotNull(method);
		return (bool)method.Invoke(null, new object[] { card });
	}

	private static Deck CreateDeck(EntityManager entityManager)
	{
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		return deck;
	}

	private static Entity AddCard(
		EntityManager entityManager,
		Deck deck,
		ICollection<Entity> zone,
		CardBase definition)
	{
		var card = entityManager.CreateEntity(definition.CardId);
		entityManager.AddComponent(card, new CardData { Card = definition });
		deck.Cards.Add(card);
		zone.Add(card);
		return card;
	}
}
