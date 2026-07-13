#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Meta;

public sealed class MetaGameComposition
{
    private readonly IGameSystem[] systems;
    private readonly IGameSystem[] compatibilitySystems;
    private readonly IEventRoute[] routes;
    private readonly MetaGameCrossDomainRoutes crossDomainRoutes;

    private MetaGameComposition(
        IGameSystem[] systems,
        IGameSystem[] compatibilitySystems,
        IEventRoute[] routes,
        MetaGameCrossDomainRoutes crossDomainRoutes)
    {
        this.systems = systems;
        this.compatibilitySystems = compatibilitySystems;
        this.routes = routes;
        this.crossDomainRoutes = crossDomainRoutes;
    }

    /// <summary>Only consolidated, operational systems safe for the root scheduler.</summary>
    public ReadOnlySpan<IGameSystem> Systems => systems;
    /// <summary>Exactly twelve legacy responsibility descriptors; never register these with the scheduler.</summary>
    public ReadOnlySpan<IGameSystem> CompatibilitySystems => compatibilitySystems;
    public ReadOnlySpan<IEventRoute> Routes => routes;
    public MetaGameCrossDomainRoutes CrossDomainRoutes => crossDomainRoutes;

    public IEventRoute[] GetRoutes() => (IEventRoute[])routes.Clone();

    public static MetaGameComposition Create(World world, MetaGameEventHub events, MetaGameRouteConsumers? rootConsumers = null)
    {
        ArgumentNullException.ThrowIfNull(world);
        ArgumentNullException.ThrowIfNull(events);

        var climb = new ClimbRuntimeSystem(world);
        var wayStation = new WayStationRuntimeSystem(world);
        var rewards = new RewardRuntimeSystem(world);
        var achievements = new AchievementRuntimeSystem(world, events);
        var tutorials = new TutorialRuntimeSystem(world, events);
        var dialogue = new DialogueRuntimeSystem(world, events);
        var run = new RunLifecycleRuntimeSystem(world);
        var achievementTriggers = new MetaAchievementTriggerConsumer(events);
        var crossDomain = new MetaGameCrossDomainRoutes(world, events, wayStation);

        var consumers = new MetaGameRouteConsumers()
            .Add<ClimbEncounterSlotSelectedEvent>(climb, 100)
            .Add<ClimbEventSlotSelectedEvent>(climb, 100)
            .Add<ClimbShopSlotSelectedEvent>(climb, 100)
            .Add<ClimbPreviewStartedEvent>(climb, 100)
            .Add<ClimbPreviewClearedEvent>(climb, 100)
            .Add<DialogueSequenceCompleted>(climb, 50)
            .Add<OpenWayStationClimbSettingsModalEvent>(wayStation, 100)
            .Add<ShowBoosterPackOpeningOverlayEvent>(rewards, 100)
            .Add<BoosterPackOpeningDismissedEvent>(rewards, 100)
            .Add<BoosterPackOpeningDismissedEvent>(achievementTriggers, 0)
            .Add<CloseBoosterPackOpeningOverlayEvent>(rewards, 100)
            .Add<ShowQuestRewardOverlay>(rewards, 100)
            .Add<ShowQuestRewardOverlay>(achievementTriggers, 0)
            .Add<StartBattleRequested>(achievementTriggers, 0)
            .Add<ModifyCourageRequestEvent>(achievementTriggers, 0)
            .Add<ClimbEndedEvent>(achievementTriggers, 0)
            .Add<ClimbPointsSegmentAwardedEvent>(achievementTriggers, 0)
            .Add<AchievementAnimationsComplete>(achievementTriggers, 0)
            .Add<AchievementProgressUpdatedEvent>(achievements, 100)
            .Add<AchievementSeenEvent>(achievements, 100)
            .Add<AchievementRevealClickedEvent>(achievements, 100)
            .Add<TutorialStartedEvent>(tutorials, 100)
            .Add<AdvanceTutorialEvent>(tutorials, 100)
            .Add<GuidedTutorialRestartRequested>(tutorials, 100)
            .Add<GuidedTutorialSkipRequested>(tutorials, 100)
            .Add<DialogueSequenceRequested>(dialogue, 100)
            .Add<WayStationDialoguePoiSelectedEvent>(dialogue, 100)
            .Add<DialogSkipRequested>(dialogue, 100)
            .Add<DialogEnded>(dialogue, 100)
            .Add<NarrativeModalChoiceRequested>(dialogue, 100)
            .Add<LoadoutCardAdded>(run, 100)
            .Add<LoadoutCardRemoved>(run, 100)
            .Add<QuestSelected>(run, 100)
            .Add<QuestSelected>(achievementTriggers, 0);

        IGameSystem[] compatibility =
        [
            new MetaCompatibilitySystem(MetaGameSystemIds.AchievementExplosion, "AchievementExplosionSystem", SceneGroup.Achievement),
            new MetaCompatibilitySystem(MetaGameSystemIds.AchievementScene, "AchievementSceneSystem", SceneGroup.Achievement),
            new MetaCompatibilitySystem(MetaGameSystemIds.GuidedTutorialDirector, "GuidedTutorialDirectorSystem", SceneGroup.Battle),
            new MetaCompatibilitySystem(MetaGameSystemIds.TutorialManager, "TutorialManager", SceneGroup.Battle),
            new MetaCompatibilitySystem(MetaGameSystemIds.ClimbScene, "ClimbSceneSystem", SceneGroup.Climb),
            new MetaCompatibilitySystem(MetaGameSystemIds.WayStationClimbSettingsModal, "WayStationClimbSettingsModalSystem", SceneGroup.WayStation),
            new MetaCompatibilitySystem(MetaGameSystemIds.WayStationDialogue, "WayStationDialogueSystem", SceneGroup.WayStation),
            new MetaCompatibilitySystem(MetaGameSystemIds.WayStationSaintsMedalsModal, "WayStationSaintsMedalsModalSystem", SceneGroup.WayStation),
            new MetaCompatibilitySystem(MetaGameSystemIds.ClimbEncounter, "ClimbEncounterSystem", SceneGroup.Climb),
            new MetaCompatibilitySystem(MetaGameSystemIds.ClimbEvent, "ClimbEventSystem", SceneGroup.Climb),
            new MetaCompatibilitySystem(MetaGameSystemIds.CollectionProgression, "CollectionProgressionSystem", SceneGroup.Global),
            new MetaCompatibilitySystem(MetaGameSystemIds.RunDeckLifecycle, "RunDeckLifecycleSystem", SceneGroup.Global),
        ];

        return new MetaGameComposition(
            [climb, wayStation, rewards, achievements, tutorials, dialogue, run],
            compatibility,
            events.BuildRoutes(consumers, rootConsumers),
            crossDomain);
    }
}
