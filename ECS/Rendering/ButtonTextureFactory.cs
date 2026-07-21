using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering
{
    /// <summary>
    /// Factory for generating button textures (rounded rect background + centered text).
    /// No internal caching; callers can cache as needed.
    /// </summary>
    public static class ButtonTextureFactory
    {
        public static Texture2D Create(
            GraphicsDevice graphicsDevice,
            string text,
            Color backgroundColor,
            Color textColor,
            float textScale = 0.2f,
            int horizontalPadding = 28,
            int verticalPadding = 8,
            int cornerRadius = 12)
        {
            SpriteFont font = FontSingleton.ContentFont;
            Vector2 textSize = font.MeasureString(text) * textScale;

            int width = (int)System.Math.Ceiling(textSize.X) + horizontalPadding * 2;
            int height = (int)System.Math.Ceiling(textSize.Y) + verticalPadding * 2;

            Texture2D backgroundTexture = RoundedRectTextureFactory.CreateRoundedRect(
                graphicsDevice, width, height, cornerRadius);

            var renderTarget = new RenderTarget2D(graphicsDevice, width, height);

            graphicsDevice.SetRenderTarget(renderTarget);
            graphicsDevice.Clear(Color.Transparent);

            var spriteBatch = new SpriteBatch(graphicsDevice);
            spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend);

            spriteBatch.Draw(backgroundTexture, Vector2.Zero, backgroundColor);

            Vector2 textPosition = new Vector2(
                (width - textSize.X) / 2f,
                (height - textSize.Y) / 2f);
            spriteBatch.DrawString(font, text, textPosition, textColor, 0f,
                Vector2.Zero, textScale, SpriteEffects.None, 0f);

            spriteBatch.End();
            spriteBatch.Dispose();

            graphicsDevice.SetRenderTarget(null);
            backgroundTexture.Dispose();

            return renderTarget;
        }
    }
}
