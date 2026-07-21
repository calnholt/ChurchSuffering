using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Services;

public static class EnemyAttackProgressOverrideService
{
	/// <summary>
	/// Glass Cannon: when exactly <paramref name="requiredBlockCount"/> non-equipment cards block,
	/// fully prevent damage and mark those cards for exhaust on block.
	/// Returns true when the override was applied (caller should early-exit recompute).
	/// </summary>
	public static bool TryApplyExactBlockCountPrevention(
		EntityManager entityManager,
		int requiredBlockCount,
		int attackDamage)
	{
		var progressEntity = entityManager.GetEntitiesWithComponent<EnemyAttackProgress>().FirstOrDefault();
		if (progressEntity == null) return false;

		var p = progressEntity.GetComponent<EnemyAttackProgress>();
		if (p == null) return false;

		var assignedBlockCards = entityManager.GetEntitiesWithComponent<AssignedBlockCard>()
			.Where(e => !e.GetComponent<AssignedBlockCard>().IsEquipment)
			.ToList();

		foreach (var card in entityManager.GetEntitiesWithComponent<CardData>())
		{
			entityManager.RemoveComponent<ExhaustOnBlock>(card);
		}

		if (assignedBlockCards.Count == requiredBlockCount)
		{
			int fullDamage = p.DamageBeforePrevention > 0 ? p.DamageBeforePrevention : attackDamage;
			int blockFromCards = Math.Max(0, p.AssignedBlockTotal);

			p.ActualDamage = 0;
			p.IsConditionMet = true;
			p.BaseDamage = attackDamage;
			p.AegisTotal = 0;
			p.PreventedDamageFromBlockCondition = Math.Max(0, fullDamage - blockFromCards);
			p.TotalPreventedDamage = fullDamage;
			p.FullyPreventedBySpecial = true;

			foreach (var blocker in assignedBlockCards)
			{
				entityManager.AddComponent(blocker, new ExhaustOnBlock { Owner = blocker });
			}

			return true;
		}

		p.IsConditionMet = false;
		return false;
	}
}
