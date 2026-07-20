using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class HandStateLoggingServiceTests
{
    [Fact]
    public void BuildHandSnapshot_flags_hidden_card_that_still_counts_for_draw()
    {
        var entityManager = new EntityManager();
        var deck = new Deck();
        var filteredCard = CreateCard(entityManager, new Strike());
        entityManager.AddComponent(filteredCard, new FilteredFromHand());
        deck.Hand.Add(filteredCard);

        var snapshot = HandStateLoggingService.BuildHandSnapshot(deck, "test", SubPhase.EnemyStart);

        Assert.False(HandStateLoggingService.CountsForHandLayout(filteredCard));
        Assert.True(HandStateLoggingService.CountsForDraw(filteredCard));
        Assert.Equal("FilteredFromHand", HandStateLoggingService.GetLayoutExclusionReason(filteredCard));
        Assert.Equal(1, snapshot["deckHandCount"]!.GetValue<int>());
        Assert.Equal(0, snapshot["visibleHandCount"]!.GetValue<int>());
        Assert.Equal(1, snapshot["effectiveDrawHandCount"]!.GetValue<int>());
        Assert.True(snapshot["mismatch"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildHandSnapshot_marks_pledged_cards_visible_but_not_counted_for_draw()
    {
        var entityManager = new EntityManager();
        var deck = new Deck();
        var pledgedCard = CreateCard(entityManager, new Strike());
        entityManager.AddComponent(pledgedCard, new Pledge { CanPlay = false });
        deck.Hand.Add(pledgedCard);

        var snapshot = HandStateLoggingService.BuildHandSnapshot(deck, "test", SubPhase.EnemyStart);

        Assert.True(HandStateLoggingService.CountsForHandLayout(pledgedCard));
        Assert.False(HandStateLoggingService.CountsForDraw(pledgedCard));
        Assert.Equal("Visible", HandStateLoggingService.GetLayoutExclusionReason(pledgedCard));
        Assert.Equal(1, snapshot["deckHandCount"]!.GetValue<int>());
        Assert.Equal(1, snapshot["visibleHandCount"]!.GetValue<int>());
        Assert.Equal(0, snapshot["effectiveDrawHandCount"]!.GetValue<int>());
        Assert.True(snapshot["mismatch"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildHandSnapshot_marks_token_cards_visible_but_not_counted_for_draw()
    {
        var entityManager = new EntityManager();
        var deck = new Deck();
        var tokenCard = CreateCard(entityManager, new CardBase { CardId = "token", IsToken = true });
        deck.Hand.Add(tokenCard);

        var snapshot = HandStateLoggingService.BuildHandSnapshot(deck, "test", SubPhase.EnemyStart);

        Assert.True(HandStateLoggingService.CountsForHandLayout(tokenCard));
        Assert.False(HandStateLoggingService.CountsForDraw(tokenCard));
        Assert.Equal("Visible", HandStateLoggingService.GetLayoutExclusionReason(tokenCard));
        Assert.Equal("token", HandStateLoggingService.GetDrawCountReason(tokenCard));
        Assert.Equal(1, snapshot["deckHandCount"]!.GetValue<int>());
        Assert.Equal(1, snapshot["visibleHandCount"]!.GetValue<int>());
        Assert.Equal(0, snapshot["effectiveDrawHandCount"]!.GetValue<int>());
        Assert.True(snapshot["mismatch"]!.GetValue<bool>());
    }

    [Fact]
    public void BuildCardSnapshot_includes_input_routing_state()
    {
        var entityManager = new EntityManager();
        var card = CreateCard(entityManager, new Strike());
        var ui = card.GetComponent<UIElement>();
        ui.IsHidden = true;
        ui.LayerType = UILayerType.Overlay;
        ui.IsPreventDefaultClick = true;
        entityManager.AddComponent(card, new InputContextMember { ContextId = "overlay.card-list" });

        var snapshot = HandStateLoggingService.BuildCardSnapshot(card);

        Assert.True(snapshot.ContainsKey("isHidden"), snapshot.ToJsonString());
        Assert.True(snapshot["isHidden"]!.GetValue<bool>());
        Assert.Equal("Overlay", snapshot["uiLayerType"]!.GetValue<string>());
        Assert.True(snapshot["isPreventDefaultClick"]!.GetValue<bool>());
        Assert.Equal("overlay.card-list", snapshot["inputContextId"]!.GetValue<string>());
        Assert.Equal(0, snapshot["zOrder"]!.GetValue<int>());
    }

    private static Entity CreateCard(EntityManager entityManager, CardBase card)
    {
        var entity = entityManager.CreateEntity(card.CardId);
        entityManager.AddComponent(entity, new CardData { Card = card });
        entityManager.AddComponent(entity, new Transform { Position = new Vector2(10, 20) });
        entityManager.AddComponent(entity, new UIElement { Bounds = new Rectangle(1, 2, 3, 4), IsInteractable = true });
        return entity;
    }
}
