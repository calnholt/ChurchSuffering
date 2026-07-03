using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public class ShackleManagementSystemTests : System.IDisposable
{
	public ShackleManagementSystemTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void PreBlock_applies_shackles_only_once_per_enemy_turn()
	{
		var entityManager = BuildWorld(out _);
		_ = new ShackleManagementSystem(entityManager);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });
		int shackledAfterFirst = CountShackled(entityManager);
		Assert.Equal(2, shackledAfterFirst);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });
		Assert.Equal(2, CountShackled(entityManager));
	}

	[Fact]
	public void PreBlock_reapplies_shackles_after_enemy_turn_resets()
	{
		var entityManager = BuildWorld(out _);
		_ = new ShackleManagementSystem(entityManager);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });
		Assert.Equal(2, CountShackled(entityManager));

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyEnd });
		Assert.Equal(0, CountShackled(entityManager));

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.EnemyStart });
		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });
		Assert.Equal(2, CountShackled(entityManager));
	}

	[Fact]
	public void BeginDefeatPresentation_clears_shackles_without_enemy_end()
	{
		var entityManager = BuildWorld(out _);
		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new Enemy());
		_ = new ShackleManagementSystem(entityManager);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });
		Assert.Equal(2, CountShackled(entityManager));

		EventManager.Publish(new BeginDefeatPresentationEvent { Enemy = enemy, IsPreview = false });
		Assert.Equal(0, CountShackled(entityManager));
	}

	[Fact]
	public void EnemyPhaseReset_clears_shackles_without_enemy_end()
	{
		var entityManager = BuildWorld(out _);
		_ = new ShackleManagementSystem(entityManager);

		EventManager.Publish(new ChangeBattlePhaseEvent { Current = SubPhase.PreBlock });
		Assert.Equal(2, CountShackled(entityManager));

		EventManager.Publish(new EnemyPhaseResetEvent());
		Assert.Equal(0, CountShackled(entityManager));
	}

	private static EntityManager BuildWorld(out Entity player)
	{
		var entityManager = new EntityManager();

		player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());
		var passives = new AppliedPassives();
		passives.Passives[AppliedPassiveType.Shackled] = 1;
		entityManager.AddComponent(player, passives);

		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);

		for (int i = 0; i < 4; i++)
		{
			var card = entityManager.CreateEntity($"Card_{i}");
			entityManager.AddComponent(card, new CardData { Card = new CardBase { Block = 2 } });
			deck.Hand.Add(card);
		}

		return entityManager;
	}

	private static int CountShackled(EntityManager entityManager)
	{
		return entityManager.GetEntitiesWithComponent<Shackle>().Count();
	}
}
