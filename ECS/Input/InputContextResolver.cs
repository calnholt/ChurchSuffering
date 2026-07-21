using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;

namespace ChurchSuffering.ECS.Input
{
    public static class InputContextResolver
    {
        public static string ResolveCursorContext(EntityManager entityManager, Vector2 pointerPosition)
        {
            List<InputContext> active = GetActiveContexts(
                entityManager,
                context => context.AcceptsCursor);
            foreach (InputContext diagnostic in active
                .Where(context => context.IsDiagnostic)
                .OrderByDescending(context => context.Priority))
            {
                bool pointerInside = entityManager.GetEntitiesWithComponent<InputContextMember>()
                    .Where(entity => entity.GetComponent<InputContextMember>()?.ContextId == diagnostic.Id)
                    .Any(entity =>
                    {
                        UIElement ui = entity.GetComponent<UIElement>();
                        if (ui == null || !ui.IsInteractable || ui.IsHidden) return false;
                        Rectangle bounds = TransformResolverService.ResolveUIBounds(
                            entityManager,
                            entity,
                            ui);
                        return bounds.Contains(pointerPosition);
                    });
                if (pointerInside)
                {
                    return diagnostic.Id;
                }
            }

            return ResolveNonDiagnostic(entityManager, active);
        }

        public static string ResolveCommandContext(EntityManager entityManager)
        {
            List<InputContext> active = GetActiveContexts(
                entityManager,
                context => context.AcceptsCommands);
            return ResolveNonDiagnostic(entityManager, active);
        }

        public static bool IsMember(Entity entity, string activeContextId)
        {
            var membership = entity.GetComponent<InputContextMember>();
            string entityContext = membership?.ContextId
                ?? (entity.GetComponent<UIElement>()?.LayerType == UILayerType.Overlay
                    ? InputContextIds.Overlay
                    : InputContextIds.Gameplay);
            return entityContext == activeContextId;
        }

        private static List<InputContext> GetActiveContexts(
            EntityManager entityManager,
            System.Func<InputContext, bool> acceptsInput)
        {
            return entityManager
                .GetEntitiesWithComponent<InputContext>()
                .Select(entity => entity.GetComponent<InputContext>())
                .Where(context => context != null && context.IsActive && acceptsInput(context))
                .ToList();
        }

        private static string ResolveNonDiagnostic(
            EntityManager entityManager,
            List<InputContext> active)
        {
            InputContext explicitContext = active
                .Where(context => !context.IsDiagnostic)
                .OrderByDescending(context => context.Priority)
                .FirstOrDefault();
            if (explicitContext != null)
            {
                return explicitContext.Id;
            }

            bool overlayPresent = entityManager.GetEntitiesWithComponent<UIElement>()
                .Any(entity =>
                {
                    UIElement ui = entity.GetComponent<UIElement>();
                    return ui != null
                        && ui.LayerType == UILayerType.Overlay
                        && ui.IsInteractable
                        && !ui.IsHidden
                        && ui.Bounds.Width > 0
                        && ui.Bounds.Height > 0;
                });
            return overlayPresent ? InputContextIds.Overlay : InputContextIds.Gameplay;
        }
    }
}
