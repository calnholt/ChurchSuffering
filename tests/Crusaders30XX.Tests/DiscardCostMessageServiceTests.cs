using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class DiscardCostMessageServiceTests
{
	[Theory]
	[InlineData(new[] { "Any" }, "You need another card in your hand to pay for the discard cost")]
	[InlineData(new[] { "Any", "Any" }, "You need two other cards in your hand to pay for the discard cost")]
	[InlineData(new[] { "Any", "Any", "Any" }, "You need three other cards in your hand to pay for the discard cost")]
	[InlineData(new[] { "Red" }, "You need a red card in your hand to pay for the discard cost")]
	[InlineData(new[] { "White" }, "You need a white card in your hand to pay for the discard cost")]
	[InlineData(new[] { "Black" }, "You need a black card in your hand to pay for the discard cost")]
	[InlineData(new[] { "Red", "Any" }, "You need a red card and another card to pay for the discard cost")]
	[InlineData(new[] { "White", "Any" }, "You need a white card and another card to pay for the discard cost")]
	[InlineData(new[] { "Red", "White" }, "You need a red card and a white card to pay for the discard cost")]
	[InlineData(
		new[] { "Red", "Black", "Any", "Any", "Any" },
		"You need a red card, a black card, and three other cards to pay for the discard cost")]
	public void GetUnsatisfiableCostMessage_formats_required_costs(string[] costs, string expected)
	{
		Assert.Equal(expected, DiscardCostMessageService.GetUnsatisfiableCostMessage(costs));
	}

	[Fact]
	public void GetUnsatisfiableCostMessage_handles_empty_cost_list()
	{
		Assert.Equal(
			"You need another card in your hand to pay for the discard cost",
			DiscardCostMessageService.GetUnsatisfiableCostMessage([]));
	}
}
