using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Services
{
    public readonly struct CardVisualGeometry
    {
        public Rectangle Bounds { get; init; }
        public Vector2 Center { get; init; }
        public float Scale { get; init; }
        public float Rotation { get; init; }
    }

    public static class CardGeometryService
    {
        public static CardGeometrySettings GetSettings(EntityManager entityManager)
        {
            return entityManager
                .GetEntitiesWithComponent<CardGeometrySettings>()
                .FirstOrDefault()
                ?.GetComponent<CardGeometrySettings>();
        }

        public static Rectangle GetVisualRect(EntityManager entityManager, Vector2 position, float scale = 1f)
        {
            return GetVisualRect(GetSettings(entityManager), position, scale);
        }

        public static Rectangle GetVisualRect(CardGeometrySettings settings, Vector2 position, float scale = 1f)
        {
            int width = settings?.CardWidth ?? CardGeometrySettings.DefaultWidth;
            int height = settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
            int offsetYExtra = settings?.CardOffsetYExtra ?? CardGeometrySettings.DefaultOffsetYExtra;

            int scaledWidth = (int)Math.Round(width * scale);
            int scaledHeight = (int)Math.Round(height * scale);
            int scaledOffsetY = (int)Math.Round(offsetYExtra * scale);

            return new Rectangle(
                (int)position.X - scaledWidth / 2,
                (int)position.Y - (scaledHeight / 2 + scaledOffsetY),
                scaledWidth,
                scaledHeight);
        }

        public static Vector2 GetVisualCenter(EntityManager entityManager, Vector2 position, float scale = 1f)
        {
            return GetVisualCenter(GetSettings(entityManager), position, scale);
        }

        public static Vector2 GetVisualCenter(CardGeometrySettings settings, Vector2 position, float scale = 1f)
        {
            var rect = GetVisualRect(settings, position, scale);
            return new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
        }

        public static CardVisualGeometry GetVisualGeometry(
            EntityManager entityManager,
            Entity card,
            Vector2? positionOverride = null,
            float? scaleOverride = null,
            float? rotationOverride = null)
        {
            var settings = GetSettings(entityManager);
            var transform = card?.GetComponent<Transform>();

            Vector2 position = positionOverride ?? transform?.Position ?? Vector2.Zero;
            float scale = scaleOverride ?? transform?.Scale.X ?? 1f;
            float rotation = rotationOverride ?? transform?.Rotation ?? 0f;

            var bounds = GetVisualRect(settings, position, scale);
            return new CardVisualGeometry
            {
                Bounds = bounds,
                Center = new Vector2(bounds.X + bounds.Width / 2f, bounds.Y + bounds.Height / 2f),
                Scale = scale,
                Rotation = rotation,
            };
        }
    }
}
