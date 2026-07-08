using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Rendering
{
    public static class MipmappedTextureUtility
    {
        public static Texture2D CreateMipmappedTexture(GraphicsDevice graphicsDevice, Color[] level0, int width, int height)
        {
            width = Math.Max(1, width);
            height = Math.Max(1, height);
            var texture = new Texture2D(graphicsDevice, width, height, true, SurfaceFormat.Color);
            texture.SetData(0, null, level0, 0, level0.Length);
            SetMipData(texture, level0, width, height);
            return texture;
        }

        public static Color[] ResampleBilinear(Color[] source, int srcW, int srcH, int dstW, int dstH)
        {
            dstW = Math.Max(1, dstW);
            dstH = Math.Max(1, dstH);
            var result = new Color[dstW * dstH];
            for (int y = 0; y < dstH; y++)
            {
                for (int x = 0; x < dstW; x++)
                {
                    float sourceX = dstW == 1 ? 0f : x * (srcW - 1f) / (dstW - 1f);
                    float sourceY = dstH == 1 ? 0f : y * (srcH - 1f) / (dstH - 1f);
                    result[y * dstW + x] = SampleBilinear(source, srcW, srcH, sourceX, sourceY);
                }
            }
            return result;
        }

        public static void SetMipData(Texture2D texture, Color[] baseData, int baseWidth, int baseHeight)
        {
            var previousData = baseData;
            int previousWidth = baseWidth;
            int previousHeight = baseHeight;

            for (int level = 1; level < texture.LevelCount; level++)
            {
                int width = Math.Max(1, previousWidth / 2);
                int height = Math.Max(1, previousHeight / 2);
                var data = DownsamplePremultiplied(previousData, previousWidth, previousHeight, width, height);
                texture.SetData(level, null, data, 0, data.Length);

                previousData = data;
                previousWidth = width;
                previousHeight = height;
            }
        }

        public static Color[] DownsamplePremultiplied(Color[] source, int sourceWidth, int sourceHeight, int width, int height)
        {
            var result = new Color[width * height];

            for (int y = 0; y < height; y++)
            {
                int yStart = y * sourceHeight / height;
                int yEnd = Math.Max(yStart + 1, (y + 1) * sourceHeight / height);

                for (int x = 0; x < width; x++)
                {
                    int xStart = x * sourceWidth / width;
                    int xEnd = Math.Max(xStart + 1, (x + 1) * sourceWidth / width);

                    int r = 0;
                    int g = 0;
                    int b = 0;
                    int a = 0;
                    int count = 0;
                    for (int sy = yStart; sy < yEnd; sy++)
                    {
                        for (int sx = xStart; sx < xEnd; sx++)
                        {
                            Color color = source[sy * sourceWidth + sx];
                            r += color.R;
                            g += color.G;
                            b += color.B;
                            a += color.A;
                            count++;
                        }
                    }

                    result[y * width + x] = new Color(
                        (byte)(r / count),
                        (byte)(g / count),
                        (byte)(b / count),
                        (byte)(a / count));
                }
            }

            return result;
        }

        public static Color SampleBilinear(Color[] data, int width, int height, float x, float y)
        {
            int x0 = Math.Clamp((int)Math.Floor(x), 0, width - 1);
            int y0 = Math.Clamp((int)Math.Floor(y), 0, height - 1);
            int x1 = Math.Clamp(x0 + 1, 0, width - 1);
            int y1 = Math.Clamp(y0 + 1, 0, height - 1);
            float tx = MathHelper.Clamp(x - x0, 0f, 1f);
            float ty = MathHelper.Clamp(y - y0, 0f, 1f);

            Vector4 c00 = data[y0 * width + x0].ToVector4();
            Vector4 c10 = data[y0 * width + x1].ToVector4();
            Vector4 c01 = data[y1 * width + x0].ToVector4();
            Vector4 c11 = data[y1 * width + x1].ToVector4();
            Vector4 top = Vector4.Lerp(c00, c10, tx);
            Vector4 bottom = Vector4.Lerp(c01, c11, tx);
            return new Color(Vector4.Lerp(top, bottom, ty));
        }
    }
}
