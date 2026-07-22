using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Services
{
	public enum HandBlockRestriction
	{
		Pledged,
	}

	public sealed class HandBlockRestrictionQuery
	{
		public EntityManager EntityManager { get; set; }
		public Entity Owner { get; set; }
		public Entity Card { get; set; }
		public PlannedAttack PlannedAttack { get; set; }
	}

	public interface IHandBlockRestrictionOverrideProvider
	{
		bool OverridesHandBlockRestriction(
			HandBlockRestriction restriction,
			HandBlockRestrictionQuery query);
	}

	public static class HandBlockRestrictionOverrideService
	{
		public static bool IsOverridden(
			EntityManager entityManager,
			Entity card,
			PlannedAttack plannedAttack,
			HandBlockRestriction restriction)
		{
			if (entityManager == null || card == null || plannedAttack?.AttackDefinition == null)
				return false;

			var owner = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (owner == null) return false;

			var query = new HandBlockRestrictionQuery
			{
				EntityManager = entityManager,
				Owner = owner,
				Card = card,
				PlannedAttack = plannedAttack,
			};

			foreach (var medalEntity in entityManager.GetEntitiesWithComponent<EquippedMedal>()
				.OrderBy(entity => entity.Id))
			{
				var equipped = medalEntity.GetComponent<EquippedMedal>();
				if (equipped?.EquippedOwner != owner) continue;
				if (equipped.Medal is not IHandBlockRestrictionOverrideProvider provider) continue;
				if (provider.OverridesHandBlockRestriction(restriction, query)) return true;
			}

			return false;
		}
	}
}
