#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Events;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.UI;

public sealed class UIInteractionSystem : IGameSystem
{
    private readonly Query<UIElement> elements;
    private readonly EventStream<UIHoverChangedEvent> hoverChanged;
    private readonly EventStream<UIClickEvent> clicks;
    private readonly EventStream<UIActionEvent> actions;

    public UIInteractionSystem(
        World world,
        EventStream<UIHoverChangedEvent> hoverChanged,
        EventStream<UIClickEvent> clicks,
        EventStream<UIActionEvent> actions)
    {
        ArgumentNullException.ThrowIfNull(world);
        elements = world.Query<UIElement>(new QueryFilter(DebugName: "ECS040.UiInteraction"));
        this.hoverChanged = hoverChanged ?? throw new ArgumentNullException(nameof(hoverChanged));
        this.clicks = clicks ?? throw new ArgumentNullException(nameof(clicks));
        this.actions = actions ?? throw new ArgumentNullException(nameof(actions));
        Descriptor = CreateDescriptor();
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        ResetTransientFlags();
        EntityId inputEntity = context.World.GetUnique<PlayerInputSingleton>();
        ref PlayerInputState input = ref context.World.Get<PlayerInputState>(inputEntity);
        EntityId target = input.IsCursorInteractionEnabled ? input.CursorTarget : default;
        if (!target.IsNull && (!context.World.IsAlive(target) || !context.World.Has<UIElement>(target)))
        {
            target = default;
        }

        if (target != input.PreviousHoverTarget)
        {
            hoverChanged.Publish(new UIHoverChangedEvent(
                input.PreviousHoverTarget,
                target,
                input.Frame.Device));
            input.PreviousHoverTarget = target;
        }

        if (target.IsNull)
        {
            return;
        }

        ref UIElement ui = ref context.World.Get<UIElement>(target);
        if ((ui.Flags & UIInteractionFlags.Hidden) != 0)
        {
            return;
        }

        ui.Flags |= UIInteractionFlags.Hovered;
        bool primary = input.Frame.WasPressed(PlayerInputButton.Primary);
        bool secondary = input.Frame.WasPressed(PlayerInputButton.Secondary);
        if (!primary && !secondary)
        {
            return;
        }

        EntityId settingsEntity = context.World.GetUnique<UIInteractionSettingsSingleton>();
        ref readonly UIInteractionSettings settings =
            ref context.World.Get<UIInteractionSettings>(settingsEntity);
        bool tutorialBlocked = settings.TutorialActive &&
            !context.World.Has<TutorialInteractionPermitted>(target);
        if (!ui.IsInteractable ||
            (ui.Flags & UIInteractionFlags.PreventDefaultClick) != 0 ||
            settings.SuppressAllClicks ||
            tutorialBlocked)
        {
            return;
        }

        bool isSecondary = !primary && secondary;
        if (primary)
        {
            ui.Flags |= UIInteractionFlags.Clicked;
        }

        clicks.Publish(new UIClickEvent(target, isSecondary, input.Frame.Device));
        UIElementEventType action = primary ? ui.EventType : ui.SecondaryEventType;
        if (action != UIElementEventType.None)
        {
            actions.Publish(new UIActionEvent(target, action, input.Frame.Device));
        }
    }

    private void ResetTransientFlags()
    {
        const UIInteractionFlags transient = UIInteractionFlags.Hovered | UIInteractionFlags.Clicked;
        foreach (QueryChunk<UIElement> chunk in elements)
        {
            Span<UIElement> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                values[row].Flags &= ~transient;
            }
        }
    }

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<PlayerInputState>.Id)
            .With(ComponentType<UIInteractionSettings>.Id)
            .With(ComponentType<TutorialInteractionPermitted>.Id);
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<UIElement>.Id)
            .With(ComponentType<PlayerInputState>.Id);
        return new SystemDescriptor(
            GlobalUiSystemIds.UIInteraction,
            nameof(UIInteractionSystem),
            SystemPhase.Interaction,
            SceneGroup.Global,
            reads,
            writes,
            emittedEventTypeIds:
            [
                GlobalUiEventTypeIds.UiHoverChanged,
                GlobalUiEventTypeIds.UiClick,
                GlobalUiEventTypeIds.UiAction,
            ],
            runsAfter: [GlobalUiSystemIds.ModalInputSuppression],
            eventBarrier: EventBarrier.AfterPhase);
    }
}
