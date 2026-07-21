using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Medals;

namespace ChurchSuffering.ECS.Systems
{
    public sealed class AlternateCardPlayQuery
    {
        public EntityManager EntityManager { get; set; }
        public Entity Owner { get; set; }
        public Entity Card { get; set; }
        public SubPhase Phase { get; set; }
    }

    public sealed class AlternateCardPlayProfile
    {
        public string SourceId { get; set; } = "";
        public string SourceType { get; set; } = "";
        public Entity SourceEntity { get; set; }
        public bool AllowsPlay { get; set; }
        public bool IsFreeAction { get; set; }
        public bool TreatsAsAttack { get; set; }
        public int AttackDamage { get; set; }
    }

    public interface IAlternateCardPlayProvider
    {
        AlternateCardPlayProfile GetAlternatePlayProfile(AlternateCardPlayQuery query);
    }

    internal static class AlternateCardPlayService
    {
        public static AlternateCardPlayProfile GetProfile(
            EntityManager entityManager,
            Entity card,
            SubPhase phase)
        {
            if (entityManager == null || card == null) return null;

            var owner = ResolvePlayer(entityManager);
            if (owner == null) return null;

            var query = new AlternateCardPlayQuery
            {
                EntityManager = entityManager,
                Owner = owner,
                Card = card,
                Phase = phase,
            };

            foreach (var medalEntity in entityManager.GetEntitiesWithComponent<EquippedMedal>()
                         .OrderBy(entity => entity.Id))
            {
                var equipped = medalEntity.GetComponent<EquippedMedal>();
                if (equipped?.EquippedOwner != owner) continue;
                if (equipped.Medal is not IAlternateCardPlayProvider provider) continue;

                var profile = provider.GetAlternatePlayProfile(query);
                if (profile?.AllowsPlay == true)
                    return profile;
            }

            return null;
        }

        private static Entity ResolvePlayer(EntityManager entityManager) =>
            entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
    }
}
