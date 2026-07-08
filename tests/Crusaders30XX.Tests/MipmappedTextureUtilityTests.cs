using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class MipmappedTextureUtilityTests
{
    [Fact]
    public void SampleBilinear_AtIntegerCoords_ReturnsExactPixel()
    {
        var data = new[]
        {
            Color.Red, Color.Green,
            Color.Blue, Color.Yellow,
        };

        var sample = MipmappedTextureUtility.SampleBilinear(data, 2, 2, 0f, 0f);

        Assert.Equal(Color.Red, sample);
    }

    [Fact]
    public void ResampleBilinear_2x2To2x2_PreservesPixels()
    {
        var source = new[]
        {
            new Color(255, 0, 0, 255),
            new Color(0, 255, 0, 255),
            new Color(0, 0, 255, 255),
            new Color(255, 255, 0, 255),
        };

        var result = MipmappedTextureUtility.ResampleBilinear(source, 2, 2, 2, 2);

        Assert.Equal(4, result.Length);
        Assert.Equal(source[0], result[0]);
        Assert.Equal(source[1], result[1]);
        Assert.Equal(source[2], result[2]);
        Assert.Equal(source[3], result[3]);
    }

    [Fact]
    public void DownsamplePremultiplied_HalvesDimensions_PreservesOpaqueColor()
    {
        var source = new Color[4];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = new Color(200, 100, 50, 255);
        }

        var result = MipmappedTextureUtility.DownsamplePremultiplied(source, 2, 2, 1, 1);

        Assert.Single(result);
        Assert.Equal(new Color(200, 100, 50, 255), result[0]);
    }
}
