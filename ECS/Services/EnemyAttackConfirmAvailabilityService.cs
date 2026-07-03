using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Services
{
	public static class EnemyAttackConfirmAvailabilityService
	{
		public static bool CanConfirmCurrentAttack(
			EntityManager entityManager,
			ISet<int> confirmedAttackSequences = null)
		{
			return CanResolveCurrentAttackConfirm(entityManager, confirmedAttackSequences);
		}

		public static bool CanRequestCurrentAttackConfirm(
			EntityManager entityManager,
			ISet<int> confirmedAttackSequences = null)
		{
			return MeetsCurrentAttackConfirmRequirements(
				entityManager,
				confirmedAttackSequences,
				includePendingBlockConfirm: true);
		}

		public static bool CanResolveCurrentAttackConfirm(
			EntityManager entityManager,
			ISet<int> confirmedAttackSequences = null)
		{
			return MeetsCurrentAttackConfirmRequirements(
					entityManager,
					confirmedAttackSequences,
					includePendingBlockConfirm: false)
				&& !IsAnyBlockAssignmentAnimating(entityManager);
		}

		private static bool MeetsCurrentAttackConfirmRequirements(
			EntityManager entityManager,
			ISet<int> confirmedAttackSequences,
			bool includePendingBlockConfirm)
		{
			if (entityManager == null) return false;
			if (BattleInputGate.IsBattleInputFrozen(entityManager, includePendingBlockConfirm)) return false;

			var phase = entityManager.GetEntitiesWithComponent<PhaseState>()
				.FirstOrDefault()
				?.GetComponent<PhaseState>();
			if (phase?.Sub != SubPhase.Block) return false;

			if (!EnemyAttackFlowService.TryGetCurrentEnemyAttack(entityManager, out _, out var intent, out var planned))
				return false;

			if (confirmedAttackSequences != null && confirmedAttackSequences.Contains(intent.ActiveAttackSequence))
				return false;

			if (!BattleInputGate.IsTutorialActionAllowed(entityManager, TutorialAction.ConfirmBlocks))
				return false;

			if (planned?.AttackDefinition == null) return false;

			int activeBlockCount = CountActiveAssignedBlockers(entityManager);
			return MeetsAttackBlockRequirement(planned.AttackDefinition.ConditionType, activeBlockCount);
		}

		public static int CountActiveAssignedBlockers(EntityManager entityManager)
		{
			if (entityManager == null) return 0;

			return entityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Select(entity => entity.GetComponent<AssignedBlockCard>())
				.Count(assignment => assignment != null
					&& assignment.Phase != AssignedBlockCard.PhaseState.Returning);
		}

		public static bool IsAnyBlockAssignmentAnimating(EntityManager entityManager)
		{
			if (entityManager == null) return false;

			return entityManager.GetEntitiesWithComponent<AssignedBlockCard>()
				.Select(entity => entity.GetComponent<AssignedBlockCard>())
				.Any(assignment => assignment != null
					&& assignment.Phase != AssignedBlockCard.PhaseState.Idle);
		}

		private static bool MeetsAttackBlockRequirement(ConditionType conditionType, int activeBlockCount)
		{
			return conditionType switch
			{
				ConditionType.MustBeBlockedByAtLeast1Card => activeBlockCount >= 1,
				ConditionType.MustBeBlockedByAtLeast2Cards => activeBlockCount >= 2,
				ConditionType.MustBeBlockedByExactly1Card => activeBlockCount == 1,
				ConditionType.MustBeBlockedByExactly2Cards => activeBlockCount == 2,
				_ => true,
			};
		}
	}
}
