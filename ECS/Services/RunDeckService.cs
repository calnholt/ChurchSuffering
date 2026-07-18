using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;

namespace Crusaders30XX.ECS.Services
{
	public static class RunDeckService
	{
		public const string PrimaryLoadoutId = "loadout_1";
		private const string DeckEntityName = "Deck";

		public static LoadoutDefinition GetLoadoutForRun()
		{
			return SaveCache.GetLoadout(PrimaryLoadoutId);
		}

		public static Entity EnsureRunDeck(EntityManager entityManager)
		{
			var deckEntity = GetOrCreateRunDeckEntity(entityManager);
			var deck = deckEntity.GetComponent<Deck>();
			if (deck == null) return deckEntity;

			var loadout = GetLoadoutForRun();
			var desiredEntries = BuildDesiredEntries(loadout);
			var existingByEntryId = entityManager
				.GetEntitiesWithComponent<RunDeckCard>()
				.Where(e => e.IsActive && !string.IsNullOrWhiteSpace(e.GetComponent<RunDeckCard>()?.EntryId))
				.GroupBy(e => e.GetComponent<RunDeckCard>().EntryId, StringComparer.Ordinal)
				.ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);

			foreach (var entry in desiredEntries)
			{
				if (existingByEntryId.TryGetValue(entry.entryId, out var existing)
					&& string.Equals(existing.GetComponent<RunDeckCard>()?.CardKey, entry.cardKey, StringComparison.OrdinalIgnoreCase))
				{
					SynchronizeDualColor(entityManager, existing, entry);
					continue;
				}

				if (existing != null)
				{
					RemoveCardFromDeckLists(deck, existing);
					deck.Cards.Remove(existing);
					entityManager.DestroyEntity(existing.Id);
				}

				var created = CreateRunDeckCard(entityManager, entry);
				if (created != null) existingByEntryId[entry.entryId] = created;
			}

			var desiredIds = new HashSet<string>(desiredEntries.Select(entry => entry.entryId), StringComparer.Ordinal);
			foreach (var kv in existingByEntryId.ToList())
			{
				if (desiredIds.Contains(kv.Key)) continue;
				RemoveCardFromDeckLists(deck, kv.Value);
				deck.Cards.Remove(kv.Value);
				entityManager.DestroyEntity(kv.Value.Id);
			}

			deck.Cards.Clear();
			foreach (var entry in desiredEntries)
			{
				if (existingByEntryId.TryGetValue(entry.entryId, out var card) && card != null && card.IsActive)
				{
					deck.Cards.Add(card);
				}
			}

			RunScopedStateService.HydrateRunCardRestrictions(entityManager);

			return deckEntity;
		}

		public static void ReplaceDeckFromLoadout(
			EntityManager entityManager,
			LoadoutDefinition loadout)
		{
			if (entityManager == null) return;

			var deckEntity = GetRunDeckEntity(entityManager)
				?? entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault(e => e.IsActive);
			var deck = deckEntity?.GetComponent<Deck>();
			if (deckEntity == null || deck == null)
			{
				throw new InvalidOperationException("Cannot replace deck: deck entity is missing.");
			}

			ClearEquippedWeapon(entityManager);
			DestroyAllDeckCards(entityManager, deck);

			var desiredEntries = BuildDesiredEntries(loadout);
			deck.Cards.Clear();
			foreach (var entry in desiredEntries)
			{
				var created = CreateRunDeckCard(entityManager, entry);
				if (created != null)
				{
					deck.Cards.Add(created);
				}
			}

			if (deck.Cards.Count == 0)
			{
				throw new InvalidOperationException("Cannot replace deck: generated deck is empty.");
			}
		}

		public static void AddCardFromEntry(EntityManager entityManager, string entryId)
		{
			if (string.IsNullOrWhiteSpace(entryId)) return;
			var entry = SaveCache.GetRunDeckEntry(PrimaryLoadoutId, entryId);
			if (entry == null || IsWeaponCardKey(entry.cardKey)) return;
			var deckEntity = EnsureRunDeck(entityManager);
			var deck = deckEntity.GetComponent<Deck>();
			if (deck == null) return;

			if (TryGetCardEntityByEntryId(entityManager, entryId, out var existing))
			{
				if (!deck.Cards.Contains(existing))
				{
					deck.Cards.Add(existing);
				}
				return;
			}

			var created = CreateRunDeckCard(entityManager, entry);
			if (created != null && !deck.Cards.Contains(created))
			{
				deck.Cards.Add(created);
			}
		}

		public static void RemoveCardByEntryId(EntityManager entityManager, string entryId)
		{
			if (!TryGetCardEntityByEntryId(entityManager, entryId, out var card)) return;

			var deckEntity = GetRunDeckEntity(entityManager);
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck != null)
			{
				RemoveCardFromDeckLists(deck, card);
				deck.Cards.Remove(card);
			}

			entityManager.DestroyEntity(card.Id);
		}

		public static void ExhaustRunCard(EntityManager entityManager, Entity card)
		{
			if (card == null) return;
			var runDeck = card.GetComponent<RunDeckCard>();
			if (runDeck == null || string.IsNullOrWhiteSpace(runDeck.EntryId))
			{
				DestroyNonRunCard(entityManager, card);
				return;
			}

			var deckEntity = GetRunDeckEntity(entityManager);
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck != null)
			{
				RemoveCardFromDeckLists(deck, card);
				deck.Cards.Remove(card);
			}

			SaveCache.TryRemoveRunDeckEntry(PrimaryLoadoutId, runDeck.EntryId, out _, publishChange: false);
			entityManager.DestroyEntity(card.Id);
		}

		public static bool TryGetCardEntityByEntryId(EntityManager entityManager, string entryId, out Entity card)
		{
			card = null;
			if (entityManager == null || string.IsNullOrWhiteSpace(entryId)) return false;
			card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.FirstOrDefault(entity => entity.IsActive
					&& string.Equals(entity.GetComponent<RunDeckCard>()?.EntryId, entryId, StringComparison.Ordinal));
			return card != null;
		}

		public static void DestroyRunDeck(EntityManager entityManager)
		{
			RunScopedStateService.ClearRunCardRestrictionComponents(entityManager);
			foreach (var card in entityManager.GetEntitiesWithComponent<RunDeckCard>().ToList())
			{
				if (card != null && card.IsActive)
				{
					entityManager.DestroyEntity(card.Id);
				}
			}

			var deckEntity = GetRunDeckEntity(entityManager);
			if (deckEntity != null)
			{
				entityManager.DestroyEntity(deckEntity.Id);
			}
		}

		public static Entity GetRunDeckEntity(EntityManager entityManager)
		{
			var byName = entityManager.GetEntity(DeckEntityName);
			if (byName != null && byName.HasComponent<DontDestroyOnLoad>() && byName.HasComponent<Deck>())
			{
				return byName;
			}

			return entityManager
				.GetEntitiesWithComponent<Deck>()
				.FirstOrDefault(e => e.IsActive && e.HasComponent<DontDestroyOnLoad>());
		}

		private static Entity GetOrCreateRunDeckEntity(EntityManager entityManager)
		{
			var existing = GetRunDeckEntity(entityManager);
			if (existing != null) return existing;

			var entity = entityManager.CreateEntity(DeckEntityName);
			entityManager.AddComponent(entity, new Deck());
			entityManager.AddComponent(entity, new DontDestroyOnLoad());
			return entity;
		}

		private static Entity CreateRunDeckCard(
			EntityManager entityManager,
			LoadoutCardEntry entry)
		{
			if (entry == null || string.IsNullOrWhiteSpace(entry.entryId)) return null;
			string cardKey = entry.cardKey;
			if (!TryParseCardKey(cardKey, out var cardId, out var color, out var isUpgraded)) return null;
			var card = CardFactory.Create(cardId);
			if (card == null || card.IsWeapon) return null;

			int index = StableIndexForKey(entry.entryId);
			var entity = EntityFactory.CreateCardFromDefinition(
				entityManager,
				cardId,
				color,
				allowWeapons: false,
				index: index,
				cardKey: cardKey,
				runDeckEntryId: entry.entryId,
				persistForRun: true,
				isUpgraded: isUpgraded);
			if (entity != null && entry.isStarter)
			{
				var cardData = entity.GetComponent<CardData>();
				if (cardData?.Card != null)
				{
					cardData.Card.IsStarter = true;
				}
			}
			SynchronizeDualColor(entityManager, entity, entry);
			return entity;
		}

		private static void SynchronizeDualColor(
			EntityManager entityManager,
			Entity card,
			LoadoutCardEntry entry)
		{
			if (entityManager == null || card == null || entry == null) return;
			var data = card.GetComponent<CardData>();
			bool hasValidSecondary = Enum.TryParse(
				entry.secondaryColor,
				ignoreCase: true,
				out CardData.CardColor secondaryColor)
				&& CardColorQualificationService.IsPlayableColor(secondaryColor)
				&& CardColorQualificationService.IsPlayableColor(data?.Color)
				&& data.Color != secondaryColor;

			var dualColor = card.GetComponent<DualColor>();
			if (!hasValidSecondary)
			{
				if (dualColor != null) entityManager.RemoveComponent<DualColor>(card);
				return;
			}

			if (dualColor == null)
			{
				entityManager.AddComponent(card, new DualColor
				{
					Owner = card,
					SecondaryColor = secondaryColor,
				});
				return;
			}

			dualColor.SecondaryColor = secondaryColor;
		}

		private static List<LoadoutCardEntry> BuildDesiredEntries(LoadoutDefinition loadout)
		{
			var entries = new List<LoadoutCardEntry>();
			if (loadout?.cards == null) return entries;
			foreach (var entry in loadout.cards)
			{
				if (entry == null || string.IsNullOrWhiteSpace(entry.entryId) || string.IsNullOrWhiteSpace(entry.cardKey)) continue;
				var key = entry.cardKey.Trim();
				if (IsWeaponCardKey(key)) continue;
				entries.Add(entry);
			}
			return entries;
		}

		private static bool IsWeaponCardKey(string cardKey)
		{
			if (!TryParseCardKey(cardKey, out var cardId, out _, out _)) return false;
			var card = CardFactory.Create(cardId);
			return card?.IsWeapon == true;
		}

		public static bool TryParseCardKey(string cardKey, out string cardId, out CardData.CardColor color)
		{
			return TryParseCardKey(cardKey, out cardId, out color, out _);
		}

		public static bool TryParseCardKey(string cardKey, out string cardId, out CardData.CardColor color, out bool isUpgraded)
		{
			cardId = cardKey;
			color = CardData.CardColor.White;
			isUpgraded = false;
			if (string.IsNullOrWhiteSpace(cardKey)) return false;

			var parts = cardKey.Split('|');
			cardId = parts[0].Trim();
			if (parts.Length >= 2)
			{
				color = ParseColor(parts[1]);
			}
			if (parts.Length >= 3)
			{
				isUpgraded = string.Equals(parts[2].Trim(), "Upgraded", StringComparison.OrdinalIgnoreCase);
			}
			return !string.IsNullOrWhiteSpace(cardId) && CardFactory.Create(cardId) != null;
		}

		public static string BuildCardKey(string cardId, CardData.CardColor color, bool isUpgraded = false)
		{
			if (string.IsNullOrWhiteSpace(cardId)) return string.Empty;
			string key = $"{cardId.Trim()}|{ColorToKeyString(color)}";
			return isUpgraded ? $"{key}|Upgraded" : key;
		}

		public static string BuildUpgradedCardKey(string cardKey)
		{
			if (!TryParseCardKey(cardKey, out var cardId, out var color, out _)) return string.Empty;
			return BuildCardKey(cardId, color, isUpgraded: true);
		}

		public static bool IsUpgradedCardKey(string cardKey)
		{
			return TryParseCardKey(cardKey, out _, out _, out var isUpgraded) && isUpgraded;
		}

		private static CardData.CardColor ParseColor(string color)
		{
			if (string.IsNullOrEmpty(color)) return CardData.CardColor.White;
			switch (color.Trim().ToLowerInvariant())
			{
				case "red": return CardData.CardColor.Red;
				case "black": return CardData.CardColor.Black;
				case "white":
				default: return CardData.CardColor.White;
			}
		}

		private static string ColorToKeyString(CardData.CardColor color)
		{
			return color switch
			{
				CardData.CardColor.Red => "Red",
				CardData.CardColor.Black => "Black",
				_ => "White"
			};
		}

		private static int StableIndexForKey(string cardKey)
		{
			unchecked
			{
				int hash = 17;
				foreach (char c in cardKey)
				{
					hash = hash * 31 + c;
				}
				return Math.Abs(hash % 100000);
			}
		}

		private static void RemoveCardFromDeckLists(Deck deck, Entity card)
		{
			if (deck == null || card == null) return;
			deck.DrawPile.Remove(card);
			deck.Hand.Remove(card);
			deck.DiscardPile.Remove(card);
			deck.ExhaustPile.Remove(card);
		}

		private static void DestroyNonRunCard(EntityManager entityManager, Entity card)
		{
			var deckEntity = entityManager.GetEntitiesWithComponent<Deck>().FirstOrDefault();
			var deck = deckEntity?.GetComponent<Deck>();
			if (deck != null)
			{
				RemoveCardFromDeckLists(deck, card);
				deck.Cards.Remove(card);
			}
			entityManager.DestroyEntity(card.Id);
		}

		private static void ClearEquippedWeapon(EntityManager entityManager)
		{
			var player = entityManager.GetEntity("Player");
			var equippedWeapon = player?.GetComponent<EquippedWeapon>();
			if (equippedWeapon?.SpawnedEntity == null) return;

			entityManager.DestroyEntity(equippedWeapon.SpawnedEntity.Id);
			equippedWeapon.SpawnedEntity = null;
		}

		private static void DestroyAllDeckCards(EntityManager entityManager, Deck deck)
		{
			var cards = new HashSet<Entity>(deck.Cards);
			cards.UnionWith(deck.DrawPile);
			cards.UnionWith(deck.DiscardPile);
			cards.UnionWith(deck.ExhaustPile);
			cards.UnionWith(deck.Hand);
			foreach (var card in entityManager.GetEntitiesWithComponent<RunDeckCard>().Where(card => card != null && card.IsActive))
			{
				cards.Add(card);
			}

			foreach (var card in cards.Where(card => card != null).ToList())
			{
				entityManager.DestroyEntity(card.Id);
			}

			deck.Cards.Clear();
			deck.DrawPile.Clear();
			deck.DiscardPile.Clear();
			deck.ExhaustPile.Clear();
			deck.Hand.Clear();
		}
	}
}
