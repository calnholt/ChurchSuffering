#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Input;

public sealed class PlayerInputSystem : IGameSystem
{
    private readonly InputContextResolver resolver;
    private readonly EventStream<PlayerCommandEvent> commands;

    public PlayerInputSystem(
        World world,
        StringId gameplayContext,
        StringId overlayContext,
        EventStream<PlayerCommandEvent> commands)
    {
        resolver = new InputContextResolver(world, gameplayContext, overlayContext);
        this.commands = commands ?? throw new ArgumentNullException(nameof(commands));
        Descriptor = CreateDescriptor();
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        EntityId entity = context.World.GetUnique<PlayerInputSingleton>();
        ref PlayerInputState state = ref context.World.Get<PlayerInputState>(entity);
        PlayerInputFrame frame = state.Frame;
        state.CursorContext = resolver.ResolveCursorContext(frame.PointerPosition);
        state.CommandContext = resolver.ResolveCommandContext();

        bool interactionEnabled = state.IsInputEnabled && frame.IsWindowActive;
        state.Flags = interactionEnabled
            ? state.Flags | PlayerInputFlags.CursorInteractionEnabled
            : state.Flags & ~PlayerInputFlags.CursorInteractionEnabled;
        if (interactionEnabled)
        {
            CursorResolution target = resolver.ResolveTarget(frame.PointerPosition, state.CursorContext);
            state.CursorTarget = target.Entity;
            state.TargetKind = target.Kind;
            state.CursorCoverage = target.Coverage;
            PublishCommands(in frame);
        }
        else
        {
            state.CursorTarget = default;
            state.TargetKind = CursorTargetKind.None;
            state.CursorCoverage = 0f;
        }
    }

    private void PublishCommands(in PlayerInputFrame frame)
    {
        Publish(in frame, PlayerInputButton.Cancel, PlayerCommand.Cancel);
        Publish(in frame, PlayerInputButton.ShowHint, PlayerCommand.ShowHint);
        Publish(in frame, PlayerInputButton.ToggleFullScreen, PlayerCommand.ToggleFullScreen);
        PublishModified(in frame, PlayerInputButton.ToggleDebugMenu, PlayerCommand.ToggleDebugMenu);
        PublishModified(in frame, PlayerInputButton.ToggleEntityList, PlayerCommand.ToggleEntityList);
        PublishModified(in frame, PlayerInputButton.DealDebugDamage, PlayerCommand.DealDebugDamage);
        Publish(in frame, PlayerInputButton.ToggleProfiler, PlayerCommand.ToggleProfiler);
        if (frame.WasPressed(PlayerInputButton.Quit) && frame.IsDown(PlayerInputButton.Modifier))
        {
            commands.Publish(new PlayerCommandEvent(PlayerCommand.QuitApplication, frame.Device));
        }
    }

    private void Publish(
        in PlayerInputFrame frame,
        PlayerInputButton button,
        PlayerCommand command)
    {
        if (frame.WasPressed(button))
        {
            commands.Publish(new PlayerCommandEvent(command, frame.Device));
        }
    }

    private void PublishModified(
        in PlayerInputFrame frame,
        PlayerInputButton button,
        PlayerCommand command)
    {
        if (frame.WasPressed(button) && frame.IsDown(PlayerInputButton.Modifier))
        {
            commands.Publish(new PlayerCommandEvent(command, frame.Device));
        }
    }

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<InputContext>.Id)
            .With(ComponentType<InputContextMember>.Id)
            .With(ComponentType<UIElement>.Id)
            .With(ComponentType<Transform>.Id)
            .With(ComponentType<ParentTransform>.Id)
            .With(ComponentType<TooltipMetadata>.Id)
            .With(ComponentType<FilteredFromCursor>.Id);
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<PlayerInputState>.Id);
        return new SystemDescriptor(
            GlobalUiSystemIds.PlayerInput,
            nameof(PlayerInputSystem),
            SystemPhase.Input,
            SceneGroup.Global,
            reads,
            writes,
            consumedEventTypeIds:
            [
                GlobalUiEventTypeIds.PlayerInput,
                GlobalUiEventTypeIds.SetPlayerInputEnabled,
            ],
            emittedEventTypeIds: [GlobalUiEventTypeIds.PlayerCommand],
            eventBarrier: EventBarrier.AfterPhase);
    }
}
