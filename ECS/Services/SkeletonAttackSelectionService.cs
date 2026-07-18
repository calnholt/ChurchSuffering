using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Utils;

namespace Crusaders30XX.ECS.Services;

/// <summary>
/// Shared multi-jab attack selection for melee skeletons.
/// Pass the chip attack that replaces Bone Strike for that variant.
/// </summary>
public static class SkeletonAttackSelectionService
{
	public static IEnumerable<EnemyAttackId> GetAttackIds(EnemyAttackId chipAttackId)
	{
		int random = Random.Shared.Next(0, 100);
		var linkers = new List<EnemyAttackId> { chipAttackId, EnemyAttackId.Sweep, EnemyAttackId.Calcify };
		if (random <= 65)
		{
			var selected = ArrayUtils.TakeRandomWithReplacement(linkers, 3);
			var sweepCount = selected.Count(x => x == EnemyAttackId.Sweep);
			while (sweepCount > 2)
			{
				selected = ArrayUtils.TakeRandomWithReplacement(linkers, 3);
				sweepCount = selected.Count(x => x == EnemyAttackId.Sweep);
			}
			int haveNoMercy = Random.Shared.Next(0, 100);
			if (haveNoMercy <= 5)
			{
				var selected2 = ArrayUtils.TakeRandomWithReplacement(linkers, 2);
				selected2 = selected2.Append(EnemyAttackId.HaveNoMercy);
				selected = ArrayUtils.Shuffled(selected2);
			}
			return selected;
		}
		return [EnemyAttackId.SkullCrusher];
	}
}
