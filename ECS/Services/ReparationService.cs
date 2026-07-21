using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;

namespace Crusaders30XX.ECS.Services
{
	public static class ReparationService
	{
		private const int ReparationSalt = unchecked((int)0x52E9A71D);
		private static readonly CardData.CardColor[] PrintedColors =
		{
			CardData.CardColor.Red,
			CardData.CardColor.White,
			CardData.CardColor.Black,
		};

		private static readonly string[] NegativeRestrictions =
		{
			RunScopedStateService.RestrictionThorned,
			RunScopedStateService.RestrictionScorched,
			RunScopedStateService.RestrictionCursed,
			RunScopedStateService.RestrictionFrozen,
			RunScopedStateService.RestrictionBrittle,
		};

		public static int Apply(
			LoadoutDefinition loadout,
			string weaponId,
			int runSeed,
			int penanceLevel,
			PlayerCollectionSave collection,
			int replacementCount)
		{
			if (loadout?.cards == null || replacementCount <= 0) return 0;

			var originalCardIds = loadout.cards
				.Select(entry => DeckRules.ParseBaseCardId(entry?.cardKey))
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			var unlockedIds = (collection?.cardIds ?? new List<string>())
				.Select(DeckRules.ParseBaseCardId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.ToHashSet(StringComparer.OrdinalIgnoreCase);
			var candidates = BuildCandidates(weaponId, unlockedIds, originalCardIds);
			var targets = loadout.cards
				.Where(entry => entry?.isStarter == true)
				.OrderBy(entry => entry.entryId, StringComparer.Ordinal)
				.ToList();
			if (candidates.Count == 0 || targets.Count == 0) return 0;

			int seed = unchecked(runSeed ^ ReparationSalt ^ StableHash(weaponId) ^ (penanceLevel * 397));
			var rng = new Random(seed);
			var counts = loadout.cards
				.Select(entry => DeckRules.ParseBaseCardId(entry?.cardKey))
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.GroupBy(id => id, StringComparer.OrdinalIgnoreCase)
				.ToDictionary(group => group.Key, group => group.Count(), StringComparer.OrdinalIgnoreCase);

			int applied = 0;
			int desired = Math.Min(replacementCount, targets.Count);
			for (int i = 0; i < desired; i++)
			{
				var legalCandidates = candidates
					.Where(id => !counts.TryGetValue(id, out int count) || count < DeckRules.MaxCopiesPerCardId)
					.ToList();
				if (legalCandidates.Count == 0) break;

				int targetIndex = rng.Next(targets.Count);
				var target = targets[targetIndex];
				targets.RemoveAt(targetIndex);

				string outgoingId = DeckRules.ParseBaseCardId(target.cardKey);
				if (!string.IsNullOrWhiteSpace(outgoingId) && counts.TryGetValue(outgoingId, out int outgoingCount))
				{
					if (outgoingCount <= 1) counts.Remove(outgoingId);
					else counts[outgoingId] = outgoingCount - 1;
				}

				string incomingId = legalCandidates[rng.Next(legalCandidates.Count)];
				var color = PrintedColors[rng.Next(PrintedColors.Length)];
				string restriction = NegativeRestrictions[rng.Next(NegativeRestrictions.Length)];

				target.cardKey = RunDeckService.BuildCardKey(incomingId, color);
				target.secondaryColor = string.Empty;
				target.isStarter = false;
				target.countsAsTraded = false;
				target.restrictions = new List<string> { restriction };
				target.restrictionStacks = new Dictionary<string, int>();
				target.boons = new List<CardBoonSave>();
				counts[incomingId] = counts.TryGetValue(incomingId, out int incomingCount) ? incomingCount + 1 : 1;
				applied++;
			}

			return applied;
		}

		private static List<string> BuildCandidates(
			string weaponId,
			IReadOnlySet<string> unlockedIds,
			IReadOnlySet<string> originalCardIds)
		{
			var result = new List<string>();
			foreach (var pair in CardFactory.GetAllCards().OrderBy(pair => pair.Key.ToKey(), StringComparer.Ordinal))
			{
				string id = pair.Key.ToKey();
				CardBase card = pair.Value;
				if (card == null
					|| !unlockedIds.Contains(id)
					|| originalCardIds.Contains(id)
					|| !card.CanAddToLoadout
					|| !card.IsEligibleForWeapon(weaponId)
					|| card.IsWeapon
					|| card.IsToken
					|| card.Rarity == Rarity.Starter)
				{
					continue;
				}
				result.Add(id);
			}
			return result;
		}

		private static int StableHash(string value)
		{
			unchecked
			{
				int hash = (int)2166136261;
				foreach (char ch in (value ?? string.Empty).Trim().ToLowerInvariant())
				{
					hash ^= ch;
					hash *= 16777619;
				}
				return hash;
			}
		}
	}
}
