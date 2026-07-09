using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class MaleficRiteTests : IDisposable
{
	public MaleficRiteTests()
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
	public void Text_updates_from_curses_removed_tracking_events()
	{
		var (entityManager, maleficRiteEntity) = BuildWorldWithMaleficRite();
		var card = GetMaleficRite(maleficRiteEntity);

		Assert.Contains("You have removed 0 curses", card.Text);

		EventManager.Publish(new TrackingEvent
		{
			Type = TrackingTypeEnum.CursesRemoved.ToString(),
			Delta = 1
		});

		Assert.Contains("You have removed 1 curse", card.Text);

		EventManager.Publish(new TrackingEvent
		{
			Type = TrackingTypeEnum.CursesRemoved.ToString(),
			Delta = 1
		});

		Assert.Contains("You have removed 2 curses", card.Text);
	}

	[Fact]
	public void Base_play_gains_four_plus_twice_curses_removed_this_climb()
	{
		var (entityManager, maleficRiteEntity) = BuildWorldWithMaleficRite();
		SetCursesRemoved(entityManager, 2);
		var passiveEvents = new List<ApplyPassiveEvent>();
		EventManager.Subscribe<ApplyPassiveEvent>(passiveEvents.Add);

		GetMaleficRite(maleficRiteEntity).OnPlay?.Invoke(entityManager, maleficRiteEntity);

		var evt = Assert.Single(passiveEvents);
		Assert.Equal(AppliedPassiveType.Aggression, evt.Type);
		Assert.Equal(8, evt.Delta);
	}

	[Fact]
	public void Upgraded_play_gains_four_plus_thrice_curses_removed_this_climb()
	{
		var (entityManager, maleficRiteEntity) = BuildWorldWithMaleficRite(isUpgraded: true);
		SetCursesRemoved(entityManager, 2);
		var passiveEvents = new List<ApplyPassiveEvent>();
		EventManager.Subscribe<ApplyPassiveEvent>(passiveEvents.Add);

		GetMaleficRite(maleficRiteEntity).OnPlay?.Invoke(entityManager, maleficRiteEntity);

		var evt = Assert.Single(passiveEvents);
		Assert.Equal(AppliedPassiveType.Aggression, evt.Type);
		Assert.Equal(10, evt.Delta);
	}

	[Fact]
	public void Disposed_card_unsubscribes_from_tracking_events()
	{
		var (_, maleficRiteEntity) = BuildWorldWithMaleficRite();
		var card = GetMaleficRite(maleficRiteEntity);

		card.Dispose();

		EventManager.Publish(new TrackingEvent
		{
			Type = TrackingTypeEnum.CursesRemoved.ToString(),
			Delta = 1
		});

		Assert.Contains("You have removed 0 curses", card.Text);
	}

	[Fact]
	public void Cursing_malefic_rite_disposes_replaced_card_subscription()
	{
		var (entityManager, maleficRiteEntity) = BuildWorldWithMaleficRite();
		var original = GetMaleficRite(maleficRiteEntity);
		_ = new CardApplicationManagementSystem(entityManager);
		_ = new CursedManagementSystem(entityManager);

		EventManager.Publish(new ApplyCardApplicationEvent
		{
			Card = maleficRiteEntity,
			Amount = 1,
			Type = CardApplicationType.Cursed,
			Target = CardApplicationTarget.Deck,
		});

		Assert.Equal(Curse.CardIdValue, maleficRiteEntity.GetComponent<CardData>()?.Card?.CardId);

		EventManager.Publish(new TrackingEvent
		{
			Type = TrackingTypeEnum.CursesRemoved.ToString(),
			Delta = 1
		});

		Assert.Contains("You have removed 0 curses", original.Text);
	}

	private static (EntityManager EntityManager, Entity MaleficRiteEntity) BuildWorldWithMaleficRite(
		bool isUpgraded = false)
	{
		var entityManager = new EntityManager();
		_ = new BattleStateInfoManagementSystem(entityManager);

		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);

		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player { DeckEntity = deckEntity });
		entityManager.AddComponent(player, new BattleStateInfo());

		var maleficRiteEntity = EntityFactory.CreateCardFromDefinition(
			entityManager,
			"malefic_rite",
			CardData.CardColor.Black,
			index: 0,
			isUpgraded: isUpgraded);
		Assert.NotNull(maleficRiteEntity);

		deck.Cards.Add(maleficRiteEntity);
		deck.Hand.Add(maleficRiteEntity);
		return (entityManager, maleficRiteEntity);
	}

	private static MaleficRite GetMaleficRite(Entity entity)
	{
		var card = Assert.IsType<MaleficRite>(entity.GetComponent<CardData>()?.Card);
		return card;
	}

	private static void SetCursesRemoved(EntityManager entityManager, int amount)
	{
		var player = entityManager.GetEntity("Player");
		Assert.NotNull(player);
		var battleState = player.GetComponent<BattleStateInfo>();
		Assert.NotNull(battleState);
		battleState.RunTracking[TrackingTypeEnum.CursesRemoved.ToString()] = amount;
	}
}
