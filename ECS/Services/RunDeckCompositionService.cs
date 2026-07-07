using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Services
{
	public static class RunDeckCompositionService
	{
		public static int CountCursedCardsInLoadout(string loadoutId = null)
		{
			string resolvedLoadoutId = string.IsNullOrWhiteSpace(loadoutId)
				? RunDeckService.PrimaryLoadoutId
				: loadoutId;
			var loadout = SaveCache.GetLoadout(resolvedLoadoutId);
			if (loadout?.cards == null) return 0;

			int count = 0;
			foreach (var entry in loadout.cards)
			{
				if (!IsCountableDeckEntry(entry)) continue;
				var restrictions = SaveCache.GetRunDeckEntryRestrictions(resolvedLoadoutId, entry.entryId);
				if (restrictions != null && restrictions.Contains(RunScopedStateService.RestrictionCursed))
				{
					count++;
				}
			}

			return count;
		}

		public static (int red, int white, int black) GetQualifiedColorCounts(string loadoutId = null)
		{
			string resolvedLoadoutId = string.IsNullOrWhiteSpace(loadoutId)
				? RunDeckService.PrimaryLoadoutId
				: loadoutId;
			var loadout = SaveCache.GetLoadout(resolvedLoadoutId);
			if (loadout?.cards == null) return (0, 0, 0);

			int red = 0;
			int white = 0;
			int black = 0;
			foreach (var entry in loadout.cards)
			{
				if (!IsCountableDeckEntry(entry)) continue;

				var restrictions = SaveCache.GetRunDeckEntryRestrictions(resolvedLoadoutId, entry.entryId);
				if (restrictions != null && restrictions.Contains(RunScopedStateService.RestrictionColorless))
				{
					continue;
				}

				if (!RunDeckService.TryParseCardKey(entry.cardKey, out _, out var color, out _)) continue;
				switch (color)
				{
					case CardData.CardColor.Red:
						red++;
						break;
					case CardData.CardColor.White:
						white++;
						break;
					case CardData.CardColor.Black:
						black++;
						break;
				}
			}

			return (red, white, black);
		}

		public static bool HasEliminatedColor(string loadoutId = null)
		{
			var (red, white, black) = GetQualifiedColorCounts(loadoutId);
			return red == 0 || white == 0 || black == 0;
		}

		private static bool IsCountableDeckEntry(LoadoutCardEntry entry)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.entryId) || string.IsNullOrWhiteSpace(entry.cardKey))
			{
				return false;
			}

			if (!RunDeckService.TryParseCardKey(entry.cardKey, out var cardId, out _, out _)) return false;
			var card = CardFactory.Create(cardId);
			return card != null && !card.IsWeapon;
		}
	}

}
