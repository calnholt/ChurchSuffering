#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Cards;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Input;

/// <summary>Allocation-free binding winner, hold tracker, and hot-key action dispatcher.</summary>
public sealed class HotKeySystem : IGameSystem
{
    private readonly World world;
    private readonly InputContextResolver contextResolver;
    private readonly Query<HotKey, UIElement> hotKeys;
    private readonly EventStream<HotKeyHoldCompletedEvent> holdCompleted;
    private readonly EventStream<HotKeySelectEvent> selected;
    private readonly EventStream<UIActionEvent> actions;

    public HotKeySystem(
        World world,
        StringId gameplayContext,
        StringId overlayContext,
        EventStream<HotKeyHoldCompletedEvent> holdCompleted,
        EventStream<HotKeySelectEvent> selected,
        EventStream<UIActionEvent> actions)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        contextResolver = new InputContextResolver(world, gameplayContext, overlayContext);
        hotKeys = world.Query<HotKey, UIElement>(new QueryFilter(DebugName: "ECS040.HotKeys"));
        this.holdCompleted = holdCompleted ?? throw new ArgumentNullException(nameof(holdCompleted));
        this.selected = selected ?? throw new ArgumentNullException(nameof(selected));
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Descriptor = CreateDescriptor();
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        EntityId inputEntity = world.GetUnique<PlayerInputSingleton>();
        ref readonly PlayerInputState input = ref world.Get<PlayerInputState>(inputEntity);
        PlayerInputFrame frame = input.Frame;
        if (!input.IsInputEnabled || !frame.IsWindowActive)
        {
            CancelAllHolds();
            return;
        }

        // A hold belongs to the device/binding that started it. Clear the old hold before finding
        // a winner so a newly pressed binding on the replacement device can still start normally.
        if (frame.DeviceChanged)
        {
            CancelAllHolds();
        }

        StringId commandContext = contextResolver.ResolveCommandContext();
        EntityId pressedWinner = FindPressedWinner(in frame, commandContext);
        float elapsed = (float)context.Elapsed.TotalSeconds;

        foreach (QueryChunk<HotKey, UIElement> chunk in hotKeys)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<HotKey> bindings = chunk.Component1;
            Span<UIElement> elements = chunk.Component2;
            foreach (int row in chunk.Rows)
            {
                EntityId entity = entities[row];
                ref HotKey hotKey = ref bindings[row];
                ref UIElement ui = ref elements[row];
                bool eligible = IsEligible(entity, in hotKey, in ui, commandContext);
                bool winner = entity == pressedWinner;

                if (winner && (hotKey.Flags & HotKeyFlags.RequiresHold) == 0)
                {
                    hotKey.Flags |= HotKeyFlags.Pressed;
                    Dispatch(
                        entity,
                        in hotKey,
                        frame.Device,
                        ResolveBinding(in frame, in hotKey));
                }
                else
                {
                    hotKey.Flags &= ~HotKeyFlags.Pressed;
                }

                if (winner && (hotKey.Flags & HotKeyFlags.RequiresHold) != 0)
                {
                    hotKey.Flags |= HotKeyFlags.Holding;
                    hotKey.HoldProgressSeconds = 0f;
                }

                if ((hotKey.Flags & HotKeyFlags.Holding) == 0)
                {
                    continue;
                }

                if (!eligible || !IsBindingDown(in frame, in hotKey))
                {
                    hotKey.Flags &= ~HotKeyFlags.Holding;
                    hotKey.HoldProgressSeconds = 0f;
                    continue;
                }

                hotKey.HoldProgressSeconds += Math.Max(0f, elapsed);
                if (hotKey.HoldProgressSeconds < Math.Max(0.001f, hotKey.HoldDurationSeconds))
                {
                    continue;
                }

                hotKey.Flags &= ~HotKeyFlags.Holding;
                hotKey.HoldProgressSeconds = 0f;
                int binding = ResolveBinding(in frame, in hotKey);
                holdCompleted.Publish(new HotKeyHoldCompletedEvent(entity, binding));
                Dispatch(entity, in hotKey, frame.Device, binding);
            }
        }
    }

    private EntityId FindPressedWinner(in PlayerInputFrame frame, StringId commandContext)
    {
        EntityId winner = default;
        var winnerZ = int.MinValue;
        foreach (QueryChunk<HotKey, UIElement> chunk in hotKeys)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<HotKey> bindings = chunk.Component1;
            Span<UIElement> elements = chunk.Component2;
            foreach (int row in chunk.Rows)
            {
                EntityId entity = entities[row];
                ref readonly HotKey hotKey = ref bindings[row];
                ref readonly UIElement ui = ref elements[row];
                if (!IsEligible(entity, in hotKey, in ui, commandContext) ||
                    !WasBindingPressed(in frame, in hotKey))
                {
                    continue;
                }

                int z = world.TryGet<Transform>(entity, out Transform transform)
                    ? transform.ZOrder
                    : 0;
                if (winner.IsNull || z > winnerZ || (z == winnerZ && entity.Index < winner.Index))
                {
                    winner = entity;
                    winnerZ = z;
                }
            }
        }

        return winner;
    }

    private bool IsEligible(
        EntityId entity,
        in HotKey hotKey,
        in UIElement ui,
        StringId commandContext)
    {
        if ((hotKey.Flags & HotKeyFlags.Active) == 0 ||
            (ui.Flags & UIInteractionFlags.Hidden) != 0 ||
            (!ui.IsInteractable && (hotKey.Flags & HotKeyFlags.AllowWhenNonInteractable) == 0) ||
            !contextResolver.IsMember(entity, ui.LayerType, commandContext))
        {
            return false;
        }

        EntityId settingsEntity = world.GetUnique<UIInteractionSettingsSingleton>();
        ref readonly UIInteractionSettings settings = ref world.Get<UIInteractionSettings>(settingsEntity);
        return !settings.SuppressAllClicks || world.Has<TutorialInteractionPermitted>(entity);
    }

    private void Dispatch(
        EntityId source,
        in HotKey hotKey,
        PlayerInputDevice device,
        int binding)
    {
        EntityId target = !hotKey.Parent.IsNull && world.IsAlive(hotKey.Parent)
            ? hotKey.Parent
            : source;
        if (!world.TryGet<UIElement>(target, out UIElement ui))
        {
            return;
        }

        ui.Flags |= UIInteractionFlags.Clicked;
        world.Set(target, in ui);
        if (ui.EventType != UIElementEventType.None)
        {
            actions.Publish(new UIActionEvent(target, ui.EventType, device));
        }

        selected.Publish(new HotKeySelectEvent(target, binding));
    }

    private void CancelAllHolds()
    {
        foreach (QueryChunk<HotKey, UIElement> chunk in hotKeys)
        {
            Span<HotKey> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                values[row].Flags &= ~(HotKeyFlags.Holding | HotKeyFlags.Pressed);
                values[row].HoldProgressSeconds = 0f;
            }
        }
    }

    private static bool WasBindingPressed(in PlayerInputFrame frame, in HotKey hotKey)
    {
        int binding = ResolveBinding(in frame, in hotKey);
        return (uint)binding < 64 && (frame.PressedButtons & (1UL << binding)) != 0;
    }

    private static bool IsBindingDown(in PlayerInputFrame frame, in HotKey hotKey)
    {
        int binding = ResolveBinding(in frame, in hotKey);
        return (uint)binding < 64 && (frame.DownButtons & (1UL << binding)) != 0;
    }

    private static int ResolveBinding(in PlayerInputFrame frame, in HotKey hotKey) =>
        frame.Device == PlayerInputDevice.Gamepad ? hotKey.GamepadBinding : hotKey.KeyboardBinding;

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<PlayerInputState>.Id)
            .With(ComponentType<InputContext>.Id)
            .With(ComponentType<InputContextMember>.Id)
            .With(ComponentType<UIInteractionSettings>.Id)
            .With(ComponentType<Transform>.Id)
            .With(ComponentType<TutorialInteractionPermitted>.Id);
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<HotKey>.Id)
            .With(ComponentType<UIElement>.Id);
        return new SystemDescriptor(
            GlobalUiSystemIds.HotKey,
            nameof(HotKeySystem),
            SystemPhase.Interaction,
            SceneGroup.Global,
            reads,
            writes,
            emittedEventTypeIds:
            [
                GlobalUiEventTypeIds.UiAction,
                CardGameplayEventTypeIds.HotKeyHoldCompleted,
                CardGameplayEventTypeIds.HotKeySelect,
            ],
            runsAfter: [GlobalUiSystemIds.UIInteraction],
            eventBarrier: EventBarrier.AfterPhase);
    }
}
