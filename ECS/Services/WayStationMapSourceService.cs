using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
	public static class WayStationMapSourceService
	{
		public static Rectangle ComputeCenteredCoverSource(
			int textureWidth,
			int textureHeight,
			int targetWidth,
			int targetHeight)
		{
			float targetAspect = targetWidth / (float)System.Math.Max(1, targetHeight);
			float textureAspect = textureWidth / (float)textureHeight;
			int coverWidth = textureWidth;
			int coverHeight = textureHeight;
			if (textureAspect > targetAspect)
			{
				coverWidth = (int)System.Math.Round(textureHeight * targetAspect);
			}
			else
			{
				coverHeight = (int)System.Math.Round(textureWidth / targetAspect);
			}

			int x = (textureWidth - coverWidth) / 2;
			int y = (textureHeight - coverHeight) / 2;
			return new Rectangle(x, y, coverWidth, coverHeight);
		}

		public static Vector2 WorldToScreen(
			float worldX,
			float worldY,
			Rectangle source,
			int targetWidth,
			int targetHeight)
		{
			float screenX = (worldX - source.X) / System.Math.Max(1f, source.Width) * System.Math.Max(1, targetWidth);
			float screenY = (worldY - source.Y) / System.Math.Max(1f, source.Height) * System.Math.Max(1, targetHeight);
			return new Vector2(screenX, screenY);
		}
	}
}
