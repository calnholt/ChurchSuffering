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
        private const string MedalAssetPrefix = "Medals/";
        private const float PlaceholderNameScale = 0.07f;
        private const float PlaceholderMinNameScale = 0.03f;

        public static Texture2D TryLoadMedalTexture(ImageAssetService imageAssets, string medalId)
        {
            return imageAssets?.TryGetTexture($"{MedalAssetPrefix}{medalId}");
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
            float rotationRad = 0f,
            float softenStrength = 0f,
            float opacity = 1f)
        {
            opacity = MathHelper.Clamp(opacity, 0f, 1f);
            var tex = TryLoadMedalTexture(imageAssets, medalId);
            if (tex != null)
            {
                return DrawTextureMedal(spriteBatch, center, iconSize, medalId, tex, imageAssets, scale, rotationRad, softenStrength, opacity);
            }
            return DrawPlaceholderMedal(spriteBatch, graphicsDevice, font, center, iconSize, medalId, scale, rotationRad, opacity);
        }

        private static Rectangle DrawTextureMedal(
            SpriteBatch spriteBatch,
            Vector2 center,
            int iconSize,
            string medalId,
            Texture2D tex,
            ImageAssetService imageAssets,
            float scale,
            float rotationRad,
            float softenStrength,
            float opacity)
        {
            int drawW = iconSize;
            int drawH = iconSize;
            if (tex.Width > 0 && tex.Height > 0)
            {
                float fitScale = System.Math.Min(iconSize / (float)tex.Width, iconSize / (float)tex.Height);
                drawW = System.Math.Max(1, (int)System.Math.Round(tex.Width * fitScale));
                drawH = System.Math.Max(1, (int)System.Math.Round(tex.Height * fitScale));
            }

            float animationScale = System.Math.Max(0.1f, scale);
            int scaledDrawW = (int)System.Math.Round(drawW * animationScale);
            int scaledDrawH = (int)System.Math.Round(drawH * animationScale);
            int left = (int)System.Math.Round(center.X - scaledDrawW / 2f);
            int top = (int)System.Math.Round(center.Y - scaledDrawH / 2f);

            var scaledTex = imageAssets?.GetScaledMipmappedTexture($"{MedalAssetPrefix}{medalId}", tex, drawW, drawH, softenStrength) ?? tex;
            var origin = new Vector2(scaledTex.Width / 2f, scaledTex.Height / 2f);
            spriteBatch.Draw(scaledTex, center, null, Color.White * opacity, rotationRad, origin, animationScale, SpriteEffects.None, 0f);
            return new Rectangle(left, top, scaledDrawW, scaledDrawH);
        }

        private static Rectangle DrawPlaceholderMedal(
            SpriteBatch spriteBatch,
            GraphicsDevice graphicsDevice,
            SpriteFont font,
            Vector2 center,
            int iconSize,
            string medalId,
            float scale,
            float rotationRad,
            float opacity)
        {
            int radius = System.Math.Max(4, (int)System.Math.Round(iconSize / 2f));
            var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(graphicsDevice, radius);
            var circleOrigin = new Vector2(radius, radius);
            float circleScale = System.Math.Max(0.1f, scale);
            spriteBatch.Draw(
                circle,
                center,
                null,
                Color.Black * opacity,
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
                    rotationRad,
                    opacity);
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
            float rotationRad,
            float opacity)
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
                    Color.White * opacity,
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
