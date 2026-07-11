using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Services
{
	/// <summary>
	/// Maps enemy ids to MonoGame content asset names for battle portraits.
	/// </summary>
	public static class EnemyPortraitContent
	{
		private static readonly HashSet<string> PortraitEnemyIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
		{
			"demon",
			"mummy",
			"earth_demon",
			"ogre",
			"skeleton",
			"skeletal_archer",
			"spider",
			"succubus",
			"thornreaver",
			"dust_wuurm",
			"sorcerer",
			"ice_demon",
			"glacial_guardian",
			"cinderbolt_demon",
			"fire_skeleton",
			"berserker",
			"shadow",
			"wyvern",
			"sand_golem",
			"fallen_shepherd",
			// "medusa",
			// "training_demon", // test-fight only
			// "ninja",
		};

		public static string ToAssetName(string enemyId)
		{
			if (string.IsNullOrEmpty(enemyId)) return string.Empty;
			var parts = enemyId.Split('_');
			return string.Join("_", parts.Select(p =>
			{
				if (string.IsNullOrEmpty(p)) return p;
				return char.ToUpperInvariant(p[0]) + p.Substring(1);
			}));
		}

		public static bool HasPortrait(string enemyId) =>
			!string.IsNullOrEmpty(enemyId) && PortraitEnemyIds.Contains(enemyId);

		public static IReadOnlyList<string> GetClimbEncounterEnemyPool()
		{
			return EnemyFactory.GetAllEnemies()
				.Where(entry => entry.Value != null
					&& !entry.Value.IsBoss
					&& !entry.Value.IsTutorialOnly
					&& HasPortrait(entry.Key.ToKey())
				)
				.Select(entry => entry.Key.ToKey())
				.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}
	}
}
