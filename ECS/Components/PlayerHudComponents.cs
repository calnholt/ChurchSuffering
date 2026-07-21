using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Components
{
	public enum PlayerHudRegionType
	{
		Root,
		Health,
		Courage,
		Temperance,
		ActionPoint,
		Pledge,
	}

	/// <summary>
	/// Stable battle HUD anchor and shared visual settings published by PlayerHudLayoutSystem.
	/// Bounds are local to the HUD root and include the permanently reserved pledge region.
	/// </summary>
	public class PlayerHudAnchor : IComponent
	{
		public Entity Owner { get; set; }
		public Rectangle Bounds { get; set; }
		public Rectangle StablePortraitBounds { get; set; }
		public Rectangle HealthRowBounds { get; set; }
		public Rectangle ResourceRowBounds { get; set; }

		public int ChipHeight { get; set; } = 44;
		public int Slant { get; set; } = 14;
		public int RegionOverlap { get; set; } = 13;
		public int HpWidthExtension { get; set; } = 6;
		public int PledgeIconSize { get; set; } = 36;
		public int ContentGap { get; set; } = 8;
		public int LabelLetterSpacing { get; set; } = 3;
		public int HealthPaddingLeft { get; set; } = 14;
		public int HealthPaddingRight { get; set; } = 18;
		public int HealthPaddingVertical { get; set; } = 8;
		public int HealthTrackHeight { get; set; } = 26;
		public int HealthTrackBorderThickness { get; set; } = 2;
		public int CouragePaddingLeft { get; set; } = 14;
		public int CouragePaddingRight { get; set; } = 18;
		public int TemperancePaddingLeft { get; set; } = 14;
		public int TemperancePaddingRight { get; set; } = 16;
		public int ActionPointPaddingLeft { get; set; } = 14;
		public int ActionPointPaddingRight { get; set; } = 20;
		public int PledgePaddingLeft { get; set; } = 12;
		public int PledgePaddingRight { get; set; } = 18;
		public int PledgePaddingVertical { get; set; } = 4;
		public int PledgeContentGap { get; set; } = 10;
		public int TemperanceChunkWidth { get; set; } = 16;
		public int TemperanceChunkHeight { get; set; } = 22;
		public int TemperanceChunkGap { get; set; } = 2;

		public Color HudRed { get; set; } = new Color(196, 30, 58);
		public Color HudBlack { get; set; } = new Color(10, 10, 10);
		public Color HudWhite { get; set; } = Color.White;
		public float LabelFontScale { get; set; } = 0.13f;
		public float ValueFontScale { get; set; } = 0.20f;
		public int CourageInsetShadowHeight { get; set; } = 4;
		public byte CourageInsetShadowAlpha { get; set; } = 64;
		public int ActionPointGlowRadius { get; set; } = 10;
		public byte ActionPointGlowAlpha { get; set; } = 115;

		public int ShadowOffsetY { get; set; } = 6;
		public int ShadowBlurRadius { get; set; } = 20;
		public byte ShadowAlpha { get; set; } = 140;
		public float ResourcePulseDurationSeconds { get; set; } = 0.30f;
		public float ResourcePulseMaxScale { get; set; } = 1.12f;
	}

	/// <summary>
	/// Identifies one layout-owned HUD region. Renderers may control IsVisible but not bounds.
	/// </summary>
	public class PlayerHudRegion : IComponent
	{
		public Entity Owner { get; set; }
		public PlayerHudRegionType Type { get; set; }
		public Rectangle Bounds { get; set; }
		public bool IsVisible { get; set; } = true;
	}

	/// <summary>
	/// Renderer-consumed resource pulse state. It deliberately contains no transform data.
	/// </summary>
	public class PlayerHudFeedbackState : IComponent
	{
		public Entity Owner { get; set; }
		public bool IsPulsing { get; set; }
		public float ElapsedSeconds { get; set; }
		public float DurationSeconds { get; set; }
		public float Scale { get; set; } = 1f;
	}
}
