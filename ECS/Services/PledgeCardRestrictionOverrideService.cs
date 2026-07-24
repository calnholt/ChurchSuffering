using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Services
{
	public enum PledgeCardRestriction
	{
		Block,
	}

	public sealed class PledgeCardRestrictionQuery
	{
		public EntityManager EntityManager { get; set; }
		public Entity Owner { get; set; }
		public Entity Card { get; set; }
	}

	public interface IPledgeCardRestrictionOverrideProvider
	{
		bool OverridesPledgeCardRestriction(
			PledgeCardRestriction restriction,
			PledgeCardRestrictionQuery query);
	}

	public static class PledgeCardRestrictionOverrideService
	{
		public static bool IsOverridden(
			EntityManager entityManager,
			Entity card,
			PledgeCardRestriction restriction)
		{
			if (entityManager == null || card == null)
				return false;

			var owner = entityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (owner == null) return false;

			var query = new PledgeCardRestrictionQuery
			{
				EntityManager = entityManager,
				Owner = owner,
				Card = card,
			};

			foreach (var medalEntity in entityManager.GetEntitiesWithComponent<EquippedMedal>()
				.OrderBy(entity => entity.Id))
			{
				var equipped = medalEntity.GetComponent<EquippedMedal>();
				if (equipped?.EquippedOwner != owner) continue;
				if (equipped.Medal is not IPledgeCardRestrictionOverrideProvider provider) continue;
				if (provider.OverridesPledgeCardRestriction(restriction, query)) return true;
			}

			return false;
		}
	}
}
