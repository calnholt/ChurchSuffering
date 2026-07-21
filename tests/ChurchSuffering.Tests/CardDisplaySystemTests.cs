using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class CardDisplaySystemTests
{
	[Fact]
	public void CardRenderScaledEvent_defaults_to_zero_rotation()
	{
		Assert.Equal(0f, new CardRenderScaledEvent().Rotation);
	}

	[Fact]
	public void CardRenderScaledEvent_does_not_prefer_cached_base_by_default()
	{
		Assert.False(new CardRenderScaledEvent().PreferCachedBase);
	}

	[Fact]
	public void CardRenderEvent_does_not_prefer_cached_base_by_default()
	{
		Assert.False(new CardRenderEvent().PreferCachedBase);
	}

	[Theory]
	[InlineData(1f, 0, true)]
	[InlineData(0.5f, 0, false)]
	[InlineData(1f, 1, false)]
	public void Base_cache_eligibility_bypasses_faded_and_animated_presentations(
		float alpha,
		int waivedPipCount,
		bool expected)
	{
		Assert.Equal(expected, CardDisplaySystem.IsBaseCacheEligible(alpha, waivedPipCount));
	}

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
