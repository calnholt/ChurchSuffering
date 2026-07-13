#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Effects;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Meta;
using Crusaders30XX.ECS.DataOriented.Generated;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Storage;
using Crusaders30XX.ECS.DataOriented.Systems;
using Xunit;

namespace Crusaders30XX.Tests.DataOriented.Gameplay.Meta;

public sealed class MetaGameRuntimeTests
{
    [Fact]
    public void Deterministic_climb_fixture_is_repeatable_and_column_complete()
    {
        var first = new ClimbSlotEntry[15];
        var second = new ClimbSlotEntry[15];
        DeterministicClimbGenerator.Generate(0xC0FFEEu, 5, 3, first);
        DeterministicClimbGenerator.Generate(0xC0FFEEu, 5, 3, second);

        Assert.Equal(first, second);
        Assert.Equal(
            [4170923632u, 3677032778u, 3241624533u, 1919745502u, 1350647954u],
            [first[0].Roll, first[3].Roll, first[6].Roll, first[9].Roll, first[12].Roll]);
        for (var column = 0; column < 5; column++)
            Assert.Equal(ClimbSlotKind.Encounter, first[column * 3 + 2].Kind);
    }

    [Fact]
    public void Fresh_save_materializes_run_cards_equipment_achievements_and_round_trips()
    {
        World world = CreateWorld();
        MetaSaveDto fresh = MetaSaveDto.Fresh(1234);

        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, fresh);

        Assert.True(world.IsAlive(roots.Run));
        Assert.True(world.Has<ClimbSceneRoot>(roots.Climb));
        Assert.Equal(3, Count(world.Query<RunDeckCard>()));
        Assert.Equal(1, Count(world.Query<EquippedEquipment>()));
        Assert.Equal(19, Count(world.Query<AchievementGridItem>()));

        MetaSaveDto extracted = MetaSaveAdapter.Extract(world, 1234, 0, 25);
        Assert.Equal(fresh.Cards, extracted.Cards);
        Assert.Equal(fresh.Equipment, extracted.Equipment);
        Assert.Equal(19, extracted.Achievements.Length);
        Assert.Throws<NotSupportedException>(() => MetaSaveAdapter.Spawn(CreateWorld(), new MetaSaveDto { Version = 0 }));
    }

    [Fact]
    public void Way_station_modal_interrupts_active_dialogue_and_suppresses_it_until_resumed()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh());
        var events = new MetaGameEventHub();
        MetaGameComposition composition = MetaGameComposition.Create(world, events);
        Attach(world, composition);

        events.DialogueSequenceRequested.Publish(new(roots.WayStation, new StringId(123)));
        world.Events.DrainBarrier();
        Assert.Equal(DialogueState.Playing, world.Get<DialogOverlayState>(roots.WayStation).State);

        events.OpenWayStationClimbSettingsModal.Publish(new(roots.WayStation));
        world.Events.DrainBarrier();

        DialogOverlayState dialogue = world.Get<DialogOverlayState>(roots.WayStation);
        Assert.Equal(DialogueState.Interrupted, dialogue.State);
        Assert.Equal(MetaModalKind.ClimbSettings, dialogue.InterruptedBy);
        Assert.Equal(1, world.Get<WayStationArrivalContextState>(roots.WayStation).ModalDepth);
        Query<WayStationClimbModalDifficultyChoice> modals = world.Query<WayStationClimbModalDifficultyChoice>(
            new QueryFilter(All: ComponentSignature.Empty.With(ComponentType<WayStationClimbModalRoot>.Id)));
        Assert.Equal(1, Count(modals));
    }

    [Fact]
    public void Dialogue_tutorial_and_achievement_routes_form_deterministic_operational_flows()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh());
        EntityId tutorialEntity = world.Create(default);
        DynamicBufferHandle<TutorialStepEntry> steps = world.CreateDynamicBuffer<TutorialStepEntry>(tutorialEntity, 2);
        world.GetDynamicBuffer(steps).Add(new(new StringId(1), 10, 0));
        world.GetDynamicBuffer(steps).Add(new(new StringId(2), 20, 0));
        var tutorial = new GuidedTutorial { Steps = steps };
        world.Add(tutorialEntity, in tutorial);

        var events = new MetaGameEventHub();
        MetaGameComposition composition = MetaGameComposition.Create(world, events);
        var completed = new Capture<AchievementCompletedEvent>();
        var tutorialCompleted = new Capture<TutorialCompletedEvent>();
        var root = new MetaGameRouteConsumers()
            .Add<AchievementCompletedEvent>(completed, -10)
            .Add<TutorialCompletedEvent>(tutorialCompleted, -10);
        composition = MetaGameComposition.Create(world, events, root);
        Attach(world, composition);

        events.WayStationDialoguePoiSelected.Publish(new(roots.WayStation, new StringId(400)));
        events.TutorialStarted.Publish(new(tutorialEntity, 0));
        events.AchievementProgressUpdated.Publish(new(AchievementId.FirstVictory, 1, 0, 0));
        world.Events.DrainBarrier();

        Assert.Equal(DialogueState.Playing, world.Get<DialogOverlayState>(roots.WayStation).State);
        Assert.Equal(TutorialState.Running, world.Get<GuidedTutorial>(tutorialEntity).State);
        Assert.Equal(AchievementId.FirstVictory, completed.Value.Achievement);

        events.AdvanceTutorial.Publish(new(tutorialEntity, 10));
        events.AdvanceTutorial.Publish(new(tutorialEntity, 20));
        world.Events.DrainBarrier();
        Assert.Equal(TutorialState.Complete, world.Get<GuidedTutorial>(tutorialEntity).State);
        Assert.Equal(tutorialEntity, tutorialCompleted.Value.Tutorial);
    }

    [Fact]
    public void Routes_ledgers_and_operational_metadata_are_complete_and_unique()
    {
        World world = CreateWorld();
        var hub = new MetaGameEventHub();
        MetaGameComposition composition = MetaGameComposition.Create(world, hub);
        IEventRoute[] routes = composition.GetRoutes();

        Assert.Equal(80, routes.Length);
        Assert.Equal(80, routes.Select(value => value.EventTypeId).Distinct().Count());
        Assert.Equal(MetaGameEventTypeIds.First, routes[0].EventTypeId);
        Assert.Equal(MetaGameEventTypeIds.Last, routes[^1].EventTypeId);
        Assert.Equal(12, composition.CompatibilitySystems.Length);
        Assert.Equal(12, composition.CompatibilitySystems.ToArray().Select(value => value.Descriptor.Name).Distinct().Count());
        Assert.Equal(7, composition.Systems.Length);
        Assert.Empty(composition.Systems.ToArray().Select(value => value.Descriptor.Id)
            .Intersect(composition.CompatibilitySystems.ToArray().Select(value => value.Descriptor.Id)));
        Assert.All(composition.Systems.ToArray(), system =>
        {
            SystemDescriptor descriptor = system.Descriptor;
            Assert.True(descriptor.Id.IsValid);
            Assert.True(descriptor.WriteComponents != ComponentSignature.Empty ||
                        descriptor.WriteDynamicBufferTypes.Length > 0 || descriptor.RecordsStructuralCommands);
            Assert.True(descriptor.ConsumedEventTypeIds.Length > 0);
        });

        Type[] componentTypes = typeof(AchievementGridItem).Assembly.GetTypes()
            .Where(type => type.Namespace == typeof(AchievementGridItem).Namespace &&
                           (typeof(IComponent).IsAssignableFrom(type) || typeof(ITag).IsAssignableFrom(type)))
            .ToArray();
        Assert.Equal(50, componentTypes.Length);
        Assert.Equal(22, GeneratedMetaObjectCatalog.Definitions.Length);
    }

    [Fact]
    public void Cross_domain_card_and_global_routes_feed_meta_progression_without_duplicate_streams()
    {
        World world = CreateWorld();
        MetaRuntimeEntities roots = MetaSaveAdapter.Spawn(world, MetaSaveDto.Fresh());
        var metaHub = new MetaGameEventHub();
        MetaGameComposition meta = MetaGameComposition.Create(world, metaHub);
        var cards = new CardGameplayEventHub();
        var global = new GlobalUiEventHub();
        IEventRoute[] cardRoutes = cards.BuildRoutes(meta.CrossDomainRoutes.RegisterCards());
        IEventRoute[] globalRoutes = global.BuildRoutes(world, meta.CrossDomainRoutes.RegisterGlobal());
        IEventRoute[] metaRoutes = meta.GetRoutes();
        var routes = new IEventRoute[cardRoutes.Length + globalRoutes.Length + metaRoutes.Length];
        cardRoutes.CopyTo(routes, 0);
        globalRoutes.CopyTo(routes, cardRoutes.Length);
        metaRoutes.CopyTo(routes, cardRoutes.Length + globalRoutes.Length);
        world.AttachEventRuntime(new EventRuntime(new EventRoutingEndpoint(routes)));

        cards.CardPlayed.Publish(new(EntityId.Null, roots.Run, EntityId.Null, 0, 0));
        global.OpenWayStationSaintsMedalsModal.Publish(default);
        world.Events.DrainBarrier();

        AchievementGridItem player = FindAchievement(world, AchievementId.CardPlayer);
        Assert.Equal(1, player.Progress);
        Assert.Equal(1, Count(world.Query<WayStationSaintsMedalsModalRoot>()));
    }

    private static World CreateWorld() => new(GeneratedComponentRegistry.Create());

    private static void Attach(World world, MetaGameComposition composition) =>
        world.AttachEventRuntime(new EventRuntime(new EventRoutingEndpoint(composition.GetRoutes())));

    private static int Count<T>(Query<T> query) where T : unmanaged, IComponent
    {
        var count = 0;
        foreach (QueryChunk<T> chunk in query) count += chunk.Count;
        return count;
    }

    private static AchievementGridItem FindAchievement(World world, AchievementId id)
    {
        foreach (QueryChunk<AchievementGridItem> chunk in world.Query<AchievementGridItem>())
        foreach (int row in chunk.Rows)
            if (chunk.Component1[row].Achievement == id) return chunk.Component1[row];
        throw new InvalidOperationException($"Achievement {id} was not materialized.");
    }

    private sealed class Capture<T> : IEventConsumer<T> where T : unmanaged
    {
        public T Value { get; private set; }
        public void Consume(in T value, ref EventDispatchContext context) => Value = value;
    }
}
