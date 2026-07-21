using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Services
{
	public static class PortraitPixelBurstSampler
	{
		private static readonly Dictionary<Texture2D, Color[]> _pixelCache = new();

		public static void ClearCache()
		{
			_pixelCache.Clear();
		}

		public static List<PixelBurstSpawn> Sample(
			Texture2D texture,
			Vector2 center,
			Vector2 drawTopLeft,
			Vector2 drawScale,
			int pixelStep,
			int alphaThreshold,
			int maxParticles,
			float blastRadius,
			float speedMin,
			float speedMax,
			float outwardBias,
			float velocityJitter,
			float lifetimeMin,
			float lifetimeMax,
			float sizeMin,
			float sizeMax,
			Random rng)
		{
			if (texture == null || maxParticles <= 0) return new List<PixelBurstSpawn>();
			return SamplePixels(
				GetOrReadPixels(texture),
				texture.Width,
				texture.Height,
				center,
				drawTopLeft,
				drawScale,
				pixelStep,
				alphaThreshold,
				maxParticles,
				blastRadius,
				speedMin,
				speedMax,
				outwardBias,
				velocityJitter,
				lifetimeMin,
				lifetimeMax,
				sizeMin,
				sizeMax,
				rng);
		}

		public static List<PixelBurstSpawn> SamplePixels(
			Color[] pixels,
			int width,
			int height,
			Vector2 center,
			Vector2 drawTopLeft,
			Vector2 drawScale,
			int pixelStep,
			int alphaThreshold,
			int maxParticles,
			float blastRadius,
			float speedMin,
			float speedMax,
			float outwardBias,
			float velocityJitter,
			float lifetimeMin,
			float lifetimeMax,
			float sizeMin,
			float sizeMax,
			Random rng)
		{
			if (pixels == null || width <= 0 || height <= 0 || maxParticles <= 0) return new List<PixelBurstSpawn>();
			if (pixels.Length < width * height) return new List<PixelBurstSpawn>();

			int step = System.Math.Max(1, pixelStep);
			int opaqueCount = CountOpaqueCandidates(pixels, width, height, step, alphaThreshold);
			if (opaqueCount == 0) return new List<PixelBurstSpawn>();

			int targetCount = System.Math.Min(maxParticles, opaqueCount);
			var spawns = new List<PixelBurstSpawn>(targetCount);
			int[] selectedOrdinals = opaqueCount > targetCount
				? SelectCandidateOrdinals(opaqueCount, targetCount, rng)
				: null;
			int opaqueOrdinal = 0;
			int selectedIndex = 0;

			for (int y = 0; y < height; y += step)
			{
				for (int x = 0; x < width; x += step)
				{
					var c = pixels[y * width + x];
					if (c.A < alphaThreshold) continue;

					bool shouldEmit = selectedOrdinals == null || selectedOrdinals[selectedIndex] == opaqueOrdinal;
					if (shouldEmit)
					{
						spawns.Add(CreateSpawn(
							c,
							x,
							y,
							center,
							drawTopLeft,
							drawScale,
							blastRadius,
							speedMin,
							speedMax,
							outwardBias,
							velocityJitter,
							lifetimeMin,
							lifetimeMax,
							sizeMin,
							sizeMax,
							rng));

						selectedIndex++;
						if (selectedOrdinals != null && selectedIndex >= selectedOrdinals.Length)
						{
							return spawns;
						}
					}

					opaqueOrdinal++;
				}
			}

			return spawns;
		}

		private static Color[] GetOrReadPixels(Texture2D texture)
		{
			if (_pixelCache.TryGetValue(texture, out var cached)) return cached;

			var data = new Color[texture.Width * texture.Height];
			texture.GetData(data);
			_pixelCache[texture] = data;
			return data;
		}

		private static int CountOpaqueCandidates(Color[] pixels, int width, int height, int step, int alphaThreshold)
		{
			int count = 0;
			for (int y = 0; y < height; y += step)
			{
				for (int x = 0; x < width; x += step)
				{
					if (pixels[y * width + x].A >= alphaThreshold)
					{
						count++;
					}
				}
			}

			return count;
		}

		private static int[] SelectCandidateOrdinals(int candidateCount, int targetCount, Random rng)
		{
			var ordinals = new int[targetCount];
			for (int i = 0; i < targetCount; i++)
			{
				long bucketStart = (long)i * candidateCount / targetCount;
				long bucketEnd = (long)(i + 1) * candidateCount / targetCount;
				int bucketSize = (int)(bucketEnd - bucketStart);
				ordinals[i] = (int)bucketStart + rng.Next(bucketSize);
			}

			return ordinals;
		}

		private static PixelBurstSpawn CreateSpawn(
			Color color,
			int texX,
			int texY,
			Vector2 center,
			Vector2 drawTopLeft,
			Vector2 drawScale,
			float blastRadius,
			float speedMin,
			float speedMax,
			float outwardBias,
			float velocityJitter,
			float lifetimeMin,
			float lifetimeMax,
			float sizeMin,
			float sizeMax,
			Random rng)
		{
			var worldPos = PortraitPixelBurstLayout.TexturePixelToWorld(drawTopLeft, texX, texY, drawScale);
			var offset = worldPos - center;
			Vector2 radialDir = offset.LengthSquared() > 0.0001f
				? Vector2.Normalize(offset)
				: RandomUnitVector(rng);

			float ang = (float)(rng.NextDouble() * Math.PI * 2);
			var randomDir = new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
			float bias = MathHelper.Clamp(outwardBias, 0f, 1f);
			var dir = Vector2.Normalize(radialDir * bias + randomDir * (1f - bias));

			float speed = MathHelper.Lerp(speedMin, speedMax, (float)rng.NextDouble());
			speed *= 1f + (float)(rng.NextDouble() * 2 - 1) * MathHelper.Clamp(velocityJitter, 0f, 1f);
			speed = Math.Max(0f, speed);

			float life = MathHelper.Lerp(lifetimeMin, lifetimeMax, (float)rng.NextDouble());
			float size = MathHelper.Lerp(sizeMin, sizeMax, (float)rng.NextDouble());

			return new PixelBurstSpawn
			{
				Position = worldPos,
				Velocity = dir * speed,
				Color = color,
				Lifetime = life,
				Size = size,
				BlastRadius = blastRadius
			};
		}

		private static Vector2 RandomUnitVector(Random rng)
		{
			float ang = (float)(rng.NextDouble() * Math.PI * 2);
			return new Vector2((float)Math.Cos(ang), (float)Math.Sin(ang));
		}
	}

	public struct PixelBurstSpawn
	{
		public Vector2 Position;
		public Vector2 Velocity;
		public Color Color;
		public float Lifetime;
		public float Size;
		public float BlastRadius;
	}
}
