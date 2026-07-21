using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;

namespace ChurchSuffering.ECS.Objects.Cards
{
	public static class CardBoonRules
	{
		public static bool IsEligible(string boonType, LoadoutCardEntry entry)
		{
			var card = CreateEffectiveCard(entry);
			if (card == null) return false;

			int existingAmount = CardBoonApplicator.GetAmount(entry.boons, boonType);
			return boonType switch
			{
				CardBoonKinds.Wild => existingAmount == 0
					&& card.Cost != null
					&& card.Cost.Count > 0
					&& card.Cost.Any(cost => !string.Equals(cost, "Any", StringComparison.OrdinalIgnoreCase)),
				CardBoonKinds.Overcharged => card.Type == CardType.Attack
					&& (card.Cost?.Count ?? 0) < 4,
				CardBoonKinds.Quickened => existingAmount == 0 && !card.IsFreeAction,
				CardBoonKinds.Versatile => existingAmount == 0
					&& string.IsNullOrWhiteSpace(entry.secondaryColor),
				CardBoonKinds.Honed => card.Type == CardType.Attack,
				CardBoonKinds.Guarded => true,
				_ => false,
			};
		}

		public static CardBase CreateEffectiveCard(LoadoutCardEntry entry)
		{
			if (entry == null
				|| !RunDeckService.TryParseCardKey(entry.cardKey, out var cardId, out _, out bool isUpgraded))
			{
				return null;
			}

			var card = CardFactory.Create(cardId);
			if (card == null || card.IsWeapon || card.IsToken || !card.CanAddToLoadout) return null;
			if (isUpgraded) CardUpgradeService.InvokeUpgradeConfirmedOnCard(card);
			CardBoonApplicator.ApplyToDefinition(card, entry.boons);
			return card;
		}

		public static CardData.CardColor RollSecondaryColor(CardData.CardColor printedColor, Random rng)
		{
			rng ??= Random.Shared;
			var colors = new[]
			{
				CardData.CardColor.Red,
				CardData.CardColor.White,
				CardData.CardColor.Black,
			}
			.Where(color => color != printedColor)
			.ToArray();
			return colors[rng.Next(colors.Length)];
		}
	}
}
