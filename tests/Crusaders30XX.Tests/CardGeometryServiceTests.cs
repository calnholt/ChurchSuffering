using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CardGeometryServiceTests
{
    [Fact]
    public void GetVisualGeometry_uses_transform_position_scale_and_rotation()
    {
        var entityManager = BuildEntityManagerWithSettings();
        var card = CreateCard(entityManager, new Vector2(400f, 300f), 0.85f, 0.25f);

        var geometry = CardGeometryService.GetVisualGeometry(entityManager, card);
        var expectedBounds = CardGeometryService.GetVisualRect(
            CardGeometryService.GetSettings(entityManager),
            new Vector2(400f, 300f),
            0.85f);

        Assert.Equal(expectedBounds, geometry.Bounds);
        Assert.Equal(new Vector2(expectedBounds.X + expectedBounds.Width / 2f, expectedBounds.Y + expectedBounds.Height / 2f), geometry.Center);
        Assert.Equal(0.85f, geometry.Scale, 3);
        Assert.Equal(0.25f, geometry.Rotation, 3);
    }

    [Fact]
    public void GetVisualGeometry_overrides_match_get_visual_rect()
    {
        var entityManager = BuildEntityManagerWithSettings();
        var card = CreateCard(entityManager, new Vector2(100f, 100f), 0.5f, 0f);
        var settings = CardGeometryService.GetSettings(entityManager);
        var overridePosition = new Vector2(500f, 420f);
        const float overrideScale = 1.25f;
        const float overrideRotation = 0.75f;

        var geometry = CardGeometryService.GetVisualGeometry(
            entityManager,
            card,
            overridePosition,
            overrideScale,
            overrideRotation);
        var expectedBounds = CardGeometryService.GetVisualRect(settings, overridePosition, overrideScale);

        Assert.Equal(expectedBounds, geometry.Bounds);
        Assert.Equal(overrideScale, geometry.Scale, 3);
        Assert.Equal(overrideRotation, geometry.Rotation, 3);
    }

    [Fact]
    public void GetVisualGeometry_without_transform_falls_back_conservatively()
    {
        var entityManager = BuildEntityManagerWithSettings();
        var card = entityManager.CreateEntity("NoTransformCard");

        var geometry = CardGeometryService.GetVisualGeometry(entityManager, card);
        var expectedBounds = CardGeometryService.GetVisualRect(
            CardGeometryService.GetSettings(entityManager),
            Vector2.Zero,
            1f);

        Assert.Equal(expectedBounds, geometry.Bounds);
        Assert.Equal(1f, geometry.Scale, 3);
        Assert.Equal(0f, geometry.Rotation, 3);
    }

    [Fact]
    public void Resting_hand_card_geometry_matches_ui_bounds_after_layout()
    {
        var entityManager = BuildBattleHand(3, out var cards);
        var display = new HandDisplaySystem(entityManager, null)
        {
            HandHoverScale = 0.85f,
        };

        display.Update(Frame());

        var card = cards[1];
        var uiBounds = card.GetComponent<UIElement>().Bounds;
        var geometry = CardGeometryService.GetVisualGeometry(entityManager, card);

        Assert.Equal(0.85f, card.GetComponent<Transform>().Scale.X, 3);
        Assert.Equal(uiBounds, geometry.Bounds);
    }

    [Fact]
    public void Hovered_hand_card_geometry_uses_full_scale()
    {
        var entityManager = BuildBattleHand(3, out var cards);
        var display = new HandDisplaySystem(entityManager, null)
        {
            HandHoverScale = 0.85f,
        };
        display.Update(Frame());

        var hovered = cards[1];
        hovered.GetComponent<UIElement>().IsHovered = true;
        display.Update(Frame());

        var geometry = CardGeometryService.GetVisualGeometry(entityManager, hovered);
        var settings = CardGeometryService.GetSettings(entityManager);

        Assert.Equal(1.1f, geometry.Scale, 3);
        Assert.Equal((int)Math.Round(CardGeometrySettings.DefaultWidth * 1.1f), geometry.Bounds.Width);
        Assert.Equal(
            CardGeometryService.GetVisualRect(settings, hovered.GetComponent<Transform>().Position, 1.1f),
            geometry.Bounds);
    }

    private static EntityManager BuildEntityManagerWithSettings()
    {
        var entityManager = new EntityManager();
        CreateDefaultCardGeometrySettings(entityManager);
        return entityManager;
    }

    private static void CreateDefaultCardGeometrySettings(EntityManager entityManager)
    {
        var settingsEntity = entityManager.CreateEntity("CardGeometrySettings");
        entityManager.AddComponent(settingsEntity, new CardGeometrySettings
        {
            CardWidth = CardGeometrySettings.DefaultWidth,
            CardHeight = CardGeometrySettings.DefaultHeight,
            CardOffsetYExtra = CardGeometrySettings.DefaultOffsetYExtra,
            CardGap = CardGeometrySettings.DefaultGap,
            CardCornerRadius = CardGeometrySettings.DefaultCornerRadius,
            HighlightBorderThickness = CardGeometrySettings.DefaultHighlightBorderThickness,
        });
    }

    private static Entity CreateCard(EntityManager entityManager, Vector2 position, float scale, float rotation)
    {
        var card = entityManager.CreateEntity("Card");
        entityManager.AddComponent(card, new Transform
        {
            Position = position,
            Scale = new Vector2(scale, scale),
            Rotation = rotation,
        });
        return card;
    }

    private static EntityManager BuildBattleHand(int cardCount, out System.Collections.Generic.List<Entity> cards)
    {
        var entityManager = new EntityManager();
        CreateDefaultCardGeometrySettings(entityManager);

        var scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState { Current = SceneId.Battle });
        var phase = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phase, new PhaseState
        {
            Main = MainPhase.PlayerTurn,
            Sub = SubPhase.Action,
        });
        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);
        cards = new System.Collections.Generic.List<Entity>();

        for (int i = 0; i < cardCount; i++)
        {
            var card = entityManager.CreateEntity($"Card_{i}");
            entityManager.AddComponent(card, new CardData
            {
                Card = new ECS.Objects.Cards.CardBase { CardId = $"test_card_{i}", Name = $"Test {i}" },
            });
            entityManager.AddComponent(card, new Transform());
            entityManager.AddComponent(card, new UIElement { IsInteractable = true });
            entityManager.AddComponent(card, new PositionTween());
            deck.Hand.Add(card);
            cards.Add(card);
        }

        return entityManager;
    }

    private static GameTime Frame()
    {
        return new GameTime(System.TimeSpan.FromSeconds(1), System.TimeSpan.FromSeconds(1d / 60d));
    }
}
