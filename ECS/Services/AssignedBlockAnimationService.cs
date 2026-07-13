using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Services
{
	public static class AssignedBlockAnimationService
	{
		public static float CubicOut(float progress)
		{
			float p = MathHelper.Clamp(progress, 0f, 1f);
			return 1f - (1f - p) * (1f - p) * (1f - p);
		}

		public static Vector2 LerpPosition(Vector2 start, Vector2 target, float progress)
		{
			return Vector2.Lerp(start, target, CubicOut(progress));
		}

		public static float LerpScale(float start, float target, float progress)
		{
			return MathHelper.Lerp(start, target, CubicOut(progress));
		}
	}
}
