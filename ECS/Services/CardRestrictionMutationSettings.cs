using Crusaders30XX.Diagnostics;

namespace Crusaders30XX.ECS.Services
{
	/// <summary>
	/// Shared timing and layout tunables for card restriction mutation cutscenes.
	/// </summary>
	[DebugTab("Card Mutation Anim")]
	public static class CardRestrictionMutationSettings
	{
		[DebugEditable(DisplayName = "Enter Duration (s)", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public static float EnterDurationSec { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Exit Duration (s)", Step = 0.01f, Min = 0.01f, Max = 5f)]
		public static float ExitDurationSec { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Hold Duration (s)", Step = 0.01f, Min = 0f, Max = 5f)]
		public static float HoldDurationSec { get; set; } = 0.7f;

		[DebugEditable(DisplayName = "Card Scale", Step = 0.01f, Min = 0.01f, Max = 3f)]
		public static float CardScale { get; set; } = 1f;

		[DebugEditable(DisplayName = "Center Offset X", Step = 1, Min = -1000, Max = 1000)]
		public static int CenterOffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Center Offset Y", Step = 1, Min = -1000, Max = 1000)]
		public static int CenterOffsetY { get; set; } = 0;

		[DebugEditable(DisplayName = "Left Exit Offset X", Step = 1, Min = -2000, Max = 0)]
		public static int LeftExitOffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Right Exit Offset X", Step = 1, Min = 0, Max = 2000)]
		public static int RightExitOffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Vertical Center Offset Y", Step = 1, Min = -1000, Max = 1000)]
		public static int VerticalCenterOffsetY { get; set; } = 0;

		[DebugEditable(DisplayName = "Arc Height Enter", Step = 1, Min = -1000, Max = 1000)]
		public static int ArcHeightEnter { get; set; } = 120;

		[DebugEditable(DisplayName = "Arc Height Exit", Step = 1, Min = -1000, Max = 1000)]
		public static int ArcHeightExit { get; set; } = 120;

		[DebugEditable(DisplayName = "EaseOut Pow", Step = 0.1f, Min = 0.1f, Max = 8f)]
		public static float EaseOutPow { get; set; } = 1f;

		[DebugEditable(DisplayName = "EaseIn Pow", Step = 0.1f, Min = 0.1f, Max = 8f)]
		public static float EaseInPow { get; set; } = 1f;

		[DebugEditable(DisplayName = "Pulse Duration (s)", Step = 0.01f, Min = 0.05f, Max = 3f)]
		public static float PulseDurationSeconds { get; set; } = 0.6f;

		[DebugEditable(DisplayName = "Pulse Scale Amplitude", Step = 0.01f, Min = 0f, Max = 1f)]
		public static float PulseScaleAmplitude { get; set; } = 0.2f;

		[DebugEditable(DisplayName = "Jiggle Degrees", Step = 0.5f, Min = 0f, Max = 45f)]
		public static float JiggleDegrees { get; set; } = 5f;

		[DebugEditable(DisplayName = "Pulse Frequency Hz", Step = 0.1f, Min = 0.1f, Max = 10f)]
		public static float PulseFrequencyHz { get; set; } = 1.7f;

		public static void UseInstantDurationsForTests()
		{
			EnterDurationSec = 0.001f;
			ExitDurationSec = 0.001f;
			HoldDurationSec = 0f;
			PulseDurationSeconds = 0.01f;
		}

		public static void ResetToDefaults()
		{
			EnterDurationSec = 0.1f;
			ExitDurationSec = 0.1f;
			HoldDurationSec = 0.7f;
			CardScale = 1f;
			CenterOffsetX = 0;
			CenterOffsetY = 0;
			LeftExitOffsetX = 0;
			RightExitOffsetX = 0;
			VerticalCenterOffsetY = 0;
			ArcHeightEnter = 120;
			ArcHeightExit = 120;
			EaseOutPow = 1f;
			EaseInPow = 1f;
			PulseDurationSeconds = 0.6f;
			PulseScaleAmplitude = 0.2f;
			JiggleDegrees = 5f;
			PulseFrequencyHz = 1.7f;
		}
	}
}
