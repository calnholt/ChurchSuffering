using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class RewardModalDisplaySystemTests
{
	private static readonly string[] AllPreviewRestrictions =
	{
		RunScopedStateService.RestrictionFrozen,
		RunScopedStateService.RestrictionSealed,
		RunScopedStateService.RestrictionBrittle,
		RunScopedStateService.RestrictionCursed,
	};

	[Fact]
	public void ApplyDeckRewardPreviewRestrictions_outgoing_card_hydrates_saved_restrictions()
	{
		var entityManager = new EntityManager();
		string entryId = SeedRestrictedEntry("smite|White", AllPreviewRestrictions);
		var option = new DeckRewardOfferOptionSave
		{
			kind = DeckRewardOfferKinds.Exchange,
			outgoingEntryId = entryId,
			outgoingCardKey = "smite|White",
			incomingCardKey = "fervor|Red",
		};
		var outgoing = CreatePreviewCard(entityManager, option.outgoingCardKey);

		RewardModalDisplaySystem.ApplyDeckRewardPreviewRestrictions(entityManager, outgoing, option, forIncomingCard: false);

		AssertHasAllPreviewRestrictions(outgoing);
	}

	[Fact]
	public void ApplyDeckRewardPreviewRestrictions_upgrade_incoming_card_copies_outgoing_restrictions()
	{
		var entityManager = new EntityManager();
		string entryId = SeedRestrictedEntry("smite|White", AllPreviewRestrictions);
		var option = new DeckRewardOfferOptionSave
		{
			kind = DeckRewardOfferKinds.Upgrade,
			outgoingEntryId = entryId,
			outgoingCardKey = "smite|White",
			upgradedCardKey = "smite|White|Upgraded",
		};
		var incoming = CreatePreviewCard(entityManager, option.upgradedCardKey);

		RewardModalDisplaySystem.ApplyDeckRewardPreviewRestrictions(entityManager, incoming, option, forIncomingCard: true);

		AssertHasAllPreviewRestrictions(incoming);
	}

	[Fact]
	public void ApplyDeckRewardPreviewRestrictions_exchange_incoming_card_copies_outgoing_restrictions()
	{
		var entityManager = new EntityManager();
		string entryId = SeedRestrictedEntry("smite|White", AllPreviewRestrictions);
		var option = new DeckRewardOfferOptionSave
		{
			kind = DeckRewardOfferKinds.Exchange,
			outgoingEntryId = entryId,
			outgoingCardKey = "smite|White",
			incomingCardKey = "fervor|Red",
		};
		var outgoing = CreatePreviewCard(entityManager, option.outgoingCardKey);
		var incoming = CreatePreviewCard(entityManager, option.incomingCardKey);

		RewardModalDisplaySystem.ApplyDeckRewardPreviewRestrictions(entityManager, outgoing, option, forIncomingCard: false);
		RewardModalDisplaySystem.ApplyDeckRewardPreviewRestrictions(entityManager, incoming, option, forIncomingCard: true);

		AssertHasAllPreviewRestrictions(outgoing);
		AssertHasAllPreviewRestrictions(incoming);
	}

	[Fact]
	public void Entrance_center_lane_starts_at_its_230ms_delay_with_full_blur_and_grayscale()
	{
		var visual = RewardModalDisplaySystem.ComputeEntranceLaneVisual(1, 0.23f);

		Assert.Equal(0f, visual.Alpha, 3);
		Assert.Equal(0.72f, visual.Scale, 3);
		Assert.Equal(9f, visual.Blur, 3);
		Assert.Equal(1f, visual.Grayscale, 3);
	}

	[Fact]
	public void Entrance_outer_lanes_remain_delayed_after_center_lane_begins()
	{
		var center = RewardModalDisplaySystem.ComputeEntranceLaneVisual(1, 0.28f);
		var outer = RewardModalDisplaySystem.ComputeEntranceLaneVisual(0, 0.28f);

		Assert.True(center.Alpha > 0f);
		Assert.Equal(0f, outer.Alpha, 3);
	}

	[Fact]
	public void Entrance_lane_finishes_without_blur_or_grayscale()
	{
		var visual = RewardModalDisplaySystem.ComputeEntranceLaneVisual(0, 1.01f);

		Assert.Equal(1f, visual.Alpha, 3);
		Assert.Equal(1f, visual.Scale, 3);
		Assert.Equal(0f, visual.Blur, 3);
		Assert.Equal(0f, visual.Grayscale, 3);
	}

	[Fact]
	public void Incoming_claim_holds_at_ascended_position_before_final_release()
	{
		float elapsed = 0.08f + 1.37f * 0.5f;
		var visual = RewardModalDisplaySystem.ComputeIncomingClaimVisual(elapsed);

		Assert.Equal(1f, visual.Alpha, 3);
		Assert.Equal(1.18f, visual.Scale, 3);
		Assert.Equal(-150f, visual.Offset.Y, 3);
		Assert.Equal(0f, visual.Blur, 3);
	}

	[Fact]
	public void Incoming_claim_finishes_bright_blurred_and_released()
	{
		var visual = RewardModalDisplaySystem.ComputeIncomingClaimVisual(1.45f);

		Assert.Equal(0f, visual.Alpha, 3);
		Assert.Equal(0.72f, visual.Scale, 3);
		Assert.Equal(-330f, visual.Offset.Y, 3);
		Assert.Equal(10f, visual.Blur, 3);
		Assert.Equal(1.55f, visual.Brightness, 3);
	}

	[Fact]
	public void Random_debug_offer_contains_two_exchanges_and_one_upgrade_with_valid_keys()
	{
		var offer = RewardModalDisplaySystem.BuildRandomDebugOffer(new Random(30030));

		Assert.Equal(3, offer.options.Count);
		Assert.Equal(2, offer.options.Count(option => option.kind == DeckRewardOfferKinds.Exchange));
		Assert.Single(offer.options, option => option.kind == DeckRewardOfferKinds.Upgrade);
		Assert.All(offer.options, option => Assert.True(RunDeckService.TryParseCardKey(option.outgoingCardKey, out _, out _, out _)));
		Assert.All(offer.options, option =>
		{
			string incoming = option.kind == DeckRewardOfferKinds.Upgrade ? option.upgradedCardKey : option.incomingCardKey;
			Assert.True(RunDeckService.TryParseCardKey(incoming, out _, out _, out _));
		});
	}

	[Fact]
	public void Preview_selection_and_skip_leave_pending_offer_unchanged()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var pending = RewardModalDisplaySystem.BuildRandomDebugOffer(new Random(44));
		SaveCache.SetPendingDeckRewardOffer(pending);
		var state = new QuestRewardOverlayState { IsPreviewOnly = true, DeckRewardOffer = pending };

		Assert.True(RewardModalDisplaySystem.TryCommitSelection(state, 0));
		RewardModalDisplaySystem.CommitSkip(state);

		var after = SaveCache.GetPendingDeckRewardOffer();
		Assert.NotNull(after);
		Assert.Equal(pending.options.Count, after.options.Count);
		Assert.Equal(pending.options[0].outgoingCardKey, after.options[0].outgoingCardKey);
	}

	private static string SeedRestrictedEntry(string cardKey, IEnumerable<string> restrictions)
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = new List<LoadoutCardEntry>
		{
			new()
			{
				entryId = "reward_modal_test_entry",
				cardKey = cardKey,
				isStarter = true,
				countsAsTraded = false,
				restrictions = new List<string>(),
			}
		};
		foreach (var restriction in restrictions)
		{
			loadout.cards[0].restrictions.Add(restriction);
		}
		SaveCache.SaveLoadout(loadout);
		return loadout.cards[0].entryId;
	}

	private static Entity CreatePreviewCard(EntityManager entityManager, string cardKey)
	{
		if (!RunDeckService.TryParseCardKey(cardKey, out var cardId, out var color, out var isUpgraded)) return null;
		return EntityFactory.CreateCardFromDefinition(
			entityManager,
			cardId,
			color,
			suppressStatDeltaDisplay: true,
			isUpgraded: isUpgraded);
	}

	private static void AssertHasAllPreviewRestrictions(Entity card)
	{
		Assert.NotNull(card);
		Assert.True(card.HasComponent<Frozen>());
		Assert.True(card.HasComponent<Sealed>());
		Assert.True(card.HasComponent<Brittle>());
		Assert.True(card.HasComponent<Cursed>());
		Assert.Equal(Curse.CardIdValue, card.GetComponent<CardData>()?.Card?.CardId);
	}

	[Fact]
	public void ShouldSuppressBattleSceneDisplay_returns_true_when_overlay_open()
	{
		var entityManager = new EntityManager();
		var overlay = entityManager.CreateEntity("QuestRewardOverlay");
		entityManager.AddComponent(overlay, new QuestRewardOverlayState { IsOpen = true, DismissInProgress = false });

		Assert.True(RewardModalDisplaySystem.ShouldSuppressBattleSceneDisplay(entityManager));
	}

	[Fact]
	public void ShouldSuppressBattleSceneDisplay_returns_true_while_dismiss_in_progress()
	{
		var entityManager = new EntityManager();
		var overlay = entityManager.CreateEntity("QuestRewardOverlay");
		entityManager.AddComponent(overlay, new QuestRewardOverlayState { IsOpen = false, DismissInProgress = true });

		Assert.True(RewardModalDisplaySystem.ShouldSuppressBattleSceneDisplay(entityManager));
	}

	[Fact]
	public void ShouldSuppressBattleSceneDisplay_returns_false_when_overlay_fully_closed()
	{
		var entityManager = new EntityManager();
		var overlay = entityManager.CreateEntity("QuestRewardOverlay");
		entityManager.AddComponent(overlay, new QuestRewardOverlayState { IsOpen = false, DismissInProgress = false });

		Assert.False(RewardModalDisplaySystem.ShouldSuppressBattleSceneDisplay(entityManager));
	}

	[Fact]
	public void ShouldSuppressBattleSceneDisplay_returns_false_when_overlay_missing()
	{
		var entityManager = new EntityManager();

		Assert.False(RewardModalDisplaySystem.ShouldSuppressBattleSceneDisplay(entityManager));
	}

}
