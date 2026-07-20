using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardListPresentationTests
{
    [Theory]
    [InlineData(0, 2)]
    [InlineData(450, 4)]
    [InlineData(1200, 5)]
    public void Visible_row_range_includes_one_overscan_row(int scrollOffset, int expectedLastRow)
    {
        var range = CardListModalSystem.CalculateOverscanRowRange(
            cardCount: 60,
            columns: 4,
            rowStride: 400,
            scrollOffset,
            viewportHeight: 800);

        Assert.Equal(expectedLastRow, range.LastRow);
        Assert.Equal(Math.Max(0, scrollOffset / 400 - 1), range.FirstRow);
    }

    [Fact]
    public void Ordering_uses_name_then_color_then_entity_id_and_ignores_invalid_entries()
    {
        var entityManager = new EntityManager();
        Entity beta = Card(entityManager, "Beta", CardData.CardColor.White);
        Entity alphaRed = Card(entityManager, "Alpha", CardData.CardColor.Red);
        Entity alphaBlack = Card(entityManager, "Alpha", CardData.CardColor.Black);
        Entity invalid = entityManager.CreateEntity("invalid");

        List<Entity> ordered = CardListModalSystem.OrderCardsForPresentation(
            new[] { beta, invalid, alphaRed, null, alphaBlack });

        Assert.Equal(new[] { alphaBlack, alphaRed, beta }, ordered);
    }

    [Fact]
    public void Releasing_non_hand_modal_card_restores_visibility_but_not_interaction()
    {
        var entityManager = new EntityManager();
        Entity card = Card(entityManager, "Hidden", CardData.CardColor.Black);
        entityManager.AddComponent(card, new UIElement
        {
            Bounds = new Rectangle(100, 100, 200, 300),
            IsInteractable = true,
            IsHovered = true,
            IsClicked = true,
            IsHidden = true,
            LayerType = UILayerType.Overlay,
        });
        entityManager.AddComponent(card, new InputContextMember { ContextId = "overlay.card-list" });
        entityManager.AddComponent(card, new CardListModalSelectionMetadata());

        CardListModalSystem.ReleaseCardFromModal(entityManager, card);

        UIElement ui = card.GetComponent<UIElement>();
        Assert.False(card.HasComponent<InputContextMember>());
        Assert.False(card.HasComponent<CardListModalSelectionMetadata>());
        Assert.False(ui.IsHidden);
        Assert.False(ui.IsInteractable);
        Assert.False(ui.IsHovered);
        Assert.False(ui.IsClicked);
        Assert.Equal(UILayerType.Default, ui.LayerType);
        Assert.Equal(Rectangle.Empty, ui.Bounds);
    }

    [Fact]
    public void Releasing_modal_card_that_moved_to_hand_restores_interaction()
    {
        var entityManager = new EntityManager();
        Entity deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        Entity card = Card(entityManager, "Drawn", CardData.CardColor.Red);
        entityManager.AddComponent(card, new UIElement
        {
            Bounds = new Rectangle(100, 100, 200, 300),
            IsInteractable = false,
            IsHovered = true,
            IsClicked = true,
            IsHidden = true,
            LayerType = UILayerType.Overlay,
            EventType = UIElementEventType.CardClicked,
        });
        entityManager.AddComponent(card, new InputContextMember { ContextId = "overlay.card-list" });
        deck.Hand.Add(card);

        CardListModalSystem.ReleaseCardFromModal(entityManager, card);

        UIElement ui = card.GetComponent<UIElement>();
        Assert.False(card.HasComponent<InputContextMember>());
        Assert.True(ui.BaseInteractable);
        Assert.True(ui.IsInteractable);
        Assert.False(ui.IsHidden);
        Assert.False(ui.IsHovered);
        Assert.False(ui.IsClicked);
        Assert.Equal(UILayerType.Default, ui.LayerType);
        Assert.Equal(UIElementEventType.CardClicked, ui.EventType);
        Assert.Equal(Rectangle.Empty, ui.Bounds);
    }

    [Fact]
    public void Releasing_transient_hand_card_does_not_restore_interaction()
    {
        var entityManager = new EntityManager();
        Entity deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        Entity card = Card(entityManager, "Animating", CardData.CardColor.White);
        entityManager.AddComponent(card, new UIElement { IsInteractable = false });
        entityManager.AddComponent(card, new AnimatingHandToZone());
        deck.Hand.Add(card);

        CardListModalSystem.ReleaseCardFromModal(entityManager, card);

        Assert.False(card.GetComponent<UIElement>().BaseInteractable);
        Assert.False(card.GetComponent<UIElement>().IsInteractable);
    }

    [Fact]
    public void Releasing_hand_card_preserves_other_input_suppression()
    {
        var entityManager = new EntityManager();
        Entity deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        Entity card = Card(entityManager, "Suppressed", CardData.CardColor.Black);
        entityManager.AddComponent(card, new UIElement
        {
            IsInteractable = false,
            SuppressCount = 1,
        });
        deck.Hand.Add(card);

        CardListModalSystem.ReleaseCardFromModal(entityManager, card);

        UIElement ui = card.GetComponent<UIElement>();
        Assert.True(ui.BaseInteractable);
        Assert.False(ui.IsInteractable);
        Assert.Equal(1, ui.SuppressCount);
    }

    [Fact]
    public void Shader_bounds_include_brittle_overflow_beyond_base_card()
    {
        var entityManager = new EntityManager();
        Entity settings = entityManager.CreateEntity("CardGeometrySettings");
        entityManager.AddComponent(settings, new CardGeometrySettings
        {
            CardWidth = CardGeometrySettings.DefaultWidth,
            CardHeight = CardGeometrySettings.DefaultHeight,
            CardOffsetYExtra = CardGeometrySettings.DefaultOffsetYExtra,
            CardGap = CardGeometrySettings.DefaultGap,
            CardCornerRadius = CardGeometrySettings.DefaultCornerRadius,
            HighlightBorderThickness = CardGeometrySettings.DefaultHighlightBorderThickness,
        });
        Entity card = Card(entityManager, "Brittle", CardData.CardColor.White);
        entityManager.AddComponent(card, new Transform());
        entityManager.AddComponent(card, new Brittle());
        var position = new Vector2(400f, 400f);

        Rectangle baseBounds = CardGeometryService.GetVisualRect(
            CardGeometryService.GetSettings(entityManager),
            position,
            1f);
        Rectangle renderBounds = CardRenderBoundsService.GetBounds(
            entityManager,
            card,
            position,
            1f,
            0f);

        Assert.True(renderBounds.Width > baseBounds.Width);
        Assert.True(renderBounds.Bottom > baseBounds.Bottom + 200);
    }

    private static Entity Card(EntityManager entityManager, string name, CardData.CardColor color)
    {
        Entity entity = entityManager.CreateEntity(name);
        entityManager.AddComponent(entity, new CardData
        {
            Color = color,
            Card = new CardBase { CardId = name.ToLowerInvariant(), Name = name },
        });
        return entity;
    }
}
