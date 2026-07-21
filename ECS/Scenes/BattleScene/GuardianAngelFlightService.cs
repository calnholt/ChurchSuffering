using System;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Systems
{
    internal readonly record struct GuardianFlightSample(Vector2 Offset, float ScaleMultiplier, float SparkleMultiplier);

    internal static class GuardianAngelFlightService
    {
        public static GuardianFlightSample SampleGesture(GuardianFlightGesture gesture, float elapsed, float duration)
        {
            if (gesture == GuardianFlightGesture.None || duration <= 0f)
                return new GuardianFlightSample(Vector2.Zero, 1f, 1f);

            float progress = MathHelper.Clamp(elapsed / duration, 0f, 1f);
            float arc = MathF.Sin(progress * MathF.PI);
            Vector2 offset = gesture switch
            {
                GuardianFlightGesture.CardHop => new Vector2(MathF.Sin(progress * MathF.Tau) * 12f, -arc * 28f),
                GuardianFlightGesture.MedalLoop => new Vector2(MathF.Sin(progress * MathF.Tau) * 34f, -arc * 22f),
                GuardianFlightGesture.EnemyBrace => new Vector2(-arc * 30f, MathF.Sin(progress * MathF.Tau) * 8f),
                GuardianFlightGesture.Flourish => new Vector2(MathF.Sin(progress * MathF.Tau) * 20f, -arc * 18f),
                _ => Vector2.Zero,
            };
            return new GuardianFlightSample(offset, 1f + arc * 0.08f, 1f + arc * 0.8f);
        }

        public static Vector2 ClampToPlayerSideBounds(Vector2 offset, float boundsX, float boundsY) =>
            new(
                MathHelper.Clamp(offset.X, -MathF.Max(1f, boundsX), MathF.Max(1f, boundsX)),
                MathHelper.Clamp(offset.Y, -MathF.Max(1f, boundsY), MathF.Max(1f, boundsY)));
    }
}
