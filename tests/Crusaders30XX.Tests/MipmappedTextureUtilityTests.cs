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

    [Fact]
    public void Soften_StrengthZero_PreservesPixels()
    {
        var source = new[]
        {
            new Color(255, 0, 0, 255),
            new Color(0, 255, 0, 255),
            new Color(0, 0, 255, 255),
            new Color(255, 255, 0, 255),
        };

        var result = MipmappedTextureUtility.Soften(source, 2, 2, 0f);

        Assert.Same(source, result);
    }

    [Fact]
    public void Soften_UniformOpaque_Unchanged()
    {
        var source = new Color[9];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = new Color(200, 100, 50, 255);
        }

        var result = MipmappedTextureUtility.Soften(source, 3, 3, 1f);

        Assert.Equal(9, result.Length);
        for (int i = 0; i < result.Length; i++)
        {
            Assert.Equal(new Color(200, 100, 50, 255), result[i]);
        }
    }

    [Fact]
    public void Soften_HighContrastCenter_MovesTowardNeighbors()
    {
        // 3x3: black surround, white center
        var source = new Color[9];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = Color.Black;
        }
        source[4] = Color.White;

        var result = MipmappedTextureUtility.Soften(source, 3, 3, 1f);

        Assert.True(result[4].R < 255, "Center should darken toward neighbors");
        Assert.True(result[4].R > 0, "Center should not become fully black");
    }

    [Fact]
    public void Soften_FractionalStrength_ScalesContinuously()
    {
        var source = new Color[9];
        for (int i = 0; i < source.Length; i++)
        {
            source[i] = Color.Black;
        }
        source[4] = Color.White;

        var light = MipmappedTextureUtility.Soften(source, 3, 3, 0.25f);
        var heavy = MipmappedTextureUtility.Soften(source, 3, 3, 0.75f);
        var full = MipmappedTextureUtility.Soften(source, 3, 3, 1f);

        Assert.True(light[4].R < 255, "0.25 should soften center");
        Assert.True(light[4].R > heavy[4].R, "0.25 should soften less than 0.75");
        Assert.True(heavy[4].R > full[4].R, "0.75 should soften less than 1.0");
    }
}
