using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.EnemyAttacks;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class EnemyAttackFlowTests : System.IDisposable
{
	public EnemyAttackFlowTests()
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
	public void Two_attack_turn_advances_with_fresh_progress_between_attacks()
	{
		var entityManager = CreateTwoAttackCombat(attack1Damage: 5, attack2Damage: 7);
		var confirmedSequences = new HashSet<int>();
		var progressSystem = new EnemyAttackProgressManagementSystem(entityManager);
		_ = new AttackResolutionSystem(entityManager);
		_ = new EnemyDamageManagerSystem(entityManager);
		_ = new PhaseCoordinatorSystem(entityManager);

		var card = CreateBlockerCard(entityManager);
		EventManager.Publish(new BlockAssignmentAdded
		{
			Card = card,
			DeltaBlock = 3,
			Colors = [CardData.CardColor.Red],
		});
		progressSystem.Update(new GameTime());

		Assert.True(EnemyAttackFlowService.TryGetCurrentProgress(entityManager, out var progress1));
		Assert.Equal(3, progress1.AssignedBlockTotal);
		Assert.Equal(1, progress1.AttackSequence);
		Assert.True(EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			confirmedSequences));

		ConfirmAndResolveCurrentAttack(entityManager, confirmedSequences);
		PumpEventQueue();

		var intent = GetEnemyIntent(entityManager);
		Assert.Single(intent.Planned);
		Assert.Equal(2, intent.ActiveAttackSequence);
		Assert.Empty(entityManager.GetEntitiesWithComponent<AssignedBlockCard>());

		progressSystem.Update(new GameTime());
		Assert.True(EnemyAttackFlowService.TryGetCurrentProgress(entityManager, out var progress2));
		Assert.Equal(0, progress2.AssignedBlockTotal);
		Assert.Equal(2, progress2.AttackSequence);
		Assert.NotSame(progress1, progress2);
		Assert.True(EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			confirmedSequences));

		ConfirmAndResolveCurrentAttack(entityManager, confirmedSequences);
		PumpEventQueue();

		var phase = entityManager.GetEntitiesWithComponent<PhaseState>().Single().GetComponent<PhaseState>();
		Assert.Equal(SubPhase.Action, phase.Sub);
		Assert.Equal(MainPhase.PlayerTurn, phase.Main);
		Assert.Empty(intent.Planned);
		Assert.Equal(2, confirmedSequences.Count);
	}

	[Fact]
	public void Repeated_attack_id_second_attack_is_not_treated_as_already_confirmed()
	{
		var entityManager = CreateTwoAttackCombat(
			attack1Damage: 4,
			attack2Damage: 6,
			sameAttackId: true);
		var confirmedSequences = new HashSet<int>();
		var progressSystem = new EnemyAttackProgressManagementSystem(entityManager);
		_ = new AttackResolutionSystem(entityManager);
		_ = new EnemyDamageManagerSystem(entityManager);
		_ = new PhaseCoordinatorSystem(entityManager);

		Assert.True(EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			confirmedSequences));

		ConfirmAndResolveCurrentAttack(entityManager, confirmedSequences);
		PumpEventQueue();

		var intent = GetEnemyIntent(entityManager);
		Assert.Equal(EnemyAttackId.Cinderbolt, intent.Planned[0].AttackId);
		Assert.Equal(2, intent.ActiveAttackSequence);
		Assert.Contains(1, confirmedSequences);
		Assert.DoesNotContain(2, confirmedSequences);
		Assert.True(EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			confirmedSequences));

		progressSystem.Update(new GameTime());
		Assert.True(EnemyAttackFlowService.TryGetCurrentProgress(entityManager, out var progress));
		Assert.Equal(2, progress.AttackSequence);
		Assert.Equal(0, progress.AssignedBlockTotal);
	}

	private static EntityManager CreateTwoAttackCombat(
		int attack1Damage,
		int attack2Damage,
		bool sameAttackId = false)
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
		entityManager.AddComponent(player, new HP { Max = 30, Current = 30 });

		var attack1 = new EnemyAttackBase
		{
			Id = EnemyAttackId.Cinderbolt,
			Name = "Attack One",
			Damage = attack1Damage,
		};
		var attack2 = new EnemyAttackBase
		{
			Id = sameAttackId ? EnemyAttackId.Cinderbolt : EnemyAttackId.InsidiousBolt,
			Name = "Attack Two",
			Damage = attack2Damage,
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

	private static Entity CreateBlockerCard(EntityManager entityManager)
	{
		var card = entityManager.CreateEntity("BlockerCard");
		entityManager.AddComponent(card, new CardData());
		entityManager.AddComponent(card, new AssignedBlockCard());
		entityManager.AddComponent(card, new AssignedBlockPresentation { Phase = AssignedBlockPresentation.PhaseState.Idle });
		return card;
	}

	private static AttackIntent GetEnemyIntent(EntityManager entityManager)
	{
		return entityManager.GetEntitiesWithComponent<AttackIntent>().Single().GetComponent<AttackIntent>();
	}

	private static void ConfirmAndResolveCurrentAttack(
		EntityManager entityManager,
		HashSet<int> confirmedSequences)
	{
		if (!EnemyAttackFlowService.TryGetCurrentEnemyAttack(
			entityManager,
			out _,
			out var intent,
			out _))
		{
			return;
		}

		confirmedSequences.Add(intent.ActiveAttackSequence);

		var resolver = new EnemyAttackResolver(entityManager, new ImmediateAttackPresentationGate());
		resolver.ResolveCurrentAttack();
	}

	private static void PumpEventQueue()
	{
		while (!EventQueue.IsIdle)
		{
			EventQueue.Update(AppliedPassivesManagementSystem.Duration + 0.1f);
		}
	}
}
