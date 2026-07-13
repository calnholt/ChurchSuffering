#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Gameplay.Global;
using Crusaders30XX.ECS.DataOriented.Resources;
using Crusaders30XX.ECS.DataOriented.Systems;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.UI;

public sealed class ModalInputSuppressionSystem : IGameSystem
{
    private readonly Query<ModalAnimation> animations;
    private readonly Query<UIElement> elements;

    public ModalInputSuppressionSystem(World world)
    {
        ArgumentNullException.ThrowIfNull(world);
        animations = world.Query<ModalAnimation>(new QueryFilter(DebugName: "ECS040.ModalAnimations"));
        elements = world.Query<UIElement>(new QueryFilter(DebugName: "ECS040.ModalElements"));
        Descriptor = CreateDescriptor();
    }

    public SystemDescriptor Descriptor { get; }

    public void Update(ref SystemContext context)
    {
        float elapsed = (float)context.Elapsed.TotalSeconds;
        AdvanceAnimations(context.World, elapsed);
        ApplySuppression(ref context);
    }

    private void AdvanceAnimations(World world, float elapsed)
    {
        foreach (QueryChunk<ModalAnimation> chunk in animations)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<ModalAnimation> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref ModalAnimation animation = ref values[row];
                ApplyRequestedPhase(ref animation);
                Advance(ref animation, elapsed);
                EntityId entity = entities[row];
                if (world.TryGet<InputContext>(entity, out InputContext inputContext))
                {
                    inputContext.Flags = animation.BlocksInput
                        ? inputContext.Flags | InputContextFlags.Active
                        : inputContext.Flags & ~InputContextFlags.Active;
                    world.Set(entity, in inputContext);
                }
            }
        }
    }

    private void ApplySuppression(ref SystemContext context)
    {
        foreach (QueryChunk<UIElement> chunk in elements)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<UIElement> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                EntityId entity = entities[row];
                if (!context.World.TryGet<InputContextMember>(entity, out InputContextMember member))
                {
                    continue;
                }

                bool shouldApply = IsTransitioning(member.ContextId);
                ref UIElement ui = ref values[row];
                if (context.World.TryGet<ModalInputSuppression>(entity, out ModalInputSuppression suppression))
                {
                    if (shouldApply && !suppression.IsApplied)
                    {
                        ui.SuppressCount++;
                        suppression.IsApplied = true;
                        suppression.ContextId = member.ContextId;
                        context.World.Set(entity, in suppression);
                    }
                    else if (!shouldApply && suppression.IsApplied)
                    {
                        ui.SuppressCount = Math.Max(0, ui.SuppressCount - 1);
                        suppression.IsApplied = false;
                        context.World.Set(entity, in suppression);
                    }

                    continue;
                }

                if (!shouldApply)
                {
                    continue;
                }

                ui.SuppressCount++;
                context.Commands.Add(entity, new ModalInputSuppression
                {
                    ContextId = member.ContextId,
                    IsApplied = true,
                });
            }
        }
    }

    private bool IsTransitioning(StringId contextId)
    {
        foreach (QueryChunk<ModalAnimation> chunk in animations)
        {
            Span<ModalAnimation> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref readonly ModalAnimation animation = ref values[row];
                if (animation.InputContextId == contextId && animation.IsTransitioning)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static void ApplyRequestedPhase(ref ModalAnimation animation)
    {
        if (animation.RequestedVisible)
        {
            if (animation.Phase is ModalAnimationPhase.Hidden or ModalAnimationPhase.Exiting)
            {
                animation.Phase = ModalAnimationPhase.Entering;
                animation.ElapsedSeconds = 0f;
            }

            return;
        }

        if (animation.Phase is ModalAnimationPhase.Visible or ModalAnimationPhase.Entering)
        {
            animation.Phase = ModalAnimationPhase.Exiting;
            animation.ElapsedSeconds = 0f;
            animation.ExitSequence++;
        }
    }

    private static void Advance(ref ModalAnimation animation, float elapsed)
    {
        if (animation.Phase == ModalAnimationPhase.Entering)
        {
            animation.ElapsedSeconds += elapsed;
            if (animation.ElapsedSeconds >= Math.Max(0.001f, animation.EnterDurationSeconds))
            {
                animation.ElapsedSeconds = 0f;
                animation.Phase = ModalAnimationPhase.Visible;
            }
        }
        else if (animation.Phase == ModalAnimationPhase.Exiting)
        {
            animation.ElapsedSeconds += elapsed;
            if (animation.ElapsedSeconds >= Math.Max(0.001f, animation.ExitDurationSeconds))
            {
                animation.ElapsedSeconds = 0f;
                animation.Phase = ModalAnimationPhase.Hidden;
                animation.CompletedExitSequence = animation.ExitSequence;
            }
        }
    }

    private static SystemDescriptor CreateDescriptor()
    {
        ComponentSignature reads = default;
        reads = reads.With(ComponentType<InputContextMember>.Id);
        ComponentSignature writes = default;
        writes = writes.With(ComponentType<ModalAnimation>.Id)
            .With(ComponentType<ModalInputSuppression>.Id)
            .With(ComponentType<InputContext>.Id)
            .With(ComponentType<UIElement>.Id);
        return new SystemDescriptor(
            GlobalUiSystemIds.ModalInputSuppression,
            nameof(ModalInputSuppressionSystem),
            SystemPhase.Interaction,
            SceneGroup.Global,
            reads,
            writes,
            recordsStructuralCommands: true);
    }
}
