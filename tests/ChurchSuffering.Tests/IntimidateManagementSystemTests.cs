using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class IntimidateManagementSystemTests : System.IDisposable
{
	public IntimidateManagementSystemTests()
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

	[Fact]
	public void ClearInteractionState_preserves_intimidation()
	{
		var entityManager = BuildWorld(out _);

		Assert.Equal(2, CountIntimidated(entityManager));

		BattleTransientStateCleanupService.ClearInteractionState(entityManager);

		Assert.Equal(2, CountIntimidated(entityManager));
	}

	[Fact]
	public void Intimidation_survives_advance_to_next_planned_attack()
	{
		var entityManager = CreateTwoAttackCombat();
		_ = new IntimidateManagementSystem(entityManager);

		EventManager.Publish(new IntimidateEvent { Amount = 1 });
		var intimidatedCard = entityManager.GetEntitiesWithComponent<Intimidated>().Single();
		Assert.NotNull(intimidatedCard);

		EventQueue.EnqueueRule(new QueuedAdvanceToNextPlannedAttackEvent(entityManager));
		PumpEventQueue();

		Assert.NotNull(intimidatedCard.GetComponent<Intimidated>());
		var intent = entityManager.GetEntitiesWithComponent<AttackIntent>().Single().GetComponent<AttackIntent>();
		Assert.Equal(2, intent.ActiveAttackSequence);
	}

	private static EntityManager CreateTwoAttackCombat()
	{
		var entityManager = new EntityManager();
		var phaseEntity = entityManager.CreateEntity("PhaseState");
		entityManager.AddComponent(phaseEntity, new PhaseState
		{
			Main = MainPhase.EnemyTurn,
			Sub = SubPhase.Block,
			TurnNumber = 1,
		});

		var player = entityManager.CreateEntity("Player");
		entityManager.AddComponent(player, new Player());

		var deckEntity = entityManager.CreateEntity("Deck");
		var deck = new Deck();
		entityManager.AddComponent(deckEntity, deck);

		for (int i = 0; i < 3; i++)
		{
			var card = entityManager.CreateEntity($"Card_{i}");
			entityManager.AddComponent(card, new CardData { Card = new CardBase { Block = 2 } });
			deck.Hand.Add(card);
		}

		var attack1 = new EnemyAttackBase
		{
			Id = EnemyAttackId.Cinderbolt,
			Name = "Attack One",
			Damage = 5,
		};
		var attack2 = new EnemyAttackBase
		{
			Id = EnemyAttackId.InsidiousBolt,
			Name = "Attack Two",
			Damage = 7,
		};

		var enemy = entityManager.CreateEntity("Enemy");
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack { AttackId = attack1.Id, AttackDefinition = attack1 },
				new PlannedAttack { AttackId = attack2.Id, AttackDefinition = attack2 },
			],
		});

		return entityManager;
	}

	private static void PumpEventQueue()
	{
		while (!EventQueue.IsIdle)
		{
			EventQueue.Update(AppliedPassivesManagementSystem.Duration + 0.1f);
		}
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
