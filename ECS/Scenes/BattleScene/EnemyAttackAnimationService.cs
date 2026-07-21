using System;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
	internal readonly record struct EnemyAttackEntranceSample(
		float PanelScaleX,
		float PanelScaleY,
		float OrnamentProgress,
		float SkullScale,
		float SkullTint,
		float TextAlpha,
		float TextOffsetY,
		float FlashAlpha,
		float RingOneProgress,
		float RingTwoProgress);

	internal readonly record struct EnemyAttackOutlineEchoSample(
		bool IsActive,
		float ExpansionProgress,
		float Alpha);

	internal static class EnemyAttackAnimationService
	{
		public const float ImpactMomentSeconds = 0.08f;
		public const float EntranceCompleteSeconds = 0.36f;
		public const float PresentationCompleteSeconds = 1.1f;

		public static float ComputeImpactIntensity(int damage)
		{
			return 0.25f + 0.75f * MathHelper.Clamp(Math.Max(0, damage) / 20f, 0f, 1f);
		}

		public static EnemyAttackEntranceSample ComputeEntrance(float elapsed, float intensity)
		{
			float safeElapsed = Math.Max(0f, elapsed);
			float safeIntensity = MathHelper.Clamp(intensity, 0f, 1f);
			float skullIn = EaseOutBack(WindowProgress(safeElapsed, 0f, 0.11f), 1.35f);
			float ornament = EaseOutCubic(WindowProgress(safeElapsed, 0f, 0.18f));
			float open = EaseOutBack(WindowProgress(safeElapsed, 0.06f, 0.18f), 0.7f + safeIntensity * 0.35f);
			float settle = EaseOutCubic(WindowProgress(safeElapsed, 0.18f, EntranceCompleteSeconds));
			float panelX = safeElapsed < 0.06f
				? MathHelper.Lerp(0.15f, 0.22f, ornament)
				: MathHelper.Lerp(0.15f, 1f, open);
			panelX = MathHelper.Lerp(panelX, 1f, settle);
			float panelY = MathHelper.Lerp(0.85f, 1f, EaseOutCubic(WindowProgress(safeElapsed, 0.06f, 0.22f)));
			float textProgress = EaseOutCubic(WindowProgress(safeElapsed, 0.16f, 0.30f));
			float flash = 1f - WindowProgress(safeElapsed, ImpactMomentSeconds, 0.16f);
			if (safeElapsed < ImpactMomentSeconds) flash = 0f;

			return new EnemyAttackEntranceSample(
				Math.Max(0.01f, panelX),
				Math.Max(0.01f, panelY),
				ornament,
				MathHelper.Lerp(0.65f, 1f, skullIn),
				1f - WindowProgress(safeElapsed, ImpactMomentSeconds, 0.24f),
				textProgress,
				MathHelper.Lerp(10f, 0f, textProgress),
				MathHelper.Clamp(flash, 0f, 1f),
				WindowProgress(safeElapsed, ImpactMomentSeconds, 0.45f),
				WindowProgress(safeElapsed, 0.13f, 0.55f));
		}

		public static Vector2 ComputeDeterministicRecoil(float elapsed, float duration, float amplitude)
		{
			if (duration <= 0f || elapsed < ImpactMomentSeconds || elapsed >= ImpactMomentSeconds + duration || amplitude <= 0f)
				return Vector2.Zero;

			float t = MathHelper.Clamp((elapsed - ImpactMomentSeconds) / duration, 0f, 1f);
			float envelope = (1f - t) * (1f - t);
			float x = MathF.Sin(t * MathHelper.TwoPi * 3.25f) * amplitude * envelope;
			float y = MathF.Sin(t * MathHelper.TwoPi * 4.5f + 0.7f) * amplitude * 0.3f * envelope;
			return new Vector2(x, y);
		}

		public static float ComputeIdlePulse(float elapsed)
		{
			return 1f + MathF.Sin(Math.Max(0f, elapsed) * MathHelper.TwoPi * 1.5f) * 0.015f;
		}

		public static EnemyAttackOutlineEchoSample ComputeOutlineEcho(float elapsed, float interval, float duration)
		{
			float safeElapsed = Math.Max(0f, elapsed);
			float safeInterval = Math.Max(0.01f, interval);
			float safeDuration = Math.Min(Math.Max(0.01f, duration), safeInterval);
			if (safeElapsed < safeInterval)
				return new EnemyAttackOutlineEchoSample(false, 0f, 0f);

			float cycleElapsed = (safeElapsed - safeInterval) % safeInterval;
			if (cycleElapsed >= safeDuration)
				return new EnemyAttackOutlineEchoSample(false, 0f, 0f);

			float progress = MathHelper.Clamp(cycleElapsed / safeDuration, 0f, 1f);
			return new EnemyAttackOutlineEchoSample(
				true,
				EaseOutCubic(progress),
				1f - progress);
		}

		private static float WindowProgress(float elapsed, float start, float end)
		{
			if (end <= start) return elapsed >= end ? 1f : 0f;
			return MathHelper.Clamp((elapsed - start) / (end - start), 0f, 1f);
		}

		private static float EaseOutCubic(float t) => 1f - MathF.Pow(1f - MathHelper.Clamp(t, 0f, 1f), 3f);

		private static float EaseOutBack(float t, float overshoot)
		{
			float x = MathHelper.Clamp(t, 0f, 1f) - 1f;
			return 1f + (overshoot + 1f) * x * x * x + overshoot * x * x;
		}

		/// <summary>
		/// Computes the absorb tween (panel shrinking toward enemy) during EnemyAttack phase.
		/// Returns the interpolated position and remaining panel scale (1 -> 0).
		/// </summary>
		public static (float panelScale, Vector2 approachPos) ComputeAbsorbTween(
			Vector2 center, Vector2 enemyPos, int yOffset, float elapsed, float duration)
		{
			var dur = Math.Max(0.05f, duration);
			float tTween = MathHelper.Clamp(elapsed / dur, 0f, 1f);
			float ease = 1f - (float)Math.Pow(1f - tTween, 3);
			var targetPos = enemyPos + new Vector2(0, yOffset);
			var approachPos = Vector2.Lerp(center, targetPos, ease);
			float panelScale = MathHelper.Lerp(1f, 0f, ease);
			return (panelScale, approachPos);
		}

		/// <summary>
		/// Computes the impact squash/stretch animation returning squash factors and content scale.
		/// </summary>
		public static (float squashX, float squashY, float contentScale) ComputeImpactSquash(
			float elapsed, float duration, float squashXFactor, float squashYFactor, float overshoot)
		{
			float t = Math.Clamp(elapsed / Math.Max(0.0001f, duration), 0f, 1f);
			float back = 1f + overshoot * (float)Math.Pow(1f - t, 3);
			float squashX = MathHelper.Lerp(squashXFactor, 1f, t) * back;
			float squashY = MathHelper.Lerp(squashYFactor, 1f, t) / back;
			float contentScale = Math.Min(squashX, squashY);
			return (squashX, squashY, contentScale);
		}

		/// <summary>
		/// Computes the shake offset during impact.
		/// </summary>
		public static Vector2 ComputeShake(float elapsed, float duration, int amplitude, Random rand)
		{
			if (elapsed >= duration || amplitude <= 0)
				return Vector2.Zero;

			float shakeT = 1f - Math.Clamp(elapsed / Math.Max(0.0001f, duration), 0f, 1f);
			int sx = rand.Next(-amplitude, amplitude + 1);
			int sy = rand.Next(-amplitude, amplitude + 1);
			return new Vector2(sx, sy) * shakeT;
		}

		/// <summary>
		/// Measures the panel size given font, lines, padding, and width constraints.
		/// Returns (width, height) in pixels.
		/// </summary>
		public static (int width, int height) MeasurePanelSize(
			Microsoft.Xna.Framework.Graphics.SpriteFont font,
			System.Collections.Generic.List<(string text, float scale)> lines,
			int padding, int maxWidth, int minWidth, int contentLimit,
			float titleSpacing, float lineSpacing)
		{
			float maxW = 0f;
			float totalH = 0f;
			bool isFirstTitle = true;
			foreach (var (text, lineScale) in lines)
			{
				var parts = ChurchSuffering.ECS.Utils.TextUtils.WrapText(font, text, lineScale, contentLimit);
				foreach (var p in parts)
				{
					var sz = font.MeasureString(p);
					maxW = Math.Max(maxW, sz.X * lineScale);
					float spacing = isFirstTitle ? titleSpacing : lineSpacing;
					totalH += sz.Y * lineScale + spacing;
					if (isFirstTitle) isFirstTitle = false;
				}
			}

			int w = (int)Math.Ceiling(Math.Min(maxW + padding * 2, maxWidth));
			w = Math.Max(w, minWidth);
			int h = (int)Math.Ceiling(totalH) + padding * 2;
			return (w, h);
		}
	}
}
