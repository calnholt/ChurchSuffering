using System.Collections.Generic;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class CursorTrailDisplaySystemTests
{
    private readonly List<Vector2> _positions = new();

    [Fact]
    public void First_position_produces_one_stamp()
    {
        var current = new Vector2(20f, 30f);

        CursorTrailDisplaySystem.BuildStampPositions(_positions, null, current, 10f, 800f);

        Assert.Equal(new[] { current }, _positions);
    }

    [Fact]
    public void Stationary_cursor_produces_one_stamp()
    {
        var current = new Vector2(20f, 30f);

        CursorTrailDisplaySystem.BuildStampPositions(_positions, current, current, 10f, 800f);

        Assert.Equal(new[] { current }, _positions);
    }

    [Theory]
    [InlineData(5f, 1)]
    [InlineData(10f, 1)]
    [InlineData(11f, 2)]
    [InlineData(25f, 3)]
    public void Movement_is_sampled_at_no_more_than_the_requested_spacing(float distance, int expectedCount)
    {
        var start = Vector2.Zero;
        var end = new Vector2(distance, 0f);

        CursorTrailDisplaySystem.BuildStampPositions(_positions, start, end, 10f, 800f);

        Assert.Equal(expectedCount, _positions.Count);
        Assert.Equal(end, _positions[^1]);
        Vector2 previous = start;
        foreach (Vector2 position in _positions)
        {
            Assert.True(Vector2.Distance(previous, position) <= 10.001f);
            previous = position;
        }
    }

    [Fact]
    public void Movement_over_bridge_threshold_starts_a_new_segment()
    {
        var end = new Vector2(801f, 0f);

        CursorTrailDisplaySystem.BuildStampPositions(_positions, Vector2.Zero, end, 10f, 800f);

        Assert.Equal(new[] { end }, _positions);
    }

    [Fact]
    public void Decay_is_equivalent_across_frame_rates()
    {
        float decayAtSixtyFps = CursorTrailDisplaySystem.CalculateFrameDecay(0.95f, 1f / 60f);
        float decayAtThirtyFps = CursorTrailDisplaySystem.CalculateFrameDecay(0.95f, 1f / 30f);

        Assert.Equal(0.95f, decayAtSixtyFps, 5);
        Assert.Equal(decayAtSixtyFps * decayAtSixtyFps, decayAtThirtyFps, 5);
    }
}
