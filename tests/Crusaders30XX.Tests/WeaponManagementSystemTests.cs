using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests;

public class WeaponManagementSystemTests : IDisposable
{
    public WeaponManagementSystemTests()
    {
        EventManager.Clear();
    }

    public void Dispose()
    {
        EventManager.Clear();
    }

    [Fact]
    public void Weapon_removed_from_hand_hides_hit_target_and_no_longer_blocks_hand_clicks()
    {
        var entityManager = new EntityManager();
        var scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());

        var phaseEntity = entityManager.CreateEntity("PhaseState");
        entityManager.AddComponent(phaseEntity, new PhaseState
        {
            Main = MainPhase.EnemyTurn,
            Sub = SubPhase.Block,
        });

        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());

        var weapon = CreateHandCard(
            entityManager,
            "Weapon",
            new Sword(),
            zOrder: 100,
            bounds: new Rectangle(700, 820, 228, 320));
        var handCard = CreateHandCard(
            entityManager,
            "HandCard",
            new CardBase { CardId = "strike" },
            zOrder: 101,
            bounds: new Rectangle(759, 835, 228, 320));

        deck.Hand.Add(weapon);
        deck.Hand.Add(handCard);

        entityManager.AddComponent(player, new EquippedWeapon
        {
            WeaponId = "sword",
            SpawnedEntity = weapon,
        });

        _ = new WeaponManagementSystem(entityManager);
        EventManager.Publish(new ChangeBattlePhaseEvent
        {
            Current = SubPhase.EnemyStart,
            Previous = SubPhase.PlayerEnd,
        });

        var weaponUi = weapon.GetComponent<UIElement>();
        Assert.DoesNotContain(weapon, deck.Hand);
        Assert.False(weaponUi.IsInteractable);
        Assert.True(weaponUi.IsHidden);
        Assert.Equal(new Rectangle(-1000, -1000, 1, 1), weaponUi.Bounds);

        var pointer = new Vector2(850, 900);
        var inputSource = new FakeInputSource(
            Frame(sequence: 1, pointer: pointer),
            Frame(
                sequence: 2,
                pointer: pointer,
                down: PlayerInputFrame.Mask(PlayerButton.Primary),
                pressed: PlayerInputFrame.Mask(PlayerButton.Primary)));
        var input = new PlayerInputSystem(entityManager, inputSource);
        var interaction = new UIInteractionSystem(entityManager);
        int assignments = 0;
        EventManager.Subscribe<AssignCardAsBlockRequested>(evt =>
        {
            Assert.Same(handCard, evt.Card);
            assignments++;
        });

        input.Update(new GameTime());
        interaction.Update(new GameTime());
        Assert.Same(handCard, GetPlayerInputState(entityManager).CursorTarget.Entity);

        input.Update(new GameTime());
        interaction.Update(new GameTime());
        Assert.Equal(1, assignments);
        Assert.True(handCard.GetComponent<UIElement>().IsClicked);
    }

    [Fact]
    public void Weapon_re_added_to_hand_restores_hit_target_visibility()
    {
        var entityManager = new EntityManager();
        var scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());

        var deckEntity = entityManager.CreateEntity("Deck");
        var deck = new Deck();
        entityManager.AddComponent(deckEntity, deck);

        var player = entityManager.CreateEntity("Player");
        entityManager.AddComponent(player, new Player());

        var weapon = CreateHandCard(
            entityManager,
            "Weapon",
            new Sword(),
            zOrder: 100,
            bounds: new Rectangle(700, 820, 228, 320));
        deck.Hand.Add(weapon);

        entityManager.AddComponent(player, new EquippedWeapon
        {
            WeaponId = "sword",
            SpawnedEntity = weapon,
        });

        _ = new WeaponManagementSystem(entityManager);
        EventManager.Publish(new ChangeBattlePhaseEvent
        {
            Current = SubPhase.EnemyStart,
            Previous = SubPhase.PlayerEnd,
        });

        EventManager.Publish(new ChangeBattlePhaseEvent
        {
            Current = SubPhase.Action,
            Previous = SubPhase.EnemyEnd,
        });

        var weaponUi = weapon.GetComponent<UIElement>();
        Assert.Contains(weapon, deck.Hand);
        Assert.False(weaponUi.IsHidden);
        Assert.True(weaponUi.IsInteractable);
    }

    private static Entity CreateHandCard(
        EntityManager entityManager,
        string name,
        CardBase card,
        int zOrder,
        Rectangle bounds)
    {
        var entity = entityManager.CreateEntity(name);
        entityManager.AddComponent(entity, new CardData { Card = card });
        entityManager.AddComponent(entity, new Transform
        {
            Position = new Vector2(bounds.Center.X, bounds.Center.Y),
            ZOrder = zOrder,
        });
        entityManager.AddComponent(entity, new UIElement
        {
            Bounds = bounds,
            IsInteractable = true,
            TooltipType = TooltipType.Card,
            EventType = UIElementEventType.CardClicked,
        });
        return entity;
    }

    private static PlayerInputState GetPlayerInputState(EntityManager entityManager)
    {
        return entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single()
            .GetComponent<PlayerInputState>();
    }

    private static PlayerInputFrame Frame(
        long sequence = 1,
        Vector2 pointer = default,
        PlayerButtonMask down = PlayerButtonMask.None,
        PlayerButtonMask pressed = PlayerButtonMask.None)
    {
        return new PlayerInputFrame(
            sequence,
            true,
            false,
            PlayerInputDevice.KeyboardMouse,
            PlayerInputDevice.KeyboardMouse,
            GamepadGlyphStyle.Xbox,
            pointer,
            Vector2.Zero,
            0f,
            Vector2.Zero,
            Vector2.Zero,
            0f,
            0f,
            down,
            pressed,
            PlayerButtonMask.None);
    }

    private sealed class FakeInputSource : IPlayerInputSource
    {
        private readonly Queue<PlayerInputFrame> _frames;

        public FakeInputSource(params PlayerInputFrame[] frames)
        {
            _frames = new Queue<PlayerInputFrame>(frames);
        }

        public PlayerInputFrame Capture(
            bool isWindowActive,
            Rectangle renderDestination,
            int virtualWidth,
            int virtualHeight)
        {
            return _frames.Dequeue();
        }

        public void SetVibration(float lowFrequency, float highFrequency)
        {
        }
    }
}
