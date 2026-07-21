using System;
using System.Linq;
using System.Reflection;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class ClimbPointsAwardAnimationTests
{
	[Theory]
	[InlineData(0, false, false, 0, 1.90f)]
	[InlineData(12, false, false, 1, 1.90f)]
	[InlineData(20, false, false, 2, 2.31f)]
	[InlineData(26, false, false, 3, 2.72f)]
	[InlineData(32, true, false, 4, 3.13f)]
	[InlineData(18, false, true, 0, 1.90f)]
	public void Scenario_timing_matches_the_mockup(
		int time,
		bool boss,
		bool abandoned,
		int expectedTiers,
		float expectedReady)
	{
		var scenario = ClimbPointsAwardAnimationService.CreateScenario(time, boss, abandoned);
		int tiers = ClimbPointsAwardAnimationService.GetEarnedTierCount(scenario);

		Assert.Equal(expectedTiers, tiers);
		Assert.Equal(expectedReady, ClimbPointsAwardAnimationService.GetReadySeconds(tiers), 3);
	}

	[Fact]
	public void Victory_route_uses_the_mockup_bottom_to_top_spacing()
	{
		Assert.Equal(20f, ClimbPointsAwardAnimationService.GetRouteRowBottom(4, 0));
		Assert.Equal(132f, ClimbPointsAwardAnimationService.GetRouteRowBottom(4, 1));
		Assert.Equal(244f, ClimbPointsAwardAnimationService.GetRouteRowBottom(4, 2));
		Assert.Equal(356f, ClimbPointsAwardAnimationService.GetRouteRowBottom(4, 3));
		Assert.Equal(402f, ClimbPointsAwardAnimationService.GetRouteFillHeight(4, 3));
	}

	[Fact]
	public void Abandoned_scenario_uses_forfeit_copy_and_no_tiers()
	{
		var scenario = ClimbPointsAwardAnimationService.CreateScenario(18, false, true);

		Assert.Equal("The Climb Is Forfeit", scenario.Title);
		Assert.Equal("No progress was banked", scenario.EmptyTitle);
		Assert.Equal(0, ClimbPointsAwardAnimationService.GetEarnedTierCount(scenario));
	}

	[Fact]
	public void Pilgrimage_scenario_uses_dynamic_refresh_thresholds_and_copy()
	{
		var beforeRefresh = ClimbPointsAwardAnimationService.CreateScenario(8, false, false, shopRefreshInterval: 9);
		var thirdRefresh = ClimbPointsAwardAnimationService.CreateScenario(27, false, false, shopRefreshInterval: 9);
		var thirdTier = ClimbPointsAwardAnimationService.Tiers.Single(tier => tier.Id == "shop3");

		Assert.Equal(0, ClimbPointsAwardAnimationService.GetEarnedTierCount(beforeRefresh));
		Assert.Equal(3, ClimbPointsAwardAnimationService.GetEarnedTierCount(thirdRefresh));
		Assert.Equal("THIRD SHOP REFRESH REACHED | TIME 27+", ClimbPointsAwardAnimationService.GetTierRequirement(thirdTier, thirdRefresh));
	}

	[Fact]
	public void Display_system_exposes_all_six_fixed_no_argument_debug_previews()
	{
		string[] expected =
		[
			"Preview Time 0 (+0)",
			"Preview Time 12 (+1)",
			"Preview Time 20 (+4)",
			"Preview Time 26 (+9)",
			"Preview Victory (+12)",
			"Preview Abandoned (+0)",
		];
		string[] actual = typeof(ClimbPointsAwardDisplaySystem)
			.GetMethods(BindingFlags.Instance | BindingFlags.Public)
			.Select(method => (Method: method, Attribute: method.GetCustomAttribute<DebugActionAttribute>()))
			.Where(item => item.Attribute != null && item.Method.GetParameters().Length == 0)
			.Select(item => item.Attribute.DisplayName)
			.Where(name => name.StartsWith("Preview ", StringComparison.Ordinal))
			.OrderBy(name => name, StringComparer.Ordinal)
			.ToArray();

		Assert.Equal(expected.OrderBy(name => name, StringComparer.Ordinal), actual);
	}

	[Theory]
	[InlineData(0, 0f)]
	[InlineData(1, 0.25f)]
	[InlineData(4, 1f)]
	public void Progress_cap_scales_with_earned_tier_count(int earnedTierCount, float expectedCap)
	{
		Assert.Equal(expectedCap, ClimbPointsAwardAnimationService.GetProgressCap(earnedTierCount), 3);
	}

	[Fact]
	public void Buildup_rumble_is_zero_before_tier_start_and_at_crest_reveal()
	{
		var settings = ClimbPointsAwardRumbleSettings.Default;
		const int earnedTierCount = 4;

		Assert.Equal(
			ClimbPointsAwardRumbleSample.Zero,
			ClimbPointsAwardAnimationService.SampleRumble(
				ClimbPointsAwardAnimationService.TierStartSeconds - 0.01f,
				earnedTierCount,
				settings));
		Assert.Equal(
			ClimbPointsAwardRumbleSample.Zero,
			ClimbPointsAwardAnimationService.SampleRumble(
				ClimbPointsAwardAnimationService.GetCrestRevealSeconds(earnedTierCount),
				earnedTierCount,
				settings));
	}

	[Fact]
	public void Buildup_rumble_increases_monotonically_during_route_reveal()
	{
		var settings = ClimbPointsAwardRumbleSettings.Default;
		const int earnedTierCount = 4;
		float start = ClimbPointsAwardAnimationService.TierStartSeconds + 0.05f;
		float end = ClimbPointsAwardAnimationService.GetCrestRevealSeconds(earnedTierCount) - 0.01f;
		float early = ClimbPointsAwardAnimationService.SampleRumble(start, earnedTierCount, settings).LowFrequency;
		float late = ClimbPointsAwardAnimationService.SampleRumble(end, earnedTierCount, settings).LowFrequency;

		Assert.True(early > 0f);
		Assert.True(late > early);
	}

	[Fact]
	public void Partial_climb_buildup_stays_below_full_victory_intensity()
	{
		var settings = ClimbPointsAwardRumbleSettings.Default;
		float relativeProgress = 0.5f;
		float oneTierEnd = ClimbPointsAwardAnimationService.GetCrestRevealSeconds(1);
		float oneTierTime = ClimbPointsAwardAnimationService.TierStartSeconds
			+ (oneTierEnd - ClimbPointsAwardAnimationService.TierStartSeconds) * relativeProgress;
		float fourTierEnd = ClimbPointsAwardAnimationService.GetCrestRevealSeconds(4);
		float fourTierTime = ClimbPointsAwardAnimationService.TierStartSeconds
			+ (fourTierEnd - ClimbPointsAwardAnimationService.TierStartSeconds) * relativeProgress;

		float oneTier = ClimbPointsAwardAnimationService.SampleRumble(oneTierTime, 1, settings).LowFrequency;
		float fourTier = ClimbPointsAwardAnimationService.SampleRumble(fourTierTime, 4, settings).LowFrequency;

		Assert.True(oneTier > 0f);
		Assert.True(fourTier > oneTier);
	}

	[Fact]
	public void Tier_pulse_rumble_peaks_near_node_slam_times()
	{
		var settings = ClimbPointsAwardRumbleSettings.Default;
		const int earnedTierCount = 4;
		float firstSlam = ClimbPointsAwardAnimationService.GetTierNodeSlamSeconds(0);
		float peak = ClimbPointsAwardAnimationService.SampleRumble(firstSlam, earnedTierCount, settings).HighFrequency;
		float before = ClimbPointsAwardAnimationService.SampleRumble(firstSlam - 0.02f, earnedTierCount, settings).HighFrequency;
		float after = ClimbPointsAwardAnimationService.SampleRumble(
			firstSlam + settings.TierPulseDurationSeconds + 0.01f,
			earnedTierCount,
			settings).HighFrequency;

		Assert.True(peak > before);
		Assert.True(peak > after);
	}

	[Fact]
	public void Abandoned_rumble_stays_within_empty_pulse_ceiling()
	{
		var settings = ClimbPointsAwardRumbleSettings.Default;
		float pulse = ClimbPointsAwardAnimationService.SampleRumble(
			ClimbPointsAwardAnimationService.TierStartSeconds,
			0,
			settings).LowFrequency;

		Assert.True(pulse > 0f);
		Assert.True(pulse <= settings.EmptyPulseLow + 0.0001f);
	}

	[Fact]
	public void GetCrossedFinaleMilestones_fires_crest_and_count_up_boundaries()
	{
		const int earnedTierCount = 4;
		float crest = ClimbPointsAwardAnimationService.GetCrestRevealSeconds(earnedTierCount);
		float countUp = ClimbPointsAwardAnimationService.GetCountUpCompleteSeconds(earnedTierCount);

		var crestCrossing = ClimbPointsAwardAnimationService.GetCrossedFinaleMilestones(
			crest - 0.01f,
			crest + 0.01f,
			earnedTierCount).ToArray();
		var countUpCrossing = ClimbPointsAwardAnimationService.GetCrossedFinaleMilestones(
			countUp - 0.01f,
			countUp + 0.01f,
			earnedTierCount).ToArray();

		Assert.Single(crestCrossing);
		Assert.Equal(ClimbPointsAwardRumbleMilestoneKind.CrestReveal, crestCrossing[0].Kind);
		Assert.Single(countUpCrossing);
		Assert.Equal(ClimbPointsAwardRumbleMilestoneKind.CountUpComplete, countUpCrossing[0].Kind);
	}

	[Fact]
	public void GetCrossedFinaleMilestones_skips_zero_tier_runs()
	{
		Assert.Empty(ClimbPointsAwardAnimationService.GetCrossedFinaleMilestones(0f, 3f, 0));
	}

	[Fact]
	public void Finale_profiles_and_scales_scale_with_progress_cap()
	{
		Assert.Equal(
			RumbleProfile.MediumImpact,
			ClimbPointsAwardAnimationService.GetFinaleRumbleProfile(
				ClimbPointsAwardRumbleMilestoneKind.CrestReveal,
				0.5f));
		Assert.Equal(
			RumbleProfile.AchievementUnlock,
			ClimbPointsAwardAnimationService.GetFinaleRumbleProfile(
				ClimbPointsAwardRumbleMilestoneKind.CountUpComplete,
				1f));
		Assert.Equal(
			RumbleProfile.HeavyImpact,
			ClimbPointsAwardAnimationService.GetFinaleRumbleProfile(
				ClimbPointsAwardRumbleMilestoneKind.CountUpComplete,
				0.5f));
		Assert.True(ClimbPointsAwardAnimationService.GetFinaleRumbleScale(
			ClimbPointsAwardRumbleMilestoneKind.CountUpComplete,
			0.5f)
			> ClimbPointsAwardAnimationService.GetFinaleRumbleScale(
				ClimbPointsAwardRumbleMilestoneKind.CrestReveal,
				0.5f));
	}
}
