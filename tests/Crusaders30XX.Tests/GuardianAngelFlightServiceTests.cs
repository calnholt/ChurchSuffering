using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class GuardianAngelFlightServiceTests
{
    [Fact]
    public void GesturesLeaveAndReturnToAmbientPath()
    {
        GuardianFlightGesture[] gestures =
        [
            GuardianFlightGesture.CardHop,
            GuardianFlightGesture.MedalLoop,
            GuardianFlightGesture.EnemyBrace,
            GuardianFlightGesture.Flourish,
        ];
        foreach (GuardianFlightGesture gesture in gestures)
        {
            GuardianFlightSample start = GuardianAngelFlightService.SampleGesture(gesture, 0f, 1f);
            GuardianFlightSample middle = GuardianAngelFlightService.SampleGesture(gesture, 0.5f, 1f);
            GuardianFlightSample end = GuardianAngelFlightService.SampleGesture(gesture, 1f, 1f);

            Assert.Equal(Vector2.Zero, start.Offset);
            Assert.NotEqual(Vector2.Zero, middle.Offset);
            Assert.True(end.Offset.Length() < 0.001f);
            Assert.True(middle.ScaleMultiplier > 1f);
            Assert.True(middle.SparkleMultiplier > 1f);
        }
    }

    [Fact]
    public void CombinedMotionIsClampedToPlayerSideBounds()
    {
        Vector2 result = GuardianAngelFlightService.ClampToPlayerSideBounds(new Vector2(500f, -300f), 95f, 48f);
        Assert.Equal(new Vector2(95f, -48f), result);
    }
}
