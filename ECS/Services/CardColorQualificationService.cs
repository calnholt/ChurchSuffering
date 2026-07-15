using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Objects.EnemyAttacks;

namespace Crusaders30XX.ECS.Services
{
	public static class CardColorQualificationService
	{
		private static readonly IReadOnlyList<CardData.CardColor> NoColors = Array.Empty<CardData.CardColor>();

		public static bool IsPlayableColor(CardData.CardColor? color)
		{
			return color is CardData.CardColor.Red or CardData.CardColor.White or CardData.CardColor.Black;
		}

		public static IReadOnlyList<CardData.CardColor> GetQualifiedColors(Entity card)
		{
			if (card == null || card.HasComponent<Colorless>()) return NoColors;

			var color = card.GetComponent<CardData>()?.Color;
			if (!IsPlayableColor(color)) return NoColors;

			var secondaryColor = card.GetComponent<DualColor>()?.SecondaryColor;
			if (!IsPlayableColor(secondaryColor) || secondaryColor == color)
			{
				return new[] { color.Value };
			}

			return new[] { color.Value, secondaryColor.Value };
		}

		public static bool QualifiesAs(Entity card, CardData.CardColor color)
		{
			return GetQualifiedColors(card).Contains(color);
		}

		public static bool IsEligibleForCost(Entity card, string cost)
		{
			if (card?.GetComponent<CardData>() == null || string.IsNullOrWhiteSpace(cost)) return false;
			if (string.Equals(cost, "Any", StringComparison.OrdinalIgnoreCase))
			{
				return card.GetComponent<CardData>().Color != CardData.CardColor.Yellow;
			}

			if (!Enum.TryParse<CardData.CardColor>(cost, true, out var requiredColor)) return false;
			return QualifiesAs(card, requiredColor);
		}

		public static bool MeetsBlockingRestriction(Entity card, BlockingRestrictionType restriction)
		{
			return restriction switch
			{
				BlockingRestrictionType.OnlyRed => QualifiesAs(card, CardData.CardColor.Red),
				BlockingRestrictionType.OnlyWhite => QualifiesAs(card, CardData.CardColor.White),
				BlockingRestrictionType.OnlyBlack => QualifiesAs(card, CardData.CardColor.Black),
				BlockingRestrictionType.NotRed => !QualifiesAs(card, CardData.CardColor.Red),
				BlockingRestrictionType.NotWhite => !QualifiesAs(card, CardData.CardColor.White),
				BlockingRestrictionType.NotBlack => !QualifiesAs(card, CardData.CardColor.Black),
				_ => true,
			};
		}
	}
}
