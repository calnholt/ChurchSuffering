#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Crusaders30XX.ECS.DataOriented.Gameplay.Scenes;
using Crusaders30XX.ECS.DataOriented.Gameplay.UI;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.GlobalUi;

public sealed class Ecs040GlobalUiTests
{
    private static readonly StringId Gameplay = new(1);
    private static readonly StringId Overlay = new(2);

    [Fact]
    public void Scene_transition_tears_down_owned_entities_and_preserves_persistent_entities()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world, SceneGroup.Battle);
        EntityId destroyed = CreateSceneOwned(world, SceneGroup.Battle);
        EntityId loadPersistent = CreateSceneOwned<DontDestroyOnLoad>(world, SceneGroup.Battle);
        EntityId reloadPersistent = CreateSceneOwned<DontDestroyOnReload>(world, SceneGroup.Battle);

        var load = new EventStream<LoadSceneEvent>();
        var runtime = new EventRuntime(new EventRoutingEndpoint(
            new EventRoute<LoadSceneEvent>(
                GlobalUiEventTypeIds.LoadScene,
                nameof(LoadSceneEvent),
                load,
                new EventConsumerRegistration<LoadSceneEvent>(
                    0,
                    "scene-request",
                    new LoadSceneEventConsumer(world)))));
        world.AttachEventRuntime(runtime);
        Guid preparationId = Guid.NewGuid();
        load.Publish(new LoadSceneEvent(preparationId, SceneGroup.Climb, SceneGroup.Battle));
        runtime.DrainBarrier();

        EventStreams streams = new();
        var lifecycle = new SceneLifecycleSystem(
            world,
            streams.Deactivating,
            streams.Prepare,
            streams.Activating,
            streams.Activated,
            streams.DeleteCaches);
        var commands = new CommandBuffer();
        Run(lifecycle, world, runtime, commands);

        Assert.False(world.IsAlive(destroyed));
        Assert.True(world.IsAlive(loadPersistent));
        Assert.False(world.IsAlive(reloadPersistent));
        EntityId global = world.GetUnique<SceneStateSingleton>();
        Assert.Equal(SceneTransitionPhase.Preparing, world.Get<SceneTransitionState>(global).Phase);

        var ready = new EventStream<ScenePreparationReady>();
        var coordinator = new SceneLoadingCoordinatorSystem(ready);
        Run(coordinator, world, runtime, commands);
        Run(lifecycle, world, runtime, commands);

        Assert.Equal(SceneGroup.Climb, world.Get<SceneState>(global).Current);
        Assert.Equal(SceneTransitionPhase.Idle, world.Get<SceneTransitionState>(global).Phase);
        Assert.Equal(1, streams.Deactivating.PendingCount);
        Assert.Equal(1, streams.Activated.PendingCount);
    }

    [Fact]
    public void Same_scene_reload_preserves_reload_persistent_entity()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world, SceneGroup.Battle);
        EntityId ordinary = CreateSceneOwned(world, SceneGroup.Battle);
        EntityId persistent = CreateSceneOwned<DontDestroyOnReload>(world, SceneGroup.Battle);
        EntityId global = world.GetUnique<SceneStateSingleton>();
        ref SceneTransitionState transition = ref world.Get<SceneTransitionState>(global);
        transition = new SceneTransitionState
        {
            PreparationId = Guid.NewGuid(),
            From = SceneGroup.Battle,
            To = SceneGroup.Battle,
            IsReload = true,
            Phase = SceneTransitionPhase.Requested,
        };

        EventStreams streams = new();
        var runtime = AttachEmptyEvents(world);
        var system = new SceneLifecycleSystem(
            world,
            streams.Deactivating,
            streams.Prepare,
            streams.Activating,
            streams.Activated,
            streams.DeleteCaches);
        Run(system, world, runtime, new CommandBuffer());

        Assert.False(world.IsAlive(ordinary));
        Assert.True(world.IsAlive(persistent));
    }

    [Fact]
    public void Cursor_context_rotation_and_z_order_select_one_deterministic_winner()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        CreateContext(world, Gameplay, priority: 0, diagnostic: false);
        EntityId low = CreateUi(world, new Rectangle(0, 0, 100, 100), z: 2, Gameplay);
        EntityId highRotated = CreateUi(
            world,
            new Rectangle(40, 40, 100, 20),
            z: 9,
            Gameplay,
            rotation: MathF.PI / 2f);
        var playerCommands = new EventStream<PlayerCommandEvent>();
        var system = new PlayerInputSystem(world, Gameplay, Overlay, playerCommands);
        SetFrame(world, new Vector2(90, 90));

        EventRuntime runtime = AttachEmptyEvents(world);
        Run(system, world, runtime, new CommandBuffer());

        PlayerInputState state = world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        Assert.Equal(highRotated, state.CursorTarget);
        Assert.NotEqual(low, state.CursorTarget);
        Assert.Equal(CursorTargetKind.UI, state.TargetKind);
    }

    [Fact]
    public void Highest_active_input_context_blocks_gameplay_and_modal_transition_blocks_click()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        CreateContext(world, Gameplay, priority: 0, diagnostic: false);
        EntityId modal = CreateContext(world, Overlay, priority: 100, diagnostic: false);
        world.Add(modal, new ModalAnimation
        {
            InputContextId = Overlay,
            RequestedVisible = true,
            Phase = ModalAnimationPhase.Hidden,
            EnterDurationSeconds = 1f,
            ExitDurationSeconds = 1f,
        });
        _ = CreateUi(world, new Rectangle(0, 0, 200, 200), z: 20, Gameplay);
        EntityId overlay = CreateUi(world, new Rectangle(0, 0, 200, 200), z: 1, Overlay);
        SetFrame(world, new Vector2(20, 20), PlayerInputFrame.Mask(PlayerInputButton.Primary));

        var commandEvents = new EventStream<PlayerCommandEvent>();
        var input = new PlayerInputSystem(world, Gameplay, Overlay, commandEvents);
        var modalSystem = new ModalInputSuppressionSystem(world);
        var hover = new EventStream<UIHoverChangedEvent>();
        var clicks = new EventStream<UIClickEvent>();
        var actions = new EventStream<UIActionEvent>();
        var interaction = new UIInteractionSystem(world, hover, clicks, actions);
        EventRuntime runtime = AttachEmptyEvents(world);
        var commands = new CommandBuffer();

        Run(input, world, runtime, commands);
        Run(modalSystem, world, runtime, commands, TimeSpan.FromMilliseconds(16));
        Run(interaction, world, runtime, commands);

        PlayerInputState state = world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        Assert.Equal(Overlay, state.CursorContext);
        Assert.Equal(overlay, state.CursorTarget);
        Assert.True(world.Get<UIElement>(overlay).SuppressCount > 0);
        Assert.Equal(0, clicks.PendingCount);
        Assert.Equal(0, actions.PendingCount);
    }

    [Fact]
    public void Interaction_resets_click_and_hover_then_dispatches_only_the_current_target()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        EntityId old = CreateUi(world, new Rectangle(0, 0, 10, 10), z: 1, Gameplay);
        EntityId current = CreateUi(world, new Rectangle(20, 20, 20, 20), z: 2, Gameplay);
        ref UIElement oldUi = ref world.Get<UIElement>(old);
        oldUi.Flags |= UIInteractionFlags.Hovered | UIInteractionFlags.Clicked;
        ref PlayerInputState state = ref world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        state.Frame = ActiveFrame(new Vector2(25, 25), PlayerInputFrame.Mask(PlayerInputButton.Primary));
        state.CursorTarget = current;
        state.PreviousHoverTarget = old;
        state.Flags |= PlayerInputFlags.CursorInteractionEnabled;
        var hover = new EventStream<UIHoverChangedEvent>();
        var clicks = new EventStream<UIClickEvent>();
        var actions = new EventStream<UIActionEvent>();
        var system = new UIInteractionSystem(world, hover, clicks, actions);

        Run(system, world, AttachEmptyEvents(world), new CommandBuffer());

        Assert.Equal(UIInteractionFlags.BaseInteractable, world.Get<UIElement>(old).Flags);
        UIInteractionFlags currentFlags = world.Get<UIElement>(current).Flags;
        Assert.True((currentFlags & UIInteractionFlags.Hovered) != 0);
        Assert.True((currentFlags & UIInteractionFlags.Clicked) != 0);
        Assert.Equal(1, hover.PendingCount);
        Assert.Equal(1, clicks.PendingCount);
        Assert.Equal(1, actions.PendingCount);
    }

    [Fact]
    public void Warm_cursor_targeting_allocates_zero_bytes()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        CreateContext(world, Gameplay, priority: 0, diagnostic: false);
        for (var index = 0; index < 64; index++)
        {
            _ = CreateUi(world, new Rectangle(index * 3, index * 2, 50, 30), index, Gameplay);
        }

        SetFrame(world, new Vector2(75, 55));
        var system = new PlayerInputSystem(world, Gameplay, Overlay, new EventStream<PlayerCommandEvent>());
        EventRuntime runtime = AttachEmptyEvents(world);
        var commands = new CommandBuffer();
        for (var index = 0; index < 16; index++)
        {
            Run(system, world, runtime, commands);
        }

        long before = GC.GetAllocatedBytesForCurrentThread();
        for (var index = 0; index < 128; index++)
        {
            Run(system, world, runtime, commands);
        }
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Assert.Equal(0, allocated);
    }

    [Fact]
    public void Descriptors_and_typed_input_route_expose_frozen_phase_and_event_contracts()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        CreateContext(world, Gameplay, priority: 0, diagnostic: false);
        var inputStream = new EventStream<PlayerInputEvent>();
        var runtime = new EventRuntime(new EventRoutingEndpoint(
            new EventRoute<PlayerInputEvent>(
                GlobalUiEventTypeIds.PlayerInput,
                nameof(PlayerInputEvent),
                inputStream,
                new EventConsumerRegistration<PlayerInputEvent>(
                    0,
                    "player-input-state",
                    new PlayerInputEventConsumer(world)))));
        world.AttachEventRuntime(runtime);
        PlayerInputFrame frame = ActiveFrame(new Vector2(12, 34));
        inputStream.Publish(new PlayerInputEvent(frame));
        runtime.DrainBarrier();
        var system = new PlayerInputSystem(world, Gameplay, Overlay, new EventStream<PlayerCommandEvent>());

        Assert.Equal(frame, world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>()).Frame);
        Assert.Equal(SystemPhase.Input, system.Descriptor.Phase);
        Assert.Equal(SceneGroup.Global, system.Descriptor.SceneGroup);
        Assert.Contains(GlobalUiEventTypeIds.PlayerInput, system.Descriptor.ConsumedEventTypeIds.ToArray());
        Assert.Contains(GlobalUiEventTypeIds.PlayerCommand, system.Descriptor.EmittedEventTypeIds.ToArray());
    }

    [Fact]
    public void Hot_key_uses_z_winner_parent_target_and_completes_hold_without_managed_tracker()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        CreateContext(world, Gameplay, priority: 0, diagnostic: false);
        EntityId parent = CreateUi(world, new Rectangle(0, 0, 40, 40), z: 0, Gameplay);
        EntityId low = CreateHotKey(world, z: 1, parent, requiresHold: false);
        EntityId high = CreateHotKey(world, z: 9, parent, requiresHold: true);
        ulong binding = PlayerInputFrame.Mask(PlayerInputButton.ShowHint);
        SetFrame(world, new Vector2(0, 0), binding);
        var holds = new EventStream<HotKeyHoldCompletedEvent>();
        var selected = new EventStream<HotKeySelectEvent>();
        var actions = new EventStream<UIActionEvent>();
        var system = new HotKeySystem(world, Gameplay, Overlay, holds, selected, actions);
        EventRuntime runtime = AttachEmptyEvents(world);
        var commands = new CommandBuffer();

        Run(system, world, runtime, commands, TimeSpan.FromMilliseconds(25));
        Assert.True((world.Get<HotKey>(high).Flags & HotKeyFlags.Holding) != 0);
        Assert.True((world.Get<HotKey>(low).Flags & HotKeyFlags.Pressed) == 0);
        ref PlayerInputState input = ref world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        input.Frame = input.Frame with { PressedButtons = 0 };
        Run(system, world, runtime, commands, TimeSpan.FromMilliseconds(100));

        Assert.True((world.Get<HotKey>(high).Flags & HotKeyFlags.Holding) == 0);
        Assert.Equal(1, holds.PendingCount);
        Assert.Equal(1, selected.PendingCount);
        Assert.Equal(1, actions.PendingCount);
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private static EventRuntime AttachEmptyEvents(World world)
    {
        if (world.HasEventRuntime)
        {
            return world.Events;
        }

        var runtime = new EventRuntime(new EventRoutingEndpoint());
        world.AttachEventRuntime(runtime);
        return runtime;
    }

    private static void Run(
        IGameSystem system,
        World world,
        EventRuntime runtime,
        CommandBuffer commands,
        TimeSpan elapsed = default)
    {
        var context = new SystemContext(world, commands, runtime, 0, elapsed, SceneGroup.Battle);
        system.Update(ref context);
        commands.Playback(world);
    }

    private static EntityId CreateSceneOwned(World world, SceneGroup scene)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new OwnedByScene { Scene = scene });
        return world.Create(in bundle);
    }

    private static EntityId CreateSceneOwned<TTag>(World world, SceneGroup scene)
        where TTag : unmanaged, ITag
    {
        var bundle = new SpawnBundle(2);
        bundle.Add(new OwnedByScene { Scene = scene });
        bundle.AddTag<TTag>();
        return world.Create(in bundle);
    }

    private static EntityId CreateContext(
        World world,
        StringId id,
        int priority,
        bool diagnostic)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new InputContext
        {
            Id = id,
            Priority = priority,
            Flags = InputContextFlags.Active |
                InputContextFlags.AcceptsCursor |
                InputContextFlags.AcceptsCommands |
                (diagnostic ? InputContextFlags.Diagnostic : InputContextFlags.None),
        });
        return world.Create(in bundle);
    }

    private static EntityId CreateUi(
        World world,
        Rectangle bounds,
        int z,
        StringId context,
        float rotation = 0f)
    {
        var bundle = new SpawnBundle(3, 128);
        bundle.Add(new UIElement
        {
            Bounds = bounds,
            Flags = UIInteractionFlags.BaseInteractable,
            EventType = UIElementEventType.CardClicked,
        });
        bundle.Add(new Transform
        {
            Scale = Vector2.One,
            Rotation = rotation,
            ZOrder = z,
        });
        bundle.Add(new InputContextMember { ContextId = context });
        return world.Create(in bundle);
    }

    private static EntityId CreateHotKey(
        World world,
        int z,
        EntityId parent,
        bool requiresHold)
    {
        var bundle = new SpawnBundle(4, 160);
        bundle.Add(new UIElement
        {
            Bounds = new Rectangle(0, 0, 20, 20),
            Flags = UIInteractionFlags.BaseInteractable,
            EventType = UIElementEventType.CardClicked,
        });
        bundle.Add(new Transform { Scale = Vector2.One, ZOrder = z });
        bundle.Add(new InputContextMember { ContextId = Gameplay });
        bundle.Add(new HotKey
        {
            Parent = parent,
            KeyboardBinding = (int)PlayerInputButton.ShowHint,
            GamepadBinding = (byte)PlayerInputButton.ShowHint,
            HoldDurationSeconds = 0.1f,
            Flags = HotKeyFlags.Active |
                (requiresHold ? HotKeyFlags.RequiresHold : HotKeyFlags.None),
        });
        return world.Create(in bundle);
    }

    private static void SetFrame(World world, Vector2 pointer, ulong pressed = 0)
    {
        ref PlayerInputState state = ref world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        state.Frame = ActiveFrame(pointer, pressed);
    }

    private static PlayerInputFrame ActiveFrame(Vector2 pointer, ulong pressed = 0) => new(
        Sequence: 1,
        PointerPosition: pointer,
        PointerDelta: Vector2.Zero,
        LeftStick: Vector2.Zero,
        RightStick: Vector2.Zero,
        ScrollDelta: 0f,
        LeftTrigger: 0f,
        RightTrigger: 0f,
        DownButtons: pressed,
        PressedButtons: pressed,
        ReleasedButtons: 0,
        Device: PlayerInputDevice.KeyboardMouse,
        IsWindowActive: true);

    private sealed class EventStreams
    {
        public EventStream<SceneDeactivating> Deactivating { get; } = new();
        public EventStream<PrepareSceneEvent> Prepare { get; } = new();
        public EventStream<SceneActivating> Activating { get; } = new();
        public EventStream<SceneActivated> Activated { get; } = new();
        public EventStream<DeleteCachesEvent> DeleteCaches { get; } = new();
    }
}
