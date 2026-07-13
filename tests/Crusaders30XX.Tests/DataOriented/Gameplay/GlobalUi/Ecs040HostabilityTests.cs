#nullable enable

using System;
using System.Linq;
using System.Reflection;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;
using Microsoft.Xna.Framework;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.GlobalUi;

public sealed class Ecs040HostabilityTests
{
    private static readonly StringId Gameplay = new(101);
    private static readonly StringId Overlay = new(102);

    [Fact]
    public void Global_hub_routes_every_owned_event_once_with_stable_prioritized_consumers()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        var hub = new GlobalUiEventHub();
        var hostCommands = new HostCommandRequestQueue();
        var routes = hub.BuildRoutes(
            world,
            new GlobalUiRouteConsumers().Add(
                hostCommands,
                GlobalUiRoutePriorities.HostOutput,
                "host.commands"));

        int[] expectedIds =
        [
            4001, 4002, 4003, 4004,
            4010, 4011, 4012, 4013, 4014, 4015, 4016, 4017,
            4020, 4021, 4022, 4023,
            4030, 4031, 4032,
            4040,
        ];
        Assert.Equal(GlobalUiEventRouteContract.RouteCount, routes.Length);
        Assert.Equal(expectedIds, routes.Select(route => route.EventTypeId).ToArray());
        Assert.Equal(routes.Length, routes.Select(route => route.EventTypeId).Distinct().Count());
        Assert.DoesNotContain(
            typeof(GlobalUiEventHub).GetMethods(BindingFlags.Public | BindingFlags.Instance),
            method => method.Name.Contains("Attach", StringComparison.Ordinal));

        IEventRoute input = routes.Single(route => route.EventTypeId == GlobalUiEventTypeIds.PlayerInput);
        Assert.Equal([GlobalUiRoutePriorities.WorldState], GetConsumerPriorities(input));
        IEventRoute commands = routes.Single(route => route.EventTypeId == GlobalUiEventTypeIds.PlayerCommand);
        Assert.Equal([GlobalUiRoutePriorities.HostOutput], GetConsumerPriorities(commands));
    }

    [Fact]
    public void Primitive_host_input_flows_through_input_ui_and_host_command_outputs()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        CreateContext(world, Gameplay, priority: 10);
        EntityId button = CreateButton(world, Gameplay);
        var hub = new GlobalUiEventHub();
        var hostCommands = new HostCommandRequestQueue();
        var actions = new RecordingConsumer<UIActionEvent>();
        var rootConsumers = new GlobalUiRouteConsumers()
            .Add(hostCommands, GlobalUiRoutePriorities.HostOutput, "host.commands")
            .Add(actions, GlobalUiRoutePriorities.CrossDomain, "ui.actions");
        GlobalUiComposition composition = GlobalUiComposition.Create(
            world,
            hub,
            Gameplay,
            Overlay,
            new EventStream<HotKeyHoldCompletedEvent>(),
            new EventStream<HotKeySelectEvent>(),
            rootConsumers);
        var runtime = new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes()));
        var scheduler = new SystemScheduler(world, runtime, profilingEnabled: false);
        foreach (IGameSystem system in composition.Systems)
        {
            scheduler.Register(system);
        }
        scheduler.Build();

        ulong buttons = PlayerInputFrame.Mask(PlayerInputButton.Primary) |
            PlayerInputFrame.Mask(PlayerInputButton.ToggleFullScreen);
        var adapter = new HostInputAdapter();
        DataOrientedInputSubmission submission = adapter.Convert(new HostInputSnapshot(
            ScreenPointer: new Vector2(125, 75),
            PreviousScreenPointer: new Vector2(100, 50),
            LeftStick: Vector2.Zero,
            RightStick: new Vector2(0, 0.5f),
            LeftTrigger: 0,
            RightTrigger: 0,
            ScrollValue: 1,
            PreviousScrollValue: 0,
            DownButtons: buttons,
            PreviousDownButtons: 0,
            Device: PlayerInputDevice.KeyboardMouse,
            IsWindowActive: true,
            RenderDestination: new Rectangle(25, 25, 200, 100),
            VirtualWidth: 100,
            VirtualHeight: 100));
        hub.PlayerInput.Publish(submission.PlayerInput);
        hub.CursorInput.Publish(submission.CursorInput);

        runtime.DrainBarrier();
        scheduler.Update(TimeSpan.FromMilliseconds(16));

        ref readonly PlayerInputState input = ref world.Get<PlayerInputState>(
            world.GetUnique<PlayerInputSingleton>());
        Assert.Equal(new Vector2(50, 50), input.Frame.PointerPosition);
        Assert.Equal(button, input.CursorTarget);
        Assert.Equal(button, actions.Last.Entity);
        Assert.Equal(1, actions.Count);
        Assert.True(hostCommands.TryDequeue(out HostCommandRequest request));
        Assert.Equal(PlayerCommand.ToggleFullScreen, request.Command);
        Assert.False(hostCommands.TryDequeue(out _));
        Assert.True(submission.CursorInput.PrimaryPressed);
        Assert.Equal(0.5f, submission.CursorInput.ScrollStickY);
    }

    [Fact]
    public void Scene_request_preparation_and_activation_flow_through_the_same_root_endpoint()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world, SceneGroup.TitleMenu);
        var hub = new GlobalUiEventHub();
        var prepare = new RecordingConsumer<PrepareSceneEvent>();
        var activated = new RecordingConsumer<SceneActivated>();
        var caches = new RecordingConsumer<DeleteCachesEvent>();
        var consumers = new GlobalUiRouteConsumers()
            .Add(prepare, GlobalUiRoutePriorities.HostOutput, "host.scene-prepare")
            .Add(activated, GlobalUiRoutePriorities.HostOutput, "host.scene-activated")
            .Add(caches, GlobalUiRoutePriorities.HostOutput, "host.cache-invalidation");
        GlobalUiComposition composition = GlobalUiComposition.Create(
            world,
            hub,
            Gameplay,
            Overlay,
            new EventStream<HotKeyHoldCompletedEvent>(),
            new EventStream<HotKeySelectEvent>(),
            consumers);
        var runtime = new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes()));
        var scheduler = new SystemScheduler(world, runtime, profilingEnabled: false);
        foreach (IGameSystem system in composition.Systems)
        {
            scheduler.Register(system);
        }
        scheduler.Build();

        Guid preparationId = Guid.NewGuid();
        hub.LoadScene.Publish(new LoadSceneEvent(
            preparationId,
            SceneGroup.WayStation,
            SceneGroup.TitleMenu));
        runtime.DrainBarrier();
        scheduler.Update(TimeSpan.Zero);

        Assert.Equal(1, prepare.Count);
        Assert.Equal(preparationId, prepare.Last.PreparationId);
        Assert.Equal(SceneTransitionPhase.Ready, world.Get<SceneTransitionState>(
            world.GetUnique<SceneStateSingleton>()).Phase);
        Assert.Equal(0, activated.Count);

        scheduler.Update(TimeSpan.Zero);
        EntityId sceneEntity = world.GetUnique<SceneStateSingleton>();
        Assert.Equal(SceneGroup.WayStation, world.Get<SceneState>(sceneEntity).Current);
        Assert.Equal(1, activated.Count);
        Assert.Equal(1, caches.Count);

        // ActiveScene synchronization belongs to the host and takes effect on the next frame.
        scheduler.ActiveScene = world.Get<SceneState>(sceneEntity).Current;
        Assert.Equal(SceneGroup.WayStation, scheduler.ActiveScene);
    }

    [Fact]
    public void Operational_allowlist_builds_and_excludes_compatibility_names()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        var hub = new GlobalUiEventHub();
        GlobalUiComposition composition = GlobalUiComposition.Create(
            world,
            hub,
            Gameplay,
            Overlay,
            new EventStream<HotKeyHoldCompletedEvent>(),
            new EventStream<HotKeySelectEvent>());
        var runtime = new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes()));
        var scheduler = new SystemScheduler(world, runtime, profilingEnabled: false);

        Assert.Equal(9, composition.Systems.Length);
        Assert.Equal(4, composition.CompatibilitySystemNames.Length);
        foreach (IGameSystem system in composition.Systems)
        {
            SystemDescriptor descriptor = system.Descriptor;
            Assert.Equal(system.GetType(), system.GetType().GetMethod(nameof(IGameSystem.Update))?.DeclaringType);
            Assert.True(
                !descriptor.ReadComponents.IsEmpty ||
                !descriptor.WriteComponents.IsEmpty ||
                !descriptor.ReadDynamicBufferTypes.IsEmpty ||
                !descriptor.WriteDynamicBufferTypes.IsEmpty ||
                !descriptor.ConsumedEventTypeIds.IsEmpty ||
                !descriptor.EmittedEventTypeIds.IsEmpty ||
                descriptor.RecordsStructuralCommands,
                descriptor.Name);
            Assert.DoesNotContain(descriptor.Name, composition.CompatibilitySystemNames.ToArray());
            scheduler.Register(system);
        }

        scheduler.Build();
        Assert.Equal(composition.Systems.Length, scheduler.Count);

        var rootProbe = new EventStream<RootProbe>();
        IEventRoute[] globalRoutes = composition.GetRoutes();
        var rootRoutes = new IEventRoute[globalRoutes.Length + 1];
        globalRoutes.CopyTo(rootRoutes, 0);
        rootRoutes[^1] = new EventRoute<RootProbe>(49001, nameof(RootProbe), rootProbe);
        var rootEndpoint = new EventRoutingEndpoint(rootRoutes);
        Assert.Equal(GlobalUiEventRouteContract.RouteCount + 1, rootEndpoint.RouteCount);
    }

    [Fact]
    public void Snapshot_host_command_queue_suppresses_nonessential_requests()
    {
        World world = CreateWorld();
        GlobalUiWorldBootstrap.Create(world);
        var hub = new GlobalUiEventHub();
        var commands = new HostCommandRequestQueue(snapshotMode: true);
        IEventRoute[] routes = hub.BuildRoutes(
            world,
            new GlobalUiRouteConsumers().Add(
                commands,
                GlobalUiRoutePriorities.HostOutput,
                "snapshot.commands"));
        var runtime = new EventRuntime(new EventRoutingEndpoint(routes));
        world.AttachEventRuntime(runtime);

        hub.PlayerCommand.Publish(new PlayerCommandEvent(
            PlayerCommand.ToggleProfiler,
            PlayerInputDevice.KeyboardMouse));
        hub.PlayerCommand.Publish(new PlayerCommandEvent(
            PlayerCommand.QuitApplication,
            PlayerInputDevice.KeyboardMouse));
        runtime.DrainBarrier();

        Assert.Equal(1, commands.Count);
        Assert.True(commands.TryDequeue(out HostCommandRequest request));
        Assert.Equal(PlayerCommand.QuitApplication, request.Command);
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private static EntityId CreateContext(World world, StringId id, int priority)
    {
        var bundle = new SpawnBundle(1);
        bundle.Add(new InputContext
        {
            Id = id,
            Priority = priority,
            Flags = InputContextFlags.Active |
                InputContextFlags.AcceptsCursor |
                InputContextFlags.AcceptsCommands,
        });
        return world.Create(in bundle);
    }

    private static EntityId CreateButton(World world, StringId context)
    {
        var bundle = new SpawnBundle(3, 128);
        bundle.Add(new UIElement
        {
            Bounds = new Rectangle(40, 40, 20, 20),
            Flags = UIInteractionFlags.BaseInteractable,
            EventType = UIElementEventType.CardClicked,
        });
        bundle.Add(new Transform { Scale = Vector2.One, ZOrder = 1 });
        bundle.Add(new InputContextMember { ContextId = context });
        return world.Create(in bundle);
    }

    private static int[] GetConsumerPriorities(IEventRoute route)
    {
        FieldInfo field = route.GetType().GetField(
            "consumers",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
        Array values = (Array)field.GetValue(route)!;
        var priorities = new int[values.Length];
        for (var index = 0; index < values.Length; index++)
        {
            priorities[index] = (int)values.GetValue(index)!.GetType()
                .GetProperty(nameof(EventConsumerRegistration<RootProbe>.Priority))!
                .GetValue(values.GetValue(index))!;
        }

        return priorities;
    }

    private readonly record struct RootProbe(int Value);

    private sealed class RecordingConsumer<T> : IEventConsumer<T> where T : unmanaged
    {
        public int Count { get; private set; }
        public T Last { get; private set; }

        public void Consume(in T value, ref EventDispatchContext context)
        {
            Count++;
            Last = value;
        }
    }
}
