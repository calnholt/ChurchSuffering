using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	internal enum HourglassIconStyle
	{
		WhiteMeter,
		WhiteCost,
		WhiteFaded,
		Red,
		RedFaded,
	}

	internal struct HourglassGlowTuning
	{
		public float RedGlowAlpha;
		public float WhiteMeterGlowAlpha;
		public float GlowRadius;
	}

	internal static class ClimbSceneDrawHelpers
	{
		public static readonly Color Black0 = new Color(5, 5, 5);
		public static readonly Color Black1 = new Color(10, 10, 10);
		public static readonly Color Black2 = new Color(20, 20, 20);
		public static readonly Color Black3 = new Color(30, 30, 30);
		public static readonly Color Black4 = new Color(42, 42, 42);
		public static readonly Color White1 = Color.White;
		public static readonly Color White2 = new Color(240, 236, 230);
		public static readonly Color White3 = new Color(200, 192, 184);
		public static readonly Color Red3 = new Color(196, 30, 58);
		public static readonly Color Red2 = new Color(255, 77, 94);
		public static readonly Color RedDim = new Color(160, 0, 0);
		public static readonly Color RedGlow = new Color(255, 77, 94);
		public static readonly Color CardFill = new Color(8, 8, 8) * 0.92f;
		public static readonly Color HeaderFill = new Color(10, 10, 10) * 0.82f;
		public static readonly Color ResourceBarFill = new Color(8, 8, 8) * 0.92f;

		private static Texture2D _hourglassFrame;
		private static Texture2D _hourglassSand;
		private static bool _hourglassTexturesLoaded;

		private static Texture2D _redGem;
		private static Texture2D _whiteGem;
		private static Texture2D _blackGem;
		private static bool _resourceTexturesLoaded;

		private static readonly (int Dx, int Dy)[] GlowDirections =
		{
			(1, 0), (-1, 0), (0, 1), (0, -1),
			(1, 1), (-1, 1), (1, -1), (-1, -1),
		};

		public static void EnsureHourglassTextures(ImageAssetService imageAssets)
		{
			if (_hourglassTexturesLoaded) return;
			_hourglassFrame = imageAssets.GetRequiredTexture("time_icon_hourglass_frame");
			_hourglassSand = imageAssets.GetRequiredTexture("time_icon_hourglass_sand");
			_hourglassTexturesLoaded = true;
		}

		public static void EnsureResourceTextures(ImageAssetService imageAssets)
		{
			if (_resourceTexturesLoaded) return;
			_redGem = imageAssets.GetRequiredTexture("Climb_UI/red_gem");
			_whiteGem = imageAssets.GetRequiredTexture("Climb_UI/white_gem");
			_blackGem = imageAssets.GetRequiredTexture("Climb_UI/black_gem");
			_resourceTexturesLoaded = true;
		}

		public static void DrawBorder(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness = 1)
		{
			if (rect.Width <= 0 || rect.Height <= 0 || thickness <= 0) return;
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
		}

		public static void DrawVerticalGradient(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color top, Color bottom, int strips = 16)
		{
			strips = Math.Max(1, strips);
			for (int i = 0; i < strips; i++)
			{
				int y0 = rect.Y + rect.Height * i / strips;
				int y1 = rect.Y + rect.Height * (i + 1) / strips;
				float t = strips <= 1 ? 0f : i / (float)(strips - 1);
				spriteBatch.Draw(pixel, new Rectangle(rect.X, y0, rect.Width, Math.Max(1, y1 - y0)), Color.Lerp(top, bottom, t));
			}
		}

		public static void DrawRadialPortraitGradient(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float opacity = 1f)
		{
			opacity = MathHelper.Clamp(opacity, 0f, 1f);
			int strips = Math.Max(8, rect.Height / 8);
			for (int i = 0; i < strips; i++)
			{
				int y0 = rect.Y + rect.Height * i / strips;
				int y1 = rect.Y + rect.Height * (i + 1) / strips;
				float t = strips <= 1 ? 0f : i / (float)(strips - 1);
				float centerWeight = 1f - Math.Abs(t - 0.2f) * 1.4f;
				centerWeight = MathHelper.Clamp(centerWeight, 0f, 1f);
				var color = Color.Lerp(Black1, Black3, centerWeight) * opacity;
				spriteBatch.Draw(pixel, new Rectangle(rect.X, y0, rect.Width, Math.Max(1, y1 - y0)), color);
			}
		}

		public static void DrawTitleText(SpriteBatch spriteBatch, string text, Vector2 position, float scale, Color color)
		{
			DrawFontText(spriteBatch, FontSingleton.TitleFont, text, position, scale, color);
		}

		public static void DrawBodyText(SpriteBatch spriteBatch, string text, Vector2 position, float scale, Color color)
		{
			DrawFontText(spriteBatch, FontSingleton.ChakraPetchFont, text, position, scale, color);
		}

		public static void DrawText(SpriteBatch spriteBatch, string text, Vector2 position, float scale, Color color)
		{
			DrawBodyText(spriteBatch, text, position, scale, color);
		}

		private static void DrawFontText(SpriteBatch spriteBatch, SpriteFont font, string text, Vector2 position, float scale, Color color)
		{
			if (string.IsNullOrEmpty(text) || font == null) return;
			spriteBatch.DrawString(font, ToAscii(text), position, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		public static Vector2 MeasureBodyText(string text, float scale)
		{
			if (string.IsNullOrEmpty(text) || FontSingleton.ChakraPetchFont == null) return Vector2.Zero;
			return FontSingleton.ChakraPetchFont.MeasureString(ToAscii(text)) * scale;
		}

		public static Vector2 MeasureTitleText(string text, float scale)
		{
			if (string.IsNullOrEmpty(text) || FontSingleton.TitleFont == null) return Vector2.Zero;
			return FontSingleton.TitleFont.MeasureString(ToAscii(text)) * scale;
		}

		public static string ToUpperAscii(string text)
		{
			return ToAscii(text ?? string.Empty).ToUpperInvariant();
		}

		public static void DrawResourceIcon(
			SpriteBatch spriteBatch,
			GraphicsDevice graphicsDevice,
			Texture2D pixel,
			Vector2 position,
			ClimbResourceType type,
			int size,
			Color color,
			bool compact = false,
			float glowAlpha = 0f,
			float opacity = 1f)
		{
			size = Math.Max(4, size);
			opacity = MathHelper.Clamp(opacity, 0f, 1f);
			if (type == ClimbResourceType.Red && glowAlpha > 0.001f)
			{
				int glowSize = size + 8;
				var glowRect = new Rectangle((int)position.X - 4, (int)position.Y - 4, glowSize, glowSize);
				var glowCircle = PrimitiveTextureFactory.GetAntiAliasedCircle(graphicsDevice, Math.Max(2, glowSize / 2));
				spriteBatch.Draw(glowCircle, glowRect, RedGlow * (glowAlpha * opacity));
			}

			Texture2D texture = type switch
			{
				ClimbResourceType.Red => _redGem,
				ClimbResourceType.White => _whiteGem,
				ClimbResourceType.Black => _blackGem,
				_ => null,
			};
			if (texture == null) return;

			var rect = new Rectangle((int)position.X, (int)position.Y, size, size);
			spriteBatch.Draw(texture, rect, Color.White * opacity);
		}

		public static void DrawMetaBlock(
			SpriteBatch spriteBatch,
			Texture2D pixel,
			Rectangle rect,
			string label,
			float labelScale,
			float borderAlpha,
			float fillAlpha,
			Action<SpriteBatch, Rectangle> drawContent,
			float opacity = 1f)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			opacity = MathHelper.Clamp(opacity, 0f, 1f);
			spriteBatch.Draw(pixel, rect, Color.White * (fillAlpha * opacity));
			DrawBorder(spriteBatch, pixel, rect, Color.White * (borderAlpha * opacity), 1);

			int paddingX = 8;
			int paddingY = 4;
			var content = new Rectangle(
				rect.X + paddingX,
				rect.Y + paddingY,
				Math.Max(0, rect.Width - paddingX * 2),
				Math.Max(0, rect.Height - paddingY * 2));
			if (content.Width <= 0 || content.Height <= 0) return;

			if (!string.IsNullOrWhiteSpace(label))
			{
				string upper = ToUpperAscii(label);
				var labelSize = MeasureBodyText(upper, labelScale);
				DrawBodyText(spriteBatch, upper, new Vector2(content.X, content.Y + (content.Height - labelSize.Y) / 2f), labelScale, White2 * opacity);
				content = new Rectangle(
					(int)(content.X + labelSize.X + 6),
					content.Y,
					Math.Max(0, content.Right - (int)(content.X + labelSize.X + 6)),
					content.Height);
			}

			drawContent?.Invoke(spriteBatch, content);
		}

		public static void DrawGlyphBox(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, float titleScale, float opacity = 1f)
		{
			opacity = MathHelper.Clamp(opacity, 0f, 1f);
			spriteBatch.Draw(pixel, rect, Black3 * opacity);
			DrawBorder(spriteBatch, pixel, rect, Black4 * opacity, 1);
			var glyphSize = MeasureTitleText("?", titleScale);
			DrawTitleText(spriteBatch, "?", new Vector2(rect.Center.X - glyphSize.X / 2f, rect.Center.Y - glyphSize.Y / 2f), titleScale, White2 * opacity);
		}

		public static void DrawShopTitleIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			int x = rect.X;
			int y = rect.Y;
			int w = rect.Width;
			int h = rect.Height;
			spriteBatch.Draw(pixel, new Rectangle(x, y + h * 5 / 16, w, Math.Max(1, h * 2 / 16)), color);
			spriteBatch.Draw(pixel, new Rectangle(x + w / 6, y + h / 5, w * 2 / 3, Math.Max(1, h / 5)), color * 0.35f);
			spriteBatch.Draw(pixel, new Rectangle(x + w / 8, y + h * 9 / 16, w / 5, h * 7 / 16), color);
			spriteBatch.Draw(pixel, new Rectangle(x + w * 11 / 16, y + h * 9 / 16, w / 5, h * 7 / 16), color);
			DrawBorder(spriteBatch, pixel, new Rectangle(x + w / 3, y + h * 9 / 16, w / 3, h * 5 / 16), color, 1);
			spriteBatch.Draw(pixel, new Rectangle(x + w / 3, y + h * 11 / 16, w / 3, 1), color);
		}

		public static void DrawCircularFramedImage(
			SpriteBatch spriteBatch,
			GraphicsDevice graphicsDevice,
			Texture2D pixel,
			Texture2D image,
			Rectangle rect,
			Color borderColor,
			int borderThickness,
			bool hovered)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			int radius = Math.Max(1, Math.Min(rect.Width, rect.Height) / 2);
			var circle = RoundedRectTextureFactory.CreateRoundedRect(graphicsDevice, rect.Width, rect.Height, radius);
			spriteBatch.Draw(circle, rect, new Color(8, 8, 8) * 0.92f);
			if (image != null)
			{
				int inset = Math.Max(0, borderThickness);
				var imageRect = new Rectangle(rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);
				spriteBatch.Draw(image, imageRect, Color.White);
			}

			var border = hovered ? Red2 : borderColor;
			DrawBorder(spriteBatch, pixel, rect, border, borderThickness);
		}

		public static void DrawPortraitCropped(
			SpriteBatch spriteBatch,
			Texture2D texture,
			Rectangle dest,
			float topCenterBias = 0.15f,
			float opacity = 1f)
		{
			if (texture == null || dest.Width <= 0 || dest.Height <= 0) return;
			float scale = Math.Max(dest.Width / (float)texture.Width, dest.Height / (float)texture.Height);
			int srcW = Math.Min(texture.Width, (int)Math.Ceiling(dest.Width / scale));
			int srcH = Math.Min(texture.Height, (int)Math.Ceiling(dest.Height / scale));
			int srcX = (texture.Width - srcW) / 2;
			int srcY = (int)MathHelper.Clamp(texture.Height * topCenterBias, 0, Math.Max(0, texture.Height - srcH));
			var source = new Rectangle(srcX, srcY, srcW, srcH);
			spriteBatch.Draw(texture, dest, source, Color.White * MathHelper.Clamp(opacity, 0f, 1f));
		}

		public static void DrawPortraitCropped(
			SpriteBatch spriteBatch,
			Texture2D texture,
			Rectangle dest,
			float topCenterBias,
			Vector2 parallaxOffset,
			float zoom,
			float opacity = 1f)
		{
			if (texture == null || dest.Width <= 0 || dest.Height <= 0) return;

			zoom = Math.Max(1f, zoom);
			float scale = Math.Max(dest.Width / (float)texture.Width, dest.Height / (float)texture.Height) * zoom;
			int srcW = Math.Min(texture.Width, (int)Math.Ceiling(dest.Width / scale));
			int srcH = Math.Min(texture.Height, (int)Math.Ceiling(dest.Height / scale));
			float sourcePerScreenPixel = 1f / scale;

			int centeredX = (texture.Width - srcW) / 2;
			int biasedY = (int)MathHelper.Clamp(texture.Height * topCenterBias, 0, Math.Max(0, texture.Height - srcH));
			int srcX = (int)Math.Round(centeredX - parallaxOffset.X * sourcePerScreenPixel);
			int srcY = (int)Math.Round(biasedY - parallaxOffset.Y * sourcePerScreenPixel);
			srcX = (int)MathHelper.Clamp(srcX, 0, Math.Max(0, texture.Width - srcW));
			srcY = (int)MathHelper.Clamp(srcY, 0, Math.Max(0, texture.Height - srcH));

			var source = new Rectangle(srcX, srcY, srcW, srcH);
			spriteBatch.Draw(texture, dest, source, Color.White * MathHelper.Clamp(opacity, 0f, 1f));
		}

		public static void DrawHourglassIcon(
			SpriteBatch spriteBatch,
			Rectangle dest,
			HourglassIconStyle style,
			Color frame,
			Color sand,
			bool filled,
			float alpha,
			HourglassGlowTuning glow)
		{
			if (dest.Width <= 0 || dest.Height <= 0 || _hourglassFrame == null) return;

			DrawHourglassGlow(spriteBatch, dest, style, filled, alpha, glow);

			frame *= alpha;
			sand *= alpha;
			spriteBatch.Draw(_hourglassFrame, dest, frame);
			if (filled && _hourglassSand != null)
				spriteBatch.Draw(_hourglassSand, dest, sand);
		}

		private static void DrawHourglassGlow(
			SpriteBatch spriteBatch,
			Rectangle dest,
			HourglassIconStyle style,
			bool filled,
			float alpha,
			HourglassGlowTuning glow)
		{
			if (alpha <= 0.001f) return;

			float sizeScale = dest.Height / 64f;
			int outlineRadius = Math.Max(1, (int)Math.Round(sizeScale));
			int haloRadius = Math.Max(1, (int)Math.Round(glow.GlowRadius * sizeScale));

			switch (style)
			{
				case HourglassIconStyle.WhiteCost:
				case HourglassIconStyle.WhiteMeter:
				case HourglassIconStyle.Red:
					DrawHourglassSilhouetteGlow(spriteBatch, dest, filled, Color.Black * 0.95f, outlineRadius, alpha * 0.95f);
					break;
				case HourglassIconStyle.RedFaded:
					DrawHourglassSilhouetteGlow(spriteBatch, dest, filled, Color.Black * 0.8f, outlineRadius, alpha * 0.8f);
					break;
			}

			switch (style)
			{
				case HourglassIconStyle.WhiteMeter:
					DrawHourglassSilhouetteGlow(
						spriteBatch,
						dest,
						filled,
						Color.White * 0.55f,
						haloRadius,
						alpha * glow.WhiteMeterGlowAlpha);
					break;
				case HourglassIconStyle.Red:
					DrawHourglassSilhouetteGlow(
						spriteBatch,
						dest,
						filled,
						RedGlow * 0.65f,
						haloRadius,
						alpha * glow.RedGlowAlpha);
					break;
			}
		}

		private static void DrawHourglassSilhouetteGlow(
			SpriteBatch spriteBatch,
			Rectangle dest,
			bool filled,
			Color color,
			int radius,
			float alpha)
		{
			if (alpha <= 0.001f || radius <= 0) return;

			for (int ring = 1; ring <= radius; ring++)
			{
				float ringWeight = 1f - (ring - 1) / (float)Math.Max(1, radius);
				float layerAlpha = alpha * ringWeight * 0.35f;
				if (layerAlpha <= 0.001f) continue;

				foreach (var (dx, dy) in GlowDirections)
				{
					int offsetX = dx * ring;
					int offsetY = dy * ring;
					var glowDest = new Rectangle(dest.X + offsetX, dest.Y + offsetY, dest.Width, dest.Height);
					spriteBatch.Draw(_hourglassFrame, glowDest, color * layerAlpha);
					if (filled && _hourglassSand != null)
						spriteBatch.Draw(_hourglassSand, glowDest, color * layerAlpha);
				}
			}
		}

		public static void DrawShopMarkerIcon(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color)
		{
			spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width / 5, rect.Y + rect.Height / 3, rect.Width * 3 / 5, rect.Height / 2), color);
			spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width / 4, rect.Y + rect.Height / 5, rect.Width / 2, rect.Height / 5), color);
			DrawBorder(spriteBatch, pixel, rect, color, 1);
		}

		public static float PreviewVanishPulseAlpha(float periodSeconds = 2f)
		{
			float t = (float)(DateTime.UtcNow.TimeOfDay.TotalSeconds % periodSeconds) / periodSeconds;
			if (t < 0.4f) return SmoothStepLerp(1f, 0.18f, t / 0.4f);
			if (t < 0.7f) return SmoothStepLerp(0.18f, 0.55f, (t - 0.4f) / 0.3f);
			return SmoothStepLerp(0.55f, 1f, (t - 0.7f) / 0.3f);
		}

		private static float SmoothStepLerp(float from, float to, float t)
		{
			t = MathHelper.Clamp(t, 0f, 1f);
			float eased = t * t * (3f - 2f * t);
			return MathHelper.Lerp(from, to, eased);
		}

		public static string ToAscii(string text)
		{
			if (string.IsNullOrEmpty(text)) return string.Empty;
			var chars = text.ToCharArray();
			for (int i = 0; i < chars.Length; i++)
			{
				if (chars[i] < 32 || chars[i] > 126) chars[i] = '?';
			}
			return new string(chars);
		}

		public static string ResolveShopTitle(ClimbShopSlotSave slot)
		{
			if (slot == null) return "Empty";
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase))
				return MedalFactory.Create(slot.itemId)?.Name ?? "Medal";
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase))
				return EquipmentFactory.Create(slot.itemId)?.Name ?? "Equipment";
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase))
				return ResolveCardName(slot.cardKey, fallback: "Upgrade");
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase))
				return ResolveCardName(slot.cardKey, fallback: "New Card");
			return "Empty";
		}

		public static string ResolveCardName(string cardKey, string fallback)
		{
			if (!RunDeckService.TryParseCardKey(cardKey, out var cardId, out _, out var isUpgraded)) return fallback;
			var card = CardFactory.Create(cardId);
			if (card == null) return fallback;
			card.IsUpgraded = isUpgraded;
			return card.DisplayName;
		}

		public static string ResolveShopLabel(ClimbShopSlotSave slot)
		{
			if (slot == null) return string.Empty;
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase)) return "Medal";
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase)) return "Gear";
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase)) return "Upgrade";
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase)) return "Replace";
			return string.Empty;
		}

		public static string FormatResources(ClimbResourceSave resources, string empty = "0")
		{
			resources ??= new ClimbResourceSave { red = 0, white = 0, black = 0 };
			if (resources.red == 0 && resources.white == 0 && resources.black == 0) return empty;
			return $"R{resources.red} W{resources.white} B{resources.black}";
		}
	}
}
