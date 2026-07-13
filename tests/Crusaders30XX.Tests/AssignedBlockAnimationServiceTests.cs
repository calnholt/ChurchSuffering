using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class AssignedBlockAnimationServiceTests
{
	[Theory]
	[InlineData(0f, 0f)]
	[InlineData(0.5f, 0.875f)]
	[InlineData(1f, 1f)]
	public void Cubic_out_depends_only_on_total_progress(float progress, float expected)
	{
		Assert.Equal(expected, AssignedBlockAnimationService.CubicOut(progress), 4);
	}

	[Fact]
	public void Pose_evaluation_uses_fixed_start_and_is_repeatable()
	{
		var start = new Vector2(10f, 20f);
		var target = new Vector2(110f, 220f);

		Vector2 first = AssignedBlockAnimationService.LerpPosition(start, target, 0.5f);
		Vector2 second = AssignedBlockAnimationService.LerpPosition(start, target, 0.5f);

		Assert.Equal(first, second);
		Assert.Equal(new Vector2(97.5f, 195f), first);
	}
}
