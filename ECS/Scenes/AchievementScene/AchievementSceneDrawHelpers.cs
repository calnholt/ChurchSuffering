using System;
using System.Text;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	internal static class AchievementSceneDrawHelpers
	{
		public static readonly Color Black0 = new(5, 5, 5);
		public static readonly Color Black1 = new(10, 10, 10);
		public static readonly Color Black2 = new(20, 20, 20);
		public static readonly Color Black3 = new(30, 30, 30);
		public static readonly Color Black4 = new(42, 42, 42);
		public static readonly Color White = Color.White;
		public static readonly Color WarmWhite = new(240, 236, 230);
		public static readonly Color MutedWhite = new(200, 192, 184);
		public static readonly Color Red = new(196, 30, 58);
		public static readonly Color RedBright = new(255, 77, 94);
		public static readonly Color RedDim = new(112, 16, 30);

		public static Rectangle GridPanel => new(72, 148, 1176, 738);
		public static Rectangle DetailPanel => new(1272, 238, 576, 430);
		public static Rectangle FooterRail => new(72, 914, 1776, 112);

		public static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness = 1)
		{
			if (rect.Width <= 0 || rect.Height <= 0 || thickness <= 0) return;
			int t = Math.Min(thickness, Math.Min(rect.Width, rect.Height));
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, t), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - t, rect.Width, t), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, t, rect.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - t, rect.Y, t, rect.Height), color);
		}

		public static void DrawPanel(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float alpha = 1f)
		{
			alpha = MathHelper.Clamp(alpha, 0f, 1f);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + 8, rect.Width, Math.Max(1, rect.Height - 8)), Color.Black * (0.62f * alpha));
			spriteBatch.Draw(pixel, rect, new Color(8, 8, 8) * (0.88f * alpha));
			DrawBorder(spriteBatch, pixel, rect, Color.White * (0.28f * alpha), 1);
			DrawBorder(spriteBatch, pixel, new Rectangle(rect.X + 2, rect.Y + 2, rect.Width - 4, rect.Height - 4), Color.White * (0.05f * alpha), 1);
		}

		public static void DrawTitleText(SpriteBatch spriteBatch, string text, Vector2 position, float scale, Color color)
		{
			var font = FontSingleton.TitleFont;
			if (font == null || string.IsNullOrEmpty(text)) return;
			string ascii = ToAscii(text);
			spriteBatch.DrawString(font, ascii, position + new Vector2(0f, 4f), Color.Black * (color.A / 255f), 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
			spriteBatch.DrawString(font, ascii, position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		public static void DrawBodyText(SpriteBatch spriteBatch, string text, Vector2 position, float scale, Color color)
		{
			var font = FontSingleton.ChakraPetchFont;
			if (font == null || string.IsNullOrEmpty(text)) return;
			spriteBatch.DrawString(font, ToAscii(text), position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		public static Vector2 MeasureBodyText(string text, float scale)
		{
			var font = FontSingleton.ChakraPetchFont;
			return font == null || string.IsNullOrEmpty(text) ? Vector2.Zero : font.MeasureString(ToAscii(text)) * scale;
		}

		public static string ToAscii(string text)
		{
			if (string.IsNullOrEmpty(text)) return string.Empty;
			var result = new StringBuilder(text.Length);
			foreach (char c in text)
			{
				if (c == '\r' || c == '\n')
				{
					result.Append(c);
					continue;
				}
				if (c >= ' ' && c <= '~') result.Append(c);
				else result.Append(' ');
			}
			return result.ToString();
		}
	}
}
