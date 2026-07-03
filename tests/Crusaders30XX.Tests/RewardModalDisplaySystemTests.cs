using System.Collections.Generic;
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
	private const float AnimDuration = 0.55f;
	private const float PulseAmplitude = 0.12f;
	private const float PulseFrequencyHz = 6f;

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
	public void ComputeDeckColumnSelectionVisual_non_selected_column_fades_out()
	{
		RewardModalDisplaySystem.ComputeDeckColumnSelectionVisual(
			columnIndex: 0,
			selectedColumnIndex: 1,
			isOutgoing: true,
			elapsedSeconds: AnimDuration,
			durationSeconds: AnimDuration,
			pulseAmplitude: PulseAmplitude,
			pulseFrequencyHz: PulseFrequencyHz,
			out var scale,
			out var alpha);

		Assert.Equal(1f, scale);
		Assert.Equal(0f, alpha, 3);
	}

	[Fact]
	public void ComputeDeckColumnSelectionVisual_selected_outgoing_shrinks_to_zero()
	{
		RewardModalDisplaySystem.ComputeDeckColumnSelectionVisual(
			columnIndex: 1,
			selectedColumnIndex: 1,
			isOutgoing: true,
			elapsedSeconds: AnimDuration,
			durationSeconds: AnimDuration,
			pulseAmplitude: PulseAmplitude,
			pulseFrequencyHz: PulseFrequencyHz,
			out var scale,
			out var alpha);

		Assert.Equal(0f, scale, 3);
		Assert.Equal(1f, alpha);
	}

	[Fact]
	public void ComputeDeckColumnSelectionVisual_selected_incoming_pulses_without_shrinking_at_start()
	{
		RewardModalDisplaySystem.ComputeDeckColumnSelectionVisual(
			columnIndex: 1,
			selectedColumnIndex: 1,
			isOutgoing: false,
			elapsedSeconds: 0f,
			durationSeconds: AnimDuration,
			pulseAmplitude: PulseAmplitude,
			pulseFrequencyHz: PulseFrequencyHz,
			out var scale,
			out var alpha);

		Assert.Equal(1f, scale, 3);
		Assert.Equal(1f, alpha);
	}

	[Fact]
	public void GetDeckColumnChromeAlpha_selected_column_stays_opaque()
	{
		var state = new QuestRewardOverlayState
		{
			DeckColumnSelectionInProgress = true,
			SelectedDeckRewardColumnIndex = 2,
			DeckColumnSelectionElapsedSeconds = AnimDuration * 0.5f,
		};

		float alpha = RewardModalDisplaySystem.GetDeckColumnChromeAlpha(2, state, AnimDuration);

		Assert.Equal(1f, alpha);
	}

	[Fact]
	public void GetDeckColumnChromeAlpha_non_selected_column_fades()
	{
		var state = new QuestRewardOverlayState
		{
			DeckColumnSelectionInProgress = true,
			SelectedDeckRewardColumnIndex = 1,
			DeckColumnSelectionElapsedSeconds = AnimDuration,
		};

		float alpha = RewardModalDisplaySystem.GetDeckColumnChromeAlpha(0, state, AnimDuration);

		Assert.Equal(0f, alpha, 3);
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
