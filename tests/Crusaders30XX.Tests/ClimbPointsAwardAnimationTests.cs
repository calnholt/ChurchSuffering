using System;
using System.Linq;
using System.Reflection;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

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
}
