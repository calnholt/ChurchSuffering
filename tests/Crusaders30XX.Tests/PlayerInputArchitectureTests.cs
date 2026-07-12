using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Input;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Xunit;
using EcsSystem = Crusaders30XX.ECS.Core.System;

namespace Crusaders30XX.Tests;

public class PlayerInputArchitectureTests
{
    [Fact]
    public void System_manager_executes_declared_phases_in_order()
    {
        var entityManager = new EntityManager();
        var manager = new SystemManager(entityManager);
        var calls = new List<string>();

        manager.AddSystem(new RecordingSystem(entityManager, "gameplay", calls));
        manager.AddSystem(new RecordingSystem(entityManager, "input", calls), SystemUpdatePhase.Input);
        manager.AddSystem(new RecordingSystem(entityManager, "presentation", calls), SystemUpdatePhase.Presentation);
        manager.AddSystem(new RecordingSystem(entityManager, "interaction", calls), SystemUpdatePhase.Interaction);
        manager.AddLateSystem(new RecordingSystem(entityManager, "late", calls));

        manager.Update(new GameTime());
        manager.LateUpdate(new GameTime());

        Assert.Equal(
            new[] { "input", "interaction", "gameplay", "presentation", "late" },
            calls);
        Assert.Equal(5, manager.GetAllSystems().Count());
    }

    [Fact]
    public void Input_frame_button_masks_are_immutable_and_report_edges()
    {
        PlayerButtonMask primary = PlayerInputFrame.Mask(PlayerButton.Primary);
        var pressed = Frame(
            down: primary,
            pressed: primary);
        var held = Frame(down: primary);
        var released = Frame(released: primary);

        Assert.True(pressed.IsDown(PlayerButton.Primary));
        Assert.True(pressed.WasPressed(PlayerButton.Primary));
        Assert.False(held.WasPressed(PlayerButton.Primary));
        Assert.True(released.WasReleased(PlayerButton.Primary));

        PlayerInputFrame copy = pressed with { Sequence = 2 };
        Assert.True(pressed.WasPressed(PlayerButton.Primary));
        Assert.Equal(1, pressed.Sequence);
        Assert.Equal(2, copy.Sequence);
    }

    [Fact]
    public void Player_input_system_publishes_device_transitions_and_top_cursor_target()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());

        Entity lower = CreateUi(entityManager, "Lower", 10, new Rectangle(0, 0, 100, 100));
        Entity upper = CreateUi(entityManager, "Upper", 20, new Rectangle(0, 0, 100, 100));
        var source = new MixedRumbleFakeInputSource(
            Frame(sequence: 1, pointer: new Vector2(50, 50)),
            Frame(
                sequence: 2,
                pointer: new Vector2(50, 50),
                device: PlayerInputDevice.Gamepad,
                previousDevice: PlayerInputDevice.KeyboardMouse,
                gamepadConnected: true));
        var events = new List<PlayerInputFrame>();
        EventManager.Subscribe<PlayerInputEvent>(e => events.Add(e.Frame));
        var system = new PlayerInputSystem(entityManager, source);

        system.Update(new GameTime());
        PlayerInputState state = entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single()
            .GetComponent<PlayerInputState>();
        Assert.Same(upper, state.CursorTarget.Entity);
        Assert.NotSame(lower, state.CursorTarget.Entity);

        system.Update(new GameTime());
        Assert.Equal(2, events.Count);
        Assert.True(events[1].DeviceChanged);
        Assert.Equal(PlayerInputDevice.Gamepad, events[1].Device);
        EventManager.Clear();
    }

    [Fact]
    public void Player_input_state_is_persistent_and_recovers_after_entity_destruction()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());
        var source = new MixedRumbleFakeInputSource(
            Frame(sequence: 1),
            Frame(sequence: 2));
        var system = new PlayerInputSystem(entityManager, source);

        system.Update(new GameTime());
        Entity original = entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single();
        Assert.NotNull(original.GetComponent<DontDestroyOnLoad>());

        entityManager.DestroyEntity(original.Id);
        system.Update(new GameTime());

        Entity replacement = entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single();
        Assert.NotEqual(original.Id, replacement.Id);
        Assert.Equal(2, replacement.GetComponent<PlayerInputState>().Frame.Sequence);
        Assert.NotNull(replacement.GetComponent<DontDestroyOnLoad>());
        EventManager.Clear();
    }

    [Fact]
    public void Cursor_target_ignores_non_interactable_entities_above_controls()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());
        Entity control = CreateUi(
            entityManager,
            "Control",
            10,
            new Rectangle(0, 0, 100, 100));
        Entity blocker = CreateUi(
            entityManager,
            "InactiveBlocker",
            100,
            new Rectangle(0, 0, 100, 100));
        blocker.GetComponent<UIElement>().IsInteractable = false;
        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(Frame(pointer: new Vector2(50, 50))));

        system.Update(new GameTime());

        PlayerInputState state = entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single()
            .GetComponent<PlayerInputState>();
        Assert.Same(control, state.CursorTarget.Entity);
        EventManager.Clear();
    }

    [Fact]
    public void Cursor_target_includes_non_interactable_tooltip_entities()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());
        Entity textTooltip = CreateUi(
            entityManager,
            "TextTooltip",
            10,
            new Rectangle(0, 0, 100, 100));
        UIElement textUi = textTooltip.GetComponent<UIElement>();
        textUi.IsInteractable = false;
        textUi.Tooltip = "Passive description";

        Entity cardTooltip = CreateUi(
            entityManager,
            "CardTooltip",
            10,
            new Rectangle(100, 0, 100, 100));
        UIElement cardUi = cardTooltip.GetComponent<UIElement>();
        cardUi.IsInteractable = false;
        cardUi.Tooltip = string.Empty;
        cardUi.TooltipType = TooltipType.Card;

        Entity keywordSourceTooltip = CreateUi(
            entityManager,
            "KeywordSourceTooltip",
            10,
            new Rectangle(200, 0, 100, 100));
        UIElement keywordSourceUi = keywordSourceTooltip.GetComponent<UIElement>();
        keywordSourceUi.IsInteractable = false;
        keywordSourceUi.Tooltip = string.Empty;
        keywordSourceUi.TooltipKeywordSource = "Gain 5 sharpen.";

        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(
                Frame(sequence: 1, pointer: new Vector2(50, 50)),
                Frame(sequence: 2, pointer: new Vector2(150, 50)),
                Frame(sequence: 3, pointer: new Vector2(250, 50))));
        var interaction = new UIInteractionSystem(entityManager);

        system.Update(new GameTime());
        interaction.Update(new GameTime());
        PlayerInputState state = entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single()
            .GetComponent<PlayerInputState>();
        Assert.Same(textTooltip, state.CursorTarget.Entity);
        Assert.True(textUi.IsHovered);

        system.Update(new GameTime());
        interaction.Update(new GameTime());
        Assert.Same(cardTooltip, state.CursorTarget.Entity);
        Assert.True(cardUi.IsHovered);
        Assert.False(textUi.IsHovered);

        system.Update(new GameTime());
        interaction.Update(new GameTime());
        Assert.Same(keywordSourceTooltip, state.CursorTarget.Entity);
        Assert.True(keywordSourceUi.IsHovered);
        Assert.False(cardUi.IsHovered);
        EventManager.Clear();
    }

    [Fact]
    public void Cursor_target_ignores_filtered_cards_with_stale_tooltip_bounds()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        const string payCostContext = "overlay.pay-cost";

        Entity overlayRoot = entityManager.CreateEntity("PayCostOverlay");
        entityManager.AddComponent(overlayRoot, new InputContext
        {
            Id = payCostContext,
            Priority = 600,
            IsActive = true,
        });

        Entity paymentCandidate = CreateUi(
            entityManager,
            "PaymentCandidate",
            10,
            new Rectangle(0, 0, 100, 100));
        entityManager.AddComponent(paymentCandidate, new InputContextMember
        {
            ContextId = payCostContext,
        });

        Entity filteredCard = CreateUi(
            entityManager,
            "FilteredCard",
            100,
            new Rectangle(0, 0, 100, 100));
        UIElement filteredUi = filteredCard.GetComponent<UIElement>();
        filteredUi.IsInteractable = false;
        filteredUi.TooltipType = TooltipType.Card;
        entityManager.AddComponent(filteredCard, new FilteredFromHand());
        entityManager.AddComponent(filteredCard, new InputContextMember
        {
            ContextId = payCostContext,
        });

        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(Frame(pointer: new Vector2(50, 50))));

        system.Update(new GameTime());

        Assert.Same(paymentCandidate, GetPlayerInputState(entityManager).CursorTarget.Entity);
        EventManager.Clear();
    }

    [Fact]
    public void UI_interaction_publishes_hover_enter_feedback_once_for_interactable_ui()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity control = CreateUi(
            entityManager,
            "Control",
            10,
            new Rectangle(0, 0, 100, 100));
        var source = new MixedRumbleFakeInputSource(
            Frame(sequence: 1, pointer: new Vector2(50, 50)),
            Frame(sequence: 2, pointer: new Vector2(50, 50)),
            Frame(sequence: 3, pointer: new Vector2(150, 50)));
        var input = new PlayerInputSystem(entityManager, source);
        var interaction = new UIInteractionSystem(entityManager);
        var hoverEvents = new List<UIElementHoverEnteredEvent>();
        var sfxEvents = new List<PlaySfxEvent>();
        EventManager.Subscribe<UIElementHoverEnteredEvent>(hoverEvents.Add);
        EventManager.Subscribe<PlaySfxEvent>(sfxEvents.Add);

        input.Update(new GameTime());
        interaction.Update(new GameTime());
        input.Update(new GameTime());
        interaction.Update(new GameTime());

        var hoverEvent = Assert.Single(hoverEvents);
        Assert.Same(control, hoverEvent.Entity);
        Assert.Equal(PlayerInputDevice.KeyboardMouse, hoverEvent.Source);
        var sfxEvent = Assert.Single(sfxEvents);
        Assert.Equal(SfxTrack.Interface, sfxEvent.Track);
        Assert.Equal(0.05f, sfxEvent.Volume);

        input.Update(new GameTime());
        interaction.Update(new GameTime());

        Assert.Single(hoverEvents);
        Assert.Single(sfxEvents);
        EventManager.Clear();
    }

    [Fact]
    public void UI_interaction_does_not_publish_hover_feedback_for_tooltip_only_ui()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity tooltip = CreateUi(
            entityManager,
            "TooltipOnly",
            10,
            new Rectangle(0, 0, 100, 100));
        UIElement ui = tooltip.GetComponent<UIElement>();
        ui.IsInteractable = false;
        ui.Tooltip = "Passive description";
        var source = new MixedRumbleFakeInputSource(
            Frame(sequence: 1, pointer: new Vector2(50, 50)));
        var input = new PlayerInputSystem(entityManager, source);
        var interaction = new UIInteractionSystem(entityManager);
        var hoverEvents = new List<UIElementHoverEnteredEvent>();
        var sfxEvents = new List<PlaySfxEvent>();
        EventManager.Subscribe<UIElementHoverEnteredEvent>(hoverEvents.Add);
        EventManager.Subscribe<PlaySfxEvent>(sfxEvents.Add);

        input.Update(new GameTime());
        interaction.Update(new GameTime());

        Assert.Empty(hoverEvents);
        Assert.Empty(sfxEvents);
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_cursor_slows_over_interactable_ui()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        CreateUi(
            entityManager,
            "Control",
            10,
            new Rectangle(0, 0, 200, 200));
        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(
                Frame(sequence: 1, pointer: new Vector2(50, 50)),
                Frame(
                    sequence: 2,
                    device: PlayerInputDevice.Gamepad,
                    previousDevice: PlayerInputDevice.KeyboardMouse,
                    gamepadConnected: true,
                    leftStick: new Vector2(1f, 0f))));

        system.Update(new GameTime());
        system.Update(TenthSecondGameTime());

        PlayerInputState state = GetPlayerInputState(entityManager);
        Assert.Equal(108f, state.Frame.PointerPosition.X, precision: 2);
        Assert.Equal(50f, state.Frame.PointerPosition.Y, precision: 2);
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_cursor_does_not_slow_over_tooltip_only_ui()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity tooltip = CreateUi(
            entityManager,
            "TooltipOnly",
            10,
            new Rectangle(0, 0, 200, 200));
        UIElement ui = tooltip.GetComponent<UIElement>();
        ui.IsInteractable = false;
        ui.Tooltip = "Passive description";
        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(
                Frame(sequence: 1, pointer: new Vector2(50, 50)),
                Frame(
                    sequence: 2,
                    device: PlayerInputDevice.Gamepad,
                    previousDevice: PlayerInputDevice.KeyboardMouse,
                    gamepadConnected: true,
                    leftStick: new Vector2(1f, 0f))));

        system.Update(new GameTime());
        system.Update(TenthSecondGameTime());

        PlayerInputState state = GetPlayerInputState(entityManager);
        Assert.Equal(195f, state.Frame.PointerPosition.X, precision: 2);
        Assert.Equal(50f, state.Frame.PointerPosition.Y, precision: 2);
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_cursor_trigger_boost_bypasses_ui_slowdown()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        CreateUi(
            entityManager,
            "Control",
            10,
            new Rectangle(0, 0, 200, 200));
        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(
                Frame(sequence: 1, pointer: new Vector2(50, 50)),
                Frame(
                    sequence: 2,
                    device: PlayerInputDevice.Gamepad,
                    previousDevice: PlayerInputDevice.KeyboardMouse,
                    gamepadConnected: true,
                    leftStick: new Vector2(1f, 0f),
                    rightTrigger: 1f)));

        system.Update(new GameTime());
        system.Update(TenthSecondGameTime());

        PlayerInputState state = GetPlayerInputState(entityManager);
        Assert.Equal(340f, state.Frame.PointerPosition.X, precision: 2);
        Assert.Equal(50f, state.Frame.PointerPosition.Y, precision: 2);
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_cursor_slowdown_uses_active_input_context()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity gameplayControl = CreateUi(
            entityManager,
            "GameplayControl",
            10,
            new Rectangle(0, 0, 200, 200));
        Entity modalRoot = entityManager.CreateEntity("ModalRoot");
        entityManager.AddComponent(modalRoot, new InputContext
        {
            Id = "overlay.modal",
            Priority = 700,
            IsActive = true,
        });
        Entity modalControl = CreateUi(
            entityManager,
            "ModalControl",
            100,
            new Rectangle(500, 500, 100, 100));
        entityManager.AddComponent(modalControl, new InputContextMember
        {
            ContextId = "overlay.modal",
        });
        Assert.True(gameplayControl.GetComponent<UIElement>().IsInteractable);
        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(
                Frame(sequence: 1, pointer: new Vector2(50, 50)),
                Frame(
                    sequence: 2,
                    device: PlayerInputDevice.Gamepad,
                    previousDevice: PlayerInputDevice.KeyboardMouse,
                    gamepadConnected: true,
                    leftStick: new Vector2(1f, 0f))));

        system.Update(new GameTime());
        system.Update(TenthSecondGameTime());

        PlayerInputState state = GetPlayerInputState(entityManager);
        Assert.Equal(195f, state.Frame.PointerPosition.X, precision: 2);
        Assert.Equal(50f, state.Frame.PointerPosition.Y, precision: 2);
        EventManager.Clear();
    }

    [Fact]
    public void Active_modal_context_routes_cursor_to_member_control()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());
        CreateUi(
            entityManager,
            "GameplayControl",
            100,
            new Rectangle(0, 0, 100, 100));
        Entity modalRoot = entityManager.CreateEntity("ModalRoot");
        entityManager.AddComponent(modalRoot, new InputContext
        {
            Id = "overlay.modal",
            Priority = 700,
            IsActive = true,
        });
        Entity modalControl = CreateUi(
            entityManager,
            "ModalControl",
            10,
            new Rectangle(0, 0, 100, 100));
        entityManager.AddComponent(modalControl, new InputContextMember
        {
            ContextId = "overlay.modal",
        });
        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(Frame(pointer: new Vector2(50, 50))));

        system.Update(new GameTime());

        PlayerInputState state = entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single()
            .GetComponent<PlayerInputState>();
        Assert.Same(modalControl, state.CursorTarget.Entity);
        EventManager.Clear();
    }

    [Fact]
    public void Closing_modal_context_restores_gameplay_cursor_routing()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());
        Entity gameplayControl = CreateUi(
            entityManager,
            "LocationControl",
            10,
            new Rectangle(0, 0, 100, 100));
        Entity modalRoot = entityManager.CreateEntity("ModalRoot");
        var context = new InputContext
        {
            Id = "overlay.modal",
            Priority = 700,
            IsActive = true,
        };
        entityManager.AddComponent(modalRoot, context);
        Entity modalControl = CreateUi(
            entityManager,
            "ModalControl",
            100,
            new Rectangle(0, 0, 100, 100));
        modalControl.GetComponent<UIElement>().LayerType = UILayerType.Overlay;
        entityManager.AddComponent(modalControl, new InputContextMember
        {
            ContextId = "overlay.modal",
        });
        var system = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(
                Frame(sequence: 1, pointer: new Vector2(50, 50)),
                Frame(sequence: 2, pointer: new Vector2(50, 50))));

        system.Update(new GameTime());
        PlayerInputState state = entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single()
            .GetComponent<PlayerInputState>();
        Assert.Same(modalControl, state.CursorTarget.Entity);

        context.IsActive = false;
        UIElement modalUi = modalControl.GetComponent<UIElement>();
        modalUi.IsInteractable = false;
        modalUi.IsHidden = true;
        modalUi.Bounds = Rectangle.Empty;

        system.Update(new GameTime());

        Assert.Same(gameplayControl, state.CursorTarget.Entity);
        EventManager.Clear();
    }

    [Fact]
    public void Full_screen_overlay_receives_primary_click()
    {
        EventManager.Clear();
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());
        Entity overlay = CreateUi(
            entityManager,
            "DialogOverlay",
            100,
            new Rectangle(0, 0, 1920, 1080));
        overlay.GetComponent<UIElement>().ShowHoverHighlight = false;
        entityManager.AddComponent(overlay, new InputContext
        {
            Id = "overlay.dialog",
            Priority = 800,
            IsActive = true,
        });
        entityManager.AddComponent(overlay, new InputContextMember
        {
            ContextId = "overlay.dialog",
        });
        var input = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(Frame(
                pointer: new Vector2(500, 500),
                down: PlayerInputFrame.Mask(PlayerButton.Primary),
                pressed: PlayerInputFrame.Mask(PlayerButton.Primary))));
        var interaction = new UIInteractionSystem(entityManager);

        input.Update(new GameTime());
        interaction.Update(new GameTime());

        UIElement ui = overlay.GetComponent<UIElement>();
        Assert.True(ui.IsHovered);
        Assert.True(ui.IsClicked);
        EventManager.Clear();
    }

    [Fact]
    public void Context_resolution_uses_priority_and_diagnostic_region_override()
    {
        var entityManager = new EntityManager();
        Entity overlayRoot = entityManager.CreateEntity("Overlay");
        entityManager.AddComponent(overlayRoot, new InputContext
        {
            Id = "overlay.test",
            Priority = 700,
            IsActive = true,
        });

        Entity diagnosticRoot = entityManager.CreateEntity("Diagnostic");
        entityManager.AddComponent(diagnosticRoot, new InputContext
        {
            Id = "diagnostic.test",
            Priority = 900,
            IsActive = true,
            IsDiagnostic = true,
        });
        Entity diagnosticControl = CreateUi(
            entityManager,
            "DiagnosticControl",
            100,
            new Rectangle(10, 10, 50, 50));
        entityManager.AddComponent(diagnosticControl, new InputContextMember
        {
            ContextId = "diagnostic.test",
        });

        Assert.Equal(
            "diagnostic.test",
            InputContextResolver.ResolveCursorContext(entityManager, new Vector2(20, 20)));
        Assert.Equal(
            "overlay.test",
            InputContextResolver.ResolveCursorContext(entityManager, new Vector2(200, 200)));
        Assert.Equal(
            "overlay.test",
            InputContextResolver.ResolveCommandContext(entityManager));
    }

    [Fact]
    public void Hotkey_hold_completes_once_and_cancels_when_ineligible()
    {
        var entityManager = new EntityManager();
        Entity entity = entityManager.CreateEntity("Hold");
        var tracker = new HotKeyHoldTracker();

        tracker.Start(entity, FaceButton.X);
        Assert.False(tracker.Advance(entity, 0.4f, 0.75f, true));
        Assert.True(tracker.Advance(entity, 0.4f, 0.75f, true));
        Assert.False(tracker.Advance(entity, 1f, 0.75f, true));

        tracker.Start(entity, FaceButton.X);
        Assert.False(tracker.Advance(entity, 0.2f, 0.75f, false));
        Assert.Empty(tracker.Progress);
    }

    [Fact]
    public void Hotkey_non_interactable_eligibility_requires_explicit_opt_in()
    {
        var entityManager = new EntityManager();
        Entity entity = CreateUi(
            entityManager,
            "HotKeyTarget",
            10,
            new Rectangle(0, 0, 100, 100));
        var hotKey = new HotKey { Button = FaceButton.X };
        entityManager.AddComponent(entity, hotKey);
        UIElement ui = entity.GetComponent<UIElement>();
        ui.IsInteractable = false;

        Assert.False(HotKeySystem.IsHotKeyEligible(
            entity,
            hotKey,
            ui,
            InputContextIds.Gameplay,
            gameplayBlocked: false));

        hotKey.AllowWhenNonInteractable = true;

        Assert.True(HotKeySystem.IsHotKeyEligible(
            entity,
            hotKey,
            ui,
            InputContextIds.Gameplay,
            gameplayBlocked: false));

        ui.IsHidden = true;

        Assert.False(HotKeySystem.IsHotKeyEligible(
            entity,
            hotKey,
            ui,
            InputContextIds.Gameplay,
            gameplayBlocked: false));
    }

    [Fact]
    public void Gamepad_back_resolves_to_view_hotkey_before_cancel()
    {
        PlayerButtonMask back = PlayerInputFrame.Mask(PlayerButton.Back);
        PlayerButtonMask cancel = PlayerInputFrame.Mask(PlayerButton.Cancel);

        FaceButton? button = HotKeySystem.GetPressedButton(Frame(
            device: PlayerInputDevice.Gamepad,
            gamepadConnected: true,
            pressed: back | cancel));

        Assert.Equal(FaceButton.View, button);
    }

    [Fact]
    public void Gamepad_start_and_cancel_resolve_to_menu_and_b_hotkeys()
    {
        FaceButton? start = HotKeySystem.GetPressedButton(Frame(
            device: PlayerInputDevice.Gamepad,
            gamepadConnected: true,
            pressed: PlayerInputFrame.Mask(PlayerButton.Start)));
        FaceButton? cancel = HotKeySystem.GetPressedButton(Frame(
            device: PlayerInputDevice.Gamepad,
            gamepadConnected: true,
            pressed: PlayerInputFrame.Mask(PlayerButton.Cancel)));

        Assert.Equal(FaceButton.Start, start);
        Assert.Equal(FaceButton.B, cancel);
    }

    [Fact]
    public void Skip_dialog_delegate_publishes_skip_request()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity skipButton = CreateUi(
            entityManager,
            "DialogEndButton",
            10,
            new Rectangle(0, 0, 100, 40));
        int skipRequests = 0;
        EventManager.Subscribe<DialogSkipRequested>(_ => skipRequests++);

        UIElementEventDelegateService.HandleEvent(
            UIElementEventType.SkipDialog,
            skipButton,
            entityManager);

        Assert.Equal(1, skipRequests);
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_start_hold_triggers_skip_dialog_delegate_without_click_fallback()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity skipButton = CreateUi(
            entityManager,
            "DialogEndButton",
            10,
            new Rectangle(0, 0, 100, 40));
        UIElement ui = skipButton.GetComponent<UIElement>();
        ui.EventType = UIElementEventType.SkipDialog;
        entityManager.AddComponent(skipButton, new HotKey
        {
            Button = FaceButton.Start,
            RequiresHold = true,
            HoldDurationSeconds = 0.75f,
        });
        var input = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(
                Frame(
                    sequence: 1,
                    device: PlayerInputDevice.Gamepad,
                    gamepadConnected: true,
                    down: PlayerInputFrame.Mask(PlayerButton.Start),
                    pressed: PlayerInputFrame.Mask(PlayerButton.Start)),
                Frame(
                    sequence: 2,
                    device: PlayerInputDevice.Gamepad,
                    gamepadConnected: true,
                    down: PlayerInputFrame.Mask(PlayerButton.Start))));
        var hotKeys = new HotKeySystem(entityManager, null, null);
        int skipRequests = 0;
        EventManager.Subscribe<DialogSkipRequested>(_ => skipRequests++);
        var gameTime = new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.4));

        input.Update(gameTime);
        hotKeys.Update(gameTime);
        input.Update(gameTime);
        hotKeys.Update(gameTime);

        Assert.Equal(1, skipRequests);
        Assert.False(ui.IsClicked);
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_back_triggers_view_hotkey_target()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity viewTarget = CreateUi(
            entityManager,
            "ViewHotKeyTarget",
            10,
            new Rectangle(0, 0, 100, 100));
        entityManager.AddComponent(viewTarget, new HotKey { Button = FaceButton.View });
        PlayerButtonMask back = PlayerInputFrame.Mask(PlayerButton.Back);
        PlayerButtonMask cancel = PlayerInputFrame.Mask(PlayerButton.Cancel);
        var input = new PlayerInputSystem(
            entityManager,
            new FakeInputSource(Frame(
                device: PlayerInputDevice.Gamepad,
                gamepadConnected: true,
                pressed: back | cancel)));
        var hotKeys = new HotKeySystem(entityManager, null, null);
        var selected = new List<Entity>();
        EventManager.Subscribe<HotKeySelectEvent>(e => selected.Add(e.Entity));

        input.Update(new GameTime());
        hotKeys.Update(new GameTime());

        Assert.True(viewTarget.GetComponent<UIElement>().IsClicked);
        Assert.Same(viewTarget, Assert.Single(selected));
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_rumbles_on_new_interactable_hover()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        CreateUi(
            entityManager,
            "Control",
            10,
            new Rectangle(0, 0, 200, 200));
        var source = new MixedRumbleFakeInputSource(
			Frame(sequence: 1, pointer: new Vector2(300, 100), gamepadConnected: true),
            Frame(
                sequence: 2,
                device: PlayerInputDevice.Gamepad,
                previousDevice: PlayerInputDevice.KeyboardMouse,
                gamepadConnected: true,
                leftStick: new Vector2(-1f, 0f)));
        var input = new PlayerInputSystem(entityManager, source);
        var rumble = new ControllerRumbleSystem(entityManager, source);
        var interaction = new UIInteractionSystem(entityManager);

        input.Update(new GameTime());
        rumble.Update(new GameTime());
        interaction.Update(new GameTime());
        input.Update(TenthSecondGameTime());
        rumble.Update(TenthSecondGameTime());
        interaction.Update(TenthSecondGameTime());

        Assert.Contains(
            source.VibrationCalls,
            call => call.Low == 0.3f && call.High == 0.2f);
        EventManager.Clear();
    }

    [Fact]
    public void Gamepad_does_not_rumble_on_tooltip_only_hover()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        Entity tooltip = CreateUi(
            entityManager,
            "TooltipOnly",
            10,
            new Rectangle(0, 0, 200, 200));
        UIElement ui = tooltip.GetComponent<UIElement>();
        ui.IsInteractable = false;
        ui.Tooltip = "Passive description";
        var source = new MixedRumbleFakeInputSource(
            Frame(sequence: 1, pointer: new Vector2(300, 100)),
            Frame(
                sequence: 2,
                device: PlayerInputDevice.Gamepad,
                previousDevice: PlayerInputDevice.KeyboardMouse,
                gamepadConnected: true,
                leftStick: new Vector2(-1f, 0f)));
        var input = new PlayerInputSystem(entityManager, source);
        var rumble = new ControllerRumbleSystem(entityManager, source);
        var interaction = new UIInteractionSystem(entityManager);

        input.Update(new GameTime());
        rumble.Update(new GameTime());
        interaction.Update(new GameTime());
        input.Update(TenthSecondGameTime());
        rumble.Update(TenthSecondGameTime());
        interaction.Update(TenthSecondGameTime());

        Assert.DoesNotContain(
            source.VibrationCalls,
            call => call.Low > 0f || call.High > 0f);
        EventManager.Clear();
    }

    [Fact]
    public void Keyboard_mouse_does_not_rumble()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        CreateUi(
            entityManager,
            "Control",
            10,
            new Rectangle(0, 0, 200, 200));
        var source = new MixedRumbleFakeInputSource(
            Frame(sequence: 1, pointer: new Vector2(300, 100)),
            Frame(
                sequence: 2,
                device: PlayerInputDevice.Gamepad,
                previousDevice: PlayerInputDevice.KeyboardMouse,
                gamepadConnected: true,
                leftStick: new Vector2(-1f, 0f)),
            Frame(
                sequence: 3,
                pointer: new Vector2(155, 100),
                device: PlayerInputDevice.KeyboardMouse,
				previousDevice: PlayerInputDevice.Gamepad,
				gamepadConnected: true));
        var input = new PlayerInputSystem(entityManager, source);
        var rumble = new ControllerRumbleSystem(entityManager, source);
        var interaction = new UIInteractionSystem(entityManager);

        input.Update(new GameTime());
        rumble.Update(new GameTime());
        interaction.Update(new GameTime());
        input.Update(TenthSecondGameTime());
        rumble.Update(TenthSecondGameTime());
        interaction.Update(TenthSecondGameTime());
        input.Update(new GameTime());
        rumble.Update(new GameTime());
        interaction.Update(new GameTime());

        Assert.Contains(
            source.VibrationCalls,
            call => call.Low == 0.3f && call.High == 0.2f);
        Assert.Contains(
            source.VibrationCalls,
            call => call.Low == 0f && call.High == 0f);
        EventManager.Clear();
    }

    [Fact]
    public void Keyboard_mouse_clears_only_ui_hover_rumble()
    {
        EventManager.Clear();
        var entityManager = CreateSceneEntityManager();
        CreateUi(
            entityManager,
            "Control",
            10,
            new Rectangle(0, 0, 200, 200));
        var source = new MixedRumbleFakeInputSource(
			Frame(sequence: 1, pointer: new Vector2(300, 100), gamepadConnected: true),
            Frame(
                sequence: 2,
                device: PlayerInputDevice.Gamepad,
                previousDevice: PlayerInputDevice.KeyboardMouse,
                gamepadConnected: true,
                leftStick: new Vector2(-1f, 0f)),
            Frame(
                sequence: 3,
                pointer: new Vector2(155, 100),
                device: PlayerInputDevice.KeyboardMouse,
				previousDevice: PlayerInputDevice.Gamepad,
				gamepadConnected: true));
		source.SetRumbleChannel("booster-pack-opening", new RumbleMotorState(0.35f, 0.55f));
        var input = new PlayerInputSystem(entityManager, source);
        var rumble = new ControllerRumbleSystem(entityManager, source);
        var interaction = new UIInteractionSystem(entityManager);

        input.Update(new GameTime());
        rumble.Update(new GameTime());
        interaction.Update(new GameTime());
        input.Update(TenthSecondGameTime());
        rumble.Update(TenthSecondGameTime());
        interaction.Update(TenthSecondGameTime());
        input.Update(new GameTime());
        rumble.Update(new GameTime());
        interaction.Update(new GameTime());

        Assert.Contains(
            source.VibrationCalls,
            call => call.Low == 0.65f && call.High == 0.75f);
        Assert.Contains(
            source.VibrationCalls,
            call => call.Low == 0.35f && call.High == 0.55f);
        EventManager.Clear();
    }

    [Fact]
    public void MonoGame_input_is_referenced_only_by_the_hardware_adapter()
    {
        string root = FindRepositoryRoot();
        string adapter = Path.GetFullPath(
            Path.Combine(root, "ECS", "Input", "MonoGamePlayerInputAdapter.cs"));
        string[] forbidden =
        {
            "Microsoft.Xna.Framework.Input",
            "Keyboard.GetState",
            "Mouse.GetState",
            "GamePad.GetState",
            "GamePad.GetCapabilities",
            "GamePad.SetVibration",
        };

        var violations = Directory
            .EnumerateFiles(root, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}"))
            .Where(path => !path.Contains($"{Path.DirectorySeparatorChar}tests{Path.DirectorySeparatorChar}"))
            .Where(path => Path.GetFullPath(path) != adapter)
            .Where(path =>
            {
                string text = File.ReadAllText(path);
                return forbidden.Any(text.Contains);
            })
            .Select(path => Path.GetRelativePath(root, path))
            .ToList();

        Assert.Empty(violations);
    }

    private static Entity CreateUi(
        EntityManager entityManager,
        string name,
        int zOrder,
        Rectangle bounds)
    {
        Entity entity = entityManager.CreateEntity(name);
        entityManager.AddComponent(entity, new Transform
        {
            Position = Vector2.Zero,
            ZOrder = zOrder,
        });
        entityManager.AddComponent(entity, new UIElement
        {
            Bounds = bounds,
            IsInteractable = true,
        });
        return entity;
    }

    private static EntityManager CreateSceneEntityManager()
    {
        var entityManager = new EntityManager();
        Entity scene = entityManager.CreateEntity("Scene");
        entityManager.AddComponent(scene, new SceneState());
        return entityManager;
    }

    private static PlayerInputState GetPlayerInputState(EntityManager entityManager)
    {
        return entityManager
            .GetEntitiesWithComponent<PlayerInputState>()
            .Single()
            .GetComponent<PlayerInputState>();
    }

    private static GameTime TenthSecondGameTime()
    {
        return new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(0.1));
    }

    private static PlayerInputFrame Frame(
        long sequence = 1,
        Vector2 pointer = default,
        PlayerInputDevice device = PlayerInputDevice.KeyboardMouse,
        PlayerInputDevice previousDevice = PlayerInputDevice.KeyboardMouse,
        bool gamepadConnected = false,
        PlayerButtonMask down = PlayerButtonMask.None,
        PlayerButtonMask pressed = PlayerButtonMask.None,
        PlayerButtonMask released = PlayerButtonMask.None,
        Vector2 leftStick = default,
        float rightTrigger = 0f)
    {
        return new PlayerInputFrame(
            sequence,
            true,
            gamepadConnected,
            device,
            previousDevice,
            GamepadGlyphStyle.Xbox,
            pointer,
            Vector2.Zero,
            0f,
            leftStick,
            Vector2.Zero,
            0f,
            rightTrigger,
            down,
            pressed,
            released);
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo directory = new(AppContext.BaseDirectory);
        while (directory != null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "Crusaders30XX.csproj")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new DirectoryNotFoundException("Could not locate repository root.");
    }

    private sealed class FakeInputSource : MixedRumbleFakeInputSource
    {
        public FakeInputSource(params PlayerInputFrame[] frames)
            : base(frames)
        {
        }
    }

    private sealed class RecordingSystem : EcsSystem
    {
        private readonly string _name;
        private readonly List<string> _calls;

        public RecordingSystem(
            EntityManager entityManager,
            string name,
            List<string> calls)
            : base(entityManager)
        {
            _name = name;
            _calls = calls;
        }

        public override void Update(GameTime gameTime)
        {
            _calls.Add(_name);
        }

        protected override IEnumerable<Entity> GetRelevantEntities()
        {
            return Enumerable.Empty<Entity>();
        }

        protected override void UpdateEntity(Entity entity, GameTime gameTime)
        {
        }
    }
}
