#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Scenes;

/// <summary>
/// Completes CPU-side scene preparation once registered jobs report completion. Asset
/// loading remains an event consumer owned by presentation and increments the counters.
/// </summary>
public sealed class SceneLoadingCoordinatorSystem : IGameSystem
{
    private readonly EventStream<ScenePreparationReady> ready;

    public SceneLoadingCoordinatorSystem(EventStream<ScenePreparationReady> ready)
    {
        this.ready = ready ?? throw new ArgumentNullException(nameof(ready));
        Descriptor = CreateDescriptor();
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        EntityId global = context.World.GetUnique<ScenePreparationSingleton>();
        ref ScenePreparationState preparation = ref context.World.Get<ScenePreparationState>(global);
        ref SceneTransitionState transition = ref context.World.Get<SceneTransitionState>(global);
        if (preparation.Status != ScenePreparationStatus.Preparing ||
            transition.Phase != SceneTransitionPhase.Preparing ||
            preparation.CompletedJobs < preparation.TotalJobs)
        {
            return;
        }

        preparation.Status = ScenePreparationStatus.Ready;
        transition.Phase = SceneTransitionPhase.Ready;
        ready.Publish(new ScenePreparationReady(preparation.PreparationId, preparation.TargetScene));
    }

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<SceneTransitionState>.Id)
            .With(ComponentType<ScenePreparationState>.Id);
        return new SystemDescriptor(
            GlobalUiSystemIds.SceneLoadingCoordinator,
            nameof(SceneLoadingCoordinatorSystem),
            SystemPhase.Gameplay,
            SceneGroup.Global,
            writeComponents: writes,
            emittedEventTypeIds: [GlobalUiEventTypeIds.ScenePreparationReady],
            runsAfter: [GlobalUiSystemIds.SceneLifecycle],
            eventBarrier: EventBarrier.AfterSystem);
    }
}
