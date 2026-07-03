using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Services
{
    public static class MedalIconRenderService
    {
        private const float PlaceholderNameScale = 0.07f;
        private const float PlaceholderMinNameScale = 0.03f;

        public static Texture2D TryLoadMedalTexture(ImageAssetService imageAssets, string medalId)
        {
            return imageAssets?.TryGetTexture(medalId);
        }

        public static Rectangle DrawMedalIcon(
            SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice,
            SpriteFont font,
            Vector2 center,
            int iconSize,
            string medalId,
            ImageAssetService imageAssets,
            float scale = 1f,
            float rotationRad = 0f)
        {
            var tex = TryLoadMedalTexture(imageAssets, medalId);
            if (tex != null)
            {
                return DrawTextureMedal(spriteBatch, center, iconSize, tex, scale, rotationRad);
            }
            return DrawPlaceholderMedal(spriteBatch, graphicsDevice, font, center, iconSize, medalId, scale, rotationRad);
        }

        private static Rectangle DrawTextureMedal(
            SpriteBatch spriteBatch,
            Vector2 center,
            int iconSize,
            Texture2D tex,
            float scale,
            float rotationRad)
        {
            float baseScale = 1f;
            if (tex.Width > 0 && tex.Height > 0)
            {
                float sx = iconSize / (float)tex.Width;
                float sy = iconSize / (float)tex.Height;
                baseScale = System.Math.Min(sx, sy);
            }
            float finalScale = baseScale * System.Math.Max(0.1f, scale);
            var origin = new Vector2(tex.Width / 2f, tex.Height / 2f);
            int drawW = (int)System.Math.Round(tex.Width * finalScale);
            int drawH = (int)System.Math.Round(tex.Height * finalScale);
            int left = (int)System.Math.Round(center.X - drawW / 2f);
            int top = (int)System.Math.Round(center.Y - drawH / 2f);
            spriteBatch.Draw(tex, center, null, Color.White, rotationRad, origin, finalScale, SpriteEffects.None, 0f);
            return new Rectangle(left, top, drawW, drawH);
        }

        private static Rectangle DrawPlaceholderMedal(
            SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice,
            SpriteFont font,
            Vector2 center,
            int iconSize,
            string medalId,
            float scale,
            float rotationRad)
        {
            int radius = System.Math.Max(4, (int)System.Math.Round(iconSize / 2f));
            var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(graphicsDevice, radius);
            var circleOrigin = new Vector2(radius, radius);
            float circleScale = System.Math.Max(0.1f, scale);
            spriteBatch.Draw(
                circle,
                center,
                null,
                Color.Black,
                rotationRad,
                circleOrigin,
                circleScale,
                SpriteEffects.None,
                0f);

            int drawSize = (int)System.Math.Round(radius * 2f * circleScale);
            var drawFont = font ?? FontSingleton.ContentFont;
            if (drawFont != null)
            {
                string label = GetPlaceholderMedalLabel(medalId);
                DrawCenteredWrappedLabel(
                    spriteBatch,
                    drawFont,
                    label,
                    center,
                    drawSize,
                    PlaceholderNameScale * circleScale,
                    rotationRad);
            }

            int left = (int)System.Math.Round(center.X - drawSize / 2f);
            int top = (int)System.Math.Round(center.Y - drawSize / 2f);
            return new Rectangle(left, top, drawSize, drawSize);
        }

        private static string GetPlaceholderMedalLabel(string medalId)
        {
            var medal = MedalFactory.Create(medalId);
            if (!string.IsNullOrWhiteSpace(medal?.Name))
            {
                return medal.Name;
            }
            return "?";
        }

        private static void DrawCenteredWrappedLabel(
            SpriteBatch spriteBatch,
            SpriteFont font,
            string label,
            Vector2 center,
            int drawSize,
            float baseTextScale,
            float rotationRad)
        {
            int maxWidth = System.Math.Max(8, (int)System.Math.Round(drawSize * 0.88f));
            float maxHeight = drawSize * 0.82f;
            float textScale = baseTextScale;
            List<string> lines = WrapToFit(font, label, textScale, maxWidth, maxHeight);

            float lineHeight = font.LineSpacing * textScale;
            float blockHeight = lines.Count * lineHeight;
            float y = -blockHeight / 2f;
            foreach (var line in lines)
            {
                if (string.IsNullOrEmpty(line))
                {
                    y += lineHeight;
                    continue;
                }
                var measure = font.MeasureString(line);
                float lineWidth = measure.X * textScale;
                var localTopLeft = new Vector2(-lineWidth / 2f, y);
                var worldTopLeft = center + RotateLocal(localTopLeft, rotationRad);
                spriteBatch.DrawString(
                    font,
                    line,
                    worldTopLeft,
                    Color.White,
                    rotationRad,
                    Vector2.Zero,
                    textScale,
                    SpriteEffects.None,
                    0f);
                y += lineHeight;
            }
        }

        private static List<string> WrapToFit(
            SpriteFont font,
            string label,
            float textScale,
            int maxWidth,
            float maxHeight)
        {
            var lines = TextUtils.WrapText(font, label, textScale, maxWidth);
            float lineHeight = font.LineSpacing * textScale;
            float blockHeight = lines.Count * lineHeight;
            float maxLineWidth = lines.Count == 0
                ? 0f
                : lines.Max(l => font.MeasureString(l).X * textScale);

            if ((blockHeight <= maxHeight && maxLineWidth <= maxWidth) || textScale <= PlaceholderMinNameScale)
            {
                return lines;
            }

            return WrapToFit(font, label, textScale * 0.9f, maxWidth, maxHeight);
        }

        private static Vector2 RotateLocal(Vector2 local, float rotationRad)
        {
            float cos = (float)Math.Cos(rotationRad);
            float sin = (float)Math.Sin(rotationRad);
            return new Vector2(local.X * cos - local.Y * sin, local.X * sin + local.Y * cos);
        }
    }
}
