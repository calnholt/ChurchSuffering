using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class HandDisplaySystemTests : IDisposable
{
    public HandDisplaySystemTests()
    {
        EventManager.Clear();
        Game1.VirtualWidth = 1920;
        Game1.VirtualHeight = 1080;
    }

    public void Dispose()
    {
        EventManager.Clear();
        Game1.VirtualWidth = 1920;
        Game1.VirtualHeight = 1080;
    }

    [Fact]
    public void Hovered_card_snaps_to_full_scale_zero_rotation_and_bottom_anchor()
    {
        var entityManager = BuildBattleHand(4, out var cards);
        var display = new HandDisplaySystem(entityManager, null)
        {
            HandHoverScale = 0.8f,
            HandFanMaxAngleDeg = 12f,
        };
        display.Update(Frame());
        var hovered = cards[1];
        float normalRotation = hovered.GetComponent<Transform>().Rotation;
        hovered.GetComponent<UIElement>().IsHovered = true;

        display.Update(Frame());

        var transform = hovered.GetComponent<Transform>();
        var bounds = hovered.GetComponent<UIElement>().Bounds;
        var targetRect = CardGeometryService.GetVisualRect((CardGeometrySettings)null, hovered.GetComponent<PositionTween>().Target, 1.1f);
        Assert.NotEqual(0f, normalRotation);
        Assert.Equal(1.1f, transform.Scale.X, 3);
        Assert.Equal(1.1f, transform.Scale.Y, 3);
        Assert.Equal(0f, transform.Rotation, 3);
        Assert.Equal((int)Math.Round(CardGeometrySettings.DefaultWidth * 1.1f), bounds.Width);
        Assert.InRange(targetRect.Bottom, Game1.VirtualHeight - 8, Game1.VirtualHeight + 8);
    }

    [Fact]
    public void Hovering_card_fans_neighbor_targets_away_then_returns_them_when_hover_clears()
    {
        var entityManager = BuildBattleHand(5, out var cards);
        var display = new HandDisplaySystem(entityManager, null)
        {
            HandHoverScale = 0.8f,
            HandFanMaxAngleDeg = 12f,
        };
        display.Update(Frame());
        float normalLeftX = cards[1].GetComponent<PositionTween>().Target.X;
        float normalRightX = cards[3].GetComponent<PositionTween>().Target.X;
        float normalLeftRotation = cards[1].GetComponent<Transform>().Rotation;
        float normalRightRotation = cards[3].GetComponent<Transform>().Rotation;
        cards[2].GetComponent<UIElement>().IsHovered = true;

        display.Update(Frame());

        Assert.True(cards[1].GetComponent<PositionTween>().Target.X < normalLeftX);
        Assert.True(cards[3].GetComponent<PositionTween>().Target.X > normalRightX);
        Assert.Equal(normalLeftRotation, cards[1].GetComponent<Transform>().Rotation, 3);
        Assert.Equal(normalRightRotation, cards[3].GetComponent<Transform>().Rotation, 3);

        cards[2].GetComponent<UIElement>().IsHovered = false;
        display.Update(Frame());

        Assert.Equal(normalLeftX, cards[1].GetComponent<PositionTween>().Target.X, 3);
        Assert.Equal(normalRightX, cards[3].GetComponent<PositionTween>().Target.X, 3);
    }

    [Fact]
    public void Weapon_at_index_zero_gets_hover_z_boost_and_stays_behind_neighbors_at_rest()
    {
        var entityManager = BuildBattleHand(3, out var cards, weaponAtIndexZero: true);
        var display = new HandDisplaySystem(entityManager, null)
        {
            HandZBase = 100,
            HandZStep = 1,
            HandZHoverBoost = 1000,
        };
        var weapon = cards[0];
        var neighbor = cards[1];

        display.Update(Frame());

        int weaponZ = weapon.GetComponent<Transform>().ZOrder;
        int neighborZ = neighbor.GetComponent<Transform>().ZOrder;
        Assert.True(weaponZ < neighborZ);
        Assert.Equal(100, weaponZ);
        Assert.Equal(101, neighborZ);

        weapon.GetComponent<UIElement>().IsHovered = true;
        display.Update(Frame());

        weaponZ = weapon.GetComponent<Transform>().ZOrder;
        Assert.Equal(1100, weaponZ);
        foreach (var card in cards.Skip(1))
        {
            Assert.True(weaponZ > card.GetComponent<Transform>().ZOrder);
        }
    }

    [Fact]
    public void Scale_tweens_down_after_hover_clears_without_tweening_up()
    {
        var entityManager = BuildBattleHand(3, out var cards);
        var display = new HandDisplaySystem(entityManager, null)
        {
            HandHoverScale = 0.8f,
        };
        var hovered = cards[1];
        hovered.GetComponent<UIElement>().IsHovered = true;

        display.Update(Frame());

        Assert.Equal(1.1f, hovered.GetComponent<Transform>().Scale.X, 3);

        hovered.GetComponent<UIElement>().IsHovered = false;
        display.Update(Frame());
        float firstReturnScale = hovered.GetComponent<Transform>().Scale.X;

        Assert.InRange(firstReturnScale, 0.8f, 1.1f);
        Assert.NotEqual(0.8f, firstReturnScale, 3);

        for (int i = 0; i < 60; i++)
        {
            display.Update(Frame());
        }

        Assert.Equal(0.8f, hovered.GetComponent<Transform>().Scale.X, 2);
    }

    private static EntityManager BuildBattleHand(int cardCount, out List<Entity> cards, bool weaponAtIndexZero = false)
    {
        var entityManager = new EntityManager();
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
        cards = new List<Entity>();

        for (int i = 0; i < cardCount; i++)
        {
            var card = entityManager.CreateEntity($"Card_{i}");
            bool isWeapon = weaponAtIndexZero && i == 0;
            entityManager.AddComponent(card, new CardData
            {
                Card = new CardBase
                {
                    CardId = isWeapon ? "weapon" : $"test_card_{i}",
                    Name = isWeapon ? "Weapon" : $"Test {i}",
                    IsWeapon = isWeapon,
                },
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
        return new GameTime(TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(1d / 60d));
    }
}
