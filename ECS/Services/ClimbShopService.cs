using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Services
{
	public static class ClimbShopService
	{
		public static bool TryPurchaseSlot(int slotIndex)
		{
			return TryPurchaseSlot(null, slotIndex);
		}

		public static bool TryPurchaseSlot(EntityManager entityManager, int slotIndex)
		{
			var climb = SaveCache.GetClimbState();
			if (!TryGetActiveShopSlot(climb, slotIndex, out var slot)) return false;

			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			if (loadout == null) return false;
			EnsureLoadoutLists(loadout);
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

			if (!applied) return false;
			slot.isSold = true;
			ClimbRuleService.AdvanceTimeAndUpdateSlots(
				climb,
				SaveCache.GetAll()?.runMapSeed ?? 0,
				loadout,
				timeCost);
			if (saveLoadout) SaveCache.SaveLoadout(loadout);
			SaveCache.SaveClimbState(climb);
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
			return false;
		}

		private static void EnsureLoadoutLists(LoadoutDefinition loadout)
		{
			loadout.cards ??= new List<LoadoutCardEntry>();
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
