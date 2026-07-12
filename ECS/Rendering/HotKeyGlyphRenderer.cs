using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Input;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering
{
	internal sealed class HotKeyGlyphRenderer : IDisposable
	{
		private static readonly Color BadgeFill = new(36, 36, 36);
		private static readonly Color KeyBorder = new(215, 215, 215);
		private static readonly Color KeyFill = new(62, 62, 62);

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font;
		private readonly Dictionary<(int Width, int Height, int Radius), Texture2D> _roundedRects = new();

		public HotKeyGlyphRenderer(GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, SpriteFont font)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_font = font;
		}

		public Point MeasureKeyboard(PlayerButton button, int hintRadius, float textScale)
		{
			string label = GetKeyboardLabel(button);
			int height = Math.Max(20, (int)Math.Round(hintRadius * 1.4f));
			int textWidth = (int)Math.Ceiling(_font.MeasureString(label).X * textScale);
			return new Point(Math.Max(height, textWidth + 18), height);
		}

		public Point MeasureGamepad(FaceButton button, int hintRadius)
		{
			return button switch
			{
				FaceButton.LB or FaceButton.RB => new Point(
					(int)Math.Round(hintRadius * 2.7f),
					(int)Math.Round(hintRadius * 1.45f)),
				FaceButton.View or FaceButton.Start => new Point(
					(int)Math.Round(hintRadius * 2.1f),
					(int)Math.Round(hintRadius * 1.45f)),
				_ => new Point(hintRadius * 2, hintRadius * 2),
			};
		}

		public void DrawKeyboard(PlayerButton button, int cx, int cy, int hintRadius, float textScale, float alpha = 1f)
		{
			Point size = MeasureKeyboard(button, hintRadius, textScale);
			Rectangle outer = CenteredRect(cx, cy, size.X, size.Y);
			Rectangle inner = new(outer.X + 2, outer.Y + 2, outer.Width - 4, outer.Height - 4);
			DrawRoundedRect(outer, Math.Max(3, outer.Height / 5), KeyBorder * alpha);
			DrawRoundedRect(inner, Math.Max(2, inner.Height / 5), KeyFill * alpha);

			string label = GetKeyboardLabel(button);
			Vector2 textSize = _font.MeasureString(label) * textScale;
			Vector2 position = new(cx - textSize.X / 2f, cy - textSize.Y / 2f - 2f);
			_spriteBatch.DrawString(_font, label, position, Color.White * alpha, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
		}

		public void DrawGamepad(FaceButton button, GamepadGlyphStyle style, int cx, int cy, int hintRadius, float textScale, float alpha = 1f)
		{
			if (button is FaceButton.LB or FaceButton.RB)
			{
				string label = style == GamepadGlyphStyle.PlayStation
					? (button == FaceButton.LB ? "L1" : "R1")
					: (button == FaceButton.LB ? "LB" : "RB");
				DrawPill(label, cx, cy, MeasureGamepad(button, hintRadius), textScale, alpha);
				return;
			}

			if (button is FaceButton.View or FaceButton.Start)
			{
				DrawSystemButton(button, style, cx, cy, hintRadius, alpha);
				return;
			}

			Texture2D circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, Math.Max(2, hintRadius));
			Rectangle circleBounds = CenteredRect(cx, cy, hintRadius * 2, hintRadius * 2);
			if (style == GamepadGlyphStyle.Xbox)
			{
				_spriteBatch.Draw(circle, circleBounds, GetXboxColor(button) * alpha);
				string label = button.ToString();
				Vector2 size = _font.MeasureString(label) * textScale;
				_spriteBatch.DrawString(_font, label, new Vector2(cx - size.X / 2f, cy - size.Y / 2f - 2f), Color.Black * alpha, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
				return;
			}

			_spriteBatch.Draw(circle, circleBounds, BadgeFill * alpha);
			DrawPlayStationFace(button, cx, cy, hintRadius, alpha);
		}

		private void DrawSystemButton(FaceButton button, GamepadGlyphStyle style, int cx, int cy, int hintRadius, float alpha)
		{
			Point size = MeasureGamepad(button, hintRadius);
			Rectangle bounds = CenteredRect(cx, cy, size.X, size.Y);
			DrawRoundedRect(bounds, Math.Max(3, bounds.Height / 2), BadgeFill * alpha);

			if (button == FaceButton.Start)
			{
				if (style == GamepadGlyphStyle.PlayStation)
				{
					DrawTextCentered("OPT", cx, cy, 0.75f, alpha);
				}
				else
				{
					int lineWidth = Math.Max(10, (int)Math.Round(hintRadius * 0.9f));
					int lineHeight = Math.Max(2, hintRadius / 7);
					for (int i = -1; i <= 1; i++)
					{
						Rectangle line = new(cx - lineWidth / 2, cy + i * (lineHeight + 3) - lineHeight / 2, lineWidth, lineHeight);
						DrawRoundedRect(line, Math.Max(1, lineHeight / 2), Color.White * alpha);
					}
				}
				return;
			}

			if (style == GamepadGlyphStyle.PlayStation)
			{
				DrawTextCentered("CREATE", cx, cy, 0.52f, alpha);
				return;
			}

			int rectSize = Math.Max(8, (int)Math.Round(hintRadius * 0.7f));
			int offset = Math.Max(3, rectSize / 3);
			Rectangle back = CenteredRect(cx - offset / 2, cy - offset / 2, rectSize, rectSize);
			Rectangle front = CenteredRect(cx + offset / 2, cy + offset / 2, rectSize, rectSize);
			DrawRoundedOutline(back, Math.Max(2, rectSize / 5), Color.White * alpha);
			DrawRoundedOutline(front, Math.Max(2, rectSize / 5), Color.White * alpha);
		}

		private void DrawPlayStationFace(FaceButton button, int cx, int cy, int hintRadius, float alpha)
		{
			int size = Math.Max(8, (int)Math.Round(hintRadius * 1.2f));
			Texture2D texture;
			Color color;
			switch (button)
			{
				case FaceButton.Y:
					texture = PrimitiveTextureFactory.GetEquilateralTriangle(_graphicsDevice, size);
					color = new Color(50, 200, 90);
					break;
				case FaceButton.B:
					texture = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, Math.Max(2, size / 2));
					color = new Color(220, 60, 60);
					break;
				case FaceButton.X:
					texture = PrimitiveTextureFactory.GetRoundedSquare(_graphicsDevice, size, Math.Max(2, size / 6));
					color = new Color(200, 80, 200);
					break;
				default:
					return;
			}

			_spriteBatch.Draw(texture, new Vector2(cx - texture.Width / 2f, cy - texture.Height / 2f), color * alpha);
		}

		private void DrawPill(string label, int cx, int cy, Point size, float textScale, float alpha)
		{
			Rectangle bounds = CenteredRect(cx, cy, size.X, size.Y);
			DrawRoundedRect(bounds, Math.Max(3, bounds.Height / 4), BadgeFill * alpha);
			Vector2 textSize = _font.MeasureString(label) * textScale;
			_spriteBatch.DrawString(_font, label, new Vector2(cx - textSize.X / 2f, cy - textSize.Y / 2f - 2f), Color.White * alpha, 0f, Vector2.Zero, textScale, SpriteEffects.None, 0f);
		}

		private void DrawTextCentered(string text, int cx, int cy, float scaleMultiplier, float alpha)
		{
			float scale = 0.15f * scaleMultiplier;
			Vector2 size = _font.MeasureString(text) * scale;
			_spriteBatch.DrawString(_font, text, new Vector2(cx - size.X / 2f, cy - size.Y / 2f - 2f), Color.White * alpha, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void DrawRoundedOutline(Rectangle bounds, int radius, Color color)
		{
			DrawRoundedRect(bounds, radius, color);
			Rectangle inner = new(bounds.X + 2, bounds.Y + 2, bounds.Width - 4, bounds.Height - 4);
			DrawRoundedRect(inner, Math.Max(1, radius - 1), BadgeFill * (color.A / 255f));
		}

		private void DrawRoundedRect(Rectangle bounds, int radius, Color color)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;
			var key = (bounds.Width, bounds.Height, radius);
			if (!_roundedRects.TryGetValue(key, out Texture2D texture))
			{
				texture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, bounds.Width, bounds.Height, radius);
				_roundedRects[key] = texture;
			}
			_spriteBatch.Draw(texture, bounds, color);
		}

		private static Rectangle CenteredRect(int cx, int cy, int width, int height)
		{
			return new Rectangle(cx - width / 2, cy - height / 2, width, height);
		}

		private static string GetKeyboardLabel(PlayerButton button)
		{
			return button switch
			{
				PlayerButton.Enter => "ENTER",
				PlayerButton.Space => "SPACE",
				PlayerButton.Escape => "ESC",
				_ => button.ToString().ToUpperInvariant(),
			};
		}

		private static Color GetXboxColor(FaceButton button)
		{
			return button switch
			{
				FaceButton.B => new Color(220, 50, 50),
				FaceButton.X => new Color(60, 120, 220),
				FaceButton.Y => new Color(220, 200, 60),
				_ => Color.White,
			};
		}

		public void Dispose()
		{
			foreach (Texture2D texture in _roundedRects.Values) texture.Dispose();
			_roundedRects.Clear();
		}
	}
}
