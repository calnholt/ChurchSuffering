using System;

namespace Crusaders30XX.ECS.Services;

public readonly record struct ClimbPointsAwardTier(
	string Id,
	int TimeThreshold,
	bool RequiresBoss,
	int Points,
	string Name,
	string Requirement);

public readonly record struct ClimbPointsAwardScenario(
	int TimeReached,
	bool CompletedFinalBoss,
	bool Abandoned,
	string Kicker,
	string Title,
	string EmptyTitle,
	string EmptyDetail);

public static class ClimbPointsAwardAnimationService
{
	public const float TierStartSeconds = 0.620f;
	public const float TierStaggerSeconds = 0.410f;
	public const float TotalAfterLastSeconds = 0.520f;
	public const float TotalAnimationSeconds = 0.760f;
	public const float CountUpSeconds = 0.580f;
	public const float ExitFadeSeconds = 0.300f;

	public static readonly ClimbPointsAwardTier[] Tiers =
	[
		new("shop1", CollectionProgressionRules.FirstShopRefreshTime, false, 1, "First Threshold", "FIRST SHOP REFRESH REACHED | TIME 8+"),
		new("shop2", CollectionProgressionRules.SecondShopRefreshTime, false, 3, "Second Threshold", "SECOND SHOP REFRESH REACHED | TIME 16+"),
		new("shop3", CollectionProgressionRules.ThirdShopRefreshTime, false, 5, "Third Threshold", "THIRD SHOP REFRESH REACHED | TIME 24+"),
		new("boss", 0, true, 3, "Final Judgment", "FINAL BOSS DEFEATED"),
	];

	public static ClimbPointsAwardScenario CreateScenario(int timeReached, bool completedFinalBoss, bool abandoned)
	{
		return new ClimbPointsAwardScenario(
			Math.Max(0, timeReached),
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
			: scenario.TimeReached >= tier.TimeThreshold;
	}

	public static float GetTierRevealSeconds(int earnedIndex) =>
		TierStartSeconds + Math.Max(0, earnedIndex) * TierStaggerSeconds;

	public static float GetTotalRevealSeconds(int earnedTierCount)
	{
		float lastTier = earnedTierCount > 0
			? GetTierRevealSeconds(earnedTierCount - 1)
			: TierStartSeconds;
		return lastTier + TotalAfterLastSeconds;
	}

	public static float GetReadySeconds(int earnedTierCount) =>
		GetTotalRevealSeconds(earnedTierCount) + TotalAnimationSeconds;

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
}
