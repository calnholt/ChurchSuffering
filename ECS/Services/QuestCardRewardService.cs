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
	public static class QuestCardRewardService
	{
		private const int MaxOfferOptions = 3;
		private const int PreferredExchangeOptions = 2;
		private const int LateRewardMutationThreshold = 10;
		private const double PreferNonStarterUpgradeChance = 0.6;

		private static readonly CardData.CardColor[] RewardColors =
		{
			CardData.CardColor.Red,
			CardData.CardColor.White,
			CardData.CardColor.Black
		};

		private readonly struct DeckEntry
		{
			public DeckEntry(int index, string entryId, string cardKey, string cardId, CardData.CardColor color, bool isUpgraded, CardBase card)
			{
				Index = index;
				EntryId = entryId;
				CardKey = cardKey;
				CardId = cardId;
				Color = color;
				IsUpgraded = isUpgraded;
				Card = card;
			}

			public int Index { get; }
			public string EntryId { get; }
			public string CardKey { get; }
			public string CardId { get; }
			public CardData.CardColor Color { get; }
			public bool IsUpgraded { get; }
			public CardBase Card { get; }
		}

		public static DeckRewardOfferSave GenerateAndPersistPendingOffer(int rewardGold = 0, bool restrictToCollection = false)
		{
			var offer = GenerateDeckRewardOffer(rewardGold, restrictToCollection);
			if (offer?.options?.Count > 0)
			{
				SaveCache.SetPendingDeckRewardOffer(offer);
			}
			else
			{
				SaveCache.ClearPendingDeckRewardOffer();
			}
			return offer;
		}

		public static DeckRewardOfferSave GenerateDeckRewardOffer(int rewardGold = 0, bool restrictToCollection = false)
		{
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			var deckEntries = loadout?.cards ?? new List<LoadoutCardEntry>();
			string weaponId = loadout?.weaponId ?? string.Empty;
			return GenerateDeckRewardOffer(
				deckEntries,
				weaponId,
				rewardGold,
				restrictToCollection,
				SaveCache.GetAcceptedDeckRewardMutationCount(),
				Random.Shared);
		}

		internal static DeckRewardOfferSave GenerateDeckRewardOffer(IReadOnlyList<string> deckKeys, string weaponId, int rewardGold = 0, bool restrictToCollection = false)
		{
			var entries = (deckKeys ?? Array.Empty<string>())
				.Select((cardKey, index) => new LoadoutCardEntry { entryId = $"test_entry_{index}", cardKey = cardKey })
				.ToList();
			return GenerateDeckRewardOffer(entries, weaponId, rewardGold, restrictToCollection, acceptedDeckRewardMutations: 0, random: Random.Shared);
		}

		internal static DeckRewardOfferSave GenerateDeckRewardOffer(IReadOnlyList<LoadoutCardEntry> deckEntries, string weaponId, int rewardGold = 0, bool restrictToCollection = false)
		{
			return GenerateDeckRewardOffer(deckEntries, weaponId, rewardGold, restrictToCollection, acceptedDeckRewardMutations: 0, random: Random.Shared);
		}

		internal static DeckRewardOfferSave GenerateDeckRewardOffer(
			IReadOnlyList<LoadoutCardEntry> deckEntries,
			string weaponId,
			int rewardGold,
			bool restrictToCollection,
			int acceptedDeckRewardMutations,
			Random random)
		{
			var offer = new DeckRewardOfferSave { rewardGold = Math.Max(0, rewardGold) };
			var usedIndices = new HashSet<int>();
			random ??= Random.Shared;
			bool useLateRewardRules = acceptedDeckRewardMutations >= LateRewardMutationThreshold;
			int exchangeOptionCount = useLateRewardRules && random.Next(2) == 0
				? 1
				: PreferredExchangeOptions;

			foreach (var entry in PickExchangeOutgoingEntries(deckEntries, exchangeOptionCount, useLateRewardRules, random))
			{
				bool forceIncomingUpgrade = useLateRewardRules && entry.IsUpgraded && entry.Card.Rarity == Rarity.Starter;
				string incomingKey = PickIncomingCardKey(entry.CardId, weaponId, restrictToCollection, forceIncomingUpgrade, random);
				if (string.IsNullOrWhiteSpace(incomingKey)) continue;

				offer.options.Add(new DeckRewardOfferOptionSave
				{
					kind = DeckRewardOfferKinds.Exchange,
					loadoutIndex = entry.Index,
					outgoingEntryId = entry.EntryId,
					outgoingCardKey = entry.CardKey,
					incomingCardKey = incomingKey
				});
				usedIndices.Add(entry.Index);
			}

			while (offer.options.Count < MaxOfferOptions)
			{
				var upgrade = PickUpgradeEntry(deckEntries, usedIndices, random);
				if (upgrade == null) break;
				var entry = upgrade.Value;
				string upgradedKey = RunDeckService.BuildUpgradedCardKey(entry.CardKey);
				if (string.IsNullOrWhiteSpace(upgradedKey)) break;

				offer.options.Add(new DeckRewardOfferOptionSave
				{
					kind = DeckRewardOfferKinds.Upgrade,
					loadoutIndex = entry.Index,
					outgoingEntryId = entry.EntryId,
					outgoingCardKey = entry.CardKey,
					upgradedCardKey = upgradedKey
				});
				usedIndices.Add(entry.Index);
			}

			return offer;
		}

		public static bool ApplyPendingOfferOption(int optionIndex)
		{
			var offer = SaveCache.GetPendingDeckRewardOffer();
			if (offer?.options == null || optionIndex < 0 || optionIndex >= offer.options.Count) return false;
			var option = offer.options[optionIndex];
			if (option == null) return false;

			bool applied = false;
			if (string.Equals(option.kind, DeckRewardOfferKinds.Exchange, StringComparison.OrdinalIgnoreCase))
			{
				var inheritedRestrictions = SaveCache.GetRunDeckEntryRestrictions(
					RunDeckService.PrimaryLoadoutId,
					option.outgoingEntryId);
				applied = SaveCache.TryReplaceRunDeckEntry(
					RunDeckService.PrimaryLoadoutId,
					option.outgoingEntryId,
					option.incomingCardKey,
					out var replacementEntry,
					countsAsTraded: true);
				if (applied && replacementEntry != null && inheritedRestrictions.Count > 0)
				{
					SaveCache.SetRunDeckEntryRestrictions(
						RunDeckService.PrimaryLoadoutId,
						replacementEntry.entryId,
						inheritedRestrictions);
				}
			}
			else if (string.Equals(option.kind, DeckRewardOfferKinds.Upgrade, StringComparison.OrdinalIgnoreCase))
			{
				applied = SaveCache.TryUpgradeRunDeckEntry(
					RunDeckService.PrimaryLoadoutId,
					option.outgoingEntryId,
					option.upgradedCardKey,
					out _);
			}

			if (applied)
			{
				if (string.Equals(option.kind, DeckRewardOfferKinds.Exchange, StringComparison.OrdinalIgnoreCase))
				{
					if (RunDeckService.IsUpgradedCardKey(option.incomingCardKey))
					{
						CardUpgradeService.InvokeUpgradeConfirmed(option.incomingCardKey);
					}
				}
				else if (string.Equals(option.kind, DeckRewardOfferKinds.Upgrade, StringComparison.OrdinalIgnoreCase))
				{
					CardUpgradeService.InvokeUpgradeConfirmed(option.upgradedCardKey);
				}
				SaveCache.RecordAcceptedDeckRewardMutation();
				SaveCache.ClearPendingDeckRewardOffer();
			}
			return applied;
		}

		public static void SkipPendingOffer()
		{
			SaveCache.ClearPendingDeckRewardOffer();
		}

		internal static IReadOnlyList<string> GetEligibleRewardCardIdsForTests(IReadOnlyList<string> deckKeys)
		{
			return GetEligibleRewardCardIdsForTests(deckKeys, string.Empty);
		}

		internal static IReadOnlyList<string> GetEligibleRewardCardIdsForTests(IReadOnlyList<string> deckKeys, string weaponId)
		{
			return BuildIncomingPool(weaponId)
				.Select(NormalizeCardId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		internal static IReadOnlyList<string> GetExchangeOutgoingCardKeysForTests(IReadOnlyList<string> deckKeys)
		{
			return PickExchangeOutgoingEntries(ToTemporaryEntries(deckKeys), PreferredExchangeOptions, useLateRewardRules: false, Random.Shared).Select(e => e.CardKey).ToList();
		}

		internal static IReadOnlyList<string> GetUpgradeCardKeysForTests(IReadOnlyList<string> deckKeys)
		{
			return BuildEligibleDeckEntries(ToTemporaryEntries(deckKeys)).Select(e => e.CardKey).ToList();
		}

		private static IReadOnlyList<DeckEntry> PickExchangeOutgoingEntries(
			IReadOnlyList<LoadoutCardEntry> deckEntries,
			int count,
			bool useLateRewardRules,
			Random random)
		{
			var eligible = BuildEligibleDeckEntries(deckEntries, includeUpgradedStarters: useLateRewardRules);
			var picked = new List<DeckEntry>();
			int targetCount = Math.Max(0, count);

			if (useLateRewardRules)
			{
				while (picked.Count < targetCount && eligible.Count > 0)
				{
					int index = random.Next(eligible.Count);
					picked.Add(eligible[index]);
					eligible.RemoveAt(index);
				}
				return picked;
			}

			foreach (var starter in eligible.Where(e => e.Card.Rarity == Rarity.Starter))
			{
				if (picked.Count >= targetCount) break;
				picked.Add(starter);
			}

			if (picked.Count >= targetCount) return picked;

			var nonStarters = eligible
				.Where(e => e.Card.Rarity != Rarity.Starter && picked.All(p => p.Index != e.Index))
				.ToList();
			while (picked.Count < targetCount && nonStarters.Count > 0)
			{
				int idx = random.Next(nonStarters.Count);
				picked.Add(nonStarters[idx]);
				nonStarters.RemoveAt(idx);
			}

			return picked;
		}

		private static DeckEntry? PickUpgradeEntry(IReadOnlyList<LoadoutCardEntry> deckEntries, HashSet<int> usedIndices, Random random)
		{
			var eligible = BuildEligibleDeckEntries(deckEntries)
				.Where(e => usedIndices == null || !usedIndices.Contains(e.Index))
				.ToList();
			if (eligible.Count == 0) return null;

			var nonStarters = eligible.Where(e => e.Card.Rarity != Rarity.Starter).ToList();
			var starters = eligible.Where(e => e.Card.Rarity == Rarity.Starter).ToList();
			bool preferNonStarter = random.NextDouble() < PreferNonStarterUpgradeChance;
			var preferred = preferNonStarter ? nonStarters : starters;
			var fallback = preferNonStarter ? starters : nonStarters;
			var pool = preferred.Count > 0 ? preferred : fallback;
			if (pool.Count == 0) return null;
			return pool[random.Next(pool.Count)];
		}

		private static List<DeckEntry> BuildEligibleDeckEntries(IReadOnlyList<LoadoutCardEntry> deckEntries, bool includeUpgradedStarters = false)
		{
			var entries = new List<DeckEntry>();
			if (deckEntries == null) return entries;

			for (int i = 0; i < deckEntries.Count; i++)
			{
				var loadoutEntry = deckEntries[i];
				if (loadoutEntry == null) continue;
				string key = loadoutEntry.cardKey;
				if (!RunDeckService.TryParseCardKey(key, out var cardId, out var color, out var isUpgraded)) continue;
				var card = CardFactory.Create(cardId);
				if (card == null || card.IsWeapon || card.IsToken || !card.CanAddToLoadout) continue;
				if (isUpgraded && (!includeUpgradedStarters || card.Rarity != Rarity.Starter)) continue;
				entries.Add(new DeckEntry(i, loadoutEntry.entryId, key, cardId, color, isUpgraded, card));
			}

			return entries;
		}

		private static List<LoadoutCardEntry> ToTemporaryEntries(IReadOnlyList<string> deckKeys)
		{
			return (deckKeys ?? Array.Empty<string>())
				.Select((cardKey, index) => new LoadoutCardEntry
				{
					entryId = $"test_entry_{index}",
					cardKey = cardKey,
				})
				.ToList();
		}

		private static string PickIncomingCardKey(
			string outgoingCardId,
			string weaponId,
			bool restrictToCollection,
			bool forceUpgrade,
			Random random)
		{
			var pool = BuildIncomingPool(weaponId)
				.Select(NormalizeCardId)
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Where(id => !string.Equals(id, outgoingCardId, StringComparison.OrdinalIgnoreCase))
				.Where(id =>
				{
					var card = CardFactory.Create(id);
					return card != null
						&& card.CanAddToLoadout
						&& !card.IsWeapon
						&& !card.IsToken
						&& (!restrictToCollection || SaveCache.IsCollectionItemUnlocked(id, ForSaleItemType.Card));
				})
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
			if (pool.Count == 0) return string.Empty;

			string incomingId = pool[random.Next(pool.Count)];
			var color = RewardColors[random.Next(RewardColors.Length)];
			string key = RunDeckService.BuildCardKey(incomingId, color);
			if (forceUpgrade || StartingDeckGeneratorService.GetAutoUpgradeCardIds(weaponId ?? string.Empty).Contains(incomingId))
			{
				key = RunDeckService.BuildUpgradedCardKey(key);
			}
			return key;
		}

		private static IEnumerable<string> BuildIncomingPool(string weaponId)
		{
			foreach (var pair in CardFactory.GetAllCards())
			{
				var card = pair.Value;
				if (card == null) continue;
				if (!card.CanAddToLoadout || card.IsWeapon || card.IsToken || card.Rarity == Rarity.Starter) continue;
				if (!card.IsEligibleForWeapon(weaponId)) continue;
				yield return pair.Key.ToKey();
			}

			foreach (var id in StartingDeckGeneratorService.GetAutoUpgradeCardIds(weaponId ?? string.Empty))
			{
				yield return id;
			}
		}

		private static string NormalizeCardId(string cardId)
		{
			if (string.IsNullOrWhiteSpace(cardId)) return string.Empty;
			string id = cardId.Trim();
			return string.Equals(id, "exhaltation", StringComparison.OrdinalIgnoreCase)
				? "exaltation"
				: id;
		}
	}
}
