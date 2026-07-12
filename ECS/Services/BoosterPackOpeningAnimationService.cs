using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Services;

public readonly record struct BoosterPackOpeningTiming(
	float SummonDuration,
	float IdleDuration,
	float ChargeDuration,
	float CrackDuration,
	float RuptureDuration,
	float ShowcaseDuration,
	float ChargeParticleInterval,
	float RevealStagger,
	float RevealTravelDuration,
	float SheenDelayFromReveal,
	float RuptureShakeDuration)
{
	public static BoosterPackOpeningTiming Default { get; } = new(
		0.76f,
		0.52f,
		0.85f,
		0.65f,
		0.76f,
		1.60f,
		0.26f,
		0.12f,
		0.72f,
		0.52f,
		0.58f);

	public float SummonStart => 0f;
	public float IdleStart => SummonStart + NonNegative(SummonDuration);
	public float ChargeStart => IdleStart + NonNegative(IdleDuration);
	public float CrackStart => ChargeStart + NonNegative(ChargeDuration);
	public float RuptureStart => CrackStart + NonNegative(CrackDuration);
	public float ShowcaseStart => RuptureStart + NonNegative(RuptureDuration);
	public float ReadyStart => ShowcaseStart + NonNegative(ShowcaseDuration);

	private static float NonNegative(float value) => Math.Max(0f, value);
}

public enum BoosterPackOpeningMilestoneKind
{
	ChargeStarted,
	ChargePulse,
	CrackStarted,
	RuptureStarted,
	ShowcaseStarted,
	ReadyStarted,
}

public readonly record struct BoosterPackOpeningMilestone(
	BoosterPackOpeningMilestoneKind Kind,
	float Seconds);

public readonly record struct BoosterPackLootAnimationSample(
	Vector2 Position,
	float Scale,
	float Rotation,
	float Alpha,
	float Progress,
	bool IsSettled);

public readonly record struct BoosterPackRumbleSample(
	float LowFrequency,
	float HighFrequency,
	float LeftTrigger = 0f,
	float RightTrigger = 0f)
{
	public static BoosterPackRumbleSample Zero => new(0f, 0f);
	public bool IsActive => LowFrequency > 0f || HighFrequency > 0f;
}

public readonly record struct BoosterPackRumbleSettings(
	float MaxBuildupLow,
	float MaxBuildupHigh,
	float LootPulseLow,
	float LootPulseHigh,
	float LootPulseDurationSeconds,
	float MaxBuildupTrigger = 0.18f,
	float LootPulseTrigger = 0.08f)
{
	public static BoosterPackRumbleSettings Default { get; } = new(0.35f, 0.55f, 0.08f, 0.12f, 0.06f, 0.18f, 0.08f);
}

public static class BoosterPackOpeningAnimationService
{
	private const float BoundaryEpsilon = 0.00001f;
	private static readonly float[] SlotStartRotationsDegrees = { -14f, 2f, 14f };

	public static BoosterPackOpeningPhase GetPhase(
		float elapsedSeconds,
		BoosterPackOpeningTiming timing)
	{
		float elapsed = ClampElapsed(elapsedSeconds);
		if (AtOrAfter(elapsed, timing.ReadyStart)) return BoosterPackOpeningPhase.Ready;
		if (AtOrAfter(elapsed, timing.ShowcaseStart)) return BoosterPackOpeningPhase.Showcase;
		if (AtOrAfter(elapsed, timing.RuptureStart)) return BoosterPackOpeningPhase.Rupture;
		if (AtOrAfter(elapsed, timing.CrackStart)) return BoosterPackOpeningPhase.Crack;
		if (AtOrAfter(elapsed, timing.ChargeStart)) return BoosterPackOpeningPhase.Charge;
		if (AtOrAfter(elapsed, timing.IdleStart)) return BoosterPackOpeningPhase.Idle;
		return BoosterPackOpeningPhase.Summon;
	}

	public static float GetPhaseProgress(
		float elapsedSeconds,
		BoosterPackOpeningPhase phase,
		BoosterPackOpeningTiming timing)
	{
		float elapsed = ClampElapsed(elapsedSeconds);
		(float start, float duration) = phase switch
		{
			BoosterPackOpeningPhase.Summon => (timing.SummonStart, timing.SummonDuration),
			BoosterPackOpeningPhase.Idle => (timing.IdleStart, timing.IdleDuration),
			BoosterPackOpeningPhase.Charge => (timing.ChargeStart, timing.ChargeDuration),
			BoosterPackOpeningPhase.Crack => (timing.CrackStart, timing.CrackDuration),
			BoosterPackOpeningPhase.Rupture => (timing.RuptureStart, timing.RuptureDuration),
			BoosterPackOpeningPhase.Showcase => (timing.ShowcaseStart, timing.ShowcaseDuration),
			_ => (timing.ReadyStart, 0f),
		};
		if (duration <= 0f) return elapsed >= start ? 1f : 0f;
		if (AtOrAfter(elapsed, start + duration)) return 1f;
		return MathHelper.Clamp((elapsed - start) / duration, 0f, 1f);
	}

	public static IEnumerable<BoosterPackOpeningMilestone> GetCrossedMilestones(
		float previousSeconds,
		float currentSeconds,
		BoosterPackOpeningTiming timing)
	{
		float previous = ClampElapsed(previousSeconds);
		float current = ClampElapsed(currentSeconds);
		if (current <= previous) yield break;

		var milestones = new List<BoosterPackOpeningMilestone>
		{
			new(BoosterPackOpeningMilestoneKind.ChargeStarted, timing.ChargeStart),
			new(BoosterPackOpeningMilestoneKind.CrackStarted, timing.CrackStart),
			new(BoosterPackOpeningMilestoneKind.RuptureStarted, timing.RuptureStart),
			new(BoosterPackOpeningMilestoneKind.ShowcaseStarted, timing.ShowcaseStart),
			new(BoosterPackOpeningMilestoneKind.ReadyStarted, timing.ReadyStart),
		};

		float interval = Math.Max(0.001f, timing.ChargeParticleInterval);
		for (float pulse = timing.ChargeStart + interval;
			pulse < timing.CrackStart;
			pulse += interval)
		{
			milestones.Add(new BoosterPackOpeningMilestone(
				BoosterPackOpeningMilestoneKind.ChargePulse,
				pulse));
		}

		milestones.Sort(static (left, right) =>
		{
			int timeOrder = left.Seconds.CompareTo(right.Seconds);
			return timeOrder != 0 ? timeOrder : left.Kind.CompareTo(right.Kind);
		});
		foreach (var milestone in milestones)
		{
			if (milestone.Seconds > previous + BoundaryEpsilon
				&& milestone.Seconds <= current + BoundaryEpsilon)
			{
				yield return milestone;
			}
		}
	}

	public static BoosterPackLootAnimationSample SampleLoot(
		float elapsedSeconds,
		int slotIndex,
		Vector2 ruptureCenter,
		Vector2 finalCenter,
		BoosterPackOpeningTiming timing,
		float arcHeightPx)
	{
		float revealStart = timing.ShowcaseStart + Math.Max(0, slotIndex) * Math.Max(0f, timing.RevealStagger);
		float duration = Math.Max(0.001f, timing.RevealTravelDuration);
		float elapsed = ClampElapsed(elapsedSeconds);
		float progress = AtOrAfter(elapsed, revealStart + duration)
			? 1f
			: MathHelper.Clamp((elapsed - revealStart) / duration, 0f, 1f);
		float eased = EaseOutCubic(progress);
		Vector2 control = Vector2.Lerp(ruptureCenter, finalCenter, 0.5f) + new Vector2(0f, -arcHeightPx);
		Vector2 position = QuadraticBezier(ruptureCenter, control, finalCenter, eased);

		float scale = progress <= 0.78f
			? MathHelper.Lerp(0.18f, 1.08f, progress / 0.78f)
			: MathHelper.Lerp(1.08f, 1f, (progress - 0.78f) / 0.22f);
		float alpha = MathHelper.Clamp(progress / 0.18f, 0f, 1f);
		int rotationIndex = Math.Clamp(slotIndex, 0, SlotStartRotationsDegrees.Length - 1);
		float startRotation = MathHelper.ToRadians(SlotStartRotationsDegrees[rotationIndex]);
		float rotation = MathHelper.Lerp(startRotation, 0f, eased);

		return new BoosterPackLootAnimationSample(
			position,
			scale,
			rotation,
			alpha,
			progress,
			progress >= 1f);
	}

	public static bool HasSheenStarted(
		float elapsedSeconds,
		int slotIndex,
		BoosterPackOpeningTiming timing)
	{
		float start = timing.ShowcaseStart
			+ Math.Max(0, slotIndex) * Math.Max(0f, timing.RevealStagger)
			+ Math.Max(0f, timing.SheenDelayFromReveal);
		return AtOrAfter(ClampElapsed(elapsedSeconds), start);
	}

	public static Vector2 SampleRuptureShake(
		float elapsedSeconds,
		BoosterPackOpeningTiming timing,
		float amplitudePx)
	{
		float duration = Math.Max(0f, timing.RuptureShakeDuration);
		float local = ClampElapsed(elapsedSeconds) - timing.RuptureStart;
		if (duration <= 0f || local < 0f || AtOrAfter(local, duration)) return Vector2.Zero;
		float progress = MathHelper.Clamp(local / duration, 0f, 1f);
		float decay = 1f - progress;
		float x = (float)Math.Sin(progress * MathHelper.TwoPi * 7f) * amplitudePx * decay;
		float y = (float)Math.Sin(progress * MathHelper.TwoPi * 11f + 1.7f) * amplitudePx * 0.75f * decay;
		return new Vector2(x, y);
	}

	public static bool CanDismiss(float elapsedSeconds, BoosterPackOpeningTiming timing)
	{
		return AtOrAfter(ClampElapsed(elapsedSeconds), timing.ReadyStart);
	}

	public static float GetLootRevealStartSeconds(int slotIndex, BoosterPackOpeningTiming timing)
	{
		return timing.ShowcaseStart + Math.Max(0, slotIndex) * Math.Max(0f, timing.RevealStagger);
	}

	public static BoosterPackRumbleSample SampleBuildupRumble(
		float elapsedSeconds,
		BoosterPackOpeningTiming timing,
		BoosterPackRumbleSettings settings)
	{
		float elapsed = ClampElapsed(elapsedSeconds);
		if (elapsed < timing.ChargeStart || AtOrAfter(elapsed, timing.RuptureStart))
		{
			return BoosterPackRumbleSample.Zero;
		}

		float duration = timing.RuptureStart - timing.ChargeStart;
		if (duration <= 0f) return BoosterPackRumbleSample.Zero;
		float progress = MathHelper.Clamp((elapsed - timing.ChargeStart) / duration, 0f, 1f);
		float intensity = progress * progress;
		return new BoosterPackRumbleSample(
			settings.MaxBuildupLow * intensity,
			settings.MaxBuildupHigh * intensity,
			settings.MaxBuildupTrigger * intensity,
			settings.MaxBuildupTrigger * intensity);
	}

	public static BoosterPackRumbleSample SampleLootRevealRumble(
		float elapsedSeconds,
		int lootCount,
		BoosterPackOpeningTiming timing,
		BoosterPackRumbleSettings settings)
	{
		float elapsed = ClampElapsed(elapsedSeconds);
		float duration = Math.Max(0.001f, settings.LootPulseDurationSeconds);
		float bestLow = 0f;
		float bestHigh = 0f;
		float bestTrigger = 0f;
		for (int slotIndex = 0; slotIndex < Math.Max(0, lootCount); slotIndex++)
		{
			float local = elapsed - GetLootRevealStartSeconds(slotIndex, timing);
			if (local < 0f || local >= duration) continue;
			float envelope = 1f - local / duration;
			bestLow = Math.Max(bestLow, settings.LootPulseLow * envelope);
			bestHigh = Math.Max(bestHigh, settings.LootPulseHigh * envelope);
			bestTrigger = Math.Max(bestTrigger, settings.LootPulseTrigger * envelope);
		}

		return bestLow <= BoundaryEpsilon && bestHigh <= BoundaryEpsilon && bestTrigger <= BoundaryEpsilon
			? BoosterPackRumbleSample.Zero
			: new BoosterPackRumbleSample(bestLow, bestHigh, bestTrigger, bestTrigger);
	}

	public static BoosterPackRumbleSample SampleRumble(
		float elapsedSeconds,
		int lootCount,
		BoosterPackOpeningTiming timing,
		BoosterPackRumbleSettings settings)
	{
		float elapsed = ClampElapsed(elapsedSeconds);
		if (elapsed < timing.RuptureStart)
		{
			return SampleBuildupRumble(elapsed, timing, settings);
		}

		return SampleLootRevealRumble(elapsed, lootCount, timing, settings);
	}

	private static float ClampElapsed(float elapsedSeconds)
	{
		return float.IsFinite(elapsedSeconds) ? Math.Max(0f, elapsedSeconds) : 0f;
	}

	private static bool AtOrAfter(float value, float boundary)
	{
		return value + BoundaryEpsilon >= boundary;
	}

	private static Vector2 QuadraticBezier(Vector2 start, Vector2 control, Vector2 end, float progress)
	{
		float inverse = 1f - progress;
		return inverse * inverse * start
			+ 2f * inverse * progress * control
			+ progress * progress * end;
	}

	private static float EaseOutCubic(float progress)
	{
		float inverse = 1f - MathHelper.Clamp(progress, 0f, 1f);
		return 1f - inverse * inverse * inverse;
	}
}
