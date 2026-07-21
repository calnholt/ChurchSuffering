using System;
using System.Collections.Generic;

namespace ChurchSuffering.ECS.Data.Loadouts
{
	public static class DeckRules
	{
		public const int StartingDeckSize = 20;
		public const int MaxCopiesPerCardId = 2;

		public static string ParseBaseCardId(string deckKey)
		{
			if (string.IsNullOrWhiteSpace(deckKey)) return string.Empty;
			int sep = deckKey.IndexOf('|');
			return (sep >= 0 ? deckKey.Substring(0, sep) : deckKey).Trim();
		}

		public static int CountCardIdInDeck(IReadOnlyList<string> deckKeys, string cardId)
		{
			if (deckKeys == null || string.IsNullOrWhiteSpace(cardId)) return 0;
			int count = 0;
			foreach (var key in deckKeys)
			{
				if (string.Equals(ParseBaseCardId(key), cardId, StringComparison.OrdinalIgnoreCase))
				{
					count++;
				}
			}
			return count;
		}

		public static bool IsWithinCardIdCopyLimit(IReadOnlyList<string> deckKeys)
		{
			if (deckKeys == null) return true;
			var counts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			foreach (var key in deckKeys)
			{
				string baseId = ParseBaseCardId(key);
				if (string.IsNullOrEmpty(baseId)) continue;
				int count = (counts.TryGetValue(baseId, out var c) ? c : 0) + 1;
				counts[baseId] = count;
				if (count > MaxCopiesPerCardId) return false;
			}
			return true;
		}
	}
}
