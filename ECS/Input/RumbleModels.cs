using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Input
{
	public readonly record struct RumbleMotorState(
		float LowFrequency,
		float HighFrequency,
		float LeftTrigger = 0f,
		float RightTrigger = 0f)
	{
		public static RumbleMotorState Zero => new(0f, 0f, 0f, 0f);

		public RumbleMotorState Clamped() => new(
			MathHelper.Clamp(LowFrequency, 0f, 1f),
			MathHelper.Clamp(HighFrequency, 0f, 1f),
			MathHelper.Clamp(LeftTrigger, 0f, 1f),
			MathHelper.Clamp(RightTrigger, 0f, 1f));

		public RumbleMotorState Scaled(float scale)
		{
			float safeScale = float.IsFinite(scale) ? Math.Max(0f, scale) : 0f;
			return new RumbleMotorState(
				LowFrequency * safeScale,
				HighFrequency * safeScale,
				LeftTrigger * safeScale,
				RightTrigger * safeScale).Clamped();
		}

		public static RumbleMotorState Add(RumbleMotorState left, RumbleMotorState right) => new(
			left.LowFrequency + right.LowFrequency,
			left.HighFrequency + right.HighFrequency,
			left.LeftTrigger + right.LeftTrigger,
			left.RightTrigger + right.RightTrigger);

		public static RumbleMotorState Lerp(RumbleMotorState start, RumbleMotorState end, float amount)
		{
			float t = MathHelper.Clamp(amount, 0f, 1f);
			return new RumbleMotorState(
				MathHelper.Lerp(start.LowFrequency, end.LowFrequency, t),
				MathHelper.Lerp(start.HighFrequency, end.HighFrequency, t),
				MathHelper.Lerp(start.LeftTrigger, end.LeftTrigger, t),
				MathHelper.Lerp(start.RightTrigger, end.RightTrigger, t));
		}
	}

	public readonly record struct RumbleSegment(
		RumbleMotorState Start,
		RumbleMotorState End,
		float DurationSeconds);

	public sealed class RumblePattern
	{
		private readonly RumbleSegment[] _segments;

		public RumblePattern(params RumbleSegment[] segments)
		{
			_segments = segments ?? Array.Empty<RumbleSegment>();
		}

		public IReadOnlyList<RumbleSegment> Segments => _segments;

		public float DurationSeconds
		{
			get
			{
				float duration = 0f;
				foreach (RumbleSegment segment in _segments)
				{
					duration += Math.Max(0f, segment.DurationSeconds);
				}
				return duration;
			}
		}

		public RumbleMotorState Sample(float elapsedSeconds)
		{
			float elapsed = Math.Max(0f, float.IsFinite(elapsedSeconds) ? elapsedSeconds : 0f);
			foreach (RumbleSegment segment in _segments)
			{
				float duration = Math.Max(0f, segment.DurationSeconds);
				if (elapsed <= duration)
				{
					float progress = duration <= 0f ? 1f : elapsed / duration;
					return RumbleMotorState.Lerp(segment.Start, segment.End, progress).Clamped();
				}
				elapsed -= duration;
			}
			return RumbleMotorState.Zero;
		}

		public RumblePattern Scaled(float scale)
		{
			var scaled = new RumbleSegment[_segments.Length];
			for (int i = 0; i < _segments.Length; i++)
			{
				scaled[i] = _segments[i] with
				{
					Start = _segments[i].Start.Scaled(scale),
					End = _segments[i].End.Scaled(scale),
				};
			}
			return new RumblePattern(scaled);
		}
	}
}
