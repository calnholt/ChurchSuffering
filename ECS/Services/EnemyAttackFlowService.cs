using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.ECS.Services
{
	public static class EnemyAttackFlowService
	{
		public static bool TryGetCurrentEnemyAttack(
			EntityManager entityManager,
			out Entity enemy,
			out AttackIntent intent,
			out PlannedAttack planned)
		{
			enemy = null;
			intent = null;
			planned = null;

			if (entityManager == null) return false;

			enemy = entityManager.GetEntitiesWithComponent<AttackIntent>().FirstOrDefault();
			if (enemy == null) return false;

			intent = enemy.GetComponent<AttackIntent>();
			if (intent?.Planned == null || intent.Planned.Count == 0) return false;

			planned = intent.Planned[0];
			return planned != null;
		}

		public static bool TryGetCurrentProgress(EntityManager entityManager, out EnemyAttackProgress progress)
		{
			progress = null;
			if (entityManager == null) return false;

			if (!TryGetCurrentEnemyAttack(entityManager, out var enemy, out var intent, out _))
				return false;

			progress = FindProgressForEnemy(entityManager, enemy);
			if (progress == null) return false;

			return progress.AttackSequence == intent.ActiveAttackSequence;
		}

		public static EnemyAttackProgress GetOrCreateCurrentProgress(
			EntityManager entityManager,
			Entity enemy,
			AttackIntent intent,
			PlannedAttack planned)
		{
			if (entityManager == null || enemy == null || intent == null || planned == null) return null;

			var existing = FindProgressForEnemy(entityManager, enemy);
			if (existing != null && existing.AttackSequence == intent.ActiveAttackSequence)
			{
				existing.AttackId = planned.AttackId;
				return existing;
			}

			if (existing != null)
			{
				var progressEntity = entityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
					.FirstOrDefault(e => e.GetComponent<EnemyAttackProgress>() == existing);
				if (progressEntity != null) entityManager.DestroyEntity(progressEntity.Id);
			}

			return CreateProgress(entityManager, enemy, intent, planned);
		}

		public static void ResetCurrentProgress(
			EntityManager entityManager,
			Entity enemy,
			AttackIntent intent,
			PlannedAttack planned)
		{
			if (entityManager == null || enemy == null) return;

			var existing = FindProgressForEnemy(entityManager, enemy);
			if (existing != null)
			{
				var progressEntity = entityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
					.FirstOrDefault(e => e.GetComponent<EnemyAttackProgress>() == existing);
				if (progressEntity != null) entityManager.DestroyEntity(progressEntity.Id);
			}

			if (intent != null && planned != null)
				CreateProgress(entityManager, enemy, intent, planned);
		}

		public static bool HasCurrentAttack(EntityManager entityManager)
		{
			return TryGetCurrentEnemyAttack(entityManager, out _, out _, out _);
		}

		public static int GetActiveAttackSequence(EntityManager entityManager)
		{
			if (!TryGetCurrentEnemyAttack(entityManager, out _, out var intent, out _))
				return -1;
			return intent.ActiveAttackSequence;
		}

		private static EnemyAttackProgress FindProgressForEnemy(EntityManager entityManager, Entity enemy)
		{
			return entityManager.GetEntitiesWithComponent<EnemyAttackProgress>()
				.Select(e => e.GetComponent<EnemyAttackProgress>())
				.FirstOrDefault(p => p != null && p.Enemy == enemy);
		}

		private static EnemyAttackProgress CreateProgress(
			EntityManager entityManager,
			Entity enemy,
			AttackIntent intent,
			PlannedAttack planned)
		{
			var entity = entityManager.CreateEntity($"EnemyAttackProgress[{intent.ActiveAttackSequence}]");
			var comp = new EnemyAttackProgress
			{
				Enemy = enemy,
				AttackId = planned.AttackId,
				AttackSequence = intent.ActiveAttackSequence,
				AssignedBlockTotal = 0,
				PlayedCards = 0,
				PlayedRed = 0,
				PlayedWhite = 0,
				PlayedBlack = 0,
				IsConditionMet = false,
				ActualDamage = 0,
				AegisTotal = DamagePredictionService.GetAegisAmount(entityManager)
			};
			entityManager.AddComponent(entity, comp);
			return comp;
		}
	}
}
