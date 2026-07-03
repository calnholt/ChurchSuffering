using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Data.VisualEffects
{
	public static class VisualEffectPresets
	{
		public static VisualEffectRecipe PlayerAttack()
		{
			return Base("player_attack", VisualEffectTimingProfile.PlayerAttack, VisualEffectTargetRole.Enemy)
				.WithModules(VisualEffectModule.ActorLunge)
				.WithStartSfx(SfxTrack.SwordAttack);
		}

		public static VisualEffectRecipe PlayerBuff()
		{
			return Base("player_buff", VisualEffectTimingProfile.PlayerBuff, VisualEffectTargetRole.Player)
				.WithModules(VisualEffectModule.ActorSquashStretch)
				.WithStartSfx(SfxTrack.Prayer);
		}

		public static VisualEffectRecipe EnemyAttackLunge()
		{
			return Base("enemy_attack_lunge", VisualEffectTimingProfile.EnemyAttackLunge, VisualEffectTargetRole.Player)
				.WithModules(VisualEffectModule.ActorLunge)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe LightSlash()
		{
			return Base("light_slash", VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy)
				.WithModules(VisualEffectModule.ActorLunge, VisualEffectModule.SwordArc, VisualEffectModule.HitFlash, VisualEffectModule.Debris)
				.WithStartSfx(SfxTrack.SwordAttack);
		}

		public static VisualEffectRecipe HeavyHammer()
		{
			return Base("heavy_hammer", VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy)
				.WithModules(
					VisualEffectModule.ActorLunge,
					VisualEffectModule.HammerArc,
					VisualEffectModule.Ring,
					VisualEffectModule.Debris,
					VisualEffectModule.Cracks,
					VisualEffectModule.HitFlash,
					VisualEffectModule.Shockwave,
					VisualEffectModule.Shake,
					VisualEffectModule.PunchZoom,
					VisualEffectModule.HitStop)
				.WithStartSfx(SfxTrack.SwordAttack)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe HolyStrike()
		{
			return Base("holy_strike", VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Enemy)
				.WithModules(
					VisualEffectModule.ActorLunge,
					VisualEffectModule.CrossBloom,
					VisualEffectModule.Beam,
					VisualEffectModule.Rays,
					VisualEffectModule.Ring,
					VisualEffectModule.WhiteWash,
					VisualEffectModule.HitFlash)
				.WithStartSfx(SfxTrack.Prayer)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe HolySupport()
		{
			return Base("holy_support", VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Player)
				.WithModules(
					VisualEffectModule.ActorSquashStretch,
					VisualEffectModule.CrossBloom,
					VisualEffectModule.Halo,
					VisualEffectModule.Beam,
					VisualEffectModule.Rays,
					VisualEffectModule.WhiteWash)
				.WithStartSfx(SfxTrack.Prayer);
		}

		public static VisualEffectRecipe DefensiveGuard()
		{
			return Base("defensive_guard", VisualEffectTimingProfile.DefensiveLock, VisualEffectTargetRole.Player)
				.WithModules(VisualEffectModule.Ring, VisualEffectModule.Halo, VisualEffectModule.WhiteWash, VisualEffectModule.PunchZoom)
				.WithStartSfx(SfxTrack.GainAegis);
		}

		public static VisualEffectRecipe BloodRitual()
		{
			return Base("blood_ritual", VisualEffectTimingProfile.RitualPulse, VisualEffectTargetRole.Self)
				.WithModules(VisualEffectModule.RedVignette, VisualEffectModule.Ring, VisualEffectModule.SmokeBlobs, VisualEffectModule.Rays);
		}

		public static VisualEffectRecipe EnemySlash()
		{
			return Base("enemy_slash", VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player)
				.WithModules(VisualEffectModule.ActorLunge, VisualEffectModule.CrossSlash, VisualEffectModule.SlashBand, VisualEffectModule.HitFlash, VisualEffectModule.Shake)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe EnemyHeavyImpact()
		{
			return Base("enemy_heavy_impact", VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player)
				.WithModules(
					VisualEffectModule.ActorLunge,
					VisualEffectModule.Ring,
					VisualEffectModule.Debris,
					VisualEffectModule.Cracks,
					VisualEffectModule.HitFlash,
					VisualEffectModule.Shockwave,
					VisualEffectModule.Shake,
					VisualEffectModule.HitStop)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe EnemyClawSlash()
		{
			return Base("enemy_claw_slash", VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player)
				.WithModules(
					VisualEffectModule.ActorLunge,
					VisualEffectModule.ClawSlash,
					VisualEffectModule.HitFlash,
					VisualEffectModule.Debris,
					VisualEffectModule.SlashBand,
					VisualEffectModule.Shake)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe EnemyBite()
		{
			return Base("enemy_bite", VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player)
				.WithModules(VisualEffectModule.ActorLunge, VisualEffectModule.Bite, VisualEffectModule.HitFlash, VisualEffectModule.RedVignette, VisualEffectModule.Shake, VisualEffectModule.HitStop)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe EnemyRockBlast()
		{
			return Base("enemy_rock_blast", VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Player)
				.WithModules(
					VisualEffectModule.ActorLunge,
					VisualEffectModule.RockBlast,
					VisualEffectModule.Ring,
					VisualEffectModule.Debris,
					VisualEffectModule.SmokeBlobs,
					VisualEffectModule.HitFlash,
					VisualEffectModule.Shockwave,
					VisualEffectModule.Shake,
					VisualEffectModule.PunchZoom,
					VisualEffectModule.HitStop)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		private static VisualEffectRecipe Base(string id, VisualEffectTimingProfile timing, VisualEffectTargetRole target)
		{
			return new VisualEffectRecipe
			{
				Id = id,
				Timing = timing,
				TargetRole = target
			};
		}
	}
}
