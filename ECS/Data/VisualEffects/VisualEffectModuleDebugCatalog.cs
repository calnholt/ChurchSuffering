using System;
using System.Collections.Generic;
using System.Linq;

namespace ChurchSuffering.ECS.Data.VisualEffects
{
	public readonly struct VisualEffectModuleDebugEntry
	{
		public VisualEffectModule Module { get; init; }
		public string Label { get; init; }
		public VisualEffectTimingProfile Timing { get; init; }
		public VisualEffectTargetRole TargetRole { get; init; }
		public VisualEffectSourceKind SourceKind { get; init; }
		public VisualEffectPalette Palette { get; init; }
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
				.WithTarget(entry.TargetRole)
				.WithPalette(entry.Palette);
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
				Entry(VisualEffectModule.TargetShake, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.PunchZoom, VisualEffectTimingProfile.DefensiveLock, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal),
				Entry(VisualEffectModule.HitStop, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.ArrowShot, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.ThrownBladeVolley, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.EnergyBolt, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card, VisualEffectPalette.Arcane),
				Entry(VisualEffectModule.SpinSlash, VisualEffectTimingProfile.FlickerChaos, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card),
				Entry(VisualEffectModule.FlameBurst, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card, VisualEffectPalette.Fire),
				Entry(VisualEffectModule.FrostBurst, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack, VisualEffectPalette.Ice),
				Entry(VisualEffectModule.ShadowTendrils, VisualEffectTimingProfile.RitualPulse, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack, VisualEffectPalette.Shadow),
				Entry(VisualEffectModule.PoisonCloud, VisualEffectTimingProfile.RitualPulse, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack, VisualEffectPalette.Poison),
				Entry(VisualEffectModule.ShieldWard, VisualEffectTimingProfile.DefensiveLock, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal, VisualEffectPalette.Holy),
				Entry(VisualEffectModule.ShieldShatter, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card, VisualEffectPalette.Arcane),
				Entry(VisualEffectModule.SoulSiphon, VisualEffectTimingProfile.RitualPulse, VisualEffectTargetRole.Enemy, VisualEffectSourceKind.Card, VisualEffectPalette.Blood),
				Entry(VisualEffectModule.ResourceMotes, VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Player, VisualEffectSourceKind.Medal, VisualEffectPalette.Holy),
				Entry(VisualEffectModule.SealStamp, VisualEffectTimingProfile.DefensiveLock, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack, VisualEffectPalette.Arcane),
				Entry(VisualEffectModule.FrostBind, VisualEffectTimingProfile.DefensiveLock, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack, VisualEffectPalette.Ice),
				Entry(VisualEffectModule.BrittleFracture, VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack, VisualEffectPalette.Earth),
				Entry(VisualEffectModule.ColorDrain, VisualEffectTimingProfile.RitualPulse, VisualEffectTargetRole.Player, VisualEffectSourceKind.EnemyAttack, VisualEffectPalette.Shadow),
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
			return Entry(module, label, timing, targetRole, sourceKind, VisualEffectPalette.Physical);
		}

		private static VisualEffectModuleDebugEntry Entry(
			VisualEffectModule module,
			string label,
			VisualEffectTimingProfile timing,
			VisualEffectTargetRole targetRole,
			VisualEffectSourceKind sourceKind,
			VisualEffectPalette palette)
		{
			return new VisualEffectModuleDebugEntry
			{
				Module = module,
				Label = label,
				Timing = timing,
				TargetRole = targetRole,
				SourceKind = sourceKind,
				Palette = palette
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

		private static VisualEffectModuleDebugEntry Entry(
			VisualEffectModule module,
			VisualEffectTimingProfile timing,
			VisualEffectTargetRole targetRole,
			VisualEffectSourceKind sourceKind,
			VisualEffectPalette palette)
		{
			return Entry(module, module.ToString(), timing, targetRole, sourceKind, palette);
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
