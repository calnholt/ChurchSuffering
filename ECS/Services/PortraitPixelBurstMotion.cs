using System;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
	public static class PortraitPixelBurstMotion
	{
		public static float ResolveFlightDelta(float previousBurstAge, float currentBurstAge, float buildupDuration)
		{
			float releaseTime = Math.Max(0f, buildupDuration);
			float previousFlightAge = Math.Max(0f, previousBurstAge - releaseTime);
			float currentFlightAge = Math.Max(0f, currentBurstAge - releaseTime);
			return Math.Max(0f, currentFlightAge - previousFlightAge);
		}

		public static float ResolveFlightAge(float burstAge, float buildupDuration)
		{
			return Math.Max(0f, burstAge - Math.Max(0f, buildupDuration));
		}

		public static Vector2 ComputeJitterOffset(
			float elapsedSeconds,
			float buildupDuration,
			float maxOffset,
			float rampPower,
			float phaseX,
			float phaseY,
			float frequencyX,
			float frequencyY)
		{
			if (elapsedSeconds <= 0f || buildupDuration <= 0f || maxOffset <= 0f)
			{
				return Vector2.Zero;
			}

			float progress = MathHelper.Clamp(elapsedSeconds / buildupDuration, 0f, 1f);
			float amplitude = maxOffset * MathF.Pow(progress, Math.Max(0.01f, rampPower));
			float x = MathF.Sin(phaseX + MathHelper.TwoPi * Math.Max(0f, frequencyX) * elapsedSeconds);
			float y = MathF.Sin(phaseY + MathHelper.TwoPi * Math.Max(0f, frequencyY) * elapsedSeconds);
			var offset = new Vector2(x, y);
			float lengthSquared = offset.LengthSquared();
			if (lengthSquared > 1f)
			{
				offset /= MathF.Sqrt(lengthSquared);
			}

			return offset * amplitude;
		}
	}
}
