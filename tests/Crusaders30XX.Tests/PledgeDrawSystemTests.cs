using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class PledgeDrawSystemTests
{
	[Fact]
	public void CalculateCardsToDraw_ignores_pledged_non_weapon_cards()
	{
		var entityManager = new EntityManager();
		var pledgedCard = CreateCard(entityManager, new Strike());
		entityManager.AddComponent(pledgedCard, new Pledge { CanPlay = false });

		int cardsToDraw = DrawHandSystem.CalculateCardsToDraw(
			intellect: 4,
			maxHandSize: 4,
			hand: [pledgedCard]);

		Assert.Equal(4, cardsToDraw);
	}

	[Fact]
	public void CalculateCardsToDraw_ignores_token_cards()
	{
		var entityManager = new EntityManager();
		var tokenCard = CreateCard(entityManager, new CardBase { CardId = "token", IsToken = true });

		int cardsToDraw = DrawHandSystem.CalculateCardsToDraw(
			intellect: 4,
			maxHandSize: 4,
			hand: [tokenCard]);

		Assert.Equal(4, cardsToDraw);
	}

	[Fact]
	public void CalculateCardsToDraw_counts_unpledged_non_weapon_cards()
	{
		var entityManager = new EntityManager();
		var handCard = CreateCard(entityManager, new Strike());

		int cardsToDraw = DrawHandSystem.CalculateCardsToDraw(
			intellect: 4,
			maxHandSize: 4,
			hand: [handCard]);

		Assert.Equal(3, cardsToDraw);
	}

	private static Entity CreateCard(EntityManager entityManager, CardBase card)
	{
		var entity = entityManager.CreateEntity(card.CardId);
		entityManager.AddComponent(entity, new CardData { Card = card });
		return entity;
	}
}
