using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class EnemyAttackEntityManagerIsolationTests
{
	[Fact]
	public void Attack_reveal_uses_the_entity_manager_passed_to_its_callback()
	{
		var redWorld = CreateWorldWithSingleHandColor(CardData.CardColor.Red);
		var whiteWorld = CreateWorldWithSingleHandColor(CardData.CardColor.White);
		var redAttack = new Cinderbolt();
		var whiteAttack = new Cinderbolt();

		redAttack.Initialize(redWorld);
		whiteAttack.Initialize(whiteWorld);
		redAttack.OnAttackReveal?.Invoke(redWorld);
		whiteAttack.OnAttackReveal?.Invoke(whiteWorld);

		Assert.Contains("red card", redAttack.Text);
		Assert.Contains("white card", whiteAttack.Text);
	}

	private static EntityManager CreateWorldWithSingleHandColor(CardData.CardColor color)
	{
		var entityManager = new EntityManager();
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AppliedPassives());

		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);
		var card = entityManager.CreateEntity($"{color}Card");
		entityManager.AddComponent(card, new CardData
		{
			Card = new Tempest(),
			Color = color
		});
		deck.Hand.Add(card);
		return entityManager;
	}
}
