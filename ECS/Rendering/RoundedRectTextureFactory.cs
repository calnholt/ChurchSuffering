using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering
{
    /// <summary>
    /// Factory for generating rounded-rectangle textures.
    /// No internal caching; callers can cache per-size as needed.
    /// </summary>
    public static class RoundedRectTextureFactory
    {
        public static Texture2D CreateRoundedRect(GraphicsDevice graphicsDevice, int width, int height, int radius)
        {
            var texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];
            int r = System.Math.Max(0, System.Math.Min(radius, System.Math.Min(width, height) / 2));
            int w = width;
            int h = height;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float alpha = 1f;
                    bool inCorner = false;
                    if (r > 0)
                    {
                        // Top-left corner
                        if (x < r && y < r)
                        {
                            inCorner = true;
                            float cx = (r - 0.5f);
                            float cy = (r - 0.5f);
                            float dx = (x + 0.5f) - cx;
                            float dy = (y + 0.5f) - cy;
                            float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                            if (d <= r - 0.5f) alpha = 1f;
                            else if (d >= r + 0.5f) alpha = 0f;
                            else alpha = System.Math.Max(0f, System.Math.Min(1f, (r + 0.5f - d)));
                        }
                        // Top-right corner
                        else if (x >= w - r && y < r)
                        {
                            inCorner = true;
                            float cx = (w - r - 0.5f);
                            float cy = (r - 0.5f);
                            float dx = (x + 0.5f) - cx;
                            float dy = (y + 0.5f) - cy;
                            float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                            if (d <= r - 0.5f) alpha = 1f;
                            else if (d >= r + 0.5f) alpha = 0f;
                            else alpha = System.Math.Max(0f, System.Math.Min(1f, (r + 0.5f - d)));
                        }
                        // Bottom-left corner
                        else if (x < r && y >= h - r)
                        {
                            inCorner = true;
                            float cx = (r - 0.5f);
                            float cy = (h - r - 0.5f);
                            float dx = (x + 0.5f) - cx;
                            float dy = (y + 0.5f) - cy;
                            float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                            if (d <= r - 0.5f) alpha = 1f;
                            else if (d >= r + 0.5f) alpha = 0f;
                            else alpha = System.Math.Max(0f, System.Math.Min(1f, (r + 0.5f - d)));
                        }
                        // Bottom-right corner
                        else if (x >= w - r && y >= h - r)
                        {
                            inCorner = true;
                            float cx = (w - r - 0.5f);
                            float cy = (h - r - 0.5f);
                            float dx = (x + 0.5f) - cx;
                            float dy = (y + 0.5f) - cy;
                            float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                            if (d <= r - 0.5f) alpha = 1f;
                            else if (d >= r + 0.5f) alpha = 0f;
                            else alpha = System.Math.Max(0f, System.Math.Min(1f, (r + 0.5f - d)));
                        }
                    }

                    if (!inCorner)
                    {
                        // Not in a rounded corner square region; inside the body of the rect
                        alpha = 1f;
                    }

                    // Use premultiplied alpha to avoid edge fringing and ensure smooth AA blending
                    data[y * w + x] = (alpha <= 0f) ? Color.Transparent : new Color(alpha, alpha, alpha, alpha);
                }
            }

            texture.SetData(data);
            return texture;
        }

        public static Texture2D CreateRoundedRectPerCorner(GraphicsDevice graphicsDevice, int width, int height, int radiusTL, int radiusTR, int radiusBR, int radiusBL)
        {
            var texture = new Texture2D(graphicsDevice, width, height);
            Color[] data = new Color[width * height];
            int w = width;
            int h = height;
            int rTL = System.Math.Max(0, System.Math.Min(radiusTL, System.Math.Min(w, h) / 2));
            int rTR = System.Math.Max(0, System.Math.Min(radiusTR, System.Math.Min(w, h) / 2));
            int rBR = System.Math.Max(0, System.Math.Min(radiusBR, System.Math.Min(w, h) / 2));
            int rBL = System.Math.Max(0, System.Math.Min(radiusBL, System.Math.Min(w, h) / 2));

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float alpha = 1f;
                    bool inCorner = false;

                    // Top-left corner
                    if (rTL > 0 && x < rTL && y < rTL)
                    {
                        inCorner = true;
                        float cx = rTL - 0.5f;
                        float cy = rTL - 0.5f;
                        float dx = (x + 0.5f) - cx;
                        float dy = (y + 0.5f) - cy;
                        float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                        if (d <= rTL - 0.5f) alpha = 1f;
                        else if (d >= rTL + 0.5f) alpha = 0f;
                        else alpha = System.Math.Max(0f, System.Math.Min(1f, rTL + 0.5f - d));
                    }
                    // Top-right corner
                    else if (rTR > 0 && x >= w - rTR && y < rTR)
                    {
                        inCorner = true;
                        float cx = w - rTR - 0.5f;
                        float cy = rTR - 0.5f;
                        float dx = (x + 0.5f) - cx;
                        float dy = (y + 0.5f) - cy;
                        float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                        if (d <= rTR - 0.5f) alpha = 1f;
                        else if (d >= rTR + 0.5f) alpha = 0f;
                        else alpha = System.Math.Max(0f, System.Math.Min(1f, rTR + 0.5f - d));
                    }
                    // Bottom-right corner
                    else if (rBR > 0 && x >= w - rBR && y >= h - rBR)
                    {
                        inCorner = true;
                        float cx = w - rBR - 0.5f;
                        float cy = h - rBR - 0.5f;
                        float dx = (x + 0.5f) - cx;
                        float dy = (y + 0.5f) - cy;
                        float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                        if (d <= rBR - 0.5f) alpha = 1f;
                        else if (d >= rBR + 0.5f) alpha = 0f;
                        else alpha = System.Math.Max(0f, System.Math.Min(1f, rBR + 0.5f - d));
                    }
                    // Bottom-left corner
                    else if (rBL > 0 && x < rBL && y >= h - rBL)
                    {
                        inCorner = true;
                        float cx = rBL - 0.5f;
                        float cy = h - rBL - 0.5f;
                        float dx = (x + 0.5f) - cx;
                        float dy = (y + 0.5f) - cy;
                        float d = (float)System.Math.Sqrt(dx * dx + dy * dy);
                        if (d <= rBL - 0.5f) alpha = 1f;
                        else if (d >= rBL + 0.5f) alpha = 0f;
                        else alpha = System.Math.Max(0f, System.Math.Min(1f, rBL + 0.5f - d));
                    }

                    if (!inCorner) alpha = 1f;

                    data[y * w + x] = (alpha <= 0f) ? Color.Transparent : new Color(alpha, alpha, alpha, alpha);
                }
            }

            texture.SetData(data);
            return texture;
        }
    }
}

