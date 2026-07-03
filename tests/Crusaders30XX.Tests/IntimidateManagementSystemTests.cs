using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class IntimidateManagementSystemTests : System.IDisposable
{
	public IntimidateManagementSystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void BeginDefeatPresentation_clears_intimidation_without_enemy_end()
	{
		var entityManager = BuildWorld(out var enemy);
		_ = new IntimidateManagementSystem(entityManager);

		Assert.Equal(2, CountIntimidated(entityManager));

		EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });
		Assert.Equal(0, CountIntimidated(entityManager));
	}

	[Fact]
	public void EnemyPhaseReset_clears_intimidation_without_enemy_end()
	{
		var entityManager = BuildWorld(out _);
		_ = new IntimidateManagementSystem(entityManager);

		Assert.Equal(2, CountIntimidated(entityManager));

		EventManager.Publish(new EnemyPhaseResetEvent());
		Assert.Equal(0, CountIntimidated(entityManager));
	}

	private static EntityManager BuildWorld(out Entity enemy)
	{
		var entityManager = new EntityManager();

		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());

		enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());

		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);

		for (int i = 0; i < 2; i++)
		{
			var card = entityManager.CreateEntity($"Card_{i}");
			entityManager.AddComponent(card, new CardData { Card = new CardBase { Block = 2 } });
			entityManager.AddComponent(card, new Intimidated { Owner = card });
			deck.Hand.Add(card);
		}

		return entityManager;
	}

	private static int CountIntimidated(EntityManager entityManager)
	{
		return entityManager.GetEntitiesWithComponent<Intimidated>().Count();
	}
}
