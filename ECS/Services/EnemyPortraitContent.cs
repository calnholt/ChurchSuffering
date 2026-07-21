using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Enemies;

namespace ChurchSuffering.ECS.Services
{
	/// <summary>
	/// Maps enemy ids to MonoGame content asset names for battle portraits.
	/// </summary>
	public static class EnemyPortraitContent
	{
		private const string PortraitFolder = "Enemies";

		private static readonly HashSet<EnemyId> PortraitEnemyIds = new HashSet<EnemyId>
		{
			EnemyId.Demon,
			EnemyId.Mummy,
			EnemyId.EarthDemon,
			EnemyId.Ogre,
			EnemyId.Skeleton,
			EnemyId.SkeletalArcher,
			EnemyId.Spider,
			EnemyId.Succubus,
			EnemyId.Thornreaver,
			EnemyId.DustWuurm,
			EnemyId.Sorcerer,
			EnemyId.IceDemon,
			EnemyId.GlacialGuardian,
			EnemyId.CinderboltDemon,
			EnemyId.FireSkeleton,
			EnemyId.EarthSkeleton,
			EnemyId.FrostSkeleton,
			EnemyId.CursedSkeleton,
			EnemyId.Berserker,
			EnemyId.Shadow,
			EnemyId.Wyvern,
			EnemyId.SandGolem,
			EnemyId.FallenShepherd,
			EnemyId.AzureWarden,
			EnemyId.Blighttongue,
			EnemyId.HexBailiff,
			EnemyId.FrostboundAeon,
			// EnemyId.TrainingDemon, // test-fight only
			// EnemyId.Ninja,
		};

		public static string ToAssetName(string enemyId)
		{
			if (string.IsNullOrEmpty(enemyId)) return string.Empty;
			var parts = enemyId.Split('_');
			var joined = string.Join("_", parts.Select(p =>
			{
				if (string.IsNullOrEmpty(p)) return p;
				return char.ToUpperInvariant(p[0]) + p.Substring(1);
			}));
			return $"{PortraitFolder}/{joined}";
		}

		public static bool HasPortrait(EnemyId enemyId) => PortraitEnemyIds.Contains(enemyId);

		public static bool HasPortrait(string enemyId) =>
			GameIdExtensions.TryParseEnemyId(enemyId, out var id) && HasPortrait(id);

		public static IReadOnlyList<string> GetClimbEncounterEnemyPool()
		{
			return GetClimbEncounterEnemyPool(pool: null);
		}

		public static IReadOnlyList<string> GetClimbEncounterEnemyPool(ClimbEncounterPool pool)
		{
			return GetClimbEncounterEnemyPool((ClimbEncounterPool?)pool);
		}

		private static IReadOnlyList<string> GetClimbEncounterEnemyPool(ClimbEncounterPool? pool)
		{
			return EnemyFactory.GetAllEnemies()
				.Where(entry => entry.Value != null
					&& !entry.Value.IsBoss
					&& !entry.Value.IsTutorialOnly
					&& entry.Value.ClimbPool != ClimbEncounterPool.None
					&& MatchesClimbPool(entry.Value.ClimbPool, pool)
					&& HasPortrait(entry.Key)
				)
				.Select(entry => entry.Key.ToKey())
				.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static bool MatchesClimbPool(ClimbEncounterPool enemyPool, ClimbEncounterPool? requested)
		{
			if (requested == null) return true;
			return requested.Value switch
			{
				ClimbEncounterPool.Early => enemyPool == ClimbEncounterPool.Early
					|| enemyPool == ClimbEncounterPool.Throughout,
				ClimbEncounterPool.Late => enemyPool == ClimbEncounterPool.Late
					|| enemyPool == ClimbEncounterPool.Throughout,
				ClimbEncounterPool.Throughout => enemyPool == ClimbEncounterPool.Throughout,
				_ => enemyPool == requested.Value,
			};
		}
	}
}
