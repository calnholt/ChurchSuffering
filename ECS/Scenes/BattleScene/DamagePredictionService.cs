using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using System;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	public static class DamagePredictionService
	{
		public static int ComputeFullDamage(EnemyAttackBase definition)
		{
			if (definition == null) return 0;

			return definition.Damage;
		}

		public static int GetAegisAmount(EntityManager entityManager)
		{
			var player = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			var passives = player?.GetComponent<AppliedPassives>()?.Passives;
			if (passives == null) return 0;
			var value = passives.TryGetValue(AppliedPassiveType.Aegis, out var aegis) ? aegis : 0;
			return Math.Max(value, 0);
		}

		public static int GetAssignedBlockForCurrentAttack(EntityManager entityManager)
		{
			if (!EnemyAttackFlowService.TryGetCurrentProgress(entityManager, out var progress))
				return 0;
			return progress.AssignedBlockTotal;
		}

		public static int ComputeActualDamage(EnemyAttackBase definition, EntityManager entityManager, bool isBlocked)
		{
			int full = ComputeFullDamage(definition);
			int aegis = GetAegisAmount(entityManager);
			int assigned = GetAssignedBlockForCurrentAttack(entityManager);
			int reduced = aegis + assigned;
			int actual = full - reduced;
			return actual < 0 ? 0 : actual;
		}

		public static int ComputePreventedDamage(EnemyAttackBase definition, EntityManager entityManager, bool isBlocked)
		{
			int aegis = GetAegisAmount(entityManager);
			int assigned = GetAssignedBlockForCurrentAttack(entityManager);
			return aegis + assigned;
		}
	}
}
