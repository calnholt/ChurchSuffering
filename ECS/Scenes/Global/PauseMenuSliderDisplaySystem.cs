using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	[DebugTab("Pause Sliders")]
	public class PauseMenuSliderDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ChakraPetchFont;
		private readonly Texture2D _pixel;

		private static readonly Color LabelColor = new Color(200, 192, 184);
		private static readonly Color ValueColor = new Color(238, 56, 86);
		private static readonly Color TrackShadowColor = Color.Black;
		private static readonly Color TrackFrameColor = new Color(12, 7, 9);
		private static readonly Color TrackBorderColor = new Color(126, 86, 96);
		private static readonly Color TrackColor = new Color(255, 255, 255);
		private static readonly Color TrackTickColor = new Color(218, 202, 198);
		private static readonly Color FillGlowColor = new Color(214, 31, 65);
		private static readonly Color FillStartColor = new Color(126, 8, 31);
		private static readonly Color FillEndColor = new Color(246, 49, 82);
		private static readonly Color FillHighlightColor = new Color(255, 159, 171);
		private static readonly Color KnobFrameColor = new Color(18, 5, 10);
		private static readonly Color KnobFillColor = new Color(240, 236, 230);
		private static readonly Color KnobCoreColor = new Color(255, 77, 98);
		private static readonly Color KnobBorderColor = new Color(196, 30, 58);
		private static readonly Color KnobGlowColor = new Color(225, 35, 70);

		[DebugEditable(DisplayName = "Label Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float LabelScale { get; set; } = 0.09f;

		[DebugEditable(DisplayName = "Value Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float ValueScale { get; set; } = 0.17f;

		[DebugEditable(DisplayName = "Header To Track Gap", Step = 1, Min = 0, Max = 80)]
		public int HeaderToTrackGap { get; set; } = 14;

		[DebugEditable(DisplayName = "Track Frame Height", Step = 1, Min = 3, Max = 40)]
		public int TrackFrameHeight { get; set; } = 11;

		[DebugEditable(DisplayName = "Track Border", Step = 1, Min = 0, Max = 8)]
		public int TrackBorder { get; set; } = 1;

		[DebugEditable(DisplayName = "Track Tick Height", Step = 1, Min = 1, Max = 40)]
		public int TrackTickHeight { get; set; } = 13;

		[DebugEditable(DisplayName = "Track Tick Width", Step = 1, Min = 1, Max = 8)]
		public int TrackTickWidth { get; set; } = 1;

		[DebugEditable(DisplayName = "Fill Glow Height", Step = 1, Min = 1, Max = 40)]
		public int FillGlowHeight { get; set; } = 13;

		[DebugEditable(DisplayName = "Knob Size", Step = 1, Min = 4, Max = 80)]
		public int KnobSize { get; set; } = 30;

		[DebugEditable(DisplayName = "Knob Border", Step = 1, Min = 0, Max = 20)]
		public int KnobBorder { get; set; } = 3;

		[DebugEditable(DisplayName = "Hover Knob Scale", Step = 0.01f, Min = 1f, Max = 2f)]
		public float HoverKnobScale { get; set; } = 1.08f;

		[DebugEditable(DisplayName = "Glow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float GlowAlpha { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Glow Size Add", Step = 1, Min = 0, Max = 80)]
		public int GlowSizeAdd { get; set; } = 18;

		public PauseMenuSliderDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<PauseMenuSlider>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var slider = entity.GetComponent<PauseMenuSlider>();
			var ui = entity.GetComponent<UIElement>();
			if (slider == null || ui == null || ui.IsHidden || !ui.IsInteractable)
			{
				if (slider != null) slider.IsDragging = false;
				return;
			}

			PlayerInputFrame input = PlayerInputService.GetFrame(EntityManager);
			if (ui.IsHovered && input.WasPressed(PlayerButton.Primary))
			{
				slider.IsDragging = true;
				if (UpdateSliderValue(slider, input.PointerPosition.X))
				{
					PersistSliderValue(slider);
				}
			}

			if (slider.IsDragging)
			{
				if (input.IsDown(PlayerButton.Primary))
				{
					if (UpdateSliderValue(slider, input.PointerPosition.X))
					{
						PersistSliderValue(slider);
					}
				}
				else
				{
					slider.IsDragging = false;
				}
			}

			SyncComputedBounds(slider);
		}

		public void Draw()
		{
			foreach (var entity in GetRelevantEntities().OrderBy(e => e.GetComponent<Transform>()?.ZOrder ?? 0))
			{
				var slider = entity.GetComponent<PauseMenuSlider>();
				var ui = entity.GetComponent<UIElement>();
				if (slider == null || ui == null || ui.IsHidden || slider.RowBounds.Width <= 0) continue;
				DrawSlider(slider, ui);
			}
		}

		private void DrawSlider(PauseMenuSlider slider, UIElement ui)
		{
			var row = slider.RowBounds;
			string label = slider.Label ?? string.Empty;
			string value = FormatSliderValue(slider);

			var labelPos = new Vector2(row.X, row.Y);
			_spriteBatch.DrawString(_font, label, labelPos, LabelColor, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);

			Vector2 valueSize = _font.MeasureString(value) * ValueScale;
			var valuePos = new Vector2(row.Right - valueSize.X, row.Y - 6);
			_spriteBatch.DrawString(_font, value, valuePos, ValueColor, 0f, Vector2.Zero, ValueScale, SpriteEffects.None, 0f);

			var track = slider.TrackBounds;
			DrawTrack(track);
			DrawFill(slider.FillBounds);

			bool active = ui.IsHovered || slider.IsDragging;
			float scale = active ? HoverKnobScale : 1f;
			DrawKnob(slider.KnobBounds, scale);
		}

		private static string FormatSliderValue(PauseMenuSlider slider)
		{
			if (slider.Setting is PauseMenuSliderSetting.CursorSpeed or PauseMenuSliderSetting.CursorFastSpeed)
			{
				return slider.Value > 0 ? $"+{slider.Value}%" : $"{slider.Value}%";
			}

			return $"{slider.Value}%";
		}

		private void DrawTrack(Rectangle track)
		{
			if (track.Width <= 0 || track.Height <= 0) return;

			int centerY = track.Center.Y;
			int frameHeight = Math.Max(track.Height, TrackFrameHeight);
			var shadow = CenteredHorizontalBand(track.X - 3, track.Width + 6, centerY + 2, frameHeight);
			var frame = CenteredHorizontalBand(track.X - 2, track.Width + 4, centerY, frameHeight);

			_spriteBatch.Draw(_pixel, shadow, TrackShadowColor * 0.7f);
			_spriteBatch.Draw(_pixel, frame, TrackFrameColor * 0.98f);
			DrawBorder(frame, TrackBorderColor * 0.42f, TrackBorder);
			_spriteBatch.Draw(_pixel, track, TrackColor * 0.08f);

			int tickHeight = Math.Max(1, TrackTickHeight);
			int tickWidth = Math.Max(1, TrackTickWidth);
			for (int i = 0; i <= 4; i++)
			{
				int x = track.X + (int)MathF.Round(track.Width * (i / 4f));
				var tick = new Rectangle(x - tickWidth / 2, centerY - tickHeight / 2, tickWidth, tickHeight);
				_spriteBatch.Draw(_pixel, tick, TrackTickColor * 0.18f);
			}
		}

		private void DrawFill(Rectangle fill)
		{
			if (fill.Width <= 0 || fill.Height <= 0) return;

			int centerY = fill.Center.Y;
			int glowHeight = Math.Max(fill.Height, FillGlowHeight);
			var glow = CenteredHorizontalBand(fill.X, fill.Width, centerY, glowHeight);
			_spriteBatch.Draw(_pixel, glow, FillGlowColor * 0.18f);
			DrawHorizontalGradient(fill, FillStartColor, FillEndColor, 24);
			_spriteBatch.Draw(
				_pixel,
				new Rectangle(fill.X, fill.Y, fill.Width, 1),
				FillHighlightColor * 0.72f);
		}

		private void DrawKnob(Rectangle knob, float scale)
		{
			var center = new Vector2(knob.X + knob.Width / 2f, knob.Y + knob.Height / 2f);
			int knobSize = Math.Max(4, KnobSize);
			int border = Math.Max(0, KnobBorder);
			int innerSize = Math.Max(3, knobSize - border * 2);
			int coreSize = Math.Max(3, innerSize / 3);

			if (GlowAlpha > 0f)
			{
				int broadGlowSize = knobSize + Math.Max(0, GlowSizeAdd);
				int closeGlowSize = knobSize + Math.Max(0, GlowSizeAdd / 2);
				DrawDiamond(center, broadGlowSize, KnobGlowColor * (GlowAlpha * 0.12f), scale);
				DrawDiamond(center, closeGlowSize, KnobGlowColor * (GlowAlpha * 0.24f), scale);
			}

			DrawDiamond(center, knobSize + 4, KnobFrameColor, scale);
			DrawDiamond(center, knobSize, KnobBorderColor, scale);
			DrawDiamond(center, innerSize, KnobFillColor, scale);
			DrawDiamond(center, coreSize, KnobCoreColor, scale);
		}

		private void DrawDiamond(Vector2 center, int size, Color color, float scale)
		{
			Texture2D diamond = PrimitiveTextureFactory.GetDiamondTexture(_graphicsDevice, Math.Max(1, size));
			_spriteBatch.Draw(
				diamond,
				center,
				null,
				color,
				0f,
				new Vector2(diamond.Width / 2f, diamond.Height / 2f),
				scale,
				SpriteEffects.None,
				0f);
		}

		private static Rectangle CenteredHorizontalBand(int x, int width, int centerY, int height)
		{
			width = Math.Max(1, width);
			height = Math.Max(1, height);
			return new Rectangle(x, centerY - height / 2, width, height);
		}

		private void DrawBorder(Rectangle rect, Color color, int thickness)
		{
			if (thickness <= 0 || rect.Width <= 0 || rect.Height <= 0) return;
			thickness = Math.Min(thickness, Math.Min(rect.Width, rect.Height));
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		private void DrawHorizontalGradient(Rectangle rect, Color left, Color right, int steps)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			steps = Math.Max(1, Math.Min(steps, rect.Width));
			float stepW = rect.Width / (float)steps;
			for (int i = 0; i < steps; i++)
			{
				float t = steps <= 1 ? 1f : i / (float)(steps - 1);
				int x = rect.X + (int)MathF.Round(i * stepW);
				int nextX = i == steps - 1
					? rect.Right
					: rect.X + (int)MathF.Round((i + 1) * stepW);
				int width = Math.Max(1, nextX - x);
				_spriteBatch.Draw(_pixel, new Rectangle(x, rect.Y, width, rect.Height), Color.Lerp(left, right, t));
			}
		}

		private static Rectangle CalculateFillBounds(PauseMenuSlider slider)
		{
			float normalized = CalculateNormalized(slider);
			return new Rectangle(
				slider.TrackBounds.X,
				slider.TrackBounds.Y,
				(int)MathF.Round(slider.TrackBounds.Width * normalized),
				slider.TrackBounds.Height);
		}

		private Rectangle CalculateKnobBounds(PauseMenuSlider slider)
		{
			float normalized = CalculateNormalized(slider);
			int centerX = slider.TrackBounds.X + (int)MathF.Round(slider.TrackBounds.Width * normalized);
			int centerY = slider.TrackBounds.Y + slider.TrackBounds.Height / 2;
			return new Rectangle(
				centerX - KnobSize / 2,
				centerY - KnobSize / 2,
				KnobSize,
				KnobSize);
		}

		private void SyncComputedBounds(PauseMenuSlider slider)
		{
			slider.FillBounds = CalculateFillBounds(slider);
			slider.KnobBounds = CalculateKnobBounds(slider);
		}

		private static float CalculateNormalized(PauseMenuSlider slider)
		{
			int range = Math.Max(1, slider.Max - slider.Min);
			return MathHelper.Clamp((slider.Value - slider.Min) / (float)range, 0f, 1f);
		}

		private static bool UpdateSliderValue(PauseMenuSlider slider, float pointerX)
		{
			if (slider.TrackBounds.Width <= 0) return false;
			float normalized = MathHelper.Clamp(
				(pointerX - slider.TrackBounds.X) / slider.TrackBounds.Width,
				0f,
				1f);
			int range = Math.Max(1, slider.Max - slider.Min);
			int value = slider.Min + (int)MathF.Round(normalized * range);
			value = Math.Clamp(value, slider.Min, slider.Max);
			if (slider.Value == value) return false;
			slider.Value = value;
			return true;
		}

		private static void PersistSliderValue(PauseMenuSlider slider)
		{
			switch (slider.Setting)
			{
				case PauseMenuSliderSetting.MusicVolume:
					SaveCache.SetMusicVolumeLevel(slider.Value);
					break;
				case PauseMenuSliderSetting.SfxVolume:
					SaveCache.SetSfxVolumeLevel(slider.Value);
					break;
				case PauseMenuSliderSetting.CursorSpeed:
					SaveCache.SetCursorSpeedLevel(slider.Value);
					break;
				case PauseMenuSliderSetting.CursorFastSpeed:
					SaveCache.SetCursorFastSpeedLevel(slider.Value);
					break;
				case PauseMenuSliderSetting.RumbleLevel:
					SaveCache.SetRumbleLevel(slider.Value);
					break;
			}
		}
	}
}
