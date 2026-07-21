using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Data.RunSetup;

namespace Crusaders30XX.ECS.Services
{
	public static class StartingDeckGeneratorService
	{
		public static readonly string[] SharedWeaponRunStarterCardPool =
		{
			CardId.Absolution.ToKey(),
			CardId.Courageous.ToKey(),
			CardId.ForgeStrike.ToKey(),
			CardId.LitanyOfWrath.ToKey(),
			CardId.Reckoning.ToKey(),
			CardId.Smite.ToKey(),
		};

		private static readonly string[] HammerCommonStarterCards =
		{
			CardId.Mantlet.ToKey(),
			CardId.StokeTheFurnace.ToKey(),
			CardId.SteadfastResolve.ToKey(),
		};

		private static readonly string[] HammerUncommonSingleCopyStarterCards =
		{
			CardId.UnburdenedStrike.ToKey(),
			CardId.IncreaseFaith.ToKey(),
		};

		private static readonly string[] DaggerCommonStarterCards =
		{
			CardId.Seize.ToKey(),
			CardId.RallyTheFaithful.ToKey(),
			CardId.Whirlwind.ToKey(),
		};

		private static readonly string[] DaggerUncommonSingleCopyStarterCards =
		{
			CardId.RazorStorm.ToKey(),
			CardId.IncreaseFaith.ToKey(),
		};

		private static readonly string[] SwordCommonStarterCards =
		{
			CardId.Fervor.ToKey(),
			CardId.HoldTheLine.ToKey(),
			CardId.Stab.ToKey(),
		};

		private static readonly string[] SwordUncommonSingleCopyStarterCards =
		{
			CardId.Exaltation.ToKey(),
			CardId.IncreaseFaith.ToKey(),
		};

		public static readonly string[] DefaultStarterCardPool = BuildDefaultStarterCardPool();

		private static readonly HashSet<string> DefaultStarterCardPoolSet = new HashSet<string>(
			DefaultStarterCardPool,
			StringComparer.OrdinalIgnoreCase);

		public static bool IsInDefaultStarterPool(string cardId)
		{
			return !string.IsNullOrWhiteSpace(cardId) && DefaultStarterCardPoolSet.Contains(cardId);
		}

		public static HashSet<string> GetAutoUpgradeCardIds(string equippedWeaponId)
		{
			var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			var weaponPools = new Dictionary<string, List<string>>()
			{
				[CardId.Hammer.ToKey()] = HammerCommonStarterCards.Concat(HammerUncommonSingleCopyStarterCards).ToList(),
				[CardId.Dagger.ToKey()] = DaggerCommonStarterCards.Concat(DaggerUncommonSingleCopyStarterCards).ToList(),
				[CardId.Sword.ToKey()] = SwordCommonStarterCards.Concat(SwordUncommonSingleCopyStarterCards).ToList(),
			};

			var allCards = CardFactory.GetAllCards();

			foreach (var (weapon, cards) in weaponPools)
			{
				if (string.Equals(weapon, equippedWeaponId, StringComparison.OrdinalIgnoreCase)) continue;
				foreach (var cardId in cards)
				{
					var card = CardFactory.Create(cardId);
					if (card?.Rarity == Rarity.Starter) continue;
					result.Add(cardId);
				}
			}

			return result;
		}

		public static IReadOnlyList<string> GetSwordStarterCardPool()
		{
			return SharedWeaponRunStarterCardPool.Concat(SwordCommonStarterCards).ToArray();
		}

		public static IReadOnlyList<string> GetSwordSingleCopyStarterCardPool()
		{
			return SwordUncommonSingleCopyStarterCards;
		}

		public static IReadOnlyList<string> GetDaggerStarterCardPool()
		{
			return SharedWeaponRunStarterCardPool.Concat(DaggerCommonStarterCards).ToArray();
		}

		public static IReadOnlyList<string> GetDaggerSingleCopyStarterCardPool()
		{
			return DaggerUncommonSingleCopyStarterCards;
		}

		public static IReadOnlyList<string> GetHammerStarterCardPool()
		{
			return SharedWeaponRunStarterCardPool.Concat(HammerCommonStarterCards).ToArray();
		}

		public static IReadOnlyList<string> GetHammerSingleCopyStarterCardPool()
		{
			return HammerUncommonSingleCopyStarterCards;
		}

		public static IReadOnlyList<string> GetStarterCardPool(string weaponId)
		{
			if (string.Equals(weaponId, CardId.Sword.ToKey(), StringComparison.OrdinalIgnoreCase))
				return GetSwordStarterCardPool();
			if (string.Equals(weaponId, CardId.Dagger.ToKey(), StringComparison.OrdinalIgnoreCase))
				return GetDaggerStarterCardPool();
			if (string.Equals(weaponId, CardId.Hammer.ToKey(), StringComparison.OrdinalIgnoreCase))
				return GetHammerStarterCardPool();
			return GetSwordStarterCardPool();
		}

		public static IReadOnlyList<string> GetSingleCopyStarterCardPool(string weaponId)
		{
			if (string.Equals(weaponId, CardId.Sword.ToKey(), StringComparison.OrdinalIgnoreCase))
				return GetSwordSingleCopyStarterCardPool();
			if (string.Equals(weaponId, CardId.Dagger.ToKey(), StringComparison.OrdinalIgnoreCase))
				return GetDaggerSingleCopyStarterCardPool();
			if (string.Equals(weaponId, CardId.Hammer.ToKey(), StringComparison.OrdinalIgnoreCase))
				return GetHammerSingleCopyStarterCardPool();
			return GetSwordSingleCopyStarterCardPool();
		}

		public static List<string> GenerateStartingDeck(string weaponId, int seed)
		{
			return Generate(
				GetStarterCardPool(weaponId),
				seed,
				GetSingleCopyStarterCardPool(weaponId));
		}

		public static LoadoutDefinition BuildStartingLoadout(string weaponId, int seed, string loadoutId = "loadout_1")
		{
			var cardKeys = GenerateStartingDeck(weaponId, seed);
			return new LoadoutDefinition
			{
				id = loadoutId,
				name = loadoutId == "test_fight" ? "Test Fight" : "Deck",
				weaponId = string.IsNullOrWhiteSpace(weaponId) ? CardId.Sword.ToKey() : weaponId,
				temperanceId = GetDefaultTemperanceId(weaponId),
				cards = cardKeys.Select((cardKey, index) => new LoadoutCardEntry
				{
					entryId = $"temporary_card_{index}",
					cardKey = cardKey,
					isStarter = true,
					countsAsTraded = false,
					restrictions = new List<string>(),
				}).ToList(),
				chestId = string.Empty,
				legsId = string.Empty,
				armsId = string.Empty,
				headId = string.Empty,
				medalIds = new List<string>(),
			};
		}

		public static string GetDefaultTemperanceId(string weaponId)
		{
			if (string.Equals(weaponId, CardId.Sword.ToKey(), StringComparison.OrdinalIgnoreCase))
				return "unsheath";
			if (string.Equals(weaponId, CardId.Hammer.ToKey(), StringComparison.OrdinalIgnoreCase))
				return "static_surge";
			if (string.Equals(weaponId, CardId.Dagger.ToKey(), StringComparison.OrdinalIgnoreCase))
				return "fling_fling";
			return "angelic_aura";
		}

		public static string GetDefaultTemperanceId(StartingWeapon weapon) => weapon switch
		{
			StartingWeapon.Sword => "unsheath",
			StartingWeapon.Hammer => "static_surge",
			StartingWeapon.Dagger => "fling_fling",
			_ => "angelic_aura",
		};

		private static string[] BuildDefaultStarterCardPool()
		{
			return GetSwordStarterCardPool()
				.Concat(GetSwordSingleCopyStarterCardPool())
				.Concat(GetDaggerStarterCardPool())
				.Concat(GetDaggerSingleCopyStarterCardPool())
				.Concat(GetHammerStarterCardPool())
				.Concat(GetHammerSingleCopyStarterCardPool())
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToArray();
		}

		private static List<string> BuildEffectivePool(
			IReadOnlyList<string> poolCardIds,
			IReadOnlyList<string> singleCopyCardIds)
		{
			return (poolCardIds ?? Array.Empty<string>())
				.Concat(singleCopyCardIds ?? Array.Empty<string>())
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.Distinct(StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		private static HashSet<string> BuildSingleCopySet(IReadOnlyList<string> singleCopyCardIds)
		{
			if (singleCopyCardIds == null || singleCopyCardIds.Count == 0)
			{
				return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			}

			return new HashSet<string>(
				singleCopyCardIds.Where(id => !string.IsNullOrWhiteSpace(id)),
				StringComparer.OrdinalIgnoreCase);
		}

		private static int GetMaxCopiesForStarterCard(string cardId, IReadOnlySet<string> singleCopyCardIds)
		{
			return singleCopyCardIds.Contains(cardId) ? 1 : DeckRules.MaxCopiesPerCardId;
		}

		public static List<string> Generate(
			IReadOnlyList<string> poolCardIds,
			int seed,
			IReadOnlyList<string> singleCopyCardIds = null)
		{
			var singleCopySet = BuildSingleCopySet(singleCopyCardIds);
			var effectivePool = BuildEffectivePool(poolCardIds, singleCopyCardIds);

			var result = TryGenerate(effectivePool, singleCopySet, new Random(seed), relaxColorQuotas: false);
			if (result.Count >= DeckRules.StartingDeckSize) return result;

			result = TryGenerate(effectivePool, singleCopySet, new Random(seed + 1), relaxColorQuotas: false);
			if (result.Count >= DeckRules.StartingDeckSize) return result;

			result = TryGenerate(effectivePool, singleCopySet, new Random(seed + 2), relaxColorQuotas: true);
			if (result.Count < DeckRules.StartingDeckSize)
			{
				Console.WriteLine($"[StartingDeckGenerator] Built {result.Count}/{DeckRules.StartingDeckSize} cards from pool size {effectivePool.Count}.");
			}
			return result;
		}

		private static List<string> TryGenerate(
			IReadOnlyList<string> distinctPool,
			IReadOnlySet<string> singleCopyCardIds,
			Random rng,
			bool relaxColorQuotas)
		{
			var finalDeck = new List<string>();
			var cardIdUsage = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
			var deckKeySet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

			int redLeft = 7, whiteLeft = 7, blackLeft = 7;
			int shortColorIndex = rng.Next(3);
			if (shortColorIndex == 0) redLeft = 6;
			else if (shortColorIndex == 1) whiteLeft = 6;
			else blackLeft = 6;

			ReserveGuaranteedSingleCopyCards(
				singleCopyCardIds,
				rng,
				relaxColorQuotas,
				ref redLeft,
				ref whiteLeft,
				ref blackLeft,
				finalDeck,
				deckKeySet,
				cardIdUsage);

			var allPairs = new List<(string Id, string Color)>();
			foreach (var cardId in distinctPool)
			{
				var card = CardFactory.Create(cardId);
				if (card == null) continue;
				if (!card.CanAddToLoadout || card.IsWeapon || card.IsToken) continue;

				allPairs.Add((card.CardId, "Red"));
				allPairs.Add((card.CardId, "White"));
				allPairs.Add((card.CardId, "Black"));
			}

			allPairs = allPairs.OrderBy(_ => rng.Next()).ToList();

			foreach (var pair in allPairs)
			{
				if (finalDeck.Count >= DeckRules.StartingDeckSize) break;

				string key = $"{pair.Id}|{pair.Color}";
				if (deckKeySet.Contains(key)) continue;

				cardIdUsage.TryGetValue(pair.Id, out int usage);
				if (usage >= GetMaxCopiesForStarterCard(pair.Id, singleCopyCardIds)) continue;

				if (!relaxColorQuotas)
				{
					if (pair.Color == "Red" && redLeft <= 0) continue;
					if (pair.Color == "White" && whiteLeft <= 0) continue;
					if (pair.Color == "Black" && blackLeft <= 0) continue;
				}

				finalDeck.Add(key);
				deckKeySet.Add(key);
				cardIdUsage[pair.Id] = usage + 1;

				if (pair.Color == "Red") redLeft--;
				else if (pair.Color == "White") whiteLeft--;
				else if (pair.Color == "Black") blackLeft--;
			}

			return finalDeck.OrderBy(_ => rng.Next()).ToList();
		}

		private static void ReserveGuaranteedSingleCopyCards(
			IReadOnlySet<string> singleCopyCardIds,
			Random rng,
			bool relaxColorQuotas,
			ref int redLeft,
			ref int whiteLeft,
			ref int blackLeft,
			List<string> finalDeck,
			HashSet<string> deckKeySet,
			Dictionary<string, int> cardIdUsage)
		{
			if (singleCopyCardIds == null || singleCopyCardIds.Count == 0) return;

			var guaranteedIds = singleCopyCardIds
				.Where(id => !string.IsNullOrWhiteSpace(id))
				.OrderBy(_ => rng.Next())
				.ToList();

			foreach (var cardId in guaranteedIds)
			{
				if (finalDeck.Count >= DeckRules.StartingDeckSize) break;
				var card = CardFactory.Create(cardId);
				if (card == null) continue;
				if (!card.CanAddToLoadout || card.IsWeapon || card.IsToken) continue;

				cardIdUsage.TryGetValue(cardId, out int usage);
				if (usage >= 1) continue;

				var colors = new List<string> { "Red", "White", "Black" };
				colors = colors.OrderBy(_ => rng.Next()).ToList();

				string chosenColor = null;
				foreach (var color in colors)
				{
					if (!relaxColorQuotas)
					{
						if (color == "Red" && redLeft <= 0) continue;
						if (color == "White" && whiteLeft <= 0) continue;
						if (color == "Black" && blackLeft <= 0) continue;
					}

					chosenColor = color;
					break;
				}

				if (chosenColor == null) continue;

				string key = $"{card.CardId}|{chosenColor}";
				if (deckKeySet.Contains(key)) continue;

				finalDeck.Add(key);
				deckKeySet.Add(key);
				cardIdUsage[card.CardId] = usage + 1;

				if (chosenColor == "Red") redLeft--;
				else if (chosenColor == "White") whiteLeft--;
				else blackLeft--;
			}
		}
	}
}
