using System;
using Crusaders30XX.ECS.Components;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Scenes.BattleScene
{
	internal readonly record struct DialogBodyLayoutKey(
		string Text,
		float Scale,
		int MaxWidth,
		int StartX,
		int StartY);

	internal readonly record struct DialogAnimationPresentation(
		float RailProgress,
		float RailAccentProgress,
		float PortraitOpacity,
		float StageOpacity,
		float StageTranslateX,
		float BottomBarProgress,
		float SpeakerDashProgress,
		float SkipButtonOpacity,
		float SkipButtonSlideY);

	internal static class DialogPresentationMath
	{
		public static DialogAnimationPresentation CalculateAnimation(
			DialogPhase phase,
			float elapsedSeconds,
			float outroDurationSeconds)
		{
			float elapsed = Math.Max(0f, elapsedSeconds);
			float outroDuration = Math.Max(0.001f, outroDurationSeconds);

			return new DialogAnimationPresentation(
				RailProgress: phase switch
				{
					DialogPhase.Active => 1f,
					DialogPhase.Intro => EaseOut(Clamp01(elapsed / 0.6f)),
					DialogPhase.Outro => 1f - EaseIn(Clamp01(elapsed / (outroDuration * (0.5f / 0.52f)))),
					_ => 0f,
				},
				RailAccentProgress: phase switch
				{
					DialogPhase.Active => 1f,
					DialogPhase.Intro => EaseOut(Clamp01((elapsed - 0.25f) / 0.55f)),
					DialogPhase.Outro => 1f - EaseIn(Clamp01(elapsed / 0.4f)),
					_ => 0f,
				},
				PortraitOpacity: phase switch
				{
					DialogPhase.Active => 1f,
					DialogPhase.Intro => EaseOut(Clamp01((elapsed - 0.2f) / 0.35f)),
					DialogPhase.Outro => 1f - EaseIn(Clamp01(elapsed / 0.35f)),
					_ => 0f,
				},
				StageOpacity: phase switch
				{
					DialogPhase.Active => 1f,
					DialogPhase.Intro => EaseOut(Clamp01((elapsed - 0.22f) / 0.5f)),
					DialogPhase.Outro => 1f - EaseOut(Clamp01(elapsed / 0.35f)),
					_ => 0f,
				},
				StageTranslateX: phase switch
				{
					DialogPhase.Active => 0f,
					DialogPhase.Intro => (1f - Clamp01((elapsed - 0.22f) / 0.5f)) * 40f,
					DialogPhase.Outro => Clamp01(elapsed / 0.35f) * 30f,
					_ => 40f,
				},
				BottomBarProgress: phase switch
				{
					DialogPhase.Active => 1f,
					DialogPhase.Intro => EaseOut(Clamp01((elapsed - 0.35f) / 0.55f)),
					DialogPhase.Outro => 1f - EaseOut(Clamp01(elapsed / 0.3f)),
					_ => 0f,
				},
				SpeakerDashProgress: phase switch
				{
					DialogPhase.Active => 1f,
					DialogPhase.Intro => EaseOut(Clamp01((elapsed - 0.22f) / 0.35f)),
					_ => 0f,
				},
				SkipButtonOpacity: phase switch
				{
					DialogPhase.Active => 1f,
					DialogPhase.Intro => EaseOut(Clamp01(elapsed / 0.35f)),
					_ => 0f,
				},
				SkipButtonSlideY: phase switch
				{
					DialogPhase.Active => 0f,
					DialogPhase.Intro => (1f - Clamp01(elapsed / 0.35f)) * -12f,
					_ => -12f,
				});
		}

		public static Vector2 TranslateStagePosition(
			Vector2 basePosition,
			Rectangle stage,
			int stageDrawX,
			int stageDrawY)
		{
			return basePosition + new Vector2(stageDrawX - stage.X, stageDrawY - stage.Y);
		}

		private static float Clamp01(float value) => Math.Clamp(value, 0f, 1f);
		private static float EaseOut(float value) => value >= 1f ? 1f : 1f - MathF.Pow(1f - value, 3f);
		private static float EaseIn(float value) => value >= 1f ? 1f : MathF.Pow(value, 3f);
	}
}
