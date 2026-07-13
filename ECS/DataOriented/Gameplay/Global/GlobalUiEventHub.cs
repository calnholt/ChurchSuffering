#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Global;

/// <summary>
/// Initialization-owned streams for the complete ECS-040 event surface. The hub only returns
/// route fragments; the application root owns the sole endpoint and event runtime.
/// </summary>
public sealed class GlobalUiEventHub
{
    public EventStream<PlayerInputEvent> PlayerInput { get; } = new();
    public EventStream<PlayerCommandEvent> PlayerCommand { get; } = new();
    public EventStream<SetPlayerInputEnabledEvent> SetPlayerInputEnabled { get; } = new();
    public EventStream<CursorInputEvent> CursorInput { get; } = new();
    public EventStream<LoadSceneEvent> LoadScene { get; } = new();
    public EventStream<PrepareSceneEvent> PrepareScene { get; } = new();
    public EventStream<ScenePreparationReady> ScenePreparationReady { get; } = new();
    public EventStream<SceneDeactivating> SceneDeactivating { get; } = new();
    public EventStream<SceneActivating> SceneActivating { get; } = new();
    public EventStream<SceneActivated> SceneActivated { get; } = new();
    public EventStream<DeleteCachesEvent> DeleteCaches { get; } = new();
    public EventStream<PrepareMusicTrackEvent> PrepareMusicTrack { get; } = new();
    public EventStream<ShowNarrativeEventOverlay> ShowNarrativeEventOverlay { get; } = new();
    public EventStream<NarrativeEventOverlayClosedEvent> NarrativeEventOverlayClosed { get; } = new();
    public EventStream<OpenWayStationSaintsMedalsModalEvent> OpenWayStationSaintsMedalsModal { get; } = new();
    public EventStream<TreasureChestOpened> TreasureChestOpened { get; } = new();
    public EventStream<UIHoverChangedEvent> UiHoverChanged { get; } = new();
    public EventStream<UIClickEvent> UiClick { get; } = new();
    public EventStream<UIActionEvent> UiAction { get; } = new();
    public EventStream<TimerElapsedEvent> TimerElapsed { get; } = new();

    /// <summary>
    /// Builds the root-composable ECS-040 fragment. World-writing consumers are always declared
    /// first at priority 100; host and cross-domain consumers are appended explicitly.
    /// </summary>
    public IEventRoute[] BuildRoutes(World world, GlobalUiRouteConsumers? additionalConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        additionalConsumers ??= new GlobalUiRouteConsumers();
        var ownedConsumers = new GlobalUiRouteConsumers()
            .Add(new PlayerInputEventConsumer(world), GlobalUiRoutePriorities.WorldState, "global.player-input-state")
            .Add(new SetPlayerInputEnabledEventConsumer(world), GlobalUiRoutePriorities.WorldState, "global.player-input-enabled")
            .Add(new LoadSceneEventConsumer(world), GlobalUiRoutePriorities.WorldState, "global.scene-load-request")
            .Add(new ScenePreparationReadyConsumer(world), GlobalUiRoutePriorities.WorldState, "global.scene-preparation-ready");

        return
        [
            Route(GlobalUiEventTypeIds.PlayerInput, nameof(PlayerInputEvent), PlayerInput, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.PlayerCommand, nameof(PlayerCommandEvent), PlayerCommand, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.SetPlayerInputEnabled, nameof(SetPlayerInputEnabledEvent), SetPlayerInputEnabled, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.CursorInput, nameof(CursorInputEvent), CursorInput, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.LoadScene, nameof(LoadSceneEvent), LoadScene, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.PrepareScene, nameof(PrepareSceneEvent), PrepareScene, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.ScenePreparationReady, nameof(ScenePreparationReady), ScenePreparationReady, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.SceneDeactivating, nameof(SceneDeactivating), SceneDeactivating, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.SceneActivating, nameof(SceneActivating), SceneActivating, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.SceneActivated, nameof(SceneActivated), SceneActivated, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.DeleteCaches, nameof(DeleteCachesEvent), DeleteCaches, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.PrepareMusicTrack, nameof(PrepareMusicTrackEvent), PrepareMusicTrack, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.ShowNarrativeEventOverlay, nameof(ShowNarrativeEventOverlay), ShowNarrativeEventOverlay, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.NarrativeEventOverlayClosed, nameof(NarrativeEventOverlayClosedEvent), NarrativeEventOverlayClosed, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.OpenWayStationSaintsMedalsModal, nameof(OpenWayStationSaintsMedalsModalEvent), OpenWayStationSaintsMedalsModal, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.TreasureChestOpened, nameof(TreasureChestOpened), TreasureChestOpened, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.UiHoverChanged, nameof(UIHoverChangedEvent), UiHoverChanged, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.UiClick, nameof(UIClickEvent), UiClick, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.UiAction, nameof(UIActionEvent), UiAction, ownedConsumers, additionalConsumers),
            Route(GlobalUiEventTypeIds.TimerElapsed, nameof(TimerElapsedEvent), TimerElapsed, ownedConsumers, additionalConsumers),
        ];
    }

    private static EventRoute<T> Route<T>(
        int id,
        string name,
        EventStream<T> stream,
        GlobalUiRouteConsumers owned,
        GlobalUiRouteConsumers additional)
        where T : unmanaged
    {
        EventConsumerRegistration<T>[] first = owned.Get<T>();
        EventConsumerRegistration<T>[] second = additional.Get<T>();
        if (first.Length == 0)
        {
            return new EventRoute<T>(id, name, stream, second);
        }

        if (second.Length == 0)
        {
            return new EventRoute<T>(id, name, stream, first);
        }

        var combined = new EventConsumerRegistration<T>[first.Length + second.Length];
        first.CopyTo(combined, 0);
        second.CopyTo(combined, first.Length);
        return new EventRoute<T>(id, name, stream, combined);
    }
}

public static class GlobalUiRoutePriorities
{
    public const int WorldState = 100;
    public const int CrossDomain = 0;
    public const int HostOutput = -100;
}

/// <summary>Initialization-only consumer declarations supplied by the root composition.</summary>
public sealed class GlobalUiRouteConsumers
{
    private readonly Dictionary<Type, object> registrations = new();

    public GlobalUiRouteConsumers Add<T>(
        IEventConsumer<T> consumer,
        int priority = GlobalUiRoutePriorities.CrossDomain,
        string? name = null)
        where T : unmanaged
    {
        ArgumentNullException.ThrowIfNull(consumer);
        if (!registrations.TryGetValue(typeof(T), out object? value))
        {
            value = new List<EventConsumerRegistration<T>>();
            registrations.Add(typeof(T), value);
        }

        ((List<EventConsumerRegistration<T>>)value).Add(new EventConsumerRegistration<T>(
            priority,
            name ?? consumer.GetType().Name,
            consumer));
        return this;
    }

    internal EventConsumerRegistration<T>[] Get<T>() where T : unmanaged =>
        registrations.TryGetValue(typeof(T), out object? value)
            ? ((List<EventConsumerRegistration<T>>)value).ToArray()
            : [];
}

public static class GlobalUiEventRouteContract
{
    public const int RouteCount = 20;
}
