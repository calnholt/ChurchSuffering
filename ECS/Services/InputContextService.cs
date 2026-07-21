using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Services
{
    public static class InputContextService
    {
        public static InputContext EnsureContext(
            EntityManager entityManager,
            Entity root,
            string id,
            int priority,
            bool isActive,
            bool isDiagnostic = false)
        {
            InputContext context = root.GetComponent<InputContext>();
            if (context == null)
            {
                context = new InputContext();
                entityManager.AddComponent(root, context);
            }

            context.Id = id;
            context.Priority = priority;
            context.IsActive = isActive;
            context.IsDiagnostic = isDiagnostic;
            EnsureMember(entityManager, root, id);
            return context;
        }

        public static void EnsureMember(
            EntityManager entityManager,
            Entity entity,
            string contextId)
        {
            if (entity == null) return;
            InputContextMember member = entity.GetComponent<InputContextMember>();
            if (member == null)
            {
                member = new InputContextMember();
                entityManager.AddComponent(entity, member);
            }
            member.ContextId = contextId;
        }

        public static void RemoveMember(
            EntityManager entityManager,
            Entity entity,
            string contextId)
        {
            InputContextMember member = entity?.GetComponent<InputContextMember>();
            if (member?.ContextId == contextId)
            {
                entityManager.RemoveComponent<InputContextMember>(entity);
            }
        }
    }
}
