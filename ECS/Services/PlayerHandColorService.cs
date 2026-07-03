using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Services
{
	public static class PlayerHandColorService
	{
		public static CardData.CardColor? GetRandomCardColorInPlayerHand(EntityManager entityManager)
		{
			var handCards = GetComponentHelper.GetHandOfCards(entityManager);
			if (handCards == null || handCards.Count == 0) return null;
			var colors = handCards
				.Select(CardColorQualificationService.GetQualifiedColor)
				.Where(color => color.HasValue)
				.Select(color => color.Value)
				.Distinct()
				.ToList();
			if (colors.Count == 0) return null;
			return colors[Random.Shared.Next(0, colors.Count)];
		}
	}
}
