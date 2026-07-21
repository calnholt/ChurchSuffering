using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public class ClimbShopServiceTests : IDisposable
{
	public ClimbShopServiceTests()
	{
		EventManager.Clear();
	}

	public void Dispose()
	{
		EventManager.Clear();
	}

	[Fact]
	public void Medal_purchase_spends_resources_adds_medal_and_marks_slot_sold()
	{
		PrepareRun(new List<string> { "smite|White" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Medal, itemId: "st_luke");
		SaveCache.SaveClimbState(state);

		Assert.True(ClimbShopService.TryPurchaseSlot(0));

		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		var after = SaveCache.GetClimbState();
		Assert.Contains("st_luke", loadout.medalIds);
		Assert.True(after.shopSlots[0].isSold);
		Assert.Equal(2, after.resources.red);
		Assert.Equal(1, after.time);
	}

	[Fact]
	public void Equipment_purchase_equips_item_and_marks_slot_sold()
	{
		PrepareRun(new List<string> { "smite|White" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Equipment, itemId: "knightly_chest");
		SaveCache.SaveClimbState(state);

		Assert.True(ClimbShopService.TryPurchaseSlot(0));

		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		Assert.Equal("knightly_chest", loadout.chestId);
		Assert.True(SaveCache.GetClimbState().shopSlots[0].isSold);
	}

	[Fact]
	public void Boon_purchase_rolls_persists_animates_and_marks_slot_sold()
	{
		PrepareRun(new List<string> { "smite|White", "fervor|Red" });
		var state = BaseState();
		state.shopSlots[2] = ShopSlot(ClimbShopSlotKinds.Boon);
		SaveCache.SaveClimbState(state);
		ClimbCardBoonAnimationRequested animation = null;
		EventManager.Subscribe<ClimbCardBoonAnimationRequested>(evt => animation = evt);

		Assert.True(ClimbShopService.TryPurchaseSlot(null, 2, new Random(42)));

		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		var applied = loadout.cards.Single(entry => entry.boons.Count > 0);
		Assert.Single(applied.boons);
		Assert.Equal(1, applied.boons[0].amount);
		Assert.True(SaveCache.GetClimbState().shopSlots[2].isSold);
		Assert.NotNull(animation);
		Assert.Equal(applied.entryId, animation.DeckEntryId);
		Assert.Empty(animation.BeforeBoons);
		Assert.Single(animation.AfterBoons);
		Assert.True(animation.DelayClimbTurnoverUntilComplete);
	}

	[Fact]
	public void Unaffordable_boon_purchase_does_not_roll_or_spend()
	{
		PrepareRun(new List<string> { "smite|White" });
		var state = BaseState();
		state.resources = new ClimbResourceSave { red = 0, white = 0, black = 0 };
		state.shopSlots[2] = ShopSlot(ClimbShopSlotKinds.Boon);
		SaveCache.SaveClimbState(state);
		var random = new CountingRandom();

		Assert.False(ClimbShopService.TryPurchaseSlot(null, 2, random));

		Assert.Equal(0, random.NextCalls);
		Assert.All(SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards, entry => Assert.Empty(entry.boons));
	}

	[Fact]
	public void Upgrade_purchase_replaces_specific_deck_entry()
	{
		PrepareRun(new List<string> { "smite|White", "fervor|Red" });
		var original = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[0];
		Assert.True(SaveCache.AddRunDeckEntryRestriction(
			RunDeckService.PrimaryLoadoutId,
			original.entryId,
			RunScopedStateService.RestrictionFrozen));
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(
			ClimbShopSlotKinds.Upgrade,
			cardKey: "smite|White|Upgraded",
			deckEntryId: original.entryId,
			deckIndex: 0);
		SaveCache.SaveClimbState(state);

		Assert.True(ClimbShopService.TryPurchaseSlot(0));

		var upgraded = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[0];
		Assert.Equal(original.entryId, upgraded.entryId);
		Assert.Equal("smite|White|Upgraded", upgraded.cardKey);
		Assert.Contains(RunScopedStateService.RestrictionFrozen, upgraded.restrictions);
		Assert.True(SaveCache.GetClimbState().shopSlots[0].isSold);
	}

	[Fact]
	public void Upgrade_purchase_across_shop_refresh_does_not_reoffer_upgraded_entry()
	{
		PrepareRun(new List<string> { "smite|White" });
		var original = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[0];
		var state = BaseState();
		state.time = PenanceRules.BaseShopRefreshInterval - 1;
		state.shopSlots[0] = ShopSlot(
			ClimbShopSlotKinds.Upgrade,
			cardKey: "smite|White|Upgraded",
			deckEntryId: original.entryId,
			deckIndex: 0);
		SaveCache.SaveClimbState(state);

		Assert.True(ClimbShopService.TryPurchaseSlot(0));

		var after = SaveCache.GetClimbState();
		var upgraded = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[0];
		Assert.Equal("smite|White|Upgraded", upgraded.cardKey);
		Assert.Equal(PenanceRules.BaseShopRefreshInterval, after.time);
		Assert.DoesNotContain(
			after.shopSlots,
			slot => slot != null
				&& string.Equals(slot.kind, ClimbShopSlotKinds.Upgrade, StringComparison.OrdinalIgnoreCase)
				&& string.Equals(slot.deckEntryId, original.entryId, StringComparison.Ordinal));
	}

	[Fact]
	public void Successful_purchase_publishes_shop_selected_event_failed_purchase_does_not()
	{
		EventManager.Clear();
		PrepareRun(new List<string> { "smite|White" });
		var original = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[0];
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(
			ClimbShopSlotKinds.Upgrade,
			cardKey: "smite|White|Upgraded",
			deckEntryId: original.entryId,
			deckIndex: 0);
		SaveCache.SaveClimbState(state);
		var selected = new List<int>();
		EventManager.Subscribe<ClimbShopSlotSelectedEvent>(evt => selected.Add(evt.SlotIndex));

		Assert.True(ClimbShopService.TryPurchaseSlot(0));
		Assert.Equal(new[] { 0 }, selected);

		selected.Clear();
		var stale = BaseState();
		stale.resources = new ClimbResourceSave { red = 3, white = 3, black = 3 };
		stale.shopSlots[0] = ShopSlot(
			ClimbShopSlotKinds.Upgrade,
			cardKey: "smite|White|Upgraded",
			deckEntryId: original.entryId,
			deckIndex: 0);
		SaveCache.SaveClimbState(stale);
		int resourcesBefore = SaveCache.GetClimbState().resources.red;

		Assert.False(ClimbShopService.TryPurchaseSlot(0));
		Assert.Empty(selected);
		Assert.Equal(resourcesBefore, SaveCache.GetClimbState().resources.red);
	}

	[Fact]
	public void Replacement_open_and_cancel_do_not_spend_or_mutate_deck()
	{
		PrepareRun(new List<string> { "smite|White", "fervor|Red" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Replacement, cardKey: "strike|Red");
		SaveCache.SaveClimbState(state);

		Assert.True(ClimbShopService.TryOpenReplacementOffer(0));
		ClimbShopService.CancelReplacementOffer();

		var after = SaveCache.GetClimbState();
		Assert.Null(after.pendingReplacementOffer);
		Assert.Equal(3, after.resources.red);
		Assert.Equal(0, after.time);
		Assert.Equal(
			new[] { "smite|White", "fervor|Red" },
			SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.Select(entry => entry.cardKey));
	}

	[Fact]
	public void Replacement_final_selection_spends_replaces_non_weapon_card_and_sells_slot()
	{
		PrepareRun(new List<string> { "smite|White", "fervor|Red" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Replacement, cardKey: "strike|Red");
		SaveCache.SaveClimbState(state);
		Assert.True(ClimbShopService.TryOpenReplacementOffer(0));
		var outgoing = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[1];
		Assert.True(SaveCache.AddRunDeckEntryRestriction(
			RunDeckService.PrimaryLoadoutId,
			outgoing.entryId,
			RunScopedStateService.RestrictionFrozen));

		Assert.True(ClimbShopService.TryFinalizeReplacement(outgoing.entryId));

		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		var after = SaveCache.GetClimbState();
		var replacement = loadout.cards[1];
		Assert.Equal("strike|Red", replacement.cardKey);
		Assert.NotEqual(outgoing.entryId, replacement.entryId);
		Assert.False(replacement.isStarter);
		Assert.True(replacement.countsAsTraded);
		Assert.Empty(replacement.restrictions);
		Assert.Empty(replacement.boons);
		Assert.Equal(2, after.resources.red);
		Assert.Equal(1, after.time);
		Assert.True(after.shopSlots[0].isSold);
		Assert.Null(after.pendingReplacementOffer);
	}

	[Fact]
	public void Medal_purchase_equips_medal_on_live_player()
	{
		PrepareRun(new List<string> { "smite|White" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Medal, itemId: "st_luke");
		SaveCache.SaveClimbState(state);
		var world = new World();
		RunPlayerService.EnsureRunPlayer(world);

		Assert.True(ClimbShopService.TryPurchaseSlot(world.EntityManager, 0));

		Assert.Contains(world.EntityManager.GetEntitiesWithComponent<EquippedMedal>(), entity =>
			string.Equals(entity.GetComponent<EquippedMedal>()?.Medal?.Id, "st_luke", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Equipment_purchase_equips_equipment_on_live_player()
	{
		PrepareRun(new List<string> { "smite|White" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Equipment, itemId: "knightly_chest");
		SaveCache.SaveClimbState(state);
		var world = new World();
		RunPlayerService.EnsureRunPlayer(world);

		Assert.True(ClimbShopService.TryPurchaseSlot(world.EntityManager, 0));

		Assert.Contains(world.EntityManager.GetEntitiesWithComponent<EquippedEquipment>(), entity =>
			string.Equals(entity.GetComponent<EquippedEquipment>()?.Equipment?.Id, "knightly_chest", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void Shop_purchase_that_caps_time_queues_final_encounter()
	{
		EventManager.Clear();
		PrepareRun(new List<string> { "smite|White" });
		var state = BaseState();
		state.time = ClimbRuleService.BaseMaxTime - 1;
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Medal, itemId: "st_luke");
		SaveCache.SaveClimbState(state);
		var world = new World();
		ShowTransition transition = null;
		EventManager.Subscribe<ShowTransition>(evt => transition = evt);

		Assert.True(ClimbShopService.TryPurchaseSlot(world.EntityManager, 0));

		var climb = SaveCache.GetClimbState();
		var queued = world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<QueuedEvents>();
		var pending = world.EntityManager.GetEntitiesWithComponent<QueuedEvents>().Single().GetComponent<PendingQuestDialog>();
		Assert.Equal(ClimbRuleService.BaseMaxTime, climb.time);
		Assert.Equal("final", queued.ClimbEncounterSlotId);
		Assert.Equal("fallen_shepherd", queued.Events.Single().EventId);
		Assert.NotNull(pending);
		Assert.Equal("fallen_shepherd", pending.DialogId);
		Assert.Equal("intro", pending.SegmentId);
		Assert.NotNull(transition);
		Assert.Equal(SceneId.Battle, transition.Scene);
	}

	[Fact]
	public void Replacement_shop_action_opens_selectable_modal_with_only_eligible_non_weapon_deck_cards()
	{
		EventManager.Clear();
		PrepareRun(new List<string> { "smite|White", "hammer|White", "kunai|White", "fervor|Red" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Replacement, cardKey: "strike|Red");
		SaveCache.SaveClimbState(state);
		var entityManager = new EntityManager();
		var sceneEntity = entityManager.CreateEntity("Scene");
		entityManager.AddComponent(sceneEntity, new SceneState { Current = SceneId.Climb });
		var slotEntity = entityManager.CreateEntity("ClimbShopSlot");
		entityManager.AddComponent(slotEntity, new ClimbShopSlotAction { SlotIndex = 0 });
		OpenCardListModalEvent opened = null;
		EventManager.Subscribe<OpenCardListModalEvent>(evt => opened = evt);

		UIElementEventDelegateService.HandleEvent(UIElementEventType.ClimbShopSlotSelect, slotEntity, entityManager);

		Assert.NotNull(opened);
		Assert.True(opened.IsSelectable);
		Assert.Equal(CardListSelectionContexts.ClimbReplacement, opened.SelectionContext);
		var openedKeys = opened.Cards
			.Select(c => c.GetComponent<RunDeckCard>()?.CardKey)
			.Where(k => !string.IsNullOrWhiteSpace(k))
			.ToList();
		Assert.Equal(new[] { "smite|White", "fervor|Red" }, openedKeys);
		Assert.All(opened.Cards, card =>
		{
			var metadata = card.GetComponent<CardListModalSelectionMetadata>();
			Assert.NotNull(metadata);
			Assert.Equal(CardListSelectionContexts.ClimbReplacement, metadata.SelectionContext);
			Assert.False(string.IsNullOrWhiteSpace(metadata.EntryId));
		});
		Assert.NotNull(SaveCache.GetClimbState().pendingReplacementOffer);
		Assert.Equal(3, SaveCache.GetClimbState().resources.red);
		Assert.Equal(0, SaveCache.GetClimbState().time);
	}

	[Fact]
	public void Replacement_modal_selection_finalizes_pending_climb_replacement()
	{
		EventManager.Clear();
		PrepareRun(new List<string> { "smite|White", "fervor|Red" });
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(ClimbShopSlotKinds.Replacement, cardKey: "strike|Red");
		SaveCache.SaveClimbState(state);
		Assert.True(ClimbShopService.TryOpenReplacementOffer(0));
		var outgoing = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[1];
		var entityManager = new EntityManager();
		var selectedCard = entityManager.CreateEntity("SelectedCard");
		entityManager.AddComponent(selectedCard, new CardListModalSelectionMetadata
		{
			SelectionContext = CardListSelectionContexts.ClimbReplacement,
			EntryId = outgoing.entryId,
			CardKey = "fervor|Red",
			SourceIndex = 1,
		});

		Assert.True(CardListModalSystem.TryFinalizeClimbReplacementSelection(selectedCard, entityManager));

		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		var after = SaveCache.GetClimbState();
		Assert.Equal("strike|Red", loadout.cards[1].cardKey);
		Assert.NotEqual(outgoing.entryId, loadout.cards[1].entryId);
		Assert.True(loadout.cards[1].countsAsTraded);
		Assert.Equal(2, after.resources.red);
		Assert.Equal(1, after.time);
		Assert.True(after.shopSlots[0].isSold);
		Assert.Null(after.pendingReplacementOffer);
	}

	[Fact]
	public void Invalid_card_shop_offer_clears_until_refresh()
	{
		var loadout = new LoadoutDefinition
		{
			id = RunDeckService.PrimaryLoadoutId,
			cards = new List<LoadoutCardEntry>
			{
				new() { entryId = "test_card_0", cardKey = "smite|White|Upgraded" },
			},
			medalIds = new List<string>(),
		};
		var state = BaseState();
		state.shopSlots[0] = ShopSlot(
			ClimbShopSlotKinds.Upgrade,
			cardKey: "smite|White|Upgraded",
			deckEntryId: "test_card_0",
			deckIndex: 0);

		Assert.True(ClimbShopService.ClearInvalidOffers(state, loadout));
		Assert.Equal(ClimbShopSlotKinds.Empty, state.shopSlots[0].kind);
	}

	[Fact]
	public void Pilgrimage_climb_state_round_trip_preserves_extended_total_time()
	{
		PrepareRun(new List<string> { "smite|White" });
		var state = BaseState();
		state.penanceLevel = 10;
		state.time = 36;

		SaveCache.SaveClimbState(state);

		var saved = SaveCache.GetClimbState();
		Assert.Equal(36, ClimbRuleService.GetMaxTime(saved));
		Assert.Equal(36, saved.time);
		Assert.True(ClimbRuleService.HasPendingFinalEncounter(saved));
	}

	private static void PrepareRun(List<string> cards)
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = cards.Select((cardKey, index) => new LoadoutCardEntry
		{
			entryId = $"test_card_{index}",
			cardKey = cardKey,
			isStarter = true,
			countsAsTraded = false,
			restrictions = new List<string>(),
		}).ToList();
		loadout.medalIds = new List<string>();
		loadout.weaponId = "sword";
		loadout.headId = string.Empty;
		loadout.chestId = string.Empty;
		loadout.armsId = string.Empty;
		loadout.legsId = string.Empty;
		SaveCache.SaveLoadout(loadout);
	}

	private static ClimbSaveState BaseState()
	{
		var state = SaveCache.GetClimbState();
		state.time = 0;
		state.resources = new ClimbResourceSave { red = 3, white = 3, black = 3 };
		state.shopSlots = Enumerable.Range(0, ClimbRuleService.ShopSlotCount)
			.Select(i => ShopSlot(ClimbShopSlotKinds.Empty))
			.ToList();
		return state;
	}

	private static ClimbShopSlotSave ShopSlot(
		string kind,
		string itemId = "",
		string cardKey = "",
		string deckEntryId = "",
		int deckIndex = -1)
	{
		return new ClimbShopSlotSave
		{
			id = "slot",
			kind = kind,
			itemId = itemId,
			cardKey = cardKey,
			deckEntryId = deckEntryId,
			deckIndex = deckIndex,
			cost = new ClimbResourceSave { red = 1, white = 0, black = 0 },
			timeCost = string.Equals(kind, ClimbShopSlotKinds.Empty, StringComparison.OrdinalIgnoreCase) ? 0 : 1,
		};
	}

	private sealed class CountingRandom : Random
	{
		public int NextCalls { get; private set; }

		public override int Next(int maxValue)
		{
			NextCalls++;
			return base.Next(maxValue);
		}
	}
}
