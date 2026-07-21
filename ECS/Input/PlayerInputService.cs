using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Input
{
    public static class PlayerInputService
    {
        public static PlayerInputFrame GetFrame(EntityManager entityManager)
        {
            return entityManager
                .GetEntitiesWithComponent<PlayerInputState>()
                .FirstOrDefault()
                ?.GetComponent<PlayerInputState>()
                ?.Frame ?? default;
        }
    }
}
