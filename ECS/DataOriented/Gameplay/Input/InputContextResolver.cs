#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Input;

/// <summary>Allocation-free resolver shared by cursor and command input systems.</summary>
public sealed class InputContextResolver
{
    private readonly World world;
    private readonly Query<InputContext> contexts;
    private readonly Query<UIElement> elements;
    private readonly StringId gameplayContext;
    private readonly StringId overlayContext;

    public InputContextResolver(World world, StringId gameplayContext, StringId overlayContext)
    {
        this.world = world ?? throw new ArgumentNullException(nameof(world));
        if (gameplayContext.IsNull)
        {
            throw new ArgumentOutOfRangeException(nameof(gameplayContext));
        }
        if (overlayContext.IsNull)
        {
            throw new ArgumentOutOfRangeException(nameof(overlayContext));
        }

        this.gameplayContext = gameplayContext;
        this.overlayContext = overlayContext;
        contexts = world.Query<InputContext>(new QueryFilter(DebugName: "ECS040.InputContexts"));
        elements = world.Query<UIElement>(new QueryFilter(DebugName: "ECS040.CursorElements"));
    }

    public StringId ResolveCursorContext(Vector2 pointerPosition)
    {
        StringId diagnostic = default;
        var diagnosticPriority = int.MinValue;
        foreach (QueryChunk<InputContext> chunk in contexts)
        {
            Span<InputContext> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref readonly InputContext candidate = ref values[row];
                if (!candidate.IsActive || !candidate.AcceptsCursor || !candidate.IsDiagnostic ||
                    candidate.Priority < diagnosticPriority ||
                    !HasEligibleElementAt(candidate.Id, pointerPosition))
                {
                    continue;
                }

                diagnostic = candidate.Id;
                diagnosticPriority = candidate.Priority;
            }
        }

        return diagnostic.IsNull ? ResolveNonDiagnostic(cursor: true) : diagnostic;
    }

    public StringId ResolveCommandContext() => ResolveNonDiagnostic(cursor: false);

    public CursorResolution ResolveTarget(Vector2 pointerPosition, StringId contextId)
    {
        EntityId winner = default;
        var winnerZ = int.MinValue;
        foreach (QueryChunk<UIElement> chunk in elements)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<UIElement> uiElements = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                EntityId entity = entities[row];
                ref readonly UIElement ui = ref uiElements[row];
                if ((ui.Flags & UIInteractionFlags.Hidden) != 0 ||
                    world.Has<FilteredFromCursor>(entity) ||
                    !IsMember(entity, ui.LayerType, contextId) ||
                    !CanReceiveHover(entity, in ui) ||
                    !ContainsPoint(entity, in ui, pointerPosition))
                {
                    continue;
                }

                int zOrder = world.TryGet<Transform>(entity, out Transform transform)
                    ? transform.ZOrder
                    : 0;
                if (winner.IsNull || zOrder > winnerZ ||
                    (zOrder == winnerZ && entity.Index < winner.Index))
                {
                    winner = entity;
                    winnerZ = zOrder;
                }
            }
        }

        if (winner.IsNull)
        {
            return default;
        }

        CursorTargetKind kind = IsDiagnosticContext(contextId)
            ? CursorTargetKind.Diagnostic
            : CursorTargetKind.UI;
        return new CursorResolution(winner, kind, 1f);
    }

    public bool IsMember(EntityId entity, UILayerType layer, StringId contextId)
    {
        StringId member = world.TryGet<InputContextMember>(entity, out InputContextMember explicitMember)
            ? explicitMember.ContextId
            : layer == UILayerType.Overlay ? overlayContext : gameplayContext;
        return member == contextId;
    }

    public bool ContainsPoint(EntityId entity, in UIElement ui, Vector2 point)
    {
        Rectangle bounds = ResolveBounds(entity, in ui);
        if (bounds.Width < 2 || bounds.Height < 2)
        {
            return false;
        }

        float rotation = world.TryGet<Transform>(entity, out Transform transform)
            ? transform.Rotation
            : 0f;
        return CursorGeometry.ContainsPoint(bounds, rotation, point);
    }

    private StringId ResolveNonDiagnostic(bool cursor)
    {
        StringId winner = default;
        var priority = int.MinValue;
        foreach (QueryChunk<InputContext> chunk in contexts)
        {
            Span<InputContext> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref readonly InputContext candidate = ref values[row];
                bool accepts = cursor ? candidate.AcceptsCursor : candidate.AcceptsCommands;
                if (!candidate.IsActive || !accepts || candidate.IsDiagnostic ||
                    candidate.Priority < priority)
                {
                    continue;
                }

                winner = candidate.Id;
                priority = candidate.Priority;
            }
        }

        if (!winner.IsNull)
        {
            return winner;
        }

        return HasActiveOverlay() ? overlayContext : gameplayContext;
    }

    private bool HasActiveOverlay()
    {
        foreach (QueryChunk<UIElement> chunk in elements)
        {
            Span<UIElement> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                ref readonly UIElement ui = ref values[row];
                if (ui.LayerType == UILayerType.Overlay && ui.IsInteractable &&
                    (ui.Flags & UIInteractionFlags.Hidden) == 0 &&
                    ui.Bounds.Width > 0 && ui.Bounds.Height > 0)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool HasEligibleElementAt(StringId contextId, Vector2 pointerPosition)
    {
        foreach (QueryChunk<UIElement> chunk in elements)
        {
            ReadOnlySpan<EntityId> entities = chunk.Entities;
            Span<UIElement> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                EntityId entity = entities[row];
                ref readonly UIElement ui = ref values[row];
                if (ui.IsInteractable && (ui.Flags & UIInteractionFlags.Hidden) == 0 &&
                    IsMember(entity, ui.LayerType, contextId) &&
                    ContainsPoint(entity, in ui, pointerPosition))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private bool IsDiagnosticContext(StringId contextId)
    {
        foreach (QueryChunk<InputContext> chunk in contexts)
        {
            Span<InputContext> values = chunk.Component1;
            foreach (int row in chunk.Rows)
            {
                if (values[row].Id == contextId)
                {
                    return values[row].IsDiagnostic;
                }
            }
        }

        return false;
    }

    private bool CanReceiveHover(EntityId entity, in UIElement ui)
    {
        return ui.IsInteractable || world.Has<TooltipMetadata>(entity);
    }

    private Rectangle ResolveBounds(EntityId entity, in UIElement ui)
    {
        if (!world.TryGet<ParentTransform>(entity, out ParentTransform parent))
        {
            return ui.Bounds;
        }

        Vector2 offset = world.TryGet<Transform>(entity, out Transform ownTransform)
            ? ownTransform.Position
            : Vector2.Zero;
        EntityId current = parent.Parent;
        for (var depth = 0; depth < 64 && !current.IsNull && world.IsAlive(current); depth++)
        {
            if (world.TryGet<Transform>(current, out Transform transform))
            {
                offset += transform.Position;
            }

            if (!world.TryGet<ParentTransform>(current, out ParentTransform next) || next.Parent == current)
            {
                break;
            }

            current = next.Parent;
        }

        return new Rectangle(
            (int)MathF.Round(ui.Bounds.X + offset.X),
            (int)MathF.Round(ui.Bounds.Y + offset.Y),
            ui.Bounds.Width,
            ui.Bounds.Height);
    }
}

public readonly record struct CursorResolution(
    EntityId Entity,
    CursorTargetKind Kind,
    float Coverage);

public static class CursorGeometry
{
    public static bool ContainsPoint(Rectangle bounds, float rotation, Vector2 point)
    {
        if (MathF.Abs(rotation) < 0.001f)
        {
            return point.X >= bounds.Left && point.X < bounds.Right &&
                point.Y >= bounds.Top && point.Y < bounds.Bottom;
        }

        var center = new Vector2(
            bounds.X + bounds.Width / 2f,
            bounds.Y + bounds.Height / 2f);
        Vector2 delta = point - center;
        float cos = MathF.Cos(rotation);
        float sin = MathF.Sin(rotation);
        float localX = delta.X * cos + delta.Y * sin;
        float localY = -delta.X * sin + delta.Y * cos;
        return MathF.Abs(localX) <= bounds.Width / 2f &&
            MathF.Abs(localY) <= bounds.Height / 2f;
    }
}
