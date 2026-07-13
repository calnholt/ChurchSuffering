#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Global;

public sealed class PlayerInputEventConsumer : IEventConsumer<PlayerInputEvent>
{
    private readonly World world;

    public PlayerInputEventConsumer(World world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public void Consume(in PlayerInputEvent value, ref EventDispatchContext context)
    {
        EntityId entity = world.GetUnique<PlayerInputSingleton>();
        ref PlayerInputState state = ref world.Get<PlayerInputState>(entity);
        state.Frame = value.Frame;
        state.Flags = value.Frame.IsWindowActive
            ? state.Flags | PlayerInputFlags.WindowActive
            : state.Flags & ~PlayerInputFlags.WindowActive;
    }
}

public sealed class SetPlayerInputEnabledEventConsumer : IEventConsumer<SetPlayerInputEnabledEvent>
{
    private readonly World world;

    public SetPlayerInputEnabledEventConsumer(World world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public void Consume(in SetPlayerInputEnabledEvent value, ref EventDispatchContext context)
    {
        EntityId entity = world.GetUnique<PlayerInputSingleton>();
        ref PlayerInputState state = ref world.Get<PlayerInputState>(entity);
        state.Flags = value.Enabled
            ? state.Flags | PlayerInputFlags.InputEnabled
            : state.Flags & ~(PlayerInputFlags.InputEnabled | PlayerInputFlags.CursorInteractionEnabled);
        if (!value.Enabled)
        {
            state.CursorTarget = default;
            state.TargetKind = CursorTargetKind.None;
            state.CursorCoverage = 0f;
        }
    }
}

public sealed class LoadSceneEventConsumer : IEventConsumer<LoadSceneEvent>
{
    private readonly World world;

    public LoadSceneEventConsumer(World world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public void Consume(in LoadSceneEvent value, ref EventDispatchContext context)
    {
        if (value.Scene == SceneGroup.Global)
        {
            throw new InvalidOperationException("Global is a scheduler group, not an activatable scene.");
        }

        EntityId entity = world.GetUnique<SceneStateSingleton>();
        ref SceneState scene = ref world.Get<SceneState>(entity);
        ref SceneTransitionState transition = ref world.Get<SceneTransitionState>(entity);
        transition.PreparationId = value.PreparationId;
        transition.From = scene.Current;
        transition.To = value.Scene;
        transition.IsReload = value.Reload || value.Scene == scene.Current;
        transition.Phase = SceneTransitionPhase.Requested;
    }
}

public sealed class ScenePreparationReadyConsumer : IEventConsumer<ScenePreparationReady>
{
    private readonly World world;

    public ScenePreparationReadyConsumer(World world) =>
        this.world = world ?? throw new ArgumentNullException(nameof(world));

    public void Consume(in ScenePreparationReady value, ref EventDispatchContext context)
    {
        EntityId entity = world.GetUnique<ScenePreparationSingleton>();
        ref ScenePreparationState preparation = ref world.Get<ScenePreparationState>(entity);
        ref SceneTransitionState transition = ref world.Get<SceneTransitionState>(entity);
        if (transition.Phase != SceneTransitionPhase.Preparing ||
            transition.PreparationId != value.PreparationId ||
            transition.To != value.Scene)
        {
            return;
        }

        preparation.Status = ScenePreparationStatus.Ready;
        transition.Phase = SceneTransitionPhase.Ready;
    }
}
