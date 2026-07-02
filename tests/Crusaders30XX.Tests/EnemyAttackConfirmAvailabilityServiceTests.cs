using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class EnemyAttackConfirmAvailabilityServiceTests
{
	[Fact]
	public void Normal_attack_can_be_confirmed_with_zero_blockers()
	{
		var entityManager = CreateCombat(ConditionType.None);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager);

		Assert.True(canConfirm);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void Must_block_at_least_two_cards_cannot_confirm_with_too_few_blockers(int blockerCount)
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlockers(entityManager, blockerCount);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager);

		Assert.False(canConfirm);
	}

	[Fact]
	public void Must_block_at_least_two_cards_can_confirm_with_two_idle_blockers()
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlockers(entityManager, 2);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager);

		Assert.True(canConfirm);
	}

	[Fact]
	public void Valid_attack_with_animating_blocker_can_be_requested_but_not_resolved()
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlocker(entityManager, AssignedBlockCard.PhaseState.Idle);
		AddBlocker(entityManager, AssignedBlockCard.PhaseState.Launch);

		var canRequest = EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(entityManager);
		var canResolve = EnemyAttackConfirmAvailabilityService.CanResolveCurrentAttackConfirm(entityManager);
		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager);

		Assert.True(canRequest);
		Assert.False(canResolve);
		Assert.False(canConfirm);
	}

	[Fact]
	public void Returning_blocker_does_not_allow_confirm()
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlockers(entityManager, 1);
		AddBlocker(entityManager, AssignedBlockCard.PhaseState.Returning);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager);

		Assert.False(canConfirm);
	}

	[Fact]
	public void Confirmed_attack_sequence_cannot_be_requested_or_resolved_again()
	{
		var entityManager = CreateCombat(ConditionType.None);
		var confirmed = new HashSet<int> { 1 };

		var canRequest = EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
			entityManager,
			confirmed);
		var canResolve = EnemyAttackConfirmAvailabilityService.CanResolveCurrentAttackConfirm(
			entityManager,
			confirmed);

		Assert.False(canRequest);
		Assert.False(canResolve);
	}

	[Fact]
	public void Pending_block_confirm_freezes_battle_input()
	{
		var entityManager = CreateCombat(ConditionType.None);
		var phase = entityManager.GetEntitiesWithComponent<PhaseState>()
			.Single()
			.GetComponent<PhaseState>();
		phase.PendingBlockConfirm = true;

		Assert.True(BattleInputGate.IsBattleInputFrozen(entityManager));
	}

	[Theory]
	[InlineData(0, false)]
	[InlineData(1, true)]
	[InlineData(2, false)]
	public void Exact_one_card_requirement_requires_exactly_one_active_blocker(
		int blockerCount,
		bool expected)
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByExactly1Card);
		AddBlockers(entityManager, blockerCount);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager);

		Assert.Equal(expected, canConfirm);
	}

	[Fact]
	public void Confirmed_attack_sequence_cannot_confirm_again()
	{
		var entityManager = CreateCombat(ConditionType.None);
		var confirmed = new HashSet<int> { 1 };

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			confirmed);

		Assert.False(canConfirm);
	}

	[Fact]
	public void Cannot_request_confirm_when_enemy_hp_is_zero()
	{
		var entityManager = CreateCombat(ConditionType.None);
		var enemy = entityManager.GetEntitiesWithComponent<AttackIntent>().First();
		entityManager.AddComponent(enemy, new Enemy());
		entityManager.AddComponent(enemy, new HP { Max = 30, Current = 0 });

		var canRequest = EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(entityManager);

		Assert.False(canRequest);
	}

	[Fact]
	public void Confirm_gating_works_without_confirmed_sequences_parameter()
	{
		var entityManager = CreateCombat(ConditionType.None);

		Assert.True(EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(entityManager));
		Assert.True(EnemyAttackConfirmAvailabilityService.CanResolveCurrentAttackConfirm(entityManager));
		Assert.True(EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager));
	}

	[Fact]
	public void Prior_confirmed_sequence_does_not_block_later_attack_sequence()
	{
		var entityManager = CreateCombat(ConditionType.None);
		var intent = entityManager.GetEntitiesWithComponent<AttackIntent>().Single().GetComponent<AttackIntent>();
		intent.ActiveAttackSequence = 2;
		var confirmed = new HashSet<int> { 1 };

		Assert.True(EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(entityManager, confirmed));
	}

	private static EntityManager CreateCombat(ConditionType conditionType)
	{
		var entityManager = new EntityManager();
		var phase = entityManager.CreateEntity("PhaseState");
		var enemy = entityManager.CreateEntity("Enemy");
		var attack = new EnemyAttackBase
		{
			Id = EnemyAttackId.Cinderbolt,
			Name = "Test Attack",
			Damage = 5,
			ConditionType = conditionType
		};

		entityManager.AddComponent(phase, new PhaseState { Sub = SubPhase.Block });
		entityManager.AddComponent(enemy, new AttackIntent
		{
			ActiveAttackSequence = 1,
			Planned =
			[
				new PlannedAttack
				{
					AttackId = attack.Id,
					AttackDefinition = attack
				}
			]
		});

		return entityManager;
	}

	private static void AddBlockers(EntityManager entityManager, int count)
	{
		for (int i = 0; i < count; i++)
		{
			AddBlocker(entityManager, AssignedBlockCard.PhaseState.Idle);
		}
	}

	private static void AddBlocker(
		EntityManager entityManager,
		AssignedBlockCard.PhaseState phase)
	{
		var card = entityManager.CreateEntity("Blocker");
		entityManager.AddComponent(card, new AssignedBlockCard
		{
			Phase = phase,
			BlockAmount = 1
		});
	}
}
