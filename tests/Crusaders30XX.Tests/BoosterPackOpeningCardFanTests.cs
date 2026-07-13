using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class BoosterPackOpeningCardFanTests
{
	[Fact]
	public void Placements_are_split_and_ordered_back_to_front()
	{
		float fanRotation = MathHelper.ToRadians(5f);
		var placements = BoosterPackOpeningDisplaySystem.ComputeCardFanPlacements(
			new Vector2(100f, 200f),
			revealScale: 1f,
			groupRotation: 0f,
			horizontalGap: 34f,
			rearDrop: 12f,
			fanRotation);

		Assert.Equal(CardData.CardColor.Black, placements[0].Color);
		Assert.Equal(new Vector2(134f, 212f), placements[0].Position);
		Assert.Equal(fanRotation, placements[0].Rotation, 5);

		Assert.Equal(CardData.CardColor.Red, placements[1].Color);
		Assert.Equal(new Vector2(66f, 212f), placements[1].Position);
		Assert.Equal(-fanRotation, placements[1].Rotation, 5);

		Assert.Equal(CardData.CardColor.White, placements[2].Color);
		Assert.Equal(new Vector2(100f, 200f), placements[2].Position);
		Assert.Equal(0f, placements[2].Rotation);
	}

	[Fact]
	public void Placements_scale_and_rotate_with_the_reveal_group()
	{
		float groupRotation = MathHelper.PiOver2;
		float fanRotation = MathHelper.ToRadians(6f);
		var placements = BoosterPackOpeningDisplaySystem.ComputeCardFanPlacements(
			new Vector2(100f, 200f),
			revealScale: 0.5f,
			groupRotation,
			horizontalGap: 20f,
			rearDrop: 10f,
			fanRotation);

		AssertVectorNear(new Vector2(95f, 210f), placements[0].Position);
		Assert.Equal(groupRotation + fanRotation, placements[0].Rotation, 5);
		AssertVectorNear(new Vector2(95f, 190f), placements[1].Position);
		Assert.Equal(groupRotation - fanRotation, placements[1].Rotation, 5);
		Assert.Equal(new Vector2(100f, 200f), placements[2].Position);
		Assert.Equal(groupRotation, placements[2].Rotation);
	}

	private static void AssertVectorNear(Vector2 expected, Vector2 actual)
	{
		Assert.InRange(actual.X, expected.X - 0.0001f, expected.X + 0.0001f);
		Assert.InRange(actual.Y, expected.Y - 0.0001f, expected.Y + 0.0001f);
	}
}
