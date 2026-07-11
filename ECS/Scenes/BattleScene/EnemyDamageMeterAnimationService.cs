using System;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Systems
{
	internal sealed class EnemyDamageMeterSegmentAnimation
	{
		public float Value;
		public float Velocity;
		public int Target;
		public float Presence;
		public bool IsActive;
		public float EmphasisElapsedSeconds;
		public float EmphasisDurationSeconds;

		public void Retarget(int target, bool isActive, float emphasisDurationSeconds, bool emphasize)
		{
			target = Math.Max(0, target);
			bool changed = target != Target || isActive != IsActive;
			Target = target;
			IsActive = isActive;

			if (changed && emphasize && emphasisDurationSeconds > 0f)
			{
				EmphasisElapsedSeconds = 0f;
				EmphasisDurationSeconds = emphasisDurationSeconds;
			}
		}

		public void Advance(float elapsedSeconds, float springResponse, float presenceDurationSeconds)
		{
			EnemyDamageMeterAnimationService.AdvanceCriticalSpring(
				ref Value,
				ref Velocity,
				Target,
				elapsedSeconds,
				springResponse);

			float presenceStep = elapsedSeconds / Math.Max(0.001f, presenceDurationSeconds);
			Presence = MathHelper.Clamp(
				Presence + (IsActive ? presenceStep : -presenceStep),
				0f,
				1f);

			if (EmphasisElapsedSeconds < EmphasisDurationSeconds)
			{
				EmphasisElapsedSeconds = Math.Min(
					EmphasisDurationSeconds,
					EmphasisElapsedSeconds + elapsedSeconds);
			}
		}

		public void Emphasize(float durationSeconds)
		{
			if (durationSeconds <= 0f) return;
			EmphasisElapsedSeconds = 0f;
			EmphasisDurationSeconds = durationSeconds;
		}

		public void Reset()
		{
			Value = 0f;
			Velocity = 0f;
			Target = 0;
			Presence = 0f;
			IsActive = false;
			EmphasisElapsedSeconds = 0f;
			EmphasisDurationSeconds = 0f;
		}
	}

	internal static class EnemyDamageMeterAnimationService
	{
		public static void AdvanceCriticalSpring(
			ref float value,
			ref float velocity,
			float target,
			float elapsedSeconds,
			float response)
		{
			if (elapsedSeconds <= 0f) return;

			float omega = Math.Max(0.01f, response);
			float displacement = value - target;
			float coefficient = velocity + omega * displacement;
			float decay = MathF.Exp(-omega * elapsedSeconds);

			value = target + (displacement + coefficient * elapsedSeconds) * decay;
			velocity = (velocity - omega * coefficient * elapsedSeconds) * decay;

			if (Math.Abs(value - target) < 0.01f && Math.Abs(velocity) < 0.01f)
			{
				value = target;
				velocity = 0f;
			}
			else if (value < 0f)
			{
				value = 0f;
				velocity = Math.Max(0f, velocity);
			}
		}

		public static float EasePresence(float presence)
		{
			float t = MathHelper.Clamp(presence, 0f, 1f);
			return t * t * (3f - 2f * t);
		}

		public static float GetEmphasisAmount(EnemyDamageMeterSegmentAnimation animation)
		{
			if (animation.EmphasisDurationSeconds <= 0f ||
				animation.EmphasisElapsedSeconds >= animation.EmphasisDurationSeconds)
			{
				return 0f;
			}

			float progress = MathHelper.Clamp(
				animation.EmphasisElapsedSeconds / animation.EmphasisDurationSeconds,
				0f,
				1f);
			return MathF.Sin(progress * MathF.PI);
		}

		public static int GetDisplayedValue(float animatedValue, int target)
		{
			if (Math.Abs(animatedValue - target) < 0.01f) return target;
			return Math.Max(0, (int)MathF.Round(animatedValue));
		}
	}
}
