#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Scenes;

/// <summary>Owns deterministic teardown and scene-state activation; presentation prepares assets separately.</summary>
public sealed class SceneLifecycleSystem : IGameSystem
{
    private readonly Query<OwnedByScene> ownedEntities;
    private readonly EventStream<SceneDeactivating> deactivating;
    private readonly EventStream<PrepareSceneEvent> prepareScene;
    private readonly EventStream<SceneActivating> activating;
    private readonly EventStream<SceneActivated> activated;
    private readonly EventStream<DeleteCachesEvent> deleteCaches;

    public SceneLifecycleSystem(
        World world,
        EventStream<SceneDeactivating> deactivating,
        EventStream<PrepareSceneEvent> prepareScene,
        EventStream<SceneActivating> activating,
        EventStream<SceneActivated> activated,
        EventStream<DeleteCachesEvent> deleteCaches)
    {
        ArgumentNullException.ThrowIfNull(world);
        this.deactivating = deactivating ?? throw new ArgumentNullException(nameof(deactivating));
        this.prepareScene = prepareScene ?? throw new ArgumentNullException(nameof(prepareScene));
        this.activating = activating ?? throw new ArgumentNullException(nameof(activating));
        this.activated = activated ?? throw new ArgumentNullException(nameof(activated));
        this.deleteCaches = deleteCaches ?? throw new ArgumentNullException(nameof(deleteCaches));
        ownedEntities = world.Query<OwnedByScene>(new QueryFilter(DebugName: "ECS040.SceneOwned"));
        Descriptor = CreateDescriptor();
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        EntityId global = context.World.GetUnique<SceneStateSingleton>();
        ref SceneState scene = ref context.World.Get<SceneState>(global);
        ref SceneTransitionState transition = ref context.World.Get<SceneTransitionState>(global);
        ref ScenePreparationState preparation = ref context.World.Get<ScenePreparationState>(global);

        if (transition.Phase == SceneTransitionPhase.Requested)
        {
            BeginTransition(ref context, ref scene, ref transition, ref preparation);
            return;
        }

        if (transition.Phase == SceneTransitionPhase.Ready)
        {
            Activate(ref scene, ref transition, ref preparation);
        }
    }

    private void BeginTransition(
        ref SystemContext context,
        ref SceneState scene,
        ref SceneTransitionState transition,
        ref ScenePreparationState preparation)
    {
        transition.From = scene.Current;
        deactivating.Publish(new SceneDeactivating(transition.From, transition.To));

        foreach (QueryChunk<OwnedByScene> chunk in ownedEntities)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<OwnedByScene> owners = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                EntityId entity = entities[row];
                if (owners[row].Scene != transition.From || ShouldPersist(context.World, entity, transition.IsReload))
                {
                    continue;
                }

                context.Commands.Destroy(entity);
            }
        }

        preparation.PreparationId = transition.PreparationId;
        preparation.TargetScene = transition.To;
        preparation.Status = ScenePreparationStatus.Preparing;
        preparation.CompletedJobs = 0;
        preparation.TotalJobs = 0;
        preparation.SlowestJobMilliseconds = 0f;
        transition.Phase = SceneTransitionPhase.Preparing;
        prepareScene.Publish(new PrepareSceneEvent(transition.PreparationId, transition.To));
    }

    private static bool ShouldPersist(World world, EntityId entity, bool reload)
    {
        if (world.Has<DontDestroyOnLoad>(entity))
        {
            return true;
        }

        return reload && world.Has<DontDestroyOnReload>(entity);
    }

    private void Activate(
        ref SceneState scene,
        ref SceneTransitionState transition,
        ref ScenePreparationState preparation)
    {
        transition.Phase = SceneTransitionPhase.Activating;
        activating.Publish(new SceneActivating(
            transition.PreparationId,
            transition.From,
            transition.To));
        scene.Current = transition.To;
        deleteCaches.Publish(new DeleteCachesEvent(transition.From));
        activated.Publish(new SceneActivated(transition.PreparationId, transition.To));
        preparation.Status = ScenePreparationStatus.Idle;
        transition.From = transition.To;
        transition.IsReload = false;
        transition.Phase = SceneTransitionPhase.Idle;
    }

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<OwnedByScene>.Id)
            .With(ComponentType<DontDestroyOnLoad>.Id)
            .With(ComponentType<DontDestroyOnReload>.Id);
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<SceneState>.Id)
            .With(ComponentType<SceneTransitionState>.Id)
            .With(ComponentType<ScenePreparationState>.Id);
        return new SystemDescriptor(
            GlobalUiSystemIds.SceneLifecycle,
            nameof(SceneLifecycleSystem),
            SystemPhase.Gameplay,
            SceneGroup.Global,
            reads,
            writes,
            consumedEventTypeIds:
            [
                GlobalUiEventTypeIds.LoadScene,
                GlobalUiEventTypeIds.ScenePreparationReady,
            ],
            emittedEventTypeIds:
            [
                GlobalUiEventTypeIds.SceneDeactivating,
                GlobalUiEventTypeIds.PrepareScene,
                GlobalUiEventTypeIds.SceneActivating,
                GlobalUiEventTypeIds.SceneActivated,
                GlobalUiEventTypeIds.DeleteCaches,
            ],
            recordsStructuralCommands: true,
            eventBarrier: EventBarrier.AfterSystem);
    }
}
