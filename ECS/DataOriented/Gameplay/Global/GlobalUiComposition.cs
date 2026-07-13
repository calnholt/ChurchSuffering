#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Crusaders30XX.ECS.DataOriented.Gameplay.Scenes;
using Crusaders30XX.ECS.DataOriented.Gameplay.UI;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Global;

/// <summary>
/// Root-ready ECS-040 fragment. <see cref="Systems"/> is the explicit operational scheduler
/// allowlist; compatibility display/adapter names are deliberately absent from it.
/// </summary>
public sealed class GlobalUiComposition
{
    private static readonly string[] CompatibilityNames =
    [
        "HotKeyProgressRingSystem",
        "MusicManagerSystem",
        "SoundEffectManagerSystem",
        "UIElementHighlightSystem",
    ];

    private readonly IGameSystem[] systems;
    private readonly IEventRoute[] routes;

    private GlobalUiComposition(IGameSystem[] systems, IEventRoute[] routes)
    {
        this.systems = systems;
        this.routes = routes;
    }

    public ReadOnlySpan<IGameSystem> Systems => systems;

    public ReadOnlySpan<IEventRoute> Routes => routes;

    public ReadOnlySpan<string> CompatibilitySystemNames => CompatibilityNames;

    public IEventRoute[] GetRoutes() => (IEventRoute[])routes.Clone();

    public static GlobalUiComposition Create(
        World world,
        GlobalUiEventHub events,
        StringId gameplayContext,
        StringId overlayContext,
        EventStream<HotKeyHoldCompletedEvent> hotKeyHoldCompleted,
        EventStream<HotKeySelectEvent> hotKeySelected,
        GlobalUiRouteConsumers? rootConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(hotKeyHoldCompleted);
        ArgumentNullException.ThrowIfNull(hotKeySelected);

        IGameSystem[] systems =
        [
            new PlayerInputSystem(world, gameplayContext, overlayContext, events.PlayerCommand),
            new PauseMenuInputSystem(world),
            new ModalInputSuppressionSystem(world),
            new UIInteractionSystem(world, events.UiHoverChanged, events.UiClick, events.UiAction),
            new HotKeySystem(
                world,
                gameplayContext,
                overlayContext,
                hotKeyHoldCompleted,
                hotKeySelected,
                events.UiAction),
            new SceneLifecycleSystem(
                world,
                events.SceneDeactivating,
                events.PrepareScene,
                events.SceneActivating,
                events.SceneActivated,
                events.DeleteCaches),
            new SceneLoadingCoordinatorSystem(events.ScenePreparationReady),
            new TimerSchedulerSystem(world, events.TimerElapsed),
            new HighlightSettingsSystem(),
        ];

        return new GlobalUiComposition(systems, events.BuildRoutes(world, rootConsumers));
    }

    /// <summary>Adds the shared rule-queue driver to the same explicit allowlist.</summary>
    public static GlobalUiComposition Create<TState>(
        World world,
        GlobalUiEventHub events,
        StringId gameplayContext,
        StringId overlayContext,
        EventStream<HotKeyHoldCompletedEvent> hotKeyHoldCompleted,
        EventStream<HotKeySelectEvent> hotKeySelected,
        QueuedRuleRuntime<TState> rules,
        SystemId[]? queueRunsAfter = null,
        GlobalUiRouteConsumers? rootConsumers = null)
        where TState : unmanaged
    {
        ArgumentNullException.ThrowIfNull(rules);
        GlobalUiComposition core = Create(
            world,
            events,
            gameplayContext,
            overlayContext,
            hotKeyHoldCompleted,
            hotKeySelected,
            rootConsumers);
        var systems = new IGameSystem[core.systems.Length + 1];
        core.systems.CopyTo(systems, 0);
        systems[^1] = new EventQueueSystem<TState>(rules, queueRunsAfter);
        return new GlobalUiComposition(systems, core.routes);
    }
}
