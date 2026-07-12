using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;

namespace Crusaders30XX.ECS.Data.VisualEffects
{
	public static class VisualEffectPresets
	{
		public static VisualEffectRecipe PlayerAttack()
		{
			return Base("player_attack", VisualEffectTimingProfile.PlayerAttack, VisualEffectTargetRole.Enemy)
				.WithModules(VisualEffectModule.ActorLunge, VisualEffectModule.TargetShake)
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
				.WithModules(VisualEffectModule.ActorLunge, VisualEffectModule.SwordArc, VisualEffectModule.HitFlash, VisualEffectModule.Debris, VisualEffectModule.TargetShake)
				.WithStartSfx(SfxTrack.SwordAttack);
		}

		public static VisualEffectRecipe HeavyHammer()
		{
			return Base("heavy_hammer", VisualEffectTimingProfile.HeavyImpact, VisualEffectTargetRole.Enemy)
				.WithPalette(VisualEffectPalette.Earth)
				.WithModules(
					VisualEffectModule.ActorLunge,
					VisualEffectModule.HammerArc,
					VisualEffectModule.Ring,
					VisualEffectModule.Debris,
					VisualEffectModule.Cracks,
					VisualEffectModule.HitFlash,
					VisualEffectModule.Shockwave,
					VisualEffectModule.Shake,
					VisualEffectModule.TargetShake,
					VisualEffectModule.PunchZoom,
					VisualEffectModule.HitStop)
				.WithStartSfx(SfxTrack.SwordAttack)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe HolyStrike()
		{
			return Base("holy_strike", VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Enemy)
				.WithPalette(VisualEffectPalette.Holy)
				.WithModules(
					VisualEffectModule.ActorLunge,
					VisualEffectModule.CrossBloom,
					VisualEffectModule.Beam,
					VisualEffectModule.Rays,
					VisualEffectModule.Ring,
					VisualEffectModule.WhiteWash,
					VisualEffectModule.HitFlash,
					VisualEffectModule.TargetShake)
				.WithStartSfx(SfxTrack.Prayer)
				.WithImpactSfx(SfxTrack.SwordImpact);
		}

		public static VisualEffectRecipe HolySupport()
		{
			return Base("holy_support", VisualEffectTimingProfile.HolyRise, VisualEffectTargetRole.Player)
				.WithPalette(VisualEffectPalette.Holy)
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
				.WithPalette(VisualEffectPalette.Holy)
				.WithModules(VisualEffectModule.Ring, VisualEffectModule.Halo, VisualEffectModule.WhiteWash, VisualEffectModule.PunchZoom)
				.WithStartSfx(SfxTrack.GainAegis);
		}

		public static VisualEffectRecipe BloodRitual()
		{
			return Base("blood_ritual", VisualEffectTimingProfile.RitualPulse, VisualEffectTargetRole.Self)
				.WithPalette(VisualEffectPalette.Blood)
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
				.WithPalette(VisualEffectPalette.Earth)
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

		public static VisualEffectRecipe ArrowVolley() => Showcase("arrow_volley", VisualEffectPalette.Physical, VisualEffectModule.ArrowShot, VisualEffectModule.HitFlash);
		public static VisualEffectRecipe KunaiVolley() => Showcase("kunai_volley", VisualEffectPalette.Physical, VisualEffectModule.ThrownBladeVolley, VisualEffectModule.HitFlash);
		public static VisualEffectRecipe FireImpact() => Showcase("fire_impact", VisualEffectPalette.Fire, VisualEffectModule.EnergyBolt, VisualEffectModule.FlameBurst, VisualEffectModule.HitFlash);
		public static VisualEffectRecipe FrostImpact() => Showcase("frost_impact", VisualEffectPalette.Ice, VisualEffectModule.EnergyBolt, VisualEffectModule.FrostBurst, VisualEffectModule.Shards);
		public static VisualEffectRecipe ShadowHex() => Showcase("shadow_hex", VisualEffectPalette.Shadow, VisualEffectModule.ShadowTendrils, VisualEffectModule.ColorDrain);
		public static VisualEffectRecipe PoisonImpact() => Showcase("poison_impact", VisualEffectPalette.Poison, VisualEffectModule.PoisonCloud, VisualEffectModule.SmokeBlobs);
		public static VisualEffectRecipe ShieldGain() => Showcase("shield_gain", VisualEffectPalette.Holy, VisualEffectModule.ShieldWard, VisualEffectModule.ResourceMotes);
		public static VisualEffectRecipe ShieldBreak() => Showcase("shield_break", VisualEffectPalette.Arcane, VisualEffectModule.ShieldShatter, VisualEffectModule.Shards);
		public static VisualEffectRecipe BlockedAttack()
		{
			return Base("blocked_attack", VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Player)
				.WithPalette(VisualEffectPalette.Holy)
				.WithModules(VisualEffectModule.ShieldWard)
				.WithImpactRumble(RumbleProfile.Guard)
				.WithStartSfx(SfxTrack.ShieldBlock);
		}
		public static VisualEffectRecipe LifeDrain() => Showcase("life_drain", VisualEffectPalette.Blood, VisualEffectModule.SoulSiphon, VisualEffectModule.ResourceMotes);
		public static VisualEffectRecipe Whirlwind() => Showcase("whirlwind", VisualEffectPalette.Physical, VisualEffectModule.SpinSlash, VisualEffectModule.Shake, VisualEffectModule.TargetShake);

		private static VisualEffectRecipe Showcase(string id, VisualEffectPalette palette, params VisualEffectModule[] modules)
		{
			return Base(id, VisualEffectTimingProfile.SnapImpact, VisualEffectTargetRole.Enemy)
				.WithPalette(palette)
				.WithModules(modules);
		}

		private static VisualEffectRecipe Base(string id, VisualEffectTimingProfile timing, VisualEffectTargetRole target)
		{
			var recipe = new VisualEffectRecipe
			{
				Id = id,
				Timing = timing,
				TargetRole = target
			};
			return recipe.WithImpactRumble(DefaultRumble(timing, target));
		}

		private static RumbleProfile DefaultRumble(
			VisualEffectTimingProfile timing,
			VisualEffectTargetRole target)
		{
			if (timing == VisualEffectTimingProfile.DefensiveLock) return RumbleProfile.Guard;
			if (target == VisualEffectTargetRole.Self || target == VisualEffectTargetRole.Player
				&& timing is VisualEffectTimingProfile.PlayerBuff
					or VisualEffectTimingProfile.HolyRise
					or VisualEffectTimingProfile.RitualPulse)
			{
				return RumbleProfile.Soft;
			}
			return timing == VisualEffectTimingProfile.HeavyImpact
				? RumbleProfile.HeavyImpact
				: RumbleProfile.MediumImpact;
		}
	}
}
