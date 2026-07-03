using System;
using System.Collections.Generic;
using System.Linq;

namespace Crusaders30XX.ECS.Data.VisualEffects
{
	public readonly struct VisualEffectModuleDebugEntry
	{
		public VisualEffectModule Module { get; init; }
		public string Label { get; init; }
		public VisualEffectTimingProfile Timing { get; init; }
		public VisualEffectTargetRole TargetRole { get; init; }
		public VisualEffectSourceKind SourceKind { get; init; }
	}

	public static class VisualEffectModuleDebugCatalog
	{
		private static readonly IReadOnlyList<VisualEffectModuleDebugEntry> AllEntries = BuildEntries();

		public static IReadOnlyList<VisualEffectModuleDebugEntry> All => AllEntries;

		public static VisualEffectRecipe BuildRecipe(VisualEffectModuleDebugEntry entry)
		{
			return new VisualEffectRecipe
			{
				Id = ToRecipeId(entry.Label)
			}
				.WithModules(entry.Module)
				.WithTiming(entry.Timing)
				.WithTarget(entry.TargetRole);
		}

		private static IReadOnlyList<VisualEffectModuleDebugEntry> BuildEntries()
		{
			var entries = new List<VisualEffectModuleDebugEntry>
			{
				Entry(VisualEffectModule.ActorLunge, "ActorLunge (player)", VisualEffectTimingProfile.PlayerAttack, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.ActorLunge, "ActorLunge (enemy)", VisualEffectTimingProfile.EnemyAttackLunge, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.ActorSquashStretch, VisualEffectTimingProfile.PlayerBuff, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.WhiteWash, VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.RedVignette, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.Shockwave, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.SlashBand, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.SmokeScreen, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.SwordArc, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.CrossSlash, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.ClawSlash, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.Bite, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.RockBlast, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.HammerArc, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.CrossBloom, VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.Ring, VisualEffectTimingProfile.DefensiveLock, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.Halo, VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.Beam, VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.Rays, VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.Shards, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.Debris, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.SmokeBlobs, VisualEffectTimingProfile.RitualPulse, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.Cracks, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.HitFlash, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.Shake, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack),
				Entry(VisualEffectModule.PunchZoom, VisualEffectTimingProfile.DefensiveLock, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.HitStop, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
			};

			return entries
				.OrderBy(entry => entry.Label, StringComparer.Ordinal)
				.ToList();
		}

		private static VisualEffectModuleDebugEntry Entry(
			VisualEffectModule module,
			string label,
			VisualEffectTimingProfile timing,
			VisualEffectTargetRole targetRole,
			VisualEffectSourceKind sourceKind)
		{
			return new VisualEffectModuleDebugEntry
			{
				Module = module,
				Label = label,
				Timing = timing,
				TargetRole = targetRole,
				SourceKind = sourceKind
			};
		}

		private static VisualEffectModuleDebugEntry Entry(
			VisualEffectModule module,
			VisualEffectTimingProfile timing,
			VisualEffectTargetRole targetRole,
			VisualEffectSourceKind sourceKind)
		{
			return Entry(module, module.ToString(), timing, targetRole, sourceKind);
		}

		private static string ToRecipeId(string label)
		{
			return "debug_" + label
				.Replace(" ", "_", StringComparison.Ordinal)
				.Replace("(", string.Empty, StringComparison.Ordinal)
				.Replace(")", string.Empty, StringComparison.Ordinal)
				.ToLowerInvariant();
		}
	}
}
