using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Pause Sliders")]
	public class PauseMenuSliderDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ChakraPetchFont;
		private readonly Texture2D _pixel;

		private static readonly Color LabelColor = new Color(200, 192, 184);
		private static readonly Color ValueColor = new Color(196, 30, 58);
		private static readonly Color TrackColor = new Color(255, 255, 255) * 0.12f;
		private static readonly Color FillStartColor = new Color(160, 0, 0);
		private static readonly Color FillEndColor = new Color(196, 30, 58);
		private static readonly Color KnobFillColor = Color.White;
		private static readonly Color KnobBorderColor = new Color(196, 30, 58);
		private static readonly Color KnobGlowColor = new Color(196, 30, 58);

		[DebugEditable(DisplayName = "Label Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float LabelScale { get; set; } = 0.09f;

		[DebugEditable(DisplayName = "Value Scale", Step = 0.01f, Min = 0.02f, Max = 1f)]
		public float ValueScale { get; set; } = 0.17f;

		[DebugEditable(DisplayName = "Header To Track Gap", Step = 1, Min = 0, Max = 80)]
		public int HeaderToTrackGap { get; set; } = 14;

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
			string value = $"{slider.Value}%";

			var labelPos = new Vector2(row.X, row.Y);
			_spriteBatch.DrawString(_font, label, labelPos, LabelColor, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);

			Vector2 valueSize = _font.MeasureString(value) * ValueScale;
			var valuePos = new Vector2(row.Right - valueSize.X, row.Y - 6);
			_spriteBatch.DrawString(_font, value, valuePos, ValueColor, 0f, Vector2.Zero, ValueScale, SpriteEffects.None, 0f);

			var track = slider.TrackBounds;
			_spriteBatch.Draw(_pixel, track, TrackColor);

			DrawHorizontalGradient(slider.FillBounds, FillStartColor, FillEndColor, 24);

			bool active = ui.IsHovered || slider.IsDragging;
			float scale = active ? HoverKnobScale : 1f;
			DrawKnob(slider.KnobBounds, scale);
		}

		private void DrawKnob(Rectangle knob, float scale)
		{
			int outerRadius = Math.Max(1, KnobSize / 2);
			int innerRadius = Math.Max(1, outerRadius - Math.Max(0, KnobBorder));
			var center = new Vector2(knob.X + knob.Width / 2f, knob.Y + knob.Height / 2f);

			if (GlowAlpha > 0f)
			{
				int glowRadius = Math.Max(outerRadius + GlowSizeAdd / 2, outerRadius);
				var glow = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, glowRadius);
				_spriteBatch.Draw(
					glow,
					center,
					null,
					KnobGlowColor * GlowAlpha,
					0f,
					new Vector2(glow.Width / 2f, glow.Height / 2f),
					scale,
					SpriteEffects.None,
					0f);
			}

			var outer = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, outerRadius);
			_spriteBatch.Draw(
				outer,
				center,
				null,
				KnobBorderColor,
				0f,
				new Vector2(outer.Width / 2f, outer.Height / 2f),
				scale,
				SpriteEffects.None,
				0f);

			var inner = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, innerRadius);
			_spriteBatch.Draw(
				inner,
				center,
				null,
				KnobFillColor,
				0f,
				new Vector2(inner.Width / 2f, inner.Height / 2f),
				scale,
				SpriteEffects.None,
				0f);
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
			}
		}
	}
}
