using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class PortraitPixelBurstLayoutTests
{
	private const float Epsilon = 0.001f;

	[Fact]
	public void ComputeWorldPosition_textureOriginPixel_equalsDrawCenter()
	{
		var drawCenter = new Vector2(640f, 400f);
		const int w = 100;
		const int h = 200;
		var drawScale = new Vector2(2.5f, 2.5f);
		int originX = (int)(w / 2f);
		int originY = (int)(h / 2f);

		var world = PortraitPixelBurstLayout.ComputeWorldPosition(
			drawCenter, w, h, originX, originY, drawScale);

		AssertNear(drawCenter, world);
	}

	[Fact]
	public void ComputeWorldPosition_corners_matchSpriteBatchTopLeftMath()
	{
		var drawCenter = new Vector2(500f, 300f);
		const int w = 80;
		const int h = 120;
		var drawScale = new Vector2(1.75f, 1.75f);
		var origin = PortraitPixelBurstLayout.ComputeTextureOrigin(w, h);

		var topLeft = drawCenter - origin * drawScale;
		var expectedTopLeft = PortraitPixelBurstLayout.ComputeTopLeft(drawCenter, w, h, drawScale);
		AssertNear(topLeft, expectedTopLeft);

		var topLeftPixel = PortraitPixelBurstLayout.ComputeWorldPosition(drawCenter, w, h, 0, 0, drawScale);
		AssertNear(topLeft, topLeftPixel);

		var bottomRight = PortraitPixelBurstLayout.ComputeWorldPosition(drawCenter, w, h, w - 1, h - 1, drawScale);
		AssertNear(topLeft + new Vector2((w - 1) * drawScale.X, (h - 1) * drawScale.Y), bottomRight);
	}

	[Fact]
	public void ComputeWorldPosition_nonUniformScaleY_spreadsVerticallyLikeSpriteBatch()
	{
		var drawCenter = new Vector2(400f, 400f);
		const int w = 10;
		const int h = 100;
		var drawScale = new Vector2(2f, 4f);
		var origin = PortraitPixelBurstLayout.ComputeTextureOrigin(w, h);
		var topLeft = drawCenter - origin * drawScale;

		var bottom = PortraitPixelBurstLayout.TexturePixelToWorld(topLeft, 5, h - 1, drawScale);
		var bottomWithUniformXOnly = topLeft + new Vector2(5 * drawScale.X, (h - 1) * drawScale.X);

		Assert.True(bottom.Y > bottomWithUniformXOnly.Y,
			"Using X scale for Y should compress the silhouette upward");
		AssertNear(topLeft + new Vector2(5 * drawScale.X, (h - 1) * drawScale.Y), bottom);
	}

	[Fact]
	public void ResolveDrawFrame_prefersLastDrawPoseOverTransform()
	{
		var transformPos = new Vector2(960f, 540f);
		var lastDrawCenter = new Vector2(1020f, 480f);
		var lastTopLeft = new Vector2(900f, 200f);
		var lastScale = new Vector2(3.25f, 3.5f);
		var info = new PortraitInfo
		{
			TextureWidth = 128,
			TextureHeight = 256,
			LastDrawCenter = lastDrawCenter,
			LastDrawTopLeft = lastTopLeft,
			LastDrawScale = lastScale,
			CurrentScale = 2f
		};

		var (center, topLeft, drawScale) = PortraitPixelBurstLayout.ResolveDrawFrame(
			info, transformPos, 128, 256, viewportHeight: 1080);

		AssertNear(lastDrawCenter, center);
		AssertNear(lastTopLeft, topLeft);
		AssertNear(lastScale, drawScale);
	}

	[Fact]
	public void ResolveDrawFrame_withoutLastDraw_fallsBackToTransformAndComputedScale()
	{
		var transformPos = new Vector2(700f, 350f);
		var info = new PortraitInfo
		{
			TextureWidth = 100,
			TextureHeight = 200,
			CurrentScale = 0f,
			LastDrawScale = Vector2.Zero
		};

		var (center, topLeft, drawScale) = PortraitPixelBurstLayout.ResolveDrawFrame(
			info, transformPos, 100, 200, viewportHeight: 1080);

		AssertNear(transformPos, center);
		float uniform = 0.30f * 1080f / 200f;
		AssertNear(new Vector2(uniform, uniform), drawScale);
		AssertNear(PortraitPixelBurstLayout.ComputeTopLeft(transformPos, 100, 200, drawScale), topLeft);
	}

	[Fact]
	public void SampledPortraitOutline_matchesEnemyDisplayBounds()
	{
		var drawCenter = new Vector2(880f, 420f);
		const int texW = 64;
		const int texH = 96;
		var drawScale = new Vector2(2f, 2f);
		var origin = PortraitPixelBurstLayout.ComputeTextureOrigin(texW, texH);
		var topLeft = drawCenter - origin * drawScale;
		int wPx = (int)System.Math.Round(texW * drawScale.X);
		int hPx = (int)System.Math.Round(texH * drawScale.Y);
		int x0 = (int)System.Math.Round(drawCenter.X - wPx / 2f);
		int y0 = (int)System.Math.Round(drawCenter.Y - hPx / 2f);

		var topLeftWorld = PortraitPixelBurstLayout.TexturePixelToWorld(topLeft, 0, 0, drawScale);
		var bottomRightWorld = PortraitPixelBurstLayout.TexturePixelToWorld(topLeft, texW - 1, texH - 1, drawScale);

		Assert.InRange(topLeftWorld.X, x0 - 1f, x0 + 1f);
		Assert.InRange(topLeftWorld.Y, y0 - 1f, y0 + 1f);
		Assert.InRange(bottomRightWorld.X, x0 + wPx - 2f, x0 + wPx);
		Assert.InRange(bottomRightWorld.Y, y0 + hPx - 2f, y0 + hPx);
	}

	[Fact]
	public void ClampTravelFromSpawn_usesSpawnNotPortraitCenter_preventsRingSnap()
	{
		var portraitCenter = new Vector2(500f, 400f);
		var spawn = portraitCenter + new Vector2(0f, 150f);
		var position = spawn + new Vector2(0f, 80f);
		const float limit = 50f;

		var clamped = PortraitPixelBurstLayout.ClampTravelFromSpawn(position, spawn, limit);
		float distFromPortraitCenter = Vector2.Distance(clamped, portraitCenter);

		Assert.True(distFromPortraitCenter > limit);
		Assert.Equal(limit, Vector2.Distance(clamped, spawn), 3);
	}

	private static void AssertNear(Vector2 expected, Vector2 actual)
	{
		Assert.True(Vector2.Distance(expected, actual) < Epsilon,
			$"Expected {expected}, got {actual}");
	}
}
