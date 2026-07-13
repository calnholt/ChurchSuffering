#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Integration.Host;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;
using LegacyButton = Crusaders30XX.ECS.Input.PlayerButton;
using LegacyDevice = Crusaders30XX.ECS.Input.PlayerInputDevice;
using LegacyFrame = Crusaders30XX.ECS.Input.PlayerInputFrame;
using LegacyGlyphStyle = Crusaders30XX.ECS.Input.GamepadGlyphStyle;
using LegacyMask = Crusaders30XX.ECS.Input.PlayerButtonMask;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Input;

public sealed class Ecs052InputParityTests
{
    private static readonly StringId GameplayContext = new(5201);
    private static readonly StringId OverlayContext = new(5202);

    [Theory]
    [InlineData(LegacyButton.Primary, PlayerInputButton.Primary)]
    [InlineData(LegacyButton.Secondary, PlayerInputButton.Secondary)]
    [InlineData(LegacyButton.Cancel, PlayerInputButton.Cancel)]
    [InlineData(LegacyButton.Escape, PlayerInputButton.Escape)]
    [InlineData(LegacyButton.Back, PlayerInputButton.Back)]
    [InlineData(LegacyButton.FaceB, PlayerInputButton.FaceB)]
    [InlineData(LegacyButton.FaceX, PlayerInputButton.FaceX)]
    [InlineData(LegacyButton.FaceY, PlayerInputButton.FaceY)]
    [InlineData(LegacyButton.Start, PlayerInputButton.Start)]
    [InlineData(LegacyButton.LeftShoulder, PlayerInputButton.LeftShoulder)]
    [InlineData(LegacyButton.RightShoulder, PlayerInputButton.RightShoulder)]
    [InlineData(LegacyButton.LeftStick, PlayerInputButton.LeftStick)]
    [InlineData(LegacyButton.Space, PlayerInputButton.Space)]
    [InlineData(LegacyButton.Enter, PlayerInputButton.Enter)]
    [InlineData(LegacyButton.Shift, PlayerInputButton.Shift)]
    [InlineData(LegacyButton.MoveUp, PlayerInputButton.MoveUp)]
    [InlineData(LegacyButton.MoveDown, PlayerInputButton.MoveDown)]
    [InlineData(LegacyButton.MoveLeft, PlayerInputButton.MoveLeft)]
    [InlineData(LegacyButton.MoveRight, PlayerInputButton.MoveRight)]
    [InlineData(LegacyButton.F11, PlayerInputButton.ToggleFullScreen)]
    [InlineData(LegacyButton.DebugMenu, PlayerInputButton.ToggleDebugMenu)]
    [InlineData(LegacyButton.EntityList, PlayerInputButton.ToggleEntityList)]
    [InlineData(LegacyButton.DebugDamage, PlayerInputButton.DealDebugDamage)]
    [InlineData(LegacyButton.Profiler, PlayerInputButton.ToggleProfiler)]
    [InlineData(LegacyButton.Quit, PlayerInputButton.Quit)]
    public void Central_adapter_preserves_each_stable_button(
        LegacyButton legacy,
        PlayerInputButton expected)
    {
        LegacyMask mask = LegacyFrame.Mask(legacy);
        LegacyFrame frame = Frame(down: mask, pressed: mask);

        PlayerInputFrame mapped = new CentralInputFrameAdapter().Convert(
            in frame,
            new Rectangle(0, 0, 1920, 1080),
            1920,
            1080).PlayerInput.Frame;

        Assert.True(mapped.IsDown(expected));
        Assert.True(mapped.WasPressed(expected));
    }

    [Fact]
    public void Semantic_aliases_are_additive_and_do_not_erase_physical_bindings()
    {
        LegacyMask mask = LegacyFrame.Mask(LegacyButton.LeftStick) |
            LegacyFrame.Mask(LegacyButton.Shift);
        LegacyFrame frame = Frame(down: mask, pressed: mask);

        PlayerInputFrame mapped = new CentralInputFrameAdapter().Convert(
            in frame,
            new Rectangle(0, 0, 1920, 1080),
            1920,
            1080).PlayerInput.Frame;

        Assert.True(mapped.WasPressed(PlayerInputButton.LeftStick));
        Assert.True(mapped.WasPressed(PlayerInputButton.ShowHint));
        Assert.True(mapped.IsDown(PlayerInputButton.Shift));
        Assert.True(mapped.IsDown(PlayerInputButton.Modifier));
    }

    [Fact]
    public void Previous_device_connectivity_and_glyph_style_cross_both_host_boundaries()
    {
        LegacyFrame frame = Frame(
            device: LegacyDevice.Gamepad,
            previousDevice: LegacyDevice.KeyboardMouse,
            gamepadConnected: true,
            glyphStyle: LegacyGlyphStyle.PlayStation,
            down: LegacyFrame.Mask(LegacyButton.Start),
            pressed: LegacyFrame.Mask(LegacyButton.Start));
        var adapter = new CentralInputFrameAdapter();

        HostInputSnapshot snapshot = adapter.CreateSnapshot(
            in frame,
            new Rectangle(100, 50, 960, 540),
            1920,
            1080);
        PlayerInputFrame mapped = adapter.Convert(
            in frame,
            new Rectangle(100, 50, 960, 540),
            1920,
            1080).PlayerInput.Frame;

        Assert.Equal(PlayerInputDevice.Gamepad, snapshot.Device);
        Assert.Equal(PlayerInputDevice.KeyboardMouse, snapshot.PreviousDevice);
        Assert.True(snapshot.IsGamepadConnected);
        Assert.Equal(GamepadGlyphStyle.PlayStation, snapshot.GamepadGlyphStyle);
        Assert.True(mapped.DeviceChanged);
        Assert.True(mapped.IsGamepadConnected);
        Assert.Equal(GamepadGlyphStyle.PlayStation, mapped.GamepadGlyphStyle);
    }

    [Fact]
    public void Inactive_window_clears_actions_but_retains_device_capabilities()
    {
        ulong button = PlayerInputFrame.Mask(PlayerInputButton.FaceX);
        var snapshot = new HostInputSnapshot(
            ScreenPointer: Vector2.Zero,
            PreviousScreenPointer: Vector2.Zero,
            LeftStick: Vector2.Zero,
            RightStick: Vector2.Zero,
            LeftTrigger: 0f,
            RightTrigger: 0f,
            ScrollValue: 0,
            PreviousScrollValue: 0,
            DownButtons: button,
            PreviousDownButtons: 0,
            Device: PlayerInputDevice.Gamepad,
            IsWindowActive: false,
            RenderDestination: new Rectangle(0, 0, 1920, 1080),
            VirtualWidth: 1920,
            VirtualHeight: 1080,
            PreviousDevice: PlayerInputDevice.KeyboardMouse,
            IsGamepadConnected: true,
            GamepadGlyphStyle: GamepadGlyphStyle.PlayStation);

        PlayerInputFrame mapped = new HostInputAdapter().Convert(in snapshot).PlayerInput.Frame;

        Assert.Equal(0UL, mapped.DownButtons);
        Assert.Equal(0UL, mapped.PressedButtons);
        Assert.True(mapped.DeviceChanged);
        Assert.True(mapped.IsGamepadConnected);
        Assert.Equal(GamepadGlyphStyle.PlayStation, mapped.GamepadGlyphStyle);
    }

    [Theory]
    [InlineData(PlayerInputButton.Primary, GamepadGlyph.A)]
    [InlineData(PlayerInputButton.Secondary, GamepadGlyph.X)]
    [InlineData(PlayerInputButton.FaceB, GamepadGlyph.B)]
    [InlineData(PlayerInputButton.FaceX, GamepadGlyph.X)]
    [InlineData(PlayerInputButton.FaceY, GamepadGlyph.Y)]
    [InlineData(PlayerInputButton.Back, GamepadGlyph.View)]
    [InlineData(PlayerInputButton.Start, GamepadGlyph.Start)]
    [InlineData(PlayerInputButton.LeftShoulder, GamepadGlyph.LeftShoulder)]
    [InlineData(PlayerInputButton.RightShoulder, GamepadGlyph.RightShoulder)]
    [InlineData(PlayerInputButton.LeftStick, GamepadGlyph.LeftStick)]
    [InlineData(PlayerInputButton.ShowHint, GamepadGlyph.LeftStick)]
    public void Gamepad_bindings_resolve_to_a_style_independent_glyph(
        PlayerInputButton button,
        GamepadGlyph expected)
    {
        Assert.True(PlayerInputGlyphs.TryResolve(button, out GamepadGlyph actual));
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void Keyboard_only_binding_has_no_gamepad_glyph()
    {
        Assert.False(PlayerInputGlyphs.TryResolve(PlayerInputButton.Enter, out _));
    }

    [Fact]
    public void Device_transition_cancels_an_existing_hotkey_hold()
    {
        World world = new(GeneratedComponentRegistry.Create());
        GlobalUiWorldBootstrap.Create(world);
        CreateContext(world);
        EntityId hotKey = CreateHotKey(world);
        var holds = new EventStream<HotKeyHoldCompletedEvent>();
        var selected = new EventStream<HotKeySelectEvent>();
        var actions = new EventStream<UIActionEvent>();
        var system = new HotKeySystem(
            world,
            GameplayContext,
            OverlayContext,
            holds,
            selected,
            actions);
        EventRuntime events = AttachEvents(world);
        var commands = new CommandBuffer();
        ref PlayerInputState input = ref world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        ulong space = PlayerInputFrame.Mask(PlayerInputButton.Space);
        input.Frame = ActiveFrame(
            device: PlayerInputDevice.KeyboardMouse,
            previousDevice: PlayerInputDevice.KeyboardMouse,
            down: space,
            pressed: space);

        Run(system, world, events, commands, TimeSpan.FromSeconds(0.4));
        Assert.True((world.Get<HotKey>(hotKey).Flags & HotKeyFlags.Holding) != 0);
        Assert.Equal(0.4f, world.Get<HotKey>(hotKey).HoldProgressSeconds);

        ulong faceX = PlayerInputFrame.Mask(PlayerInputButton.FaceX);
        input.Frame = ActiveFrame(
            device: PlayerInputDevice.Gamepad,
            previousDevice: PlayerInputDevice.KeyboardMouse,
            down: faceX,
            pressed: 0);
        Run(system, world, events, commands, TimeSpan.FromSeconds(0.4));

        Assert.True((world.Get<HotKey>(hotKey).Flags & HotKeyFlags.Holding) == 0);
        Assert.Equal(0f, world.Get<HotKey>(hotKey).HoldProgressSeconds);
        Assert.Equal(0, holds.PendingCount);
        Assert.Equal(0, selected.PendingCount);
        Assert.Equal(0, actions.PendingCount);
    }

    private static LegacyFrame Frame(
        LegacyDevice device = LegacyDevice.KeyboardMouse,
        LegacyDevice previousDevice = LegacyDevice.KeyboardMouse,
        bool gamepadConnected = false,
        LegacyGlyphStyle glyphStyle = LegacyGlyphStyle.Xbox,
        LegacyMask down = LegacyMask.None,
        LegacyMask pressed = LegacyMask.None,
        LegacyMask released = LegacyMask.None) => new(
        1,
        true,
        gamepadConnected,
        device,
        previousDevice,
        glyphStyle,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        Vector2.Zero,
        Vector2.Zero,
        0f,
        0f,
        down,
        pressed,
        released);

    private static PlayerInputFrame ActiveFrame(
        PlayerInputDevice device,
        PlayerInputDevice previousDevice,
        ulong down,
        ulong pressed) => new(
        Sequence: 1,
        PointerPosition: Vector2.Zero,
        PointerDelta: Vector2.Zero,
        LeftStick: Vector2.Zero,
        RightStick: Vector2.Zero,
        ScrollDelta: 0f,
        LeftTrigger: 0f,
        RightTrigger: 0f,
        DownButtons: down,
        PressedButtons: pressed,
        ReleasedButtons: 0,
        Device: device,
        IsWindowActive: true,
        PreviousDevice: previousDevice,
        IsGamepadConnected: true,
        GamepadGlyphStyle: GamepadGlyphStyle.Xbox);

    private static void CreateContext(World world)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new InputContext
        {
            Id = GameplayContext,
            Priority = 0,
            Flags = InputContextFlags.Active | InputContextFlags.AcceptsCommands,
        });
        world.Create(in bundle);
    }

    private static EntityId CreateHotKey(World world)
    {
        var bundle = new SpawnBundle(4, 128);
        bundle.Add(new UIElement
        {
            Bounds = new Rectangle(0, 0, 20, 20),
            Flags = UIInteractionFlags.BaseInteractable,
            EventType = UIElementEventType.CardClicked,
        });
        bundle.Add(new Transform { Scale = Vector2.One });
        bundle.Add(new InputContextMember { ContextId = GameplayContext });
        bundle.Add(new HotKey
        {
            KeyboardBinding = (int)PlayerInputButton.Space,
            GamepadBinding = (byte)PlayerInputButton.FaceX,
            HoldDurationSeconds = 0.75f,
            Flags = HotKeyFlags.Active | HotKeyFlags.RequiresHold,
        });
        return world.Create(in bundle);
    }

    private static EventRuntime AttachEvents(World world)
    {
        var runtime = new EventRuntime(new EventRoutingEndpoint());
        world.AttachEventRuntime(runtime);
        return runtime;
    }

    private static void Run(
        IGameSystem system,
        World world,
        EventRuntime events,
        CommandBuffer commands,
        TimeSpan elapsed)
    {
        var context = new SystemContext(world, commands, events, 0, elapsed, SceneGroup.Battle);
        system.Update(ref context);
        commands.Playback(world);
    }
}
