using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Services
{
	public static class BattleInputGate
	{
		public static bool IsBattleInputFrozen(EntityManager entityManager)
		{
			return IsBattleInputFrozen(entityManager, includePendingBlockConfirm: true);
		}

		public static bool IsBattleInputFrozen(EntityManager entityManager, bool includePendingBlockConfirm)
		{
			var phase = entityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
			return (phase != null && phase.DefeatPresentationActive)
				|| IsEnemyDefeated(entityManager)
				|| (includePendingBlockConfirm && phase?.PendingBlockConfirm == true)
				|| StateSingleton.IsActive;
		}

		public static bool IsEnemyDefeated(EntityManager entityManager)
		{
			var enemy = entityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
			var hp = enemy?.GetComponent<HP>();
			return hp != null && hp.Current <= 0;
		}

		public static bool ShouldSuppressEnemyAttackDisplay(EntityManager entityManager)
		{
			var enemy = entityManager.GetEntitiesWithComponent<Enemy>().FirstOrDefault();
			if (enemy == null) return false;
			if (enemy.HasComponent<SuppressPortraitRender>()) return true;
			return IsEnemyDefeated(entityManager);
		}

		public static bool TryAllowTutorialAction(
			EntityManager entityManager,
			TutorialAction action,
			Entity card = null)
		{
			return IsTutorialActionAllowed(entityManager, action, card);
		}

		public static bool IsTutorialActionAllowed(
			EntityManager entityManager,
			TutorialAction action,
			Entity card = null)
		{
			return true;
		}
	}
}
