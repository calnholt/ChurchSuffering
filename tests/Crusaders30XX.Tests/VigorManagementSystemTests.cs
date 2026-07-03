using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class VigorManagementSystemTests : System.IDisposable
{
	public VigorManagementSystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Gained_vigor_not_consumed_for_same_card()
	{
		var entityManager = BuildWorld(out var player, startingVigor: 0);
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new VigorManagementSystem(entityManager);

		var card = new SteadfastResolve { IsUpgraded = true };
		card.OnUpgrade(entityManager, entityManager.CreateEntity("SteadfastResolve"));
		var cardEntity = entityManager.CreateEntity("SteadfastResolve");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });

		card.OnPlay(entityManager, cardEntity);
		EventManager.Publish(new CardPlayedEvent { Card = cardEntity, VigorStacksAtPlay = 0 });

		Assert.Equal(4, GetVigor(player));
	}

	[Fact]
	public void Consumes_only_pre_play_stacks()
	{
		var entityManager = BuildWorld(out var player, startingVigor: 2);
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new VigorManagementSystem(entityManager);

		var card = new SteadfastResolve { IsUpgraded = true };
		card.OnUpgrade(entityManager, entityManager.CreateEntity("SteadfastResolve"));
		var cardEntity = entityManager.CreateEntity("SteadfastResolve");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });

		card.OnPlay(entityManager, cardEntity);
		EventManager.Publish(new CardPlayedEvent { Card = cardEntity, VigorStacksAtPlay = 2 });

		Assert.Equal(4, GetVigor(player));
	}

	[Fact]
	public void Consumes_pre_play_stacks_for_non_vigor_cards()
	{
		var entityManager = BuildWorld(out var player, startingVigor: 3);
		_ = new AppliedPassivesManagementSystem(entityManager);
		_ = new VigorManagementSystem(entityManager);

		var card = new CardBase
		{
			CardId = "test_cost_card",
			Cost = ["Any", "Any"],
		};
		var cardEntity = entityManager.CreateEntity("TestCostCard");
		entityManager.AddComponent(cardEntity, new CardData { Card = card });

		EventManager.Publish(new CardPlayedEvent { Card = cardEntity, VigorStacksAtPlay = 3 });

		Assert.Equal(1, GetVigor(player));
	}

	private static EntityManager BuildWorld(out Entity player, int startingVigor)
	{
		var entityManager = new EntityManager();
		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		entityManager.AddComponent(player, new AppliedPassives
		{
			Passives = { [AppliedPassiveType.Vigor] = startingVigor }
		});
		return entityManager;
	}

	private static int GetVigor(Entity player)
	{
		var passives = player.GetComponent<AppliedPassives>()?.Passives;
		if (passives == null) return 0;
		return passives.TryGetValue(AppliedPassiveType.Vigor, out int stacks) ? stacks : 0;
	}
}
