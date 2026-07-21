using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Rendering;

/// <summary>
/// Calculates the full logical bounds touched by a card, including status shader overflow.
/// </summary>
public static class CardRenderBoundsService
{
    private const float SamplingMargin = 4f;

    public static Rectangle GetBounds(
        EntityManager entityManager,
        Entity card,
        Vector2 position,
        float scale,
        float rotation) => CalculateBounds(
            entityManager,
            card,
            position,
            scale,
            rotation,
            includeStatusOverflow: true);

    /// <summary>
    /// Calculates a padded, rotation-aware surface for the static card face only.
    /// Dynamic status overflow is intentionally excluded from cached base surfaces.
    /// </summary>
    public static Rectangle GetBaseBounds(
        EntityManager entityManager,
        Entity card,
        Vector2 position,
        float scale,
        float rotation) => CalculateBounds(
            entityManager,
            card,
            position,
            scale,
            rotation,
            includeStatusOverflow: false);

    private static Rectangle CalculateBounds(
        EntityManager entityManager,
        Entity card,
        Vector2 position,
        float scale,
        float rotation,
        bool includeStatusOverflow)
    {
        CardVisualGeometry geometry = CardGeometryService.GetVisualGeometry(
            entityManager,
            card,
            position,
            Math.Max(0.001f, scale),
            rotation);
        float width = geometry.Bounds.Width;
        float height = geometry.Bounds.Height;
        float left = SamplingMargin;
        float right = SamplingMargin;
        float top = SamplingMargin;
        float bottom = SamplingMargin;

        if (includeStatusOverflow && card?.GetComponent<Brittle>() != null)
        {
            float chunk = 22f * Math.Max(0.001f, scale);
            left = Math.Max(left, chunk * 2.2f);
            right = Math.Max(right, chunk * 2.2f);
            top = Math.Max(top, chunk);
            bottom = Math.Max(bottom, chunk * 13f);
        }
        if (includeStatusOverflow && card?.GetComponent<Frozen>() != null)
        {
            left = Math.Max(left, width * 0.25f);
            right = Math.Max(right, width * 0.25f);
            top = Math.Max(top, height);
        }
        if (includeStatusOverflow && card?.GetComponent<Scorched>() != null)
        {
            float firePadding = height * 0.25f;
            left = Math.Max(left, firePadding);
            right = Math.Max(right, firePadding);
            top = Math.Max(top, firePadding);
            bottom = Math.Max(bottom, firePadding);
        }

        Vector2 center = geometry.Center;
        float halfWidth = width * 0.5f;
        float halfHeight = height * 0.5f;
        Vector2[] corners =
        {
            new(-halfWidth - left, -halfHeight - top),
            new(halfWidth + right, -halfHeight - top),
            new(halfWidth + right, halfHeight + bottom),
            new(-halfWidth - left, halfHeight + bottom),
        };
        float minX = float.MaxValue;
        float minY = float.MaxValue;
        float maxX = float.MinValue;
        float maxY = float.MinValue;
        float cosine = MathF.Cos(rotation);
        float sine = MathF.Sin(rotation);
        foreach (Vector2 corner in corners)
        {
            var rotated = new Vector2(
                corner.X * cosine - corner.Y * sine,
                corner.X * sine + corner.Y * cosine) + center;
            minX = Math.Min(minX, rotated.X);
            minY = Math.Min(minY, rotated.Y);
            maxX = Math.Max(maxX, rotated.X);
            maxY = Math.Max(maxY, rotated.Y);
        }

        int x = (int)MathF.Floor(minX);
        int y = (int)MathF.Floor(minY);
        return new Rectangle(
            x,
            y,
            Math.Max(1, (int)MathF.Ceiling(maxX) - x),
            Math.Max(1, (int)MathF.Ceiling(maxY) - y));
    }
}
