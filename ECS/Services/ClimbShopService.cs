using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;

namespace ChurchSuffering.ECS.Services
{
	public static class ClimbShopService
	{
		public static bool TryPurchaseSlot(int slotIndex)
		{
			return TryPurchaseSlot(null, slotIndex);
		}

		public static bool TryPurchaseSlot(EntityManager entityManager, int slotIndex)
		{
			return TryPurchaseSlot(entityManager, slotIndex, Random.Shared);
		}

		internal static bool TryPurchaseSlot(EntityManager entityManager, int slotIndex, Random boonRandom)
		{
			var climb = SaveCache.GetClimbState();
			if (!TryGetActiveShopSlot(climb, slotIndex, out var slot)) return false;

			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			if (loadout == null) return false;
			EnsureLoadoutLists(loadout);
			BoonPurchaseResult boonResult = null;
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Boon, StringComparison.OrdinalIgnoreCase))
			{
				if (!ClimbRuleService.CanAfford(climb.resources, slot.cost)) return false;
				boonResult = RollBoon(loadout, boonRandom);
				if (boonResult == null) return false;
			}
			if (!ClimbRuleService.TrySpend(climb.resources, slot.cost)) return false;
			int timeCost = slot.timeCost;

			bool applied = false;
			bool saveLoadout = true;
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase))
			{
				applied = TryApplyMedal(entityManager, loadout, slot.itemId);
			}
			else if (string.Equals(slot.kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase))
			{
				applied = TryApplyEquipment(entityManager, loadout, slot.itemId);
			}
			else if (string.Equals(slot.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase))
			{
				applied = TryApplyUpgrade(slot);
				saveLoadout = false;
			}
			else if (string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase))
			{
				climb.pendingReplacementOffer = new ClimbReplacementOfferSave
				{
					shopSlotIndex = slotIndex,
					incomingCardKey = slot.cardKey ?? string.Empty,
					cost = CloneResources(slot.cost),
				};
				SaveCache.SaveClimbState(climb);
				return true;
			}
			else if (string.Equals(slot.kind, ClimbShopSlotKinds.Boon, StringComparison.OrdinalIgnoreCase))
			{
				applied = TryApplyBoon(loadout, boonResult);
			}

			if (!applied) return false;
			slot.isSold = true;
			// Start purchase exit motion after success but before save/refresh so the UI
			// still shows the bought offer; failed applies never publish this event.
			EventManager.Publish(new ClimbShopSlotSelectedEvent { SlotIndex = slotIndex });
			if (boonResult != null)
			{
				EventManager.Publish(new ClimbCardBoonAnimationRequested
				{
					DeckEntryId = boonResult.EntryId,
					CardKey = boonResult.CardKey,
					RestrictionNames = new List<string>(boonResult.RestrictionNames),
					BeforeBoons = CloneBoons(boonResult.BeforeBoons),
					AfterBoons = CloneBoons(boonResult.AfterBoons),
					BeforeSecondaryColor = boonResult.BeforeSecondaryColor,
					AfterSecondaryColor = boonResult.AfterSecondaryColor,
					DelayClimbTurnoverUntilComplete = true,
				});
			}
			var loadoutForAdvance = saveLoadout
				? loadout
				: SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			ClimbRuleService.AdvanceTimeAndUpdateSlots(
				climb,
				SaveCache.GetAll()?.runMapSeed ?? 0,
				loadoutForAdvance,
				timeCost);
			if (saveLoadout) SaveCache.SaveLoadout(loadout);
			SaveCache.SaveClimbState(climb);
			if (boonResult != null && entityManager != null)
			{
				RunDeckService.EnsureRunDeck(entityManager);
			}
			if (ClimbRuleService.HasPendingFinalEncounter(climb))
			{
				ClimbEncounterService.TryQueuePendingFinalEncounter(entityManager);
			}
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.Purchase, Volume = 0.5f });
			return true;
		}

		public static bool TryOpenReplacementOffer(int slotIndex)
		{
			var climb = SaveCache.GetClimbState();
			if (!TryGetActiveShopSlot(climb, slotIndex, out var slot)) return false;
			if (!string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase)) return false;
			if (string.IsNullOrWhiteSpace(slot.cardKey)) return false;

			climb.pendingReplacementOffer = new ClimbReplacementOfferSave
			{
				shopSlotIndex = slotIndex,
				incomingCardKey = slot.cardKey,
				cost = CloneResources(slot.cost),
			};
			SaveCache.SaveClimbState(climb);
			return true;
		}

		public static void CancelReplacementOffer()
		{
			var climb = SaveCache.GetClimbState();
			if (climb.pendingReplacementOffer == null) return;
			climb.pendingReplacementOffer = null;
			SaveCache.SaveClimbState(climb);
		}

		public static bool TryFinalizeReplacement(string outgoingEntryId)
		{
			return TryFinalizeReplacement(null, outgoingEntryId);
		}

		public static bool TryFinalizeReplacement(EntityManager entityManager, string outgoingEntryId)
		{
			var climb = SaveCache.GetClimbState();
			var offer = climb.pendingReplacementOffer;
			if (offer == null || string.IsNullOrWhiteSpace(outgoingEntryId)) return false;
			if (!TryGetActiveShopSlot(climb, offer.shopSlotIndex, out var slot)) return false;
			if (!string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase)) return false;
			if (slot.isSold || string.IsNullOrWhiteSpace(offer.incomingCardKey)) return false;
			if (!ClimbRuleService.CanAfford(climb.resources, offer.cost)) return false;

			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			var outgoingEntry = loadout?.cards?.FirstOrDefault(entry => string.Equals(entry?.entryId, outgoingEntryId, StringComparison.Ordinal));
			if (outgoingEntry == null || !IsReplacementEligible(outgoingEntry.cardKey)) return false;

			if (!ClimbRuleService.TrySpend(climb.resources, offer.cost)) return false;
			int timeCost = slot.timeCost;
			if (!SaveCache.TryReplaceRunDeckEntry(
				RunDeckService.PrimaryLoadoutId,
				outgoingEntryId,
				offer.incomingCardKey,
				out _,
				countsAsTraded: true))
			{
				return false;
			}

			slot.isSold = true;
			climb.pendingReplacementOffer = null;
			if (RunDeckService.IsUpgradedCardKey(offer.incomingCardKey))
			{
				CardUpgradeService.InvokeUpgradeConfirmed(offer.incomingCardKey);
			}
			ClimbRuleService.AdvanceTimeAndUpdateSlots(
				climb,
				SaveCache.GetAll()?.runMapSeed ?? 0,
				SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId),
				timeCost);
			SaveCache.SaveClimbState(climb);
			if (ClimbRuleService.HasPendingFinalEncounter(climb))
			{
				ClimbEncounterService.TryQueuePendingFinalEncounter(entityManager);
			}
			EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.TakeReward, Volume = 0.5f });
			return true;
		}

		public static bool IsReplacementEligible(string cardKey)
		{
			if (!RunDeckService.TryParseCardKey(cardKey, out var cardId, out _)) return false;
			var card = CardFactory.Create(cardId);
			return card != null && !card.IsWeapon && !card.IsToken && card.CanAddToLoadout;
		}

		public static bool ClearInvalidOffers(ClimbSaveState climb, LoadoutDefinition loadout)
		{
			if (climb?.shopSlots == null) return false;
			bool changed = false;
			for (int i = 0; i < climb.shopSlots.Count; i++)
			{
				var slot = climb.shopSlots[i];
				if (slot == null || slot.isSold || string.Equals(slot.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase)) continue;
				if (IsOfferValid(slot, loadout)) continue;
				slot.kind = ClimbShopSlotKinds.Empty;
				slot.itemId = string.Empty;
				slot.cardKey = string.Empty;
				slot.deckIndex = -1;
				changed = true;
			}
			return changed;
		}

		private static bool TryGetActiveShopSlot(ClimbSaveState climb, int slotIndex, out ClimbShopSlotSave slot)
		{
			slot = null;
			if (climb?.shopSlots == null || slotIndex < 0 || slotIndex >= climb.shopSlots.Count) return false;
			slot = climb.shopSlots[slotIndex];
			return slot != null
				&& !slot.isSold
				&& !string.Equals(slot.kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase);
		}

		private static bool TryApplyMedal(EntityManager entityManager, LoadoutDefinition loadout, string medalId)
		{
			if (string.IsNullOrWhiteSpace(medalId) || MedalFactory.Create(medalId) == null) return false;
			if (loadout.medalIds.Any(id => string.Equals(id, medalId, StringComparison.OrdinalIgnoreCase))) return false;
			loadout.medalIds.Add(medalId);
			SaveCache.MarkWayStationMedalPurchased(medalId);
			RunMedalService.AcquireAndEquip(entityManager, medalId);
			return true;
		}

		private static bool TryApplyEquipment(EntityManager entityManager, LoadoutDefinition loadout, string equipmentId)
		{
			if (string.IsNullOrWhiteSpace(equipmentId) || EquipmentFactory.Create(equipmentId) == null) return false;
			if (SaveCache.IsItemOwned(equipmentId, ForSaleItemType.Equipment)) return false;
			RunEquipmentService.ApplyEquipmentToLoadout(loadout, equipmentId);
			RunEquipmentService.EquipOnPlayer(entityManager, equipmentId);
			return true;
		}

		private static bool TryApplyUpgrade(ClimbShopSlotSave slot)
		{
			if (string.IsNullOrWhiteSpace(slot?.deckEntryId)) return false;
			var entry = SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, slot.deckEntryId);
			if (entry == null) return false;
			string current = entry.cardKey;
			if (RunDeckService.IsUpgradedCardKey(current)) return false;
			string upgraded = RunDeckService.BuildUpgradedCardKey(current);
			if (string.IsNullOrWhiteSpace(upgraded)) return false;
			if (!string.IsNullOrWhiteSpace(slot.cardKey)
				&& !string.Equals(slot.cardKey, upgraded, StringComparison.OrdinalIgnoreCase))
			{
				return false;
			}

			if (!SaveCache.TryUpgradeRunDeckEntry(
				RunDeckService.PrimaryLoadoutId,
				slot.deckEntryId,
				upgraded,
				out _)) return false;
			EventManager.Publish(new ClimbCardUpgradeAnimationRequested
			{
				DeckEntryId = slot.deckEntryId,
				BaseCardKey = current,
				UpgradedCardKey = upgraded,
			});
			CardUpgradeService.InvokeUpgradeConfirmed(upgraded);
			return true;
		}

		private static bool IsOfferValid(ClimbShopSlotSave slot, LoadoutDefinition loadout)
		{
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Medal, StringComparison.OrdinalIgnoreCase))
				return MedalFactory.Create(slot.itemId) != null;
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Equipment, StringComparison.OrdinalIgnoreCase))
				return EquipmentFactory.Create(slot.itemId) != null;
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Replacement, StringComparison.OrdinalIgnoreCase))
				return RunDeckService.TryParseCardKey(slot.cardKey, out var incomingId, out _)
					&& CardFactory.Create(incomingId) != null;
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase))
			{
				var entry = loadout?.cards?.FirstOrDefault(candidate => string.Equals(candidate?.entryId, slot.deckEntryId, StringComparison.Ordinal));
				if (entry == null || RunDeckService.IsUpgradedCardKey(entry.cardKey)) return false;
				return !string.IsNullOrWhiteSpace(RunDeckService.BuildUpgradedCardKey(entry.cardKey));
			}
			if (string.Equals(slot.kind, ClimbShopSlotKinds.Boon, StringComparison.OrdinalIgnoreCase))
			{
				return (loadout?.cards ?? new List<LoadoutCardEntry>())
					.Any(entry => CardBoonRules.CreateEffectiveCard(entry) != null);
			}
			return false;
		}

		internal sealed class BoonPurchaseResult
		{
			public string Type { get; init; } = string.Empty;
			public string EntryId { get; init; } = string.Empty;
			public string CardKey { get; init; } = string.Empty;
			public List<string> RestrictionNames { get; init; } = new();
			public List<CardBoonSave> BeforeBoons { get; init; } = new();
			public List<CardBoonSave> AfterBoons { get; set; } = new();
			public string BeforeSecondaryColor { get; init; } = string.Empty;
			public string AfterSecondaryColor { get; set; } = string.Empty;
		}

		internal static BoonPurchaseResult RollBoon(LoadoutDefinition loadout, Random rng)
		{
			rng ??= Random.Shared;
			var queue = CardBoonKinds.All.ToList();
			for (int i = queue.Count - 1; i > 0; i--)
			{
				int swapIndex = rng.Next(i + 1);
				(queue[i], queue[swapIndex]) = (queue[swapIndex], queue[i]);
			}

			foreach (string type in queue)
			{
				var eligible = (loadout?.cards ?? new List<LoadoutCardEntry>())
					.Where(entry => CardBoonRules.IsEligible(type, entry))
					.ToList();
				if (eligible.Count == 0) continue;

				var entry = eligible[rng.Next(eligible.Count)];
				var result = new BoonPurchaseResult
				{
					Type = type,
					EntryId = entry.entryId,
					CardKey = entry.cardKey,
					RestrictionNames = new List<string>(entry.restrictions ?? new List<string>()),
					BeforeBoons = CloneBoons(entry.boons),
					BeforeSecondaryColor = entry.secondaryColor ?? string.Empty,
					AfterSecondaryColor = entry.secondaryColor ?? string.Empty,
				};
				if (string.Equals(type, CardBoonKinds.Versatile, StringComparison.OrdinalIgnoreCase)
					&& RunDeckService.TryParseCardKey(entry.cardKey, out _, out var printedColor, out _))
				{
					result.AfterSecondaryColor = CardBoonRules.RollSecondaryColor(printedColor, rng).ToString();
				}
				return result;
			}

			return null;
		}

		private static bool TryApplyBoon(LoadoutDefinition loadout, BoonPurchaseResult result)
		{
			if (result == null || string.IsNullOrWhiteSpace(result.EntryId) || string.IsNullOrWhiteSpace(result.Type)) return false;
			var entry = (loadout?.cards ?? new List<LoadoutCardEntry>())
				.FirstOrDefault(candidate => string.Equals(candidate?.entryId, result.EntryId, StringComparison.Ordinal));
			if (entry == null || !CardBoonRules.IsEligible(result.Type, entry)) return false;

			entry.boons ??= new List<CardBoonSave>();
			var existing = entry.boons.FirstOrDefault(boon => boon != null
				&& string.Equals(boon.type, result.Type, StringComparison.OrdinalIgnoreCase));
			if (existing == null)
			{
				entry.boons.Add(new CardBoonSave { type = result.Type, amount = 1 });
			}
			else
			{
				existing.amount = Math.Max(0, existing.amount) + 1;
			}

			if (string.Equals(result.Type, CardBoonKinds.Versatile, StringComparison.OrdinalIgnoreCase))
			{
				entry.secondaryColor = result.AfterSecondaryColor;
			}
			result.AfterBoons = CloneBoons(entry.boons);
			return true;
		}

		private static List<CardBoonSave> CloneBoons(IEnumerable<CardBoonSave> boons)
		{
			return CardBoonApplicator.Normalize(boons)
				.Select(boon => new CardBoonSave { type = boon.type, amount = boon.amount })
				.ToList();
		}

		private static void EnsureLoadoutLists(LoadoutDefinition loadout)
		{
			loadout.cards ??= new List<LoadoutCardEntry>();
			foreach (var entry in loadout.cards.Where(entry => entry != null)) entry.boons ??= new List<CardBoonSave>();
			loadout.medalIds ??= new List<string>();
			loadout.headId ??= string.Empty;
			loadout.chestId ??= string.Empty;
			loadout.armsId ??= string.Empty;
			loadout.legsId ??= string.Empty;
		}

		private static ClimbResourceSave CloneResources(ClimbResourceSave resources)
		{
			return new ClimbResourceSave
			{
				red = Math.Max(0, resources?.red ?? 0),
				white = Math.Max(0, resources?.white ?? 0),
				black = Math.Max(0, resources?.black ?? 0),
			};
		}
	}
}
