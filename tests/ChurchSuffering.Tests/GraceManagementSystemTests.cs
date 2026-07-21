using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class GraceManagementSystemTests : System.IDisposable
{
	public GraceManagementSystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Triggers_resurrect_and_consumes_one_stack()
	{
		var entityManager = BuildWorld(out var player, out var deckEntity, out var deck, startingGrace: 2);
		deck.DiscardPile.Add(CreateCard(entityManager));
		deck.DiscardPile.Add(CreateCard(entityManager));
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new DeckManagementSystem(entityManager);
		_ = new GraceManagementSystem(entityManager);

		PassiveTriggered triggered = null;
		EventManager.Subscribe<PassiveTriggered>(evt => triggered = evt);

		EventManager.Publish(new StartOfTurnDrawResolvedEvent
		{
			Player = player,
			Deck = deckEntity,
			Phase = SubPhase.EnemyStart,
			RequestedDrawCount = 1
		});

		Assert.Equal(1, GetGrace(player));
		Assert.Single(deck.Hand);
		Assert.Single(deck.DiscardPile);
		Assert.NotNull(triggered);
		Assert.Equal(AppliedPassiveType.Grace, triggered.Type);
		Assert.Same(player, triggered.Owner);
	}

	[Fact]
	public void Noops_when_grace_is_zero()
	{
		var entityManager = BuildWorld(out var player, out var deckEntity, out var deck, startingGrace: 0);
		deck.DiscardPile.Add(CreateCard(entityManager));
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new DeckManagementSystem(entityManager);
		_ = new GraceManagementSystem(entityManager);

		DrawRandomCardFromDiscardEvent resurrectEvent = null;
		EventManager.Subscribe<DrawRandomCardFromDiscardEvent>(evt => resurrectEvent = evt);

		EventManager.Publish(new StartOfTurnDrawResolvedEvent
		{
			Player = player,
			Deck = deckEntity,
			Phase = SubPhase.EnemyStart,
			RequestedDrawCount = 1
		});

		Assert.Equal(0, GetGrace(player));
		Assert.Empty(deck.Hand);
		Assert.Single(deck.DiscardPile);
		Assert.Null(resurrectEvent);
	}

	[Fact]
	public void Consumes_stack_even_when_discard_is_empty()
	{
		var entityManager = BuildWorld(out var player, out var deckEntity, out _, startingGrace: 1);
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new DeckManagementSystem(entityManager);
		_ = new GraceManagementSystem(entityManager);

		EventManager.Publish(new StartOfTurnDrawResolvedEvent
		{
			Player = player,
			Deck = deckEntity,
			Phase = SubPhase.EnemyStart,
			RequestedDrawCount = 0
		});

		Assert.Equal(0, GetGrace(player));
	}

	private static EntityManager BuildWorld(out Entity player, out Entity deckEntity, out Deck deck, int startingGrace)
	{
		var entityManager = new EntityManager();
		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new AppliedPassives
		{
			Passives = { [AppliedPassiveType.Grace] = startingGrace }
		});

		deckEntity = entityManager.CreateEntity("Deck");
		deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		return entityManager;
	}

	private static Entity CreateCard(EntityManager entityManager)
	{
		var entity = entityManager.CreateEntity("tempest");
		entityManager.AddComponent(entity, new CardData { Card = new Tempest() });
		entityManager.AddComponent(entity, new Transform { Position = Vector2.Zero });
		entityManager.AddComponent(entity, new UIElement { Bounds = new Rectangle(-1000, -1000, 1, 1) });
		return entity;
	}

	private static int GetGrace(Entity player)
	{
		var passives = player.GetComponent<AppliedPassives>()?.Passives;
		if (passives == null) return 0;
		return passives.TryGetValue(AppliedPassiveType.Grace, out int stacks) ? stacks : 0;
	}
}
