using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class RecoilManagementSystemTests : System.IDisposable
{
	public RecoilManagementSystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void BeginDefeatPresentation_clears_recoil_without_damage()
	{
		var entityManager = BuildWorld(out var enemy, out var card);
		int damageEvents = 0;
		EventManager.Subscribe<ModifyHpRequestEvent>(_ => damageEvents++);
		_ = new RecoilManagementSystem(entityManager);

		Assert.NotNull(card.GetComponent<Recoil>());

		EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });

		Assert.Equal(0, damageEvents);
		Assert.Null(card.GetComponent<Recoil>());
	}

	[Fact]
	public void EnemyPhaseReset_clears_recoil_without_damage()
	{
		var entityManager = BuildWorld(out _, out var card);
		int damageEvents = 0;
		EventManager.Subscribe<ModifyHpRequestEvent>(_ => damageEvents++);
		_ = new RecoilManagementSystem(entityManager);

		Assert.NotNull(card.GetComponent<Recoil>());

		EventManager.Publish(new EnemyPhaseResetEvent());

		Assert.Equal(0, damageEvents);
		Assert.Null(card.GetComponent<Recoil>());
	}

	private static EntityManager BuildWorld(out Entity enemy, out Entity card)
	{
		var entityManager = new EntityManager();

		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());

		enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());

		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);

		card = entityManager.CreateEntity("Card_0");
		entityManager.AddComponent(card, new CardData { Card = new CardBase { CardId = "strike" } });
		entityManager.AddComponent(card, new Recoil { Owner = card, Stacks = 3 });
		deck.Hand.Add(card);

		return entityManager;
	}
}
