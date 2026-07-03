using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class SubZeroTests : System.IDisposable
{
	public SubZeroTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void PreBlock_with_SubZero_freezes_one_hand_card_and_publishes_PassiveTriggered()
	{
		var entityManager = BuildWorld(hasSubZero: true, out var player, out var deck);
		var eligibleCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
		var otherCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new CardApplicationManagementSystem(entityManager);

		int passiveTriggeredCount = 0;
		EventManager.Subscribe<PassiveTriggered>(evt =>
		{
			if (evt.Type == AppliedPassiveType.SubZero && evt.Owner == player)
			{
				passiveTriggeredCount++;
			}
		});

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });

		Assert.Equal(1, passiveTriggeredCount);
		Assert.Equal(1, new[] { eligibleCard, otherCard }.Count(card => card.HasComponent<Frozen>()));
	}

	[Fact]
	public void PreBlock_without_SubZero_does_not_freeze_hand_cards()
	{
		var entityManager = BuildWorld(hasSubZero: false, out _, out var deck);
		var handCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new CardApplicationManagementSystem(entityManager);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });

		Assert.False(handCard.HasComponent<Frozen>());
	}

	[Fact]
	public void PreBlock_skips_ineligible_and_already_frozen_cards()
	{
		var entityManager = BuildWorld(hasSubZero: true, out _, out var deck);
		var eligibleCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
		var alreadyFrozenCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
		var pledgedCard = AddCard(entityManager, deck, deck.Hand, new Tempest());
		var weaponCard = AddCard(entityManager, deck, deck.Hand, new Dagger());
		entityManager.AddComponent(alreadyFrozenCard, new Frozen());
		entityManager.AddComponent(pledgedCard, new Pledge());
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new CardApplicationManagementSystem(entityManager);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });

		Assert.True(eligibleCard.HasComponent<Frozen>());
		Assert.True(alreadyFrozenCard.HasComponent<Frozen>());
		Assert.False(pledgedCard.HasComponent<Frozen>());
		Assert.False(weaponCard.HasComponent<Frozen>());
	}

	private static EntityManager BuildWorld(bool hasSubZero, out Entity player, out Deck deck)
	{
		var entityManager = new EntityManager();

		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		var passives = new AppliedPassives();
		if (hasSubZero)
		{
			passives.Passives[AppliedPassiveType.SubZero] = 1;
		}
		entityManager.AddComponent(player, passives);

		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new AppliedPassives());

		var deckEntity = entityManager.CreateEntity("Deck");
		deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);

		return entityManager;
	}

	private static Entity AddCard(
		EntityManager entityManager,
		Deck deck,
		System.Collections.Generic.ICollection<Entity> zone,
		CardBase definition)
	{
		var card = entityManager.CreateEntity(definition.CardId);
		entityManager.AddComponent(card, new CardData { Card = definition });
		deck.Cards.Add(card);
		zone.Add(card);
		return card;
	}
}
