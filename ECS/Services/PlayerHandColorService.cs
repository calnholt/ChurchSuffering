using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Services
{
	public static class PlayerHandColorService
	{
		public static CardData.CardColor? GetRandomCardColorInPlayerHand(EntityManager entityManager)
		{
			var handCards = GetComponentHelper.GetHandOfCards(entityManager);
			if (handCards == null || handCards.Count == 0) return null;
			var colors = handCards
				.SelectMany(CardColorQualificationService.GetQualifiedColors)
				.Distinct()
				.ToList();
			if (colors.Count == 0) return null;
			return colors[Random.Shared.Next(0, colors.Count)];
		}
	}
}
