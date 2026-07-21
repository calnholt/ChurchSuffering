using System;
using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Input;

namespace Crusaders30XX.ECS.Services;

public enum ClimbPointsAwardRumbleMilestoneKind
{
	CrestReveal,
	CountUpComplete,
}

public readonly record struct ClimbPointsAwardRumbleMilestone(
	ClimbPointsAwardRumbleMilestoneKind Kind,
	float Seconds);

public readonly record struct ClimbPointsAwardRumbleSample(
	float LowFrequency = 0f,
	float HighFrequency = 0f,
	float LeftTrigger = 0f,
	float RightTrigger = 0f)
{
	public static ClimbPointsAwardRumbleSample Zero => new(0f, 0f);
	public bool IsActive => LowFrequency > 0f || HighFrequency > 0f || LeftTrigger > 0f || RightTrigger > 0f;
}

public readonly record struct ClimbPointsAwardRumbleSettings(
	float MaxBuildupLow,
	float MaxBuildupHigh,
	float TierPulseLow,
	float TierPulseHigh,
	float TierPulseDurationSeconds,
	float EmptyPulseLow,
	float EmptyPulseHigh,
	float EmptyPulseDurationSeconds,
	float MaxBuildupTrigger = 0.14f,
	float TierPulseTrigger = 0.06f,
	float EmptyPulseTrigger = 0.04f)
{
	public static ClimbPointsAwardRumbleSettings Default { get; } = new(
		0.28f,
		0.42f,
		0.10f,
		0.16f,
		0.060f,
		0.06f,
		0.10f,
		0.080f,
		0.14f,
		0.06f,
		0.04f);
}

public readonly record struct ClimbPointsAwardTier(
	string Id,
	int TimeThreshold,
	bool RequiresBoss,
	int Points,
	string Name,
	string Requirement);

public readonly record struct ClimbPointsAwardScenario(
	int TimeReached,
	int ShopRefreshInterval,
	bool CompletedFinalBoss,
	bool Abandoned,
	string Kicker,
	string Title,
	string EmptyTitle,
	string EmptyDetail);

[DebugTab("Climb Points Award Timing")]
public sealed class ClimbPointsAwardAnimationTimingSettings
{
	public static ClimbPointsAwardAnimationTimingSettings Instance { get; } = new();

	[DebugEditable(DisplayName = "Tier Start (s)", Step = 0.01f, Min = 0f, Max = 5f)]
	public float TierStartSeconds { get; set; } = 0.900f;

	[DebugEditable(DisplayName = "Tier Stagger (s)", Step = 0.01f, Min = 0f, Max = 5f)]
	public float TierStaggerSeconds { get; set; } = 0.410f;

	[DebugEditable(DisplayName = "Total After Last (s)", Step = 0.01f, Min = 0f, Max = 5f)]
	public float TotalAfterLastSeconds { get; set; } = 0.520f;

	[DebugEditable(DisplayName = "Total Animation (s)", Step = 0.01f, Min = 0f, Max = 5f)]
	public float TotalAnimationSeconds { get; set; } = 0.760f;

	[DebugEditable(DisplayName = "Count Up (s)", Step = 0.01f, Min = 0f, Max = 5f)]
	public float CountUpSeconds { get; set; } = 0.580f;

	[DebugEditable(DisplayName = "Exit Fade (s)", Step = 0.01f, Min = 0f, Max = 5f)]
	public float ExitFadeSeconds { get; set; } = 0.300f;

	[DebugEditable(DisplayName = "Tier Node Slam Offset (s)", Step = 0.01f, Min = 0f, Max = 2f)]
	public float TierNodeSlamOffsetSeconds { get; set; } = 0.080f;

	[DebugEditable(DisplayName = "Progress Meter Start (s)", Step = 0.01f, Min = 0f, Max = 5f)]
	public float ProgressMeterStartDelaySeconds { get; set; } = 0.620f;

	public void ResetToDefaults()
	{
		TierStartSeconds = 0.900f;
		TierStaggerSeconds = 0.410f;
		TotalAfterLastSeconds = 0.520f;
		TotalAnimationSeconds = 0.760f;
		CountUpSeconds = 0.580f;
		ExitFadeSeconds = 0.300f;
		TierNodeSlamOffsetSeconds = 0.080f;
		ProgressMeterStartDelaySeconds = 0.620f;
	}
}

public static class ClimbPointsAwardAnimationService
{
	private static ClimbPointsAwardAnimationTimingSettings Timing =>
		ClimbPointsAwardAnimationTimingSettings.Instance;

	public static float TierStartSeconds
	{
		get => Timing.TierStartSeconds;
		set => Timing.TierStartSeconds = value;
	}

	public static float TierStaggerSeconds
	{
		get => Timing.TierStaggerSeconds;
		set => Timing.TierStaggerSeconds = value;
	}

	public static float TotalAfterLastSeconds
	{
		get => Timing.TotalAfterLastSeconds;
		set => Timing.TotalAfterLastSeconds = value;
	}

	public static float TotalAnimationSeconds
	{
		get => Timing.TotalAnimationSeconds;
		set => Timing.TotalAnimationSeconds = value;
	}

	public static float CountUpSeconds
	{
		get => Timing.CountUpSeconds;
		set => Timing.CountUpSeconds = value;
	}

	public static float ExitFadeSeconds
	{
		get => Timing.ExitFadeSeconds;
		set => Timing.ExitFadeSeconds = value;
	}

	public static float TierNodeSlamOffsetSeconds
	{
		get => Timing.TierNodeSlamOffsetSeconds;
		set => Timing.TierNodeSlamOffsetSeconds = value;
	}

	private const float BoundaryEpsilon = 0.00001f;

	public static readonly ClimbPointsAwardTier[] Tiers =
	[
		new("shop1", CollectionProgressionRules.FirstShopRefreshTime, false, 1, "First Threshold", "FIRST SHOP REFRESH REACHED | TIME 8+"),
		new("shop2", CollectionProgressionRules.SecondShopRefreshTime, false, 3, "Second Threshold", "SECOND SHOP REFRESH REACHED | TIME 16+"),
		new("shop3", CollectionProgressionRules.ThirdShopRefreshTime, false, 5, "Third Threshold", "THIRD SHOP REFRESH REACHED | TIME 24+"),
		new("boss", 0, true, 3, "Final Judgment", "FINAL BOSS DEFEATED"),
	];

	public static ClimbPointsAwardScenario CreateScenario(
		int timeReached,
		bool completedFinalBoss,
		bool abandoned,
		int shopRefreshInterval = CollectionProgressionRules.FirstShopRefreshTime)
	{
		return new ClimbPointsAwardScenario(
			Math.Max(0, timeReached),
			Math.Max(1, shopRefreshInterval),
			completedFinalBoss,
			abandoned,
			completedFinalBoss ? "A VICTORIOUS RETURN" : "RETURNED TO THE WAYSTATION",
			abandoned ? "The Climb Is Forfeit" : "The Climb Remembers",
			abandoned ? "No progress was banked" : "No threshold reached",
			abandoned ? "ABANDONED CLIMBS AWARD NO CLIMB POINTS" : "CONTINUE CLIMBING TO EARN CLIMB POINTS");
	}

	public static int GetEarnedTierCount(ClimbPointsAwardScenario scenario)
	{
		if (scenario.Abandoned) return 0;
		int count = 0;
		for (int index = 0; index < Tiers.Length; index++)
		{
			if (IsTierEarned(Tiers[index], scenario)) count++;
		}
		return count;
	}

	public static bool IsTierEarned(ClimbPointsAwardTier tier, ClimbPointsAwardScenario scenario)
	{
		if (scenario.Abandoned) return false;
		return tier.RequiresBoss
			? scenario.CompletedFinalBoss
			: scenario.TimeReached >= GetTierTimeThreshold(tier, scenario);
	}

	public static int GetTierTimeThreshold(ClimbPointsAwardTier tier, ClimbPointsAwardScenario scenario)
	{
		if (tier.RequiresBoss) return 0;
		int cycle = tier.Id switch
		{
			"shop1" => 1,
			"shop2" => 2,
			"shop3" => 3,
			_ => Math.Max(1, tier.TimeThreshold / CollectionProgressionRules.FirstShopRefreshTime),
		};
		return Math.Max(1, scenario.ShopRefreshInterval) * cycle;
	}

	public static string GetTierRequirement(ClimbPointsAwardTier tier, ClimbPointsAwardScenario scenario)
	{
		if (tier.RequiresBoss) return tier.Requirement;
		return tier.Id switch
		{
			"shop1" => $"FIRST SHOP REFRESH REACHED | TIME {GetTierTimeThreshold(tier, scenario)}+",
			"shop2" => $"SECOND SHOP REFRESH REACHED | TIME {GetTierTimeThreshold(tier, scenario)}+",
			"shop3" => $"THIRD SHOP REFRESH REACHED | TIME {GetTierTimeThreshold(tier, scenario)}+",
			_ => tier.Requirement,
		};
	}

	public static float GetTierRevealSeconds(int earnedIndex) =>
		Timing.TierStartSeconds + Math.Max(0, earnedIndex) * Timing.TierStaggerSeconds;

	public static float GetRouteFillStartSeconds(int earnedIndex) =>
		earnedIndex == 0
			? Timing.ProgressMeterStartDelaySeconds
			: GetTierRevealSeconds(earnedIndex);

	public static float GetTotalRevealSeconds(int earnedTierCount)
	{
		float lastTier = earnedTierCount > 0
			? GetTierRevealSeconds(earnedTierCount - 1)
			: Timing.TierStartSeconds;
		return lastTier + Timing.TotalAfterLastSeconds;
	}

	public static float GetReadySeconds(int earnedTierCount) =>
		GetTotalRevealSeconds(earnedTierCount) + Timing.TotalAnimationSeconds;

	public static float GetProgressCap(int earnedTierCount) =>
		earnedTierCount <= 0 ? 0f : (float)earnedTierCount / Tiers.Length;

	public static float GetTierNodeSlamSeconds(int earnedIndex) =>
		GetTierRevealSeconds(earnedIndex) + Timing.TierNodeSlamOffsetSeconds;

	public static float GetCrestRevealSeconds(int earnedTierCount) =>
		GetTotalRevealSeconds(earnedTierCount);

	public static float GetCountUpCompleteSeconds(int earnedTierCount) =>
		GetTotalRevealSeconds(earnedTierCount) + Timing.CountUpSeconds;

	public static ClimbPointsAwardRumbleSample SampleRumble(
		float elapsedSeconds,
		int earnedTierCount,
		ClimbPointsAwardRumbleSettings settings)
	{
		float elapsed = ClampElapsed(elapsedSeconds);
		if (earnedTierCount <= 0)
		{
			return SampleEmptyStateRumble(elapsed, settings);
		}

		ClimbPointsAwardRumbleSample buildup = SampleBuildupRumble(elapsed, earnedTierCount, settings);
		ClimbPointsAwardRumbleSample tierPulses = SampleTierPulseRumble(elapsed, earnedTierCount, settings);
		return CombineSamples(buildup, tierPulses);
	}

	public static IEnumerable<ClimbPointsAwardRumbleMilestone> GetCrossedFinaleMilestones(
		float previousSeconds,
		float currentSeconds,
		int earnedTierCount)
	{
		if (earnedTierCount <= 0) yield break;

		float previous = ClampElapsed(previousSeconds);
		float current = ClampElapsed(currentSeconds);
		if (current <= previous) yield break;

		var milestones = new[]
		{
			new ClimbPointsAwardRumbleMilestone(
				ClimbPointsAwardRumbleMilestoneKind.CrestReveal,
				GetCrestRevealSeconds(earnedTierCount)),
			new ClimbPointsAwardRumbleMilestone(
				ClimbPointsAwardRumbleMilestoneKind.CountUpComplete,
				GetCountUpCompleteSeconds(earnedTierCount)),
		};

		foreach (var milestone in milestones)
		{
			if (milestone.Seconds > previous + BoundaryEpsilon
				&& milestone.Seconds <= current + BoundaryEpsilon)
			{
				yield return milestone;
			}
		}
	}

	public static RumbleProfile GetFinaleRumbleProfile(
		ClimbPointsAwardRumbleMilestoneKind kind,
		float progressCap) =>
		kind switch
		{
			ClimbPointsAwardRumbleMilestoneKind.CrestReveal => RumbleProfile.MediumImpact,
			ClimbPointsAwardRumbleMilestoneKind.CountUpComplete when progressCap >= 1f => RumbleProfile.AchievementUnlock,
			ClimbPointsAwardRumbleMilestoneKind.CountUpComplete => RumbleProfile.HeavyImpact,
			_ => RumbleProfile.None,
		};

	public static float GetFinaleRumbleScale(
		ClimbPointsAwardRumbleMilestoneKind kind,
		float progressCap)
	{
		float cap = Math.Max(0f, progressCap);
		return kind switch
		{
			ClimbPointsAwardRumbleMilestoneKind.CrestReveal => cap,
			ClimbPointsAwardRumbleMilestoneKind.CountUpComplete => Math.Min(1f, cap * 1.15f),
			_ => 0f,
		};
	}

	public static float GetRouteRowBottom(int earnedTierCount, int earnedIndex)
	{
		if (earnedTierCount <= 1) return 172f;
		float spacing = Math.Min(112f, 414f / (earnedTierCount - 1));
		return 20f + earnedIndex * spacing;
	}

	public static float GetRouteFillHeight(int earnedTierCount, int earnedIndex)
	{
		if (earnedTierCount <= 0 || earnedIndex < 0) return 0f;
		if (earnedTierCount == 1) return 221f;
		float spacing = Math.Min(112f, 414f / (earnedTierCount - 1));
		return 66f + earnedIndex * spacing;
	}

	public static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);

	public static float EaseOutCubic(float value)
	{
		float t = Clamp01(value);
		return 1f - MathF.Pow(1f - t, 3f);
	}

	public static float EaseRise(float value) => CubicBezier(Clamp01(value), 0.16f, 1f, 0.3f, 1f);

	public static float EaseSlam(float value) => CubicBezier(Clamp01(value), 0.2f, 1.35f, 0.4f, 1f);

	public static float CubicBezier(float x, float x1, float y1, float x2, float y2)
	{
		float t = x;
		for (int iteration = 0; iteration < 5; iteration++)
		{
			float currentX = SampleCurve(t, x1, x2) - x;
			float derivative = SampleDerivative(t, x1, x2);
			if (MathF.Abs(derivative) < 0.0001f) break;
			t = Math.Clamp(t - currentX / derivative, 0f, 1f);
		}
		return SampleCurve(t, y1, y2);
	}

	private static float SampleCurve(float t, float p1, float p2)
	{
		float inverse = 1f - t;
		return 3f * inverse * inverse * t * p1
			+ 3f * inverse * t * t * p2
			+ t * t * t;
	}

	private static float SampleDerivative(float t, float p1, float p2)
	{
		float inverse = 1f - t;
		return 3f * inverse * inverse * p1
			+ 6f * inverse * t * (p2 - p1)
			+ 3f * t * t * (1f - p2);
	}

	private static float ClampElapsed(float elapsedSeconds) =>
		float.IsFinite(elapsedSeconds) ? Math.Max(0f, elapsedSeconds) : 0f;

	private static ClimbPointsAwardRumbleSample SampleEmptyStateRumble(
		float elapsed,
		ClimbPointsAwardRumbleSettings settings)
	{
		float local = elapsed - Timing.TierStartSeconds;
		float duration = Math.Max(0.001f, settings.EmptyPulseDurationSeconds);
		if (local < 0f || local >= duration) return ClimbPointsAwardRumbleSample.Zero;

		float envelope = 1f - local / duration;
		return new ClimbPointsAwardRumbleSample(
			settings.EmptyPulseLow * envelope,
			settings.EmptyPulseHigh * envelope,
			settings.EmptyPulseTrigger * envelope,
			settings.EmptyPulseTrigger * envelope);
	}

	private static ClimbPointsAwardRumbleSample SampleBuildupRumble(
		float elapsed,
		int earnedTierCount,
		ClimbPointsAwardRumbleSettings settings)
	{
		float totalReveal = GetTotalRevealSeconds(earnedTierCount);
		if (elapsed < Timing.TierStartSeconds || elapsed >= totalReveal)
		{
			return ClimbPointsAwardRumbleSample.Zero;
		}

		float duration = totalReveal - Timing.TierStartSeconds;
		if (duration <= 0f) return ClimbPointsAwardRumbleSample.Zero;

		float progress = Clamp01((elapsed - Timing.TierStartSeconds) / duration);
		float intensity = progress * progress * GetProgressCap(earnedTierCount);
		return new ClimbPointsAwardRumbleSample(
			settings.MaxBuildupLow * intensity,
			settings.MaxBuildupHigh * intensity,
			settings.MaxBuildupTrigger * intensity,
			settings.MaxBuildupTrigger * intensity);
	}

	private static ClimbPointsAwardRumbleSample SampleTierPulseRumble(
		float elapsed,
		int earnedTierCount,
		ClimbPointsAwardRumbleSettings settings)
	{
		float progressCap = GetProgressCap(earnedTierCount);
		float duration = Math.Max(0.001f, settings.TierPulseDurationSeconds);
		float bestLow = 0f;
		float bestHigh = 0f;
		float bestTrigger = 0f;

		for (int earnedIndex = 0; earnedIndex < earnedTierCount; earnedIndex++)
		{
			float local = elapsed - GetTierNodeSlamSeconds(earnedIndex);
			if (local < 0f || local >= duration) continue;

			float envelope = 1f - local / duration;
			float amplitude = ((earnedIndex + 1f) / earnedTierCount) * progressCap;
			bestLow = Math.Max(bestLow, settings.TierPulseLow * envelope * amplitude);
			bestHigh = Math.Max(bestHigh, settings.TierPulseHigh * envelope * amplitude);
			bestTrigger = Math.Max(bestTrigger, settings.TierPulseTrigger * envelope * amplitude);
		}

		return bestLow <= BoundaryEpsilon && bestHigh <= BoundaryEpsilon && bestTrigger <= BoundaryEpsilon
			? ClimbPointsAwardRumbleSample.Zero
			: new ClimbPointsAwardRumbleSample(bestLow, bestHigh, bestTrigger, bestTrigger);
	}

	private static ClimbPointsAwardRumbleSample CombineSamples(
		ClimbPointsAwardRumbleSample left,
		ClimbPointsAwardRumbleSample right) =>
		new(
			left.LowFrequency + right.LowFrequency,
			left.HighFrequency + right.HighFrequency,
			left.LeftTrigger + right.LeftTrigger,
			left.RightTrigger + right.RightTrigger);
}
