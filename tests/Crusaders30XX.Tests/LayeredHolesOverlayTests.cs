using Crusaders30XX.ECS.Rendering;
using Xunit;

namespace Crusaders30XX.Tests;

public class LayeredHolesOverlayTests
{
	[Fact]
	public void Hole_layer_pick_stays_stable_within_same_lifecycle_cycle()
	{
		var overlay = new LayeredHolesOverlay(null)
		{
			Time = 10f,
			HoleCount = 1,
			HolePeriodMin = 60f,
			HolePeriodMax = 60f,
			HoleLifeMin = 1f,
			HoleLifeMax = 1f,
			LayerSplit = 0.5f,
		};

		var firstFrame = overlay.BuildHoleData(aspect: 16f / 9f)[0];
		overlay.Time += 1f / 60f;
		var nextFrame = overlay.BuildHoleData(aspect: 16f / 9f)[0];

		Assert.True(firstFrame.Z > 0f);
		Assert.True(nextFrame.Z > 0f);
		Assert.Equal(firstFrame.W, nextFrame.W);
	}
}
