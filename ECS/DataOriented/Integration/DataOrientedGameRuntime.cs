#nullable enable

using System;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Authoring.Combat;
using Crusaders30XX.ECS.DataOriented.Authoring.Meta;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Combat;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Integration;

/// <summary>
/// Coordinator-owned application root. It is the only place that combines domain route fragments,
/// attaches an event runtime, and registers scheduler systems.
/// </summary>
public sealed class DataOrientedGameRuntime : IDisposable
{
    private readonly DynamicBufferStore buffers;
    private CombatPresentationAuthoringHandle? combatPresentation;
    private int sceneRequestSequence;
    private bool disposed;

    private DataOrientedGameRuntime(
        World world,
        DynamicBufferStore buffers,
        EventRuntime events,
        SystemScheduler scheduler,
        GlobalUiGlobals globals,
        MetaRuntimeEntities metaEntities,
        GlobalUiComposition global,
        CardGameplayComposition cards,
        CombatGameplayComposition combat,
        EffectGameplayComposition effects,
        MetaGameComposition meta,
        CombatSessionSlot combatSessions,
        CombatOwnedEventConsumers combatConsumers,
        MetaSceneAuthoringConsumer metaSceneAuthoring,
        PositionTweenPresentationSystem positionTweens,
        ParallaxPresentationSystem parallax,
        JigglePulsePresentationSystem jiggle,
        VisualEffectPresentationSystem visualEffects,
        SpriteRenderExtractionSystem renderExtraction,
        TextRenderExtractionSystem textRenderExtraction,
        RenderPacketStore packets,
        TextRenderPacketStore textPackets,
        PresentationRequestQueues presentationRequests,
        HostCommandRequestQueue hostCommands,
        PendingBattleRequestQueue battleRequests,
        GlobalUiEventHub globalEvents,
        CardGameplayEventHub cardEvents,
        CombatEventHub combatEvents,
        EffectGameplayEventHub effectEvents,
        MetaGameEventHub metaEvents,
        PresentationEventHub presentationEvents)
    {
        World = world;
        this.buffers = buffers;
        Events = events;
        Scheduler = scheduler;
        Globals = globals;
        MetaEntities = metaEntities;
        Global = global;
        Cards = cards;
        Combat = combat;
        Effects = effects;
        Meta = meta;
        CombatSessions = combatSessions;
        CombatConsumers = combatConsumers;
        MetaSceneAuthoring = metaSceneAuthoring;
        PositionTweens = positionTweens;
        Parallax = parallax;
        Jiggle = jiggle;
        VisualEffects = visualEffects;
        RenderExtraction = renderExtraction;
        TextRenderExtraction = textRenderExtraction;
        Packets = packets;
        TextPackets = textPackets;
        PresentationRequests = presentationRequests;
        HostCommands = hostCommands;
        BattleRequests = battleRequests;
        GlobalEvents = globalEvents;
        CardEvents = cardEvents;
        CombatEvents = combatEvents;
        EffectEvents = effectEvents;
        MetaEvents = metaEvents;
        PresentationEvents = presentationEvents;
        Input = new HostInputAdapter();
    }

    public World World { get; }
    public EventRuntime Events { get; }
    public SystemScheduler Scheduler { get; }
    public GlobalUiGlobals Globals { get; }
    public MetaRuntimeEntities MetaEntities { get; }
    public GlobalUiComposition Global { get; }
    public CardGameplayComposition Cards { get; }
    public CombatGameplayComposition Combat { get; }
    public EffectGameplayComposition Effects { get; }
    public MetaGameComposition Meta { get; }
    public CombatSessionSlot CombatSessions { get; }
    public CombatOwnedEventConsumers CombatConsumers { get; }
    public MetaSceneAuthoringConsumer MetaSceneAuthoring { get; }
    public CombatPresentationAuthoringHandle? CombatPresentation => combatPresentation;
    public PositionTweenPresentationSystem PositionTweens { get; }
    public ParallaxPresentationSystem Parallax { get; }
    public JigglePulsePresentationSystem Jiggle { get; }
    public VisualEffectPresentationSystem VisualEffects { get; }
    public SpriteRenderExtractionSystem RenderExtraction { get; }
    public TextRenderExtractionSystem TextRenderExtraction { get; }
    public RenderPacketStore Packets { get; }
    public TextRenderPacketStore TextPackets { get; }
    public PresentationRequestQueues PresentationRequests { get; }
    public HostCommandRequestQueue HostCommands { get; }
    public PendingBattleRequestQueue BattleRequests { get; }
    public HostInputAdapter Input { get; }
    public GlobalUiEventHub GlobalEvents { get; }
    public CardGameplayEventHub CardEvents { get; }
    public CombatEventHub CombatEvents { get; }
    public EffectGameplayEventHub EffectEvents { get; }
    public MetaGameEventHub MetaEvents { get; }
    public PresentationEventHub PresentationEvents { get; }

    public static DataOrientedGameRuntime Create(
        SceneGroup initialScene = SceneGroup.TitleMenu,
        MetaSaveDto? save = null,
        bool snapshotMode = false)
    {
        var world = new World(GeneratedComponentRegistry.Create());
        DynamicBufferStore buffers = world.DynamicBuffers;
        GlobalUiGlobals globals = GlobalUiWorldBootstrap.Create(world, initialScene);
        MetaRuntimeEntities metaEntities = MetaSaveAdapter.Spawn(world, save ?? MetaSaveDto.Fresh());

        var globalEvents = new GlobalUiEventHub();
        var cardEvents = new CardGameplayEventHub();
        var combatEvents = new CombatEventHub();
        var effectEvents = new EffectGameplayEventHub();
        var metaEvents = new MetaGameEventHub();
        var presentationEvents = new PresentationEventHub();

        var hostCommands = new HostCommandRequestQueue(snapshotMode: snapshotMode);
        var battleRequests = new PendingBattleRequestQueue();
        var sceneBridge = new SceneTransitionRequestConsumer(world, globalEvents);
        var combatSessions = new CombatSessionSlot(world);
        var cardInputRequests = new CardInputRequestConsumer(world, cardEvents);
        var combatInputRequests = new CombatInputRequestConsumer(combatSessions);
        var metaRootConsumers = new MetaGameRouteConsumers()
            .Add<StartBattleRequested>(battleRequests, priority: -100, name: "host.start-battle")
            .Add<SceneTransitionRequested>(sceneBridge, priority: -100, name: "host.scene-transition")
            .Add<PlayCardRequested>(cardInputRequests, priority: -50, name: "cards.input-play")
            .Add<PledgeCardRequested>(cardInputRequests, priority: -50, name: "cards.input-pledge")
            .Add<AssignCardAsBlockRequested>(combatInputRequests, priority: -50, name: "combat.input-assign")
            .Add<UnassignCardAsBlockRequested>(combatInputRequests, priority: -50, name: "combat.input-unassign")
            .Add<ConfirmBlocksRequested>(combatInputRequests, priority: -50, name: "combat.input-confirm")
            .Add<EndTurnRequested>(combatInputRequests, priority: -50, name: "combat.input-end-turn");
        MetaGameComposition meta = MetaGameComposition.Create(world, metaEvents, metaRootConsumers);

        var metaSceneAuthoring = new MetaSceneAuthoringConsumer(world);
        GlobalUiRouteConsumers globalRootConsumers = metaSceneAuthoring.Register(
            meta.CrossDomainRoutes.RegisterGlobal()
                .Add<PlayerCommandEvent>(hostCommands, GlobalUiRoutePriorities.HostOutput, "host.commands"));
        CardGameplayRouteConsumers cardRootConsumers = meta.CrossDomainRoutes.RegisterCards();
        EffectGameplayRouteConsumers effectRootConsumers = meta.CrossDomainRoutes.RegisterEffects();

        var combatConsumers = new CombatOwnedEventConsumers(combatSessions);
        CombatRouteConsumers combatRootConsumers = meta.CrossDomainRoutes.RegisterCombat();
        combatConsumers.RegisterRoutes(combatRootConsumers);

        var presentationRequests = new PresentationRequestQueues(32);
        var presentationConsumer = new PresentationRequestConsumer(presentationRequests);
        var packets = new RenderPacketStore(256);
        var textPackets = new TextRenderPacketStore(32);
        var positionTweens = new PositionTweenPresentationSystem(world);
        var parallax = new ParallaxPresentationSystem(world);
        var jiggle = new JigglePulsePresentationSystem(world);
        var visualEffects = new VisualEffectPresentationSystem(
            world,
            presentationEvents.VisualEffectImpactReached,
            presentationEvents.VisualEffectCompleted);
        var renderExtraction = new SpriteRenderExtractionSystem(world, packets);
        var textRenderExtraction = new TextRenderExtractionSystem(world, textPackets);

        GlobalUiComposition global = GlobalUiComposition.Create(
            world,
            globalEvents,
            new StringId(1001),
            new StringId(1002),
            cardEvents.HotKeyHoldCompleted,
            cardEvents.HotKeySelect,
            globalRootConsumers);
        CardGameplayComposition cards = CardGameplayComposition.Create(world, cardEvents, cardRootConsumers);
        EffectGameplayComposition effects = EffectGameplayComposition.Create(world, buffers, effectEvents, effectRootConsumers);
        CombatGameplayComposition combat = CombatGameplayComposition.Create(combatSessions);

        IEventRoute[] routes = CombineRoutes(
            global.Routes,
            cards.Routes,
            combatEvents.BuildRoutes(combatRootConsumers),
            effects.Routes,
            meta.Routes,
            presentationEvents.BuildRoutes(presentationConsumer, jiggle));
        var events = new EventRuntime(new EventRoutingEndpoint(routes));
        var scheduler = new SystemScheduler(world, events, profilingEnabled: false)
        {
            ActiveScene = initialScene,
        };

        Register(scheduler, global.Systems);
        Register(scheduler, cards.Systems);
        Register(scheduler, combat.Systems);
        Register(scheduler, effects.Systems);
        Register(scheduler, meta.Systems);
        scheduler.Register(positionTweens);
        scheduler.Register(parallax);
        scheduler.Register(jiggle);
        scheduler.Register(visualEffects);
        scheduler.Register(renderExtraction);
        scheduler.Register(textRenderExtraction);
        scheduler.Build();

        metaSceneAuthoring.Materialize(initialScene);

        return new DataOrientedGameRuntime(
            world, buffers, events, scheduler, globals, metaEntities, global, cards, combat, effects, meta,
            combatSessions, combatConsumers, metaSceneAuthoring, positionTweens, parallax, jiggle, visualEffects,
            renderExtraction, textRenderExtraction, packets, textPackets,
            presentationRequests, hostCommands, battleRequests,
            globalEvents, cardEvents, combatEvents, effectEvents, metaEvents, presentationEvents);
    }

    public void SubmitInput(in HostInputSnapshot snapshot)
    {
        ThrowIfDisposed();
        DataOrientedInputSubmission submission = Input.Convert(snapshot);
        SubmitInput(in submission);
    }

    public void SubmitInput(in DataOrientedInputSubmission submission)
    {
        ThrowIfDisposed();
        GlobalEvents.PlayerInput.Publish(submission.PlayerInput);
        GlobalEvents.CursorInput.Publish(submission.CursorInput);
        Events.DrainBarrier();
    }

    public void Update(TimeSpan elapsed)
    {
        ThrowIfDisposed();
        PresentationRequests.BeginFrame();
        Scheduler.ActiveScene = World.Get<Crusaders30XX.ECS.DataOriented.Components.SceneState>(Globals.Scene).Current;
        Scheduler.Update(elapsed);
    }

    public CombatSession BeginCombat(
        EnemyId enemy,
        int playerHealth = 30,
        ulong seed = 1,
        bool finalBattle = false)
    {
        ThrowIfDisposed();
        if (CombatSessions.Current is not null)
            throw new InvalidOperationException("End the active combat session before starting another battle.");
        CombatSession session = CombatSession.Create(World, CombatEvents, enemy, playerHealth, seed, finalBattle);
        CombatSessions.Bind(session);
        try
        {
            combatPresentation = CombatPresentationAuthoring.Materialize(session);
            BindCombatCardBoundary(session, combatPresentation);
        }
        catch
        {
            DestroyCombatSession(session);
            throw;
        }
        return session;
    }

    public CombatSession BeginTestCombat(in DataOrientedTestFightFixture fixture)
    {
        ThrowIfDisposed();
        if (CombatSessions.Current is not null)
            throw new InvalidOperationException("End the active combat session before starting another battle.");

        CombatSession session = fixture.CreateSession(World, CombatEvents);
        CombatSessions.Bind(session);
        try
        {
            combatPresentation = CombatPresentationAuthoring.MaterializeTestFight(session, in fixture);
            BindCombatCardBoundary(session, combatPresentation);
        }
        catch
        {
            DestroyCombatSession(session);
            throw;
        }
        return session;
    }

    public void EndCombat()
    {
        ThrowIfDisposed();
        CombatSession session = CombatSessions.RequireActive();
        if (Events.PendingEventCount != 0) Events.DrainBarrier();
        combatPresentation?.Dispose();
        combatPresentation = null;
        DestroyCombatSession(session);
    }

    public void RequestScene(SceneGroup scene)
    {
        ThrowIfDisposed();
        if (scene == SceneGroup.Global) throw new ArgumentOutOfRangeException(nameof(scene));
        int sequence = ++sceneRequestSequence;
        var id = new Guid(sequence, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        SceneGroup current = World.Get<Crusaders30XX.ECS.DataOriented.Components.SceneState>(Globals.Scene).Current;
        GlobalEvents.LoadScene.Publish(new LoadSceneEvent(id, scene, current));
        Events.DrainBarrier();
    }

    public MetaSaveDto ExtractSave(uint climbSeed, int currentColumn, int gold)
    {
        ThrowIfDisposed();
        return MetaSaveAdapter.Extract(World, climbSeed, currentColumn, gold);
    }

    public void Dispose()
    {
        if (disposed) return;
        if (CombatSessions.Current is not null) EndCombat();
        MetaSceneAuthoring.Dispose();
        buffers.Dispose();
        disposed = true;
    }

    private void DestroyCombatSession(CombatSession session)
    {
        EntityId player = session.Player;
        EntityId enemy = session.Enemy;
        EntityId battle = session.Battle;
        CombatSessions.Unbind(session);
        if (World.IsAlive(battle)) World.Destroy(battle);
        if (World.IsAlive(enemy)) World.Destroy(enemy);
        if (World.IsAlive(player)) World.Destroy(player);
    }

    private void BindCombatCardBoundary(
        CombatSession session,
        CombatPresentationAuthoringHandle presentation)
    {
        if (presentation.Deck.IsNull) return;
        session.BindCardBoundary(
            presentation.Deck,
            new CombatCardBoundary(World, CardEvents));
    }

    private static void Register(SystemScheduler scheduler, ReadOnlySpan<IGameSystem> systems)
    {
        for (var index = 0; index < systems.Length; index++) scheduler.Register(systems[index]);
    }

    private static IEventRoute[] CombineRoutes(
        ReadOnlySpan<IEventRoute> global,
        ReadOnlySpan<IEventRoute> cards,
        ReadOnlySpan<IEventRoute> combat,
        ReadOnlySpan<IEventRoute> effects,
        ReadOnlySpan<IEventRoute> meta,
        ReadOnlySpan<IEventRoute> presentation)
    {
        var routes = new IEventRoute[
            global.Length + cards.Length + combat.Length + effects.Length + meta.Length + presentation.Length];
        var offset = 0;
        Copy(global, routes, ref offset);
        Copy(cards, routes, ref offset);
        Copy(combat, routes, ref offset);
        Copy(effects, routes, ref offset);
        Copy(meta, routes, ref offset);
        Copy(presentation, routes, ref offset);
        return routes;
    }

    private static void Copy(ReadOnlySpan<IEventRoute> source, IEventRoute[] destination, ref int offset)
    {
        source.CopyTo(destination.AsSpan(offset));
        offset += source.Length;
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);
}

/// <summary>Fixed-capacity host hand-off for meta requests that create a combat session.</summary>
public sealed class PendingBattleRequestQueue : IEventConsumer<StartBattleRequested>
{
    private readonly StartBattleRequested[] values = new StartBattleRequested[8];
    private int readIndex;
    private int count;

    public int Count => count;

    public void Consume(in StartBattleRequested value, ref EventDispatchContext context)
    {
        if (count == values.Length) throw new InvalidOperationException("The pending battle request queue is full.");
        values[(readIndex + count) % values.Length] = value;
        count++;
    }

    public bool TryDequeue(out StartBattleRequested value)
    {
        if (count == 0)
        {
            value = default;
            return false;
        }
        value = values[readIndex];
        values[readIndex] = default;
        readIndex = (readIndex + 1) % values.Length;
        count--;
        return true;
    }
}

internal sealed class SceneTransitionRequestConsumer : IEventConsumer<SceneTransitionRequested>
{
    private readonly World world;
    private readonly GlobalUiEventHub global;
    private int sequence;

    public SceneTransitionRequestConsumer(World world, GlobalUiEventHub global)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        this.global = global ?? throw new ArgumentNullException(nameof(global));
    }

    public void Consume(in SceneTransitionRequested value, ref EventDispatchContext context)
    {
        int next = ++sequence;
        var id = new Guid(next, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        EntityId scene = world.GetUnique<Crusaders30XX.ECS.DataOriented.Components.SceneStateSingleton>();
        SceneGroup current = world.Get<Crusaders30XX.ECS.DataOriented.Components.SceneState>(scene).Current;
        global.LoadScene.Publish(new LoadSceneEvent(id, value.Scene, current));
    }
}
