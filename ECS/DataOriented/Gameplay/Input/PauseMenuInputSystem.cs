#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Input;

/// <summary>
/// Owns the pause toggle state while consuming only the centralized player-input frame. Escape
/// and gamepad Start are intentionally distinct from Back/Cancel, matching the legacy contract.
/// </summary>
public sealed class PauseMenuInputSystem : IGameSystem
{
    private readonly World world;

    public PauseMenuInputSystem(World world)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        StateEntity = FindOrCreateState(world);
        Descriptor = CreateDescriptor();
    }

    public EntityId StateEntity { get; }
    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        ref readonly PlayerInputState input = ref world.Get<PlayerInputState>(world.GetUnique<PlayerInputSingleton>());
        PlayerInputFrame frame = input.Frame;
        if (!input.IsInputEnabled || !frame.IsWindowActive) return;
        bool toggle = frame.Device == PlayerInputDevice.KeyboardMouse
            ? frame.WasPressed(PlayerInputButton.Escape)
            : frame.WasPressed(PlayerInputButton.Start);
        if (!toggle) return;

        ref PauseMenuOverlay pause = ref world.Get<PauseMenuOverlay>(StateEntity);
        bool visible = (pause.Flags & PresentationFlags.Visible) != 0;
        if (visible)
        {
            pause.Flags &= ~(PresentationFlags.Visible | PresentationFlags.Active);
            pause.Opacity = 0f;
        }
        else
        {
            pause.Flags |= PresentationFlags.Visible | PresentationFlags.Active;
            pause.Opacity = 1f;
        }
    }

    private static EntityId FindOrCreateState(World world)
    {
        Query<PauseMenuOverlay> states = world.Query<PauseMenuOverlay>();
        foreach (QueryChunk<PauseMenuOverlay> chunk in states)
        foreach (int row in chunk.Rows)
            return chunk.Entities[row];
        var bundle = new SpawnBundle(1);
        bundle.Add(new PauseMenuOverlay());
        return world.Create(in bundle);
    }

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature reads = ComponentSignature.Empty.With(ComponentType<PlayerInputState>.Id);
        ComponentSignature writes = ComponentSignature.Empty.With(ComponentType<PauseMenuOverlay>.Id);
        return new SystemDescriptor(
            GlobalUiSystemIds.PauseMenuInput,
            nameof(PauseMenuInputSystem),
            SystemPhase.Input,
            SceneGroup.Global,
            reads,
            writes,
            runsAfter: [GlobalUiSystemIds.PlayerInput]);
    }
}
