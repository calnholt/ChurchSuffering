using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class HandDisplayCacheEligibilityTests
{
    [Fact]
    public void Stable_hand_card_prefers_cached_base()
    {
        Entity card = StableCard();

        Assert.True(HandDisplaySystem.IsStableForCachedBase(card, 0.85f, 0.85f));
    }

    [Fact]
    public void Position_tween_bypasses_cache_until_settled()
    {
        Entity card = StableCard();
        card.AddComponent(new PositionTween
        {
            Initialized = true,
            Current = Vector2.Zero,
            Target = new Vector2(10f, 0f),
        });

        Assert.False(HandDisplaySystem.IsStableForCachedBase(card, 0.85f, 0.85f));

        card.GetComponent<PositionTween>().Current = card.GetComponent<PositionTween>().Target;
        Assert.True(HandDisplaySystem.IsStableForCachedBase(card, 0.85f, 0.85f));

        card.GetComponent<PositionTween>().Initialized = false;
        Assert.False(HandDisplaySystem.IsStableForCachedBase(card, 0.85f, 0.85f));
    }

    [Fact]
    public void Hover_and_scale_interpolation_bypass_cache()
    {
        Entity card = StableCard();
        card.GetComponent<UIElement>().IsHovered = true;
        Assert.False(HandDisplaySystem.IsStableForCachedBase(card, 0.85f, 0.85f));

        card.GetComponent<UIElement>().IsHovered = false;
        Assert.False(HandDisplaySystem.IsStableForCachedBase(card, 0.95f, 0.85f));
    }

    [Theory]
    [InlineData(Animation.AnimationType.Fade)]
    [InlineData(Animation.AnimationType.Scale)]
    [InlineData(Animation.AnimationType.Move)]
    [InlineData(Animation.AnimationType.Rotate)]
    public void Active_alpha_scale_move_and_rotation_animations_bypass_cache(
        Animation.AnimationType type)
    {
        Entity card = StableCard();
        card.AddComponent(new Animation { IsPlaying = true, Type = type });

        Assert.False(HandDisplaySystem.IsStableForCachedBase(card, 0.85f, 0.85f));
    }

    [Fact]
    public void Return_and_clip_related_presentations_bypass_cache()
    {
        Entity returning = StableCard();
        returning.AddComponent(new AssignedBlockPresentation
        {
            Phase = AssignedBlockPresentation.PhaseState.Returning,
        });
        Assert.False(HandDisplaySystem.IsStableForCachedBase(returning, 0.85f, 0.85f));

        Entity selected = StableCard();
        selected.AddComponent(new SelectedForPayment());
        Assert.False(HandDisplaySystem.IsStableForCachedBase(selected, 0.85f, 0.85f));

        Entity flight = StableCard();
        flight.AddComponent(new CardToDiscardFlight());
        Assert.False(HandDisplaySystem.IsStableForCachedBase(flight, 0.85f, 0.85f));

        Entity snatch = StableCard();
        snatch.AddComponent(new PlunderSnatchFlight());
        Assert.False(HandDisplaySystem.IsStableForCachedBase(snatch, 0.85f, 0.85f));

        Entity rescue = StableCard();
        rescue.AddComponent(new PlunderRescueFlight());
        Assert.False(HandDisplaySystem.IsStableForCachedBase(rescue, 0.85f, 0.85f));
    }

    private static Entity StableCard()
    {
        var card = new Entity(1) { Name = "Card" };
        card.AddComponent(new Transform
        {
            Position = new Vector2(100f, 100f),
            Scale = new Vector2(0.85f),
        });
        card.AddComponent(new UIElement());
        return card;
    }
}
