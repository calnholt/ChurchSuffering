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
