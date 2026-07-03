using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardDisplaySystemTests
{
    [Fact]
    public void CreateDescriptionTextLayout_keepsWrapMetricsStableAcrossVisualScales()
    {
        var settings = new CardGeometrySettings
        {
            CardWidth = 268,
            CardHeight = 377,
            CardOffsetYExtra = -98,
            CardGap = -77,
            CardCornerRadius = 10,
            HighlightBorderThickness = 5
        };
        const float descFontScale = 0.11f;
        const int contentMarginLeft = 68;
        const int contentPadRight = 4;

        var baseline = CardDisplaySystem.CreateDescriptionTextLayout(
            settings,
            1f,
            descFontScale,
            contentMarginLeft,
            contentPadRight);

        foreach (float visualScale in new[] { 0.85f, 1f, 1.15f })
        {
            var layout = CardDisplaySystem.CreateDescriptionTextLayout(
                settings,
                visualScale,
                descFontScale,
                contentMarginLeft,
                contentPadRight);

            Assert.Equal(baseline.WrapScale, layout.WrapScale);
            Assert.Equal(baseline.WrapMaxWidth, layout.WrapMaxWidth);
            Assert.Equal(descFontScale * visualScale, layout.DrawScale, 5);
            Assert.Equal(contentMarginLeft * visualScale, layout.ContentX, 5);
            Assert.Equal(baseline.WrapMaxWidth * visualScale, layout.ContentWidth, 5);
        }
    }
}
