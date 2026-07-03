using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Data.VisualEffects
{
	public enum VisualEffectModule
	{
		ActorLunge,
		ActorSquashStretch,
		WhiteWash,
		RedVignette,
		Shockwave,
		SlashBand,
		SmokeScreen,
		SwordArc,
		CrossSlash,
		ClawSlash,
		Bite,
		RockBlast,
		HammerArc,
		CrossBloom,
		Ring,
		Halo,
		Beam,
		Rays,
		Shards,
		Debris,
		SmokeBlobs,
		Cracks,
		HitFlash,
		Shake,
		PunchZoom,
		HitStop
	}

	public enum VisualEffectTimingProfile
	{
		PlayerAttack,
		PlayerBuff,
		EnemyAttackLunge,
		SnapImpact,
		HeavyImpact,
		HolyRise,
		RitualPulse,
		DefensiveLock,
		FlickerChaos
	}

	public enum VisualEffectTargetRole
	{
		Enemy,
		Player,
		Self,
		Opponent
	}

	public enum VisualEffectSourceKind
	{
		Card,
		Equipment,
		Medal,
		EnemyAttack,
		Debug
	}

	public sealed class VisualEffectRecipe
	{
		private VisualEffectModule[] _modules = Array.Empty<VisualEffectModule>();

		public string Id { get; init; } = string.Empty;
		public VisualEffectTimingProfile Timing { get; init; } = VisualEffectTimingProfile.SnapImpact;
		public VisualEffectTargetRole TargetRole { get; init; } = VisualEffectTargetRole.Enemy;
		public float Intensity { get; init; } = 1f;
		public float ParticleMultiplier { get; init; } = 1f;
		public IReadOnlyList<VisualEffectModule> Modules => _modules;
		public SfxTrack StartSfx { get; init; } = SfxTrack.None;
		public SfxTrack ImpactSfx { get; init; } = SfxTrack.None;
		public float StartSfxVolume { get; init; } = 0.5f;
		public float ImpactSfxVolume { get; init; } = 0.5f;
		public float StartSfxPitch { get; init; } = 0f;
		public float ImpactSfxPitch { get; init; } = 0f;

		public VisualEffectRecipe() { }

		private VisualEffectRecipe(VisualEffectRecipe source, IEnumerable<VisualEffectModule> modules)
		{
			Id = source.Id;
			Timing = source.Timing;
			TargetRole = source.TargetRole;
			Intensity = source.Intensity;
			ParticleMultiplier = source.ParticleMultiplier;
			StartSfx = source.StartSfx;
			ImpactSfx = source.ImpactSfx;
			StartSfxVolume = source.StartSfxVolume;
			ImpactSfxVolume = source.ImpactSfxVolume;
			StartSfxPitch = source.StartSfxPitch;
			ImpactSfxPitch = source.ImpactSfxPitch;
			_modules = NormalizeModules(modules);
		}

		public VisualEffectRecipe Clone()
		{
			return new VisualEffectRecipe(this, _modules);
		}

		public VisualEffectRecipe WithIntensity(float intensity)
		{
			return Copy(intensity: intensity);
		}

		public VisualEffectRecipe WithParticleMultiplier(float particleMultiplier)
		{
			return Copy(particleMultiplier: particleMultiplier);
		}

		public VisualEffectRecipe WithTarget(VisualEffectTargetRole targetRole)
		{
			return Copy(targetRole: targetRole);
		}

		public VisualEffectRecipe WithTiming(VisualEffectTimingProfile timing)
		{
			return Copy(timing: timing);
		}

		public VisualEffectRecipe WithModules(params VisualEffectModule[] modules)
		{
			return new VisualEffectRecipe(this, modules ?? Array.Empty<VisualEffectModule>());
		}

		public VisualEffectRecipe WithStartSfx(SfxTrack track, float volume = 0.5f, float pitch = 0f)
		{
			return Copy(startSfx: track, startSfxVolume: volume, startSfxPitch: pitch);
		}

		public VisualEffectRecipe WithImpactSfx(SfxTrack track, float volume = 0.5f, float pitch = 0f)
		{
			return Copy(impactSfx: track, impactSfxVolume: volume, impactSfxPitch: pitch);
		}

		internal VisualEffectRecipe Copy(
			string id = null,
			VisualEffectTimingProfile? timing = null,
			VisualEffectTargetRole? targetRole = null,
			float? intensity = null,
			float? particleMultiplier = null,
			SfxTrack? startSfx = null,
			SfxTrack? impactSfx = null,
			float? startSfxVolume = null,
			float? impactSfxVolume = null,
			float? startSfxPitch = null,
			float? impactSfxPitch = null)
		{
			return new VisualEffectRecipe
			{
				Id = id ?? Id,
				Timing = timing ?? Timing,
				TargetRole = targetRole ?? TargetRole,
				Intensity = intensity ?? Intensity,
				ParticleMultiplier = particleMultiplier ?? ParticleMultiplier,
				StartSfx = startSfx ?? StartSfx,
				ImpactSfx = impactSfx ?? ImpactSfx,
				StartSfxVolume = startSfxVolume ?? StartSfxVolume,
				ImpactSfxVolume = impactSfxVolume ?? ImpactSfxVolume,
				StartSfxPitch = startSfxPitch ?? StartSfxPitch,
				ImpactSfxPitch = impactSfxPitch ?? ImpactSfxPitch,
				_modules = NormalizeModules(_modules)
			};
		}

		private static VisualEffectModule[] NormalizeModules(IEnumerable<VisualEffectModule> modules)
		{
			if (modules == null) return Array.Empty<VisualEffectModule>();
			return modules.Distinct().ToArray();
		}
	}

	public readonly struct VisualEffectTiming
	{
		public float DurationSeconds { get; init; }
		public float ImpactTimeSeconds { get; init; }
		public float HitStopStartSeconds { get; init; }
		public float HitStopDurationSeconds { get; init; }
	}

	public static class VisualEffectTimingProfileResolver
	{
		public static VisualEffectTiming Resolve(VisualEffectTimingProfile profile)
		{
			var timing = profile switch
			{
				VisualEffectTimingProfile.PlayerAttack => Create(0.20f, 0.20f, 0.0f, 0.0f),
				VisualEffectTimingProfile.PlayerBuff => Create(0.96f, 0.36f, 0.0f, 0.0f),
				VisualEffectTimingProfile.EnemyAttackLunge => Create(0.20f, 0.20f, 0.0f, 0.0f),
				VisualEffectTimingProfile.SnapImpact => Create(0.56f, 0.18f, 0.13f, 0.08f),
				VisualEffectTimingProfile.HeavyImpact => Create(0.84f, 0.26f, 0.20f, 0.145f),
				VisualEffectTimingProfile.HolyRise => Create(1.10f, 0.36f, 0.0f, 0.0f),
				VisualEffectTimingProfile.RitualPulse => Create(0.98f, 0.30f, 0.0f, 0.0f),
				VisualEffectTimingProfile.DefensiveLock => Create(0.72f, 0.22f, 0.0f, 0.0f),
				VisualEffectTimingProfile.FlickerChaos => Create(0.86f, 0.18f, 0.14f, 0.08f),
				_ => Create(0.56f, 0.18f, 0.13f, 0.08f)
			};

			return timing with
			{
				ImpactTimeSeconds = Math.Clamp(timing.ImpactTimeSeconds, 0f, timing.DurationSeconds)
			};
		}

		private static VisualEffectTiming Create(float duration, float impact, float hitStopStart, float hitStopDuration)
		{
			return new VisualEffectTiming
			{
				DurationSeconds = duration,
				ImpactTimeSeconds = impact,
				HitStopStartSeconds = hitStopStart,
				HitStopDurationSeconds = hitStopDuration
			};
		}
	}
}
