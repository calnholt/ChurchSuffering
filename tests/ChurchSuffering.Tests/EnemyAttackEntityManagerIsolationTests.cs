using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using Xunit;

namespace ChurchSuffering.Tests;

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
