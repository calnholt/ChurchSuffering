using System;
using ChurchSuffering.ECS.Components;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Data.VisualEffects
{
	public static class VisualEffectDisplayMath
	{
		public static float SampleElapsed(ActiveVisualEffect effect)
		{
			if (effect == null) return 0f;
			float elapsed = Math.Max(0f, effect.ElapsedSeconds);
			if (!effect.Recipe.HasModule(VisualEffectModule.HitStop)) return elapsed;

			float start = Math.Max(0f, effect.Timing.HitStopStartSeconds);
			float duration = Math.Max(0f, effect.Timing.HitStopDurationSeconds);
			if (duration <= 0f || elapsed <= start) return elapsed;
			if (elapsed <= start + duration) return start;
			return elapsed - duration;
		}

		public static float Progress(ActiveVisualEffect effect)
		{
			float duration = Math.Max(0.0001f, effect?.Timing.DurationSeconds ?? 0f);
			return MathHelper.Clamp(SampleElapsed(effect) / duration, 0f, 1f);
		}

		public static float ImpactProgress(ActiveVisualEffect effect)
		{
			if (effect == null) return 0f;
			float elapsed = SampleElapsed(effect);
			float impact = effect.Timing.ImpactTimeSeconds;
			float duration = Math.Max(0.0001f, effect.Timing.DurationSeconds - impact);
			return MathHelper.Clamp((elapsed - impact) / duration, 0f, 1f);
		}

		public static float ContactProgress(ActiveVisualEffect effect)
		{
			if (effect == null) return 0f;
			float duration = Math.Max(0.0001f, effect.Timing.DurationSeconds);
			float contactSeconds = effect.Timing.HitStopDurationSeconds > 0f
				? effect.Timing.HitStopStartSeconds
				: effect.Timing.ImpactTimeSeconds;
			return MathHelper.Clamp(contactSeconds / duration, 0f, 1f);
		}

		public static float ApproachProgress(ActiveVisualEffect effect)
		{
			float contact = Math.Max(0.0001f, ContactProgress(effect));
			return MathHelper.Clamp(Progress(effect) / contact, 0f, 1f);
		}

		public static float RecoveryProgress(ActiveVisualEffect effect)
		{
			float contact = ContactProgress(effect);
			return MathHelper.Clamp((Progress(effect) - contact) / Math.Max(0.0001f, 1f - contact), 0f, 1f);
		}

		public static float Window(float progress, float start, float peakStart, float peakEnd, float end)
		{
			progress = MathHelper.Clamp(progress, 0f, 1f);
			if (progress < start || progress > end) return 0f;
			if (progress >= peakStart && progress <= peakEnd) return 1f;
			if (progress < peakStart)
			{
				float span = Math.Max(0.0001f, peakStart - start);
				return MathHelper.Clamp((progress - start) / span, 0f, 1f);
			}

			float outSpan = Math.Max(0.0001f, end - peakEnd);
			return 1f - MathHelper.Clamp((progress - peakEnd) / outSpan, 0f, 1f);
		}

		public static float EaseOutCubic(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return 1f - MathF.Pow(1f - t, 3f);
		}

		public static float EaseOutBack(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			const float c1 = 1.70158f;
			const float c3 = c1 + 1f;
			return 1f + c3 * MathF.Pow(t - 1f, 3f) + c1 * MathF.Pow(t - 1f, 2f);
		}

		public static float EaseInOutQuad(float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			return t < 0.5f ? 2f * t * t : 1f - MathF.Pow(-2f * t + 2f, 2f) * 0.5f;
		}

		public static Vector2 DirectionToTarget(ActiveVisualEffect effect)
		{
			if (effect == null) return Vector2.UnitX;
			var dir = effect.TargetAnchor - effect.SourceAnchor;
			if (dir.LengthSquared() > 0.0001f)
			{
				dir.Normalize();
				return dir;
			}
			return new Vector2(effect.DirectionSign == 0 ? 1 : effect.DirectionSign, 0f);
		}
	}
}
