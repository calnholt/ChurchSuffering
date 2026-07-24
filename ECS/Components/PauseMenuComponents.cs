using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Components
{
	public enum PauseMenuPhase
	{
		Hidden,
		FadingIn,
		Visible,
		FadingOut
	}

	public enum PauseMenuSliderSetting
	{
		None,
		MusicVolume,
		SfxVolume,
		CursorSpeed,
		CursorFastSpeed,
		RumbleLevel
	}

	public class PauseMenuOverlay : IComponent
	{
		public Entity Owner { get; set; }
		public PauseMenuPhase Phase { get; set; } = PauseMenuPhase.Hidden;
		public float Progress01 { get; set; } = 0f;
	}

	public class PauseMenuSlider : IComponent
	{
		public Entity Owner { get; set; }
		public string Label { get; set; } = string.Empty;
		public PauseMenuSliderSetting Setting { get; set; } = PauseMenuSliderSetting.None;
		public int Value { get; set; }
		public int Min { get; set; } = 0;
		public int Max { get; set; } = 100;
		public bool IsDragging { get; set; }
		public Rectangle RowBounds { get; set; }
		public Rectangle TrackBounds { get; set; }
		public Rectangle FillBounds { get; set; }
		public Rectangle KnobBounds { get; set; }
	}
}
