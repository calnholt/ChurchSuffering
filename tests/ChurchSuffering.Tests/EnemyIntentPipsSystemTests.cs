using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class EnemyIntentPipsSystemTests
{
	[Fact]
	public void Pip_bounds_cover_current_and_next_intent_rows()
	{
		Rectangle bounds = EnemyIntentPipsSystem.CalculatePipBounds(
			new Vector2(800, 400),
			currentCount: 2,
			nextCount: 2,
			pipRadius: 9,
			pipGap: 10,
			offsetY: -210,
			rowGap: 16);

		Assert.Equal(new Rectangle(777, 181, 46, 48), bounds);
		Assert.False(bounds.Contains(Game1.VirtualWidth / 2, Game1.VirtualHeight / 2));
	}
}
