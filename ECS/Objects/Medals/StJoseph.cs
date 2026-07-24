using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Medals
{
	public sealed class StJoseph :
		MedalBase,
		IHandBlockRestrictionOverrideProvider,
		IPledgeCardRestrictionOverrideProvider
	{
		public const string MedalId = "st_joseph";

		public StJoseph()
		{
			Id = MedalId;
			Name = "St. Joseph";
			Text = "You can block with your pledged card. You can pledge block cards.";
		}

		public override void Initialize(EntityManager entityManager, Entity medalEntity)
		{
			EntityManager = entityManager;
			MedalEntity = medalEntity;
		}

		public bool OverridesHandBlockRestriction(
			HandBlockRestriction restriction,
			HandBlockRestrictionQuery query)
		{
			return restriction == HandBlockRestriction.Pledged
				&& query?.Card?.GetComponent<Pledge>() != null;
		}

		public bool OverridesPledgeCardRestriction(
			PledgeCardRestriction restriction,
			PledgeCardRestrictionQuery query)
		{
			return restriction == PledgeCardRestriction.Block
				&& query?.Card?.GetComponent<CardData>()?.Card?.Type == CardType.Block;
		}
	}
}
