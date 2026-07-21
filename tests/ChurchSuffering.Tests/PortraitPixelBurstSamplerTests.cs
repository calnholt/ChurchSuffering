using System.Linq;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace ChurchSuffering.Tests;

public class PortraitPixelBurstSamplerTests
{
	[Fact]
	public void SamplePixels_opaquePixels_spawnAtLayoutWorldPositions()
	{
		const int w = 5;
		const int h = 5;
		var pixels = new Color[w * h];
		for (int i = 0; i < pixels.Length; i++)
		{
			pixels[i] = Color.Red;
		}

		var drawCenter = new Vector2(300f, 200f);
		var drawScale = new Vector2(2f, 2f);
		var topLeft = PortraitPixelBurstLayout.ComputeTopLeft(drawCenter, w, h, drawScale);

		var spawns = PortraitPixelBurstSampler.SamplePixels(
			pixels, w, h,
			drawCenter,
			topLeft,
			drawScale,
			pixelStep: 1,
			alphaThreshold: 1,
			maxParticles: 100,
			blastRadius: 50f,
			speedMin: 100f,
			speedMax: 100f,
			outwardBias: 1f,
			velocityJitter: 0f,
			lifetimeMin: 1f,
			lifetimeMax: 1f,
			sizeMin: 2f,
			sizeMax: 2f,
			rng: new System.Random(42));

		Assert.Equal(25, spawns.Count);

		for (int y = 0; y < h; y++)
		{
			for (int x = 0; x < w; x++)
			{
				var expected = PortraitPixelBurstLayout.TexturePixelToWorld(topLeft, x, y, drawScale);
				Assert.Contains(spawns, s => NearlyEqual(s.Position, expected));
			}
		}
	}

	[Fact]
	public void SamplePixels_cappedOpaqueTexture_spansFullSourceHeight()
	{
		const int w = 10;
		const int h = 100;
		const int maxParticles = 10;
		var pixels = Enumerable.Repeat(new Color(255, 0, 0, 255), w * h).ToArray();
		var drawCenter = new Vector2(500f, 500f);
		var drawScale = Vector2.One;
		var topLeft = PortraitPixelBurstLayout.ComputeTopLeft(drawCenter, w, h, drawScale);

		var spawns = PortraitPixelBurstSampler.SamplePixels(
			pixels, w, h,
			drawCenter,
			topLeft,
			drawScale,
			pixelStep: 1,
			alphaThreshold: 1,
			maxParticles: maxParticles,
			blastRadius: 50f,
			speedMin: 100f,
			speedMax: 100f,
			outwardBias: 1f,
			velocityJitter: 0f,
			lifetimeMin: 1f,
			lifetimeMax: 1f,
			sizeMin: 2f,
			sizeMax: 2f,
			rng: new System.Random(7));

		Assert.Equal(maxParticles, spawns.Count);
		Assert.True(spawns.Min(s => s.Position.Y) <= topLeft.Y + 9f);
		Assert.True(spawns.Max(s => s.Position.Y) >= topLeft.Y + 90f);
	}

	[Fact]
	public void SamplePixels_cappedSelection_keepsExactSampledPixelPositions()
	{
		const int w = 11;
		const int h = 37;
		const int pixelStep = 3;
		const int maxParticles = 8;
		var pixels = Enumerable.Repeat(new Color(100, 20, 200, 255), w * h).ToArray();
		var drawCenter = new Vector2(250f, 300f);
		var drawScale = new Vector2(1.5f, 2.25f);
		var topLeft = PortraitPixelBurstLayout.ComputeTopLeft(drawCenter, w, h, drawScale);

		var spawns = PortraitPixelBurstSampler.SamplePixels(
			pixels, w, h,
			drawCenter,
			topLeft,
			drawScale,
			pixelStep: pixelStep,
			alphaThreshold: 1,
			maxParticles: maxParticles,
			blastRadius: 50f,
			speedMin: 100f,
			speedMax: 100f,
			outwardBias: 1f,
			velocityJitter: 0f,
			lifetimeMin: 1f,
			lifetimeMax: 1f,
			sizeMin: 2f,
			sizeMax: 2f,
			rng: new System.Random(19));

		Assert.Equal(maxParticles, spawns.Count);
		Assert.True(spawns.Min(s => s.Position.Y) <= topLeft.Y + pixelStep * drawScale.Y);
		Assert.True(spawns.Max(s => s.Position.Y) >= topLeft.Y + (h - 1 - pixelStep) * drawScale.Y);

		foreach (var spawn in spawns)
		{
			float texX = (spawn.Position.X - topLeft.X) / drawScale.X;
			float texY = (spawn.Position.Y - topLeft.Y) / drawScale.Y;
			Assert.Equal(System.MathF.Round(texX), texX, 3);
			Assert.Equal(System.MathF.Round(texY), texY, 3);
			Assert.Equal(0, ((int)System.MathF.Round(texX)) % pixelStep);
			Assert.Equal(0, ((int)System.MathF.Round(texY)) % pixelStep);
		}
	}

	[Fact]
	public void SamplePixels_transparentPixels_skipped()
	{
		const int w = 3;
		const int h = 3;
		var pixels = new Color[w * h];
		pixels[4] = new Color(255, 0, 0, 255);

		var drawCenter = new Vector2(100f, 100f);
		var drawScale = Vector2.One;
		var topLeft = PortraitPixelBurstLayout.ComputeTopLeft(drawCenter, w, h, drawScale);

		var spawns = PortraitPixelBurstSampler.SamplePixels(
			pixels, w, h,
			drawCenter,
			topLeft,
			drawScale,
			pixelStep: 1,
			alphaThreshold: 10,
			maxParticles: 10,
			blastRadius: 10f,
			speedMin: 1f,
			speedMax: 1f,
			outwardBias: 1f,
			velocityJitter: 0f,
			lifetimeMin: 1f,
			lifetimeMax: 1f,
			sizeMin: 1f,
			sizeMax: 1f,
			rng: new System.Random(0));

		Assert.Single(spawns);
		var expected = PortraitPixelBurstLayout.TexturePixelToWorld(topLeft, 1, 1, drawScale);
		AssertNear(expected, spawns[0].Position);
	}

	[Fact]
	public void SamplePixels_outwardBiasOne_velocityPointsAwayFromCenter()
	{
		const int w = 3;
		const int h = 3;
		var pixels = Enumerable.Repeat(new Color(255, 0, 0, 255), w * h).ToArray();
		var drawCenter = new Vector2(50f, 50f);
		var drawScale = Vector2.One;
		var topLeft = PortraitPixelBurstLayout.ComputeTopLeft(drawCenter, w, h, drawScale);

		var spawns = PortraitPixelBurstSampler.SamplePixels(
			pixels, w, h,
			drawCenter,
			topLeft,
			drawScale,
			pixelStep: 2,
			alphaThreshold: 1,
			maxParticles: 10,
			blastRadius: 20f,
			speedMin: 100f,
			speedMax: 100f,
			outwardBias: 1f,
			velocityJitter: 0f,
			lifetimeMin: 1f,
			lifetimeMax: 1f,
			sizeMin: 1f,
			sizeMax: 1f,
			rng: new System.Random(0));

		foreach (var spawn in spawns)
		{
			if (Vector2.DistanceSquared(spawn.Position, drawCenter) < 0.0001f) continue;
			var outward = Vector2.Normalize(spawn.Position - drawCenter);
			var velDir = Vector2.Normalize(spawn.Velocity);
			Assert.True(Vector2.Dot(outward, velDir) > 0.99f,
				$"Velocity {spawn.Velocity} should align with outward {outward}");
		}
	}

	private static bool NearlyEqual(Vector2 a, Vector2 b) => Vector2.Distance(a, b) < 0.001f;

	private static void AssertNear(Vector2 expected, Vector2 actual)
	{
		Assert.True(NearlyEqual(expected, actual), $"Expected {expected}, got {actual}");
	}
}
