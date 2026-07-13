#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Authoring.Meta;

/// <summary>
/// Root-composable scene-preparation hook. Register this consumer on the root global route bag;
/// it materializes static meta shells after prior scene teardown command playback has completed.
/// Battle and snapshot authoring remain owned by their dedicated host paths.
/// </summary>
public sealed class MetaSceneAuthoringConsumer : IEventConsumer<PrepareSceneEvent>, IDisposable
{
    private readonly World world;
    private MetaAuthoredScene? current;

    public MetaSceneAuthoringConsumer(World world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public MetaAuthoredScene? Current => current;

    /// <summary>Materializes the root's initial scene before the first scheduler frame.</summary>
    public MetaAuthoredScene? Materialize(SceneGroup scene)
    {
        current?.Dispose();
        current = scene switch
        {
            SceneGroup.TitleMenu or SceneGroup.Climb or SceneGroup.WayStation or SceneGroup.Achievement =>
                MetaStaticSceneMaterializer.Materialize(world, scene),
            _ => null,
        };
        return current;
    }

    public GlobalUiRouteConsumers Register(GlobalUiRouteConsumers? routes = null)
    {
        routes ??= new GlobalUiRouteConsumers();
        return routes.Add<PrepareSceneEvent>(
            this,
            GlobalUiRoutePriorities.HostOutput,
            "authoring.meta-scene-prepare");
    }

    public void Consume(in PrepareSceneEvent value, ref EventDispatchContext context)
    {
        Materialize(value.Scene);
    }

    public void Dispose()
    {
        current?.Dispose();
        current = null;
    }
}
