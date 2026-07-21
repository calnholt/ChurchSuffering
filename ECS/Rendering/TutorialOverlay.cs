using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Rendering
{
    /// <summary>
    /// Simple alpha overlay that draws semi-transparent black rectangles with rectangular cutouts
    /// to highlight specific UI elements. No shader required.
    /// </summary>
    public class TutorialOverlay
    {
        private readonly Texture2D _pixel;

        public TutorialOverlay(GraphicsDevice graphicsDevice)
        {
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }

        /// <summary>
        /// Draws the overlay with rectangular cutouts for the specified bounds.
        /// </summary>
        /// <param name="spriteBatch">The SpriteBatch to use for drawing.</param>
        /// <param name="screenWidth">Width of the screen.</param>
        /// <param name="screenHeight">Height of the screen.</param>
        /// <param name="cutouts">List of rectangles to cut out from the overlay.</param>
        /// <param name="overlayAlpha">Alpha value (0-255) for the overlay darkness.</param>
        /// <param name="padding">Additional padding around each cutout.</param>
        public void Draw(SpriteBatch spriteBatch, int screenWidth, int screenHeight, 
            IReadOnlyList<Rectangle> cutouts, int overlayAlpha, int padding = 0)
        {
            var overlayColor = new Color(0, 0, 0, System.Math.Clamp(overlayAlpha, 0, 255));

            if (cutouts == null || cutouts.Count == 0)
            {
                // No cutouts - draw full screen overlay
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), overlayColor);
                return;
            }

            // For single cutout, use the efficient 4-rectangle approach
            if (cutouts.Count == 1)
            {
                DrawSingleCutout(spriteBatch, screenWidth, screenHeight, cutouts[0], overlayColor, padding);
                return;
            }

            // For multiple cutouts, we need a more complex approach
            // Draw the overlay with all cutouts
            DrawMultipleCutouts(spriteBatch, screenWidth, screenHeight, cutouts, overlayColor, padding);
        }

        private void DrawSingleCutout(SpriteBatch spriteBatch, int screenWidth, int screenHeight,
            Rectangle cutout, Color overlayColor, int padding)
        {
            // Expand the cutout by padding
            var expanded = new Rectangle(
                cutout.X - padding,
                cutout.Y - padding,
                cutout.Width + padding * 2,
                cutout.Height + padding * 2
            );

            // Clamp to screen bounds
            int left = System.Math.Max(0, expanded.X);
            int top = System.Math.Max(0, expanded.Y);
            int right = System.Math.Min(screenWidth, expanded.Right);
            int bottom = System.Math.Min(screenHeight, expanded.Bottom);

            // Draw 4 rectangles around the cutout:
            // Top: full width, from 0 to cutout top
            if (top > 0)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, top), overlayColor);
            }

            // Bottom: full width, from cutout bottom to screen bottom
            if (bottom < screenHeight)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, bottom, screenWidth, screenHeight - bottom), overlayColor);
            }

            // Left: from 0 to cutout left, between top and bottom
            if (left > 0)
            {
                spriteBatch.Draw(_pixel, new Rectangle(0, top, left, bottom - top), overlayColor);
            }

            // Right: from cutout right to screen right, between top and bottom
            if (right < screenWidth)
            {
                spriteBatch.Draw(_pixel, new Rectangle(right, top, screenWidth - right, bottom - top), overlayColor);
            }
        }

        private void DrawMultipleCutouts(SpriteBatch spriteBatch, int screenWidth, int screenHeight,
            IReadOnlyList<Rectangle> cutouts, Color overlayColor, int padding)
        {
            // For multiple cutouts, we use a scanline approach
            // Build a list of horizontal segments to draw per row
            // This is more complex but handles overlapping cutouts correctly

            // Expand all cutouts by padding
            var expandedCutouts = new List<Rectangle>();
            foreach (var cutout in cutouts)
            {
                expandedCutouts.Add(new Rectangle(
                    cutout.X - padding,
                    cutout.Y - padding,
                    cutout.Width + padding * 2,
                    cutout.Height + padding * 2
                ));
            }

            // Find all unique Y boundaries
            var yBounds = new SortedSet<int> { 0, screenHeight };
            foreach (var r in expandedCutouts)
            {
                if (r.Y > 0 && r.Y < screenHeight) yBounds.Add(r.Y);
                if (r.Bottom > 0 && r.Bottom < screenHeight) yBounds.Add(r.Bottom);
            }

            var yList = new List<int>(yBounds);

            // For each horizontal strip
            for (int i = 0; i < yList.Count - 1; i++)
            {
                int stripTop = yList[i];
                int stripBottom = yList[i + 1];
                int stripHeight = stripBottom - stripTop;

                // Find all cutouts that intersect this strip
                var activeXRanges = new List<(int Left, int Right)>();
                foreach (var r in expandedCutouts)
                {
                    if (r.Y < stripBottom && r.Bottom > stripTop)
                    {
                        int left = System.Math.Max(0, r.X);
                        int right = System.Math.Min(screenWidth, r.Right);
                        if (left < right)
                        {
                            activeXRanges.Add((left, right));
                        }
                    }
                }

                // Merge overlapping X ranges
                activeXRanges.Sort((a, b) => a.Left.CompareTo(b.Left));
                var mergedRanges = new List<(int Left, int Right)>();
                foreach (var range in activeXRanges)
                {
                    if (mergedRanges.Count == 0 || mergedRanges[mergedRanges.Count - 1].Right < range.Left)
                    {
                        mergedRanges.Add(range);
                    }
                    else
                    {
                        var last = mergedRanges[mergedRanges.Count - 1];
                        mergedRanges[mergedRanges.Count - 1] = (last.Left, System.Math.Max(last.Right, range.Right));
                    }
                }

                // Draw overlay segments between cutout ranges
                int x = 0;
                foreach (var range in mergedRanges)
                {
                    if (x < range.Left)
                    {
                        spriteBatch.Draw(_pixel, new Rectangle(x, stripTop, range.Left - x, stripHeight), overlayColor);
                    }
                    x = range.Right;
                }

                // Draw remaining segment after last cutout
                if (x < screenWidth)
                {
                    spriteBatch.Draw(_pixel, new Rectangle(x, stripTop, screenWidth - x, stripHeight), overlayColor);
                }
            }
        }
    }
}

