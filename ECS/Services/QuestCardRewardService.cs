using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
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

		private static readonly CardData.CardColor[] RewardColors =
		{
			CardData.CardColor.Red,
			CardData.CardColor.White,
			CardData.CardColor.Black
		};

		private static readonly string[] SharedRewardPool =
		{
			"strike",
			"crusade",
			"zealous_vow",
			"tempest",
			"shield_of_faith",
			"increase_faith",
			"renounce_and_hone",
			"sacrifice",
			"steel_the_spirit",
			"iron_covenant",
			"whirlwind",
			"pouch_of_kunai",
			"ravage",
			"reap",
			"relentless_strike",
			"serpent_crush",
			"stalwart",
			"temper_the_blade",
			"vindicate",
			"vanguards_promise",
			"steadfast_resolve",
			"exhaltation",
			"deus_vult",
			"carpe_diem",
			"crimson_rite",
			"consecrate",
			"ark_of_the_covenant",
			"dowse_with_holy_water",
			"fury"
		};

		private static readonly string[] HammerRewardPool =
		{
			"unburdened_strike",
			"battering_blow"
		};

		private static readonly string[] SwordRewardPool = Array.Empty<string>();
		private static readonly string[] DaggerRewardPool = Array.Empty<string>();

		public struct QuestCardRewardResult
		{
			public bool Granted;
			public string CardId;
			public CardData.CardColor Color;
			public string CardKey;
		}

		private readonly struct DeckEntry
		{
			public DeckEntry(int index, string entryId, string cardKey, string cardId, CardData.CardColor color, CardBase card)
			{
				Index = index;
				EntryId = entryId;
				CardKey = cardKey;
				CardId = cardId;
				Color = color;
				Card = card;
			}

			public int Index { get; }
			public string EntryId { get; }
			public string CardKey { get; }
			public string CardId { get; }
			public CardData.CardColor Color { get; }
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
			return GenerateDeckRewardOffer(deckEntries, weaponId, rewardGold, restrictToCollection);
		}

		internal static DeckRewardOfferSave GenerateDeckRewardOffer(IReadOnlyList<string> deckKeys, string weaponId, int rewardGold = 0, bool restrictToCollection = false)
		{
			var entries = (deckKeys ?? Array.Empty<string>())
				.Select((cardKey, index) => new LoadoutCardEntry { entryId = $"test_entry_{index}", cardKey = cardKey })
				.ToList();
			return GenerateDeckRewardOffer(entries, weaponId, rewardGold, restrictToCollection);
		}

		internal static DeckRewardOfferSave GenerateDeckRewardOffer(IReadOnlyList<LoadoutCardEntry> deckEntries, string weaponId, int rewardGold = 0, bool restrictToCollection = false)
		{
			var offer = new DeckRewardOfferSave { rewardGold = Math.Max(0, rewardGold) };
			var usedIndices = new HashSet<int>();

			foreach (var entry in PickExchangeOutgoingEntries(deckEntries).Take(PreferredExchangeOptions))
			{
				string incomingKey = PickIncomingCardKey(entry.CardId, weaponId, restrictToCollection);
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
				var upgrade = PickUpgradeEntry(deckEntries, usedIndices);
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
				SaveCache.ClearPendingDeckRewardOffer();
			}
			return applied;
		}

		public static void SkipPendingOffer()
		{
			SaveCache.ClearPendingDeckRewardOffer();
		}

		public static IReadOnlyList<QuestCardRewardResult> GenerateRandomCardChoices(int choiceCount = 2)
		{
			var offer = GenerateDeckRewardOffer();
			return ConvertExchangeOptionsToLegacyResults(offer, choiceCount);
		}

		internal static IReadOnlyList<QuestCardRewardResult> GenerateRandomCardChoices(IReadOnlyList<string> deckKeys, int choiceCount = 2)
		{
			var offer = GenerateDeckRewardOffer(deckKeys, string.Empty);
			return ConvertExchangeOptionsToLegacyResults(offer, choiceCount);
		}

		public static QuestCardRewardResult GrantCard(string cardKey)
		{
			var result = new QuestCardRewardResult();
			if (!RunDeckService.TryParseCardKey(cardKey, out var cardId, out var color)) return result;
			if (SaveCache.AddRunDeckEntry(RunDeckService.PrimaryLoadoutId, cardKey) == null) return result;

			result.Granted = true;
			result.CardId = cardId;
			result.Color = color;
			result.CardKey = cardKey;
			return result;
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
			return PickExchangeOutgoingEntries(ToTemporaryEntries(deckKeys)).Select(e => e.CardKey).ToList();
		}

		internal static IReadOnlyList<string> GetUpgradeCardKeysForTests(IReadOnlyList<string> deckKeys)
		{
			return BuildEligibleDeckEntries(ToTemporaryEntries(deckKeys)).Select(e => e.CardKey).ToList();
		}

		private static IReadOnlyList<QuestCardRewardResult> ConvertExchangeOptionsToLegacyResults(DeckRewardOfferSave offer, int choiceCount)
		{
			var results = new List<QuestCardRewardResult>();
			if (offer?.options == null) return results;
			foreach (var option in offer.options)
			{
				if (results.Count >= Math.Max(1, choiceCount)) break;
				if (!string.Equals(option.kind, DeckRewardOfferKinds.Exchange, StringComparison.OrdinalIgnoreCase)) continue;
				if (!RunDeckService.TryParseCardKey(option.incomingCardKey, out var cardId, out var color)) continue;
				results.Add(new QuestCardRewardResult
				{
					Granted = false,
					CardId = cardId,
					Color = color,
					CardKey = option.incomingCardKey
				});
			}
			return results;
		}

		private static IReadOnlyList<DeckEntry> PickExchangeOutgoingEntries(IReadOnlyList<LoadoutCardEntry> deckEntries)
		{
			var eligible = BuildEligibleDeckEntries(deckEntries);
			var picked = new List<DeckEntry>();

			foreach (var starter in eligible.Where(e => e.Card.Rarity == Rarity.Starter))
			{
				if (picked.Count >= PreferredExchangeOptions) break;
				picked.Add(starter);
			}

			if (picked.Count >= PreferredExchangeOptions) return picked;

			var nonStarters = eligible
				.Where(e => e.Card.Rarity != Rarity.Starter && picked.All(p => p.Index != e.Index))
				.ToList();
			while (picked.Count < PreferredExchangeOptions && nonStarters.Count > 0)
			{
				int idx = Random.Shared.Next(nonStarters.Count);
				picked.Add(nonStarters[idx]);
				nonStarters.RemoveAt(idx);
			}

			return picked;
		}

		private static DeckEntry? PickUpgradeEntry(IReadOnlyList<LoadoutCardEntry> deckEntries, HashSet<int> usedIndices)
		{
			var eligible = BuildEligibleDeckEntries(deckEntries)
				.Where(e => usedIndices == null || !usedIndices.Contains(e.Index))
				.ToList();
			if (eligible.Count == 0) return null;
			return eligible[Random.Shared.Next(eligible.Count)];
		}

		private static List<DeckEntry> BuildEligibleDeckEntries(IReadOnlyList<LoadoutCardEntry> deckEntries)
		{
			var entries = new List<DeckEntry>();
			if (deckEntries == null) return entries;

			for (int i = 0; i < deckEntries.Count; i++)
			{
				var loadoutEntry = deckEntries[i];
				if (loadoutEntry == null) continue;
				string key = loadoutEntry.cardKey;
				if (!RunDeckService.TryParseCardKey(key, out var cardId, out var color, out var isUpgraded)) continue;
				if (isUpgraded) continue;
				var card = CardFactory.Create(cardId);
				if (card == null || card.IsWeapon || card.IsToken || !card.CanAddToLoadout) continue;
				entries.Add(new DeckEntry(i, loadoutEntry.entryId, key, cardId, color, card));
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

		private static string PickIncomingCardKey(string outgoingCardId, string weaponId, bool restrictToCollection)
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

			string incomingId = pool[Random.Shared.Next(pool.Count)];
			var color = RewardColors[Random.Shared.Next(RewardColors.Length)];
			string key = RunDeckService.BuildCardKey(incomingId, color);
			if (StartingDeckGeneratorService.GetAutoUpgradeCardIds(weaponId ?? string.Empty).Contains(incomingId))
			{
				key = RunDeckService.BuildUpgradedCardKey(key);
			}
			return key;
		}

		private static IEnumerable<string> BuildIncomingPool(string weaponId)
		{
			var pool = CardFactory.GetAllCards()
				.Where(c => c.Value.CanAddToLoadout && !c.Value.IsWeapon && !c.Value.IsToken && c.Value.Rarity != Rarity.Starter)
				.Select(c => c.Key.ToString())
				.ToList();
			foreach (var id in pool)
			{
				yield return id;
			}

			if (string.Equals(weaponId, "hammer", StringComparison.OrdinalIgnoreCase))
			{
				foreach (var id in HammerRewardPool) yield return id;
			}
			else if (string.Equals(weaponId, "sword", StringComparison.OrdinalIgnoreCase))
			{
				foreach (var id in SwordRewardPool) yield return id;
			}
			else if (string.Equals(weaponId, "dagger", StringComparison.OrdinalIgnoreCase))
			{
				foreach (var id in DaggerRewardPool) yield return id;
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
