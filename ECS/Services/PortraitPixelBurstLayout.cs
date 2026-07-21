using ChurchSuffering.ECS.Components;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
	/// <summary>
	/// Pure layout math for portrait pixel bursts — matches SpriteBatch center-origin draw
	/// (component-wise origin * drawScale).
	/// </summary>
	public static class PortraitPixelBurstLayout
	{
		public static Vector2 ComputeTextureOrigin(int textureWidth, int textureHeight)
		{
			return new Vector2(textureWidth / 2f, textureHeight / 2f);
		}

		public static Vector2 ComputeTopLeft(Vector2 drawCenter, int textureWidth, int textureHeight, Vector2 drawScale)
		{
			var origin = ComputeTextureOrigin(textureWidth, textureHeight);
			return drawCenter - origin * drawScale;
		}

		public static Vector2 ComputeTopLeft(Vector2 drawCenter, int textureWidth, int textureHeight, float uniformScale)
		{
			return ComputeTopLeft(drawCenter, textureWidth, textureHeight, new Vector2(uniformScale, uniformScale));
		}

		public static Vector2 TexturePixelToWorld(Vector2 topLeft, int texX, int texY, Vector2 drawScale)
		{
			return topLeft + new Vector2(texX * drawScale.X, texY * drawScale.Y);
		}

		public static Vector2 TexturePixelToWorld(Vector2 topLeft, int texX, int texY, float uniformScale)
		{
			return TexturePixelToWorld(topLeft, texX, texY, new Vector2(uniformScale, uniformScale));
		}

		public static Vector2 ComputeWorldPosition(
			Vector2 drawCenter,
			int textureWidth,
			int textureHeight,
			int texX,
			int texY,
			Vector2 drawScale)
		{
			var topLeft = ComputeTopLeft(drawCenter, textureWidth, textureHeight, drawScale);
			return TexturePixelToWorld(topLeft, texX, texY, drawScale);
		}

		public static Vector2 ComputeWorldPosition(
			Vector2 drawCenter,
			int textureWidth,
			int textureHeight,
			int texX,
			int texY,
			float uniformScale)
		{
			return ComputeWorldPosition(drawCenter, textureWidth, textureHeight, texX, texY, new Vector2(uniformScale, uniformScale));
		}

		/// <summary>
		/// Picks burst layout from the last portrait draw when available.
		/// </summary>
		public static (Vector2 center, Vector2 topLeft, Vector2 drawScale) ResolveDrawFrame(
			PortraitInfo portraitInfo,
			Vector2 transformPosition,
			int textureWidth,
			int textureHeight,
			int viewportHeight)
		{
			if (HasValidLastDraw(portraitInfo))
			{
				return (portraitInfo.LastDrawCenter, portraitInfo.LastDrawTopLeft, portraitInfo.LastDrawScale);
			}

			float uniform = ResolveUniformScale(portraitInfo, textureHeight, viewportHeight);
			var drawScale = new Vector2(uniform, uniform);
			var topLeft = ComputeTopLeft(transformPosition, textureWidth, textureHeight, drawScale);
			return (transformPosition, topLeft, drawScale);
		}

		public static bool HasValidLastDraw(PortraitInfo portraitInfo)
		{
			return portraitInfo != null
				&& portraitInfo.TextureWidth > 0
				&& portraitInfo.LastDrawScale.X > 0f
				&& portraitInfo.LastDrawScale.Y > 0f;
		}

		public static float ResolveUniformScale(
			PortraitInfo portraitInfo,
			int textureHeight,
			int viewportHeight,
			float screenHeightCoverage = 0.30f)
		{
			if (portraitInfo != null && portraitInfo.CurrentScale > 0f && portraitInfo.TextureWidth > 0)
			{
				return portraitInfo.CurrentScale;
			}

			float desiredHeight = screenHeightCoverage * viewportHeight;
			return desiredHeight / System.Math.Max(1, textureHeight);
		}

		public static Vector2 ClampTravelFromSpawn(Vector2 position, Vector2 spawnPosition, float maxTravel)
		{
			var offset = position - spawnPosition;
			float limit = System.Math.Max(1f, maxTravel);
			float dist = offset.Length();
			if (dist > limit && dist > 0.0001f)
			{
				return spawnPosition + offset / dist * limit;
			}

			return position;
		}
	}
}
