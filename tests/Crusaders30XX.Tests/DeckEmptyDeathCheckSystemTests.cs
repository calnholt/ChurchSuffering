using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class DeckEmptyDeathCheckSystemTests
{
	[Fact]
	public void Start_of_turn_draw_resolution_with_empty_hand_publishes_player_died()
	{
		RunWithEventManagerCleanup(() =>
		{
			var entityManager = new EntityManager();
			var player = CreatePlayer(entityManager);
			var (deckEntity, _) = CreateDeck(entityManager);
			_ = new DeckEmptyDeathCheckSystem(entityManager);
			int deaths = 0;
			EventManager.Subscribe<PlayerDied>(_ => deaths++);

			PublishResolved(player, deckEntity, requestedDrawCount: 4);

			Assert.Equal(1, deaths);
		});
	}

	[Fact]
	public void Pledged_card_in_hand_prevents_deck_empty_death()
	{
		RunWithEventManagerCleanup(() =>
		{
			var entityManager = new EntityManager();
			var player = CreatePlayer(entityManager);
			var (deckEntity, deck) = CreateDeck(entityManager);
			var pledgedCard = CreateCard(entityManager);
			entityManager.AddComponent(pledgedCard, new Pledge { CanPlay = false });
			deck.Hand.Add(pledgedCard);
			_ = new DeckEmptyDeathCheckSystem(entityManager);
			int deaths = 0;
			EventManager.Subscribe<PlayerDied>(_ => deaths++);

			PublishResolved(player, deckEntity, requestedDrawCount: 4);

			Assert.Equal(0, deaths);
		});
	}

	[Fact]
	public void Drawn_card_in_hand_prevents_deck_empty_death()
	{
		RunWithEventManagerCleanup(() =>
		{
			var entityManager = new EntityManager();
			var player = CreatePlayer(entityManager);
			var (deckEntity, deck) = CreateDeck(entityManager);
			deck.Hand.Add(CreateCard(entityManager));
			_ = new DeckEmptyDeathCheckSystem(entityManager);
			int deaths = 0;
			EventManager.Subscribe<PlayerDied>(_ => deaths++);

			PublishResolved(player, deckEntity, requestedDrawCount: 4);

			Assert.Equal(0, deaths);
		});
	}

	[Fact]
	public void Zero_requested_draw_count_does_not_publish_player_died()
	{
		RunWithEventManagerCleanup(() =>
		{
			var entityManager = new EntityManager();
			var player = CreatePlayer(entityManager);
			var (deckEntity, _) = CreateDeck(entityManager);
			_ = new DeckEmptyDeathCheckSystem(entityManager);
			int deaths = 0;
			EventManager.Subscribe<PlayerDied>(_ => deaths++);

			PublishResolved(player, deckEntity, requestedDrawCount: 0);

			Assert.Equal(0, deaths);
		});
	}

	[Fact]
	public void Duplicate_empty_draw_resolutions_publish_player_died_once()
	{
		RunWithEventManagerCleanup(() =>
		{
			var entityManager = new EntityManager();
			var player = CreatePlayer(entityManager);
			var (deckEntity, _) = CreateDeck(entityManager);
			_ = new DeckEmptyDeathCheckSystem(entityManager);
			int deaths = 0;
			EventManager.Subscribe<PlayerDied>(_ => deaths++);

			PublishResolved(player, deckEntity, requestedDrawCount: 4);
			PublishResolved(player, deckEntity, requestedDrawCount: 4);

			Assert.Equal(1, deaths);
		});
	}

	private static void RunWithEventManagerCleanup(System.Action action)
	{
		EventManager.Clear();
		try
		{
			action();
		}
		finally
		{
			EventManager.Clear();
		}
	}

	private static Entity CreatePlayer(EntityManager entityManager)
	{
		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		return player;
	}

	private static (Entity Entity, Deck Deck) CreateDeck(EntityManager entityManager)
	{
		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		return (deckEntity, deck);
	}

	private static Entity CreateCard(EntityManager entityManager)
	{
		var card = entityManager.CreateEntity("strike");
		entityManager.AddComponent(card, new CardData { Card = new Strike() });
		return card;
	}

	private static void PublishResolved(Entity player, Entity deck, int requestedDrawCount)
	{
		EventManager.Publish(new StartOfTurnDrawResolvedEvent
		{
			Player = player,
			Deck = deck,
			Phase = SubPhase.EnemyStart,
			RequestedDrawCount = requestedDrawCount
		});
	}
}
