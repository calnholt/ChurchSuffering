using System.Linq;
using ChurchSuffering.ECS.Data.RunSetup;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class PenanceRulesTests
{
	[Fact]
	public void Fixed_order_has_exact_twenty_four_entries_and_final_totals()
	{
		Assert.Equal(24, PenanceRules.Order.Count);
		Assert.Equal(new[]
		{
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Abstinence,
			PenanceType.Mortification,
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Abstinence,
			PenanceType.Mortification,
			PenanceType.Reparation,
			PenanceType.PenitentialPilgrimage,
			PenanceType.Fasting,
			PenanceType.Mortification,
			PenanceType.Reparation,
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Mortification,
			PenanceType.Abstinence,
			PenanceType.Reparation,
			PenanceType.PenitentialPilgrimage,
			PenanceType.Mortification,
			PenanceType.Reparation,
			PenanceType.Fasting,
			PenanceType.Reparation,
			PenanceType.Mortification,
		}, PenanceRules.Order);

		var final = PenanceRules.Calculate(24);
		Assert.Equal(5, final.FastingStacks);
		Assert.Equal(8, final.ReparationStacks);
		Assert.Equal(3, final.AbstinenceStacks);
		Assert.Equal(6, final.MortificationStacks);
		Assert.Equal(2, final.PenitentialPilgrimageStacks);
	}

	[Theory]
	[InlineData(0, 25, 0.70f, 1, 1, 1, 8)]
	[InlineData(1, 24, 0.70f, 1, 1, 1, 8)]
	[InlineData(12, 22, 0.85f, 1, 0, 0, 9)]
	[InlineData(24, 20, 1.00f, 0, 0, 0, 10)]
	public void Calculation_matches_effect_formulas(
		int level,
		int hp,
		float enemyModifier,
		int red,
		int white,
		int black,
		int shopInterval)
	{
		var calculation = PenanceRules.Calculate(level);
		Assert.Equal(hp, calculation.PlayerMaximumHp);
		Assert.Equal(enemyModifier, calculation.EnemyHealthModifier, 3);
		Assert.Equal(new PenanceResourceCounts(red, white, black), calculation.InitialResources);
		Assert.Equal(shopInterval, calculation.ShopRefreshInterval);
	}

	[Theory]
	[InlineData(0, 1, 1, 1)]
	[InlineData(3, 1, 1, 0)]
	[InlineData(7, 1, 0, 0)]
	[InlineData(17, 0, 0, 0)]
	public void Abstinence_removes_resources_in_black_white_red_order(int level, int red, int white, int black)
	{
		Assert.Equal(new PenanceResourceCounts(red, white, black), PenanceRules.Calculate(level).InitialResources);
	}

	[Theory]
	[InlineData(-5, 0)]
	[InlineData(25, 24)]
	public void Calculate_clamps_out_of_range_levels(int supplied, int expected)
	{
		Assert.Equal(expected, PenanceRules.Calculate(supplied).Level);
	}
}
