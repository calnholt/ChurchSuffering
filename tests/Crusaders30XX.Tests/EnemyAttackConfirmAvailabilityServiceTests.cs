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

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			"test-context");

		Assert.True(canConfirm);
	}

	[Theory]
	[InlineData(0)]
	[InlineData(1)]
	public void Must_block_at_least_two_cards_cannot_confirm_with_too_few_blockers(int blockerCount)
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlockers(entityManager, "test-context", blockerCount);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			"test-context");

		Assert.False(canConfirm);
	}

	[Fact]
	public void Must_block_at_least_two_cards_can_confirm_with_two_idle_blockers()
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlockers(entityManager, "test-context", 2);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			"test-context");

		Assert.True(canConfirm);
	}

	[Fact]
	public void Valid_attack_with_animating_blocker_can_be_requested_but_not_resolved()
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlocker(entityManager, "test-context", AssignedBlockCard.PhaseState.Idle);
		AddBlocker(entityManager, "test-context", AssignedBlockCard.PhaseState.Launch);

		var canRequest = EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
			entityManager,
			"test-context");
		var canResolve = EnemyAttackConfirmAvailabilityService.CanResolveCurrentAttackConfirm(
			entityManager,
			"test-context");
		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			"test-context");

		Assert.True(canRequest);
		Assert.False(canResolve);
		Assert.False(canConfirm);
	}

	[Fact]
	public void Returning_blocker_does_not_allow_confirm()
	{
		var entityManager = CreateCombat(ConditionType.MustBeBlockedByAtLeast2Cards);
		AddBlockers(entityManager, "test-context", 1);
		AddBlocker(entityManager, "test-context", AssignedBlockCard.PhaseState.Returning);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			"test-context");

		Assert.False(canConfirm);
	}

	[Fact]
	public void Confirmed_context_cannot_be_requested_or_resolved_again()
	{
		var entityManager = CreateCombat(ConditionType.None);
		var confirmed = new HashSet<string> { "test-context" };

		var canRequest = EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
			entityManager,
			"test-context",
			confirmed);
		var canResolve = EnemyAttackConfirmAvailabilityService.CanResolveCurrentAttackConfirm(
			entityManager,
			"test-context",
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
		phase.PendingBlockConfirmContextId = "test-context";

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
		AddBlockers(entityManager, "test-context", blockerCount);

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			"test-context");

		Assert.Equal(expected, canConfirm);
	}

	[Fact]
	public void Confirmed_context_cannot_confirm_again()
	{
		var entityManager = CreateCombat(ConditionType.None);
		var confirmed = new HashSet<string> { "test-context" };

		var canConfirm = EnemyAttackConfirmAvailabilityService.CanConfirmCurrentAttack(
			entityManager,
			"test-context",
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

		var canRequest = EnemyAttackConfirmAvailabilityService.CanRequestCurrentAttackConfirm(
			entityManager,
			"test-context");

		Assert.False(canRequest);
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
			Planned =
			[
				new PlannedAttack
				{
					AttackId = attack.Id,
					ContextId = "test-context",
					AttackDefinition = attack
				}
			]
		});

		return entityManager;
	}

	private static void AddBlockers(EntityManager entityManager, string contextId, int count)
	{
		for (int i = 0; i < count; i++)
		{
			AddBlocker(entityManager, contextId, AssignedBlockCard.PhaseState.Idle);
		}
	}

	private static void AddBlocker(
		EntityManager entityManager,
		string contextId,
		AssignedBlockCard.PhaseState phase)
	{
		var card = entityManager.CreateEntity("Blocker");
		entityManager.AddComponent(card, new AssignedBlockCard
		{
			ContextId = contextId,
			Phase = phase,
			BlockAmount = 1
		});
	}
}
