using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class DisplayMetricsTests
{
	[Theory]
	[InlineData(1280, 720, 1280, 720)]
	[InlineData(1920, 1080, 1920, 1080)]
	[InlineData(2560, 1440, 2560, 1440)]
	[InlineData(3840, 2160, 3840, 2160)]
	[InlineData(5120, 2880, 3840, 2160)]
	public void Calculate_matches_native_content_area_and_caps_at_4k(
		int backBufferWidth,
		int backBufferHeight,
		int expectedRenderWidth,
		int expectedRenderHeight)
	{
		var metrics = DisplayMetrics.Calculate(backBufferWidth, backBufferHeight);

		Assert.Equal(expectedRenderWidth, metrics.RenderWidth);
		Assert.Equal(expectedRenderHeight, metrics.RenderHeight);
		Assert.Equal(new Rectangle(0, 0, backBufferWidth, backBufferHeight), metrics.RenderDestination);
		Assert.Equal(
			expectedRenderWidth == DisplayMetrics.LogicalWidth && expectedRenderHeight == DisplayMetrics.LogicalHeight,
			!metrics.SpriteBatchTransform.HasValue);
	}

	[Fact]
	public void Calculate_pillarboxes_ultrawide_without_expanding_logical_canvas()
	{
		var metrics = DisplayMetrics.Calculate(3440, 1440);

		Assert.Equal(new Rectangle(440, 0, 2560, 1440), metrics.RenderDestination);
		Assert.Equal(2560, metrics.RenderWidth);
		Assert.Equal(1440, metrics.RenderHeight);
	}

	[Fact]
	public void Calculate_letterboxes_tall_backbuffer()
	{
		var metrics = DisplayMetrics.Calculate(1080, 1920);

		Assert.Equal(new Rectangle(0, 656, 1080, 607), metrics.RenderDestination);
		Assert.Equal(1080, metrics.RenderWidth);
		Assert.Equal(607, metrics.RenderHeight);
	}

	[Fact]
	public void Render_scale_override_supports_deterministic_4k_snapshots()
	{
		var metrics = DisplayMetrics.Calculate(1920, 1080, renderScaleOverride: 2f);

		Assert.Equal(new Rectangle(0, 0, 1920, 1080), metrics.RenderDestination);
		Assert.Equal(3840, metrics.RenderWidth);
		Assert.Equal(2160, metrics.RenderHeight);
	}

	[Fact]
	public void Screen_to_logical_inverts_letterboxed_presentation_and_clamps_bars()
	{
		var metrics = DisplayMetrics.Calculate(3440, 1440);

		Assert.Equal(new Vector2(960f, 540f), metrics.ScreenToLogical(new Point(1720, 720)));
		Assert.Equal(Vector2.Zero, metrics.ScreenToLogical(new Point(0, 0)));
		Assert.Equal(
			new Vector2(DisplayMetrics.LogicalWidth, DisplayMetrics.LogicalHeight),
			metrics.ScreenToLogical(new Point(3440, 1440)));
	}

	[Fact]
	public void Logical_scissors_floor_near_edges_and_ceil_far_edges()
	{
		var metrics = DisplayMetrics.Calculate(2560, 1440);

		Assert.Equal(new Rectangle(13, 13, 15, 15), metrics.LogicalToRender(new Rectangle(10, 10, 11, 11)));
		Assert.Equal(
			new Rectangle(0, 0, 2560, 1440),
			metrics.LogicalToRender(new Rectangle(-20, -20, 2000, 1200)));
	}
}
