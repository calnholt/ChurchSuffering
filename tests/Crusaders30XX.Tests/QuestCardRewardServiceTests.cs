using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Loadouts;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Objects.Cards;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class QuestCardRewardServiceTests
{
	private static readonly string[] ExpectedSharedIncomingPool = CardFactory.GetAllCards()
		.Where(pair => pair.Value.CanAddToLoadout && !pair.Value.IsWeapon && !pair.Value.IsToken && pair.Value.Rarity != Rarity.Starter)
		.Select(pair => pair.Key.ToKey())
		.Order(StringComparer.OrdinalIgnoreCase)
		.ToArray();

	[Fact]
	public void GenerateDeckRewardOffer_prioritizes_starter_exchange_targets()
	{
		var deckKeys = new List<string>
		{
			"fervor|Red",
			"smite|White",
			"reckoning|Black",
			"seize|Red"
		};

		var offer = QuestCardRewardService.GenerateDeckRewardOffer(deckKeys, "sword", 20);

		Assert.True(offer.options.Count >= 2);
		Assert.Equal("smite|White", offer.options[0].outgoingCardKey);
		Assert.Equal("reckoning|Black", offer.options[1].outgoingCardKey);
		Assert.All(offer.options.Where(o => o.kind == DeckRewardOfferKinds.Exchange), option =>
		{
			Assert.NotEqual(
				RunDeckService.TryParseCardKey(option.outgoingCardKey, out var outgoingId, out _) ? outgoingId : string.Empty,
				RunDeckService.TryParseCardKey(option.incomingCardKey, out var incomingId, out _) ? incomingId : string.Empty,
				StringComparer.OrdinalIgnoreCase);
		});
	}

	[Fact]
	public void Incoming_exchange_pool_contains_shared_and_auto_upgrade_cards_for_sword_and_dagger()
	{
		var swordPool = QuestCardRewardService.GetEligibleRewardCardIdsForTests(Array.Empty<string>(), "sword");
		var swordAllowed = ExpectedSharedIncomingPool
			.Concat(StartingDeckGeneratorService.GetAutoUpgradeCardIds("sword"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		Assert.All(swordPool, id => Assert.Contains(id, swordAllowed, StringComparer.OrdinalIgnoreCase));
		Assert.All(swordAllowed, id => Assert.Contains(id, swordPool, StringComparer.OrdinalIgnoreCase));

		var daggerPool = QuestCardRewardService.GetEligibleRewardCardIdsForTests(Array.Empty<string>(), "dagger");
		var daggerAllowed = ExpectedSharedIncomingPool
			.Concat(StartingDeckGeneratorService.GetAutoUpgradeCardIds("dagger"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		Assert.All(daggerPool, id => Assert.Contains(id, daggerAllowed, StringComparer.OrdinalIgnoreCase));
		Assert.All(daggerAllowed, id => Assert.Contains(id, daggerPool, StringComparer.OrdinalIgnoreCase));
	}

	[Fact]
	public void Incoming_exchange_pool_adds_hammer_bonus_and_auto_upgrade_cards_for_hammer()
	{
		var expected = ExpectedSharedIncomingPool
			.Concat(new[] { "unburdened_strike", "battering_blow" })
			.Concat(StartingDeckGeneratorService.GetAutoUpgradeCardIds("hammer"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.Order(StringComparer.OrdinalIgnoreCase);

		Assert.Equal(
			expected,
			QuestCardRewardService.GetEligibleRewardCardIdsForTests(Array.Empty<string>(), "hammer")
				.Order(StringComparer.OrdinalIgnoreCase));
	}

	[Fact]
	public void Incoming_exchange_pool_normalizes_exhaltation_to_existing_exaltation()
	{
		var pool = QuestCardRewardService.GetEligibleRewardCardIdsForTests(Array.Empty<string>(), "sword");

		Assert.Contains("exaltation", pool, StringComparer.OrdinalIgnoreCase);
		Assert.DoesNotContain("exhaltation", pool, StringComparer.OrdinalIgnoreCase);
	}

	[Fact]
	public void Generated_exchange_incoming_cards_are_from_eligible_pools()
	{
		var deckKeys = new List<string>
		{
			"smite|White",
			"reckoning|Black",
			"fervor|Red"
		};
		var swordAllowed = ExpectedSharedIncomingPool
			.Concat(StartingDeckGeneratorService.GetAutoUpgradeCardIds("sword"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var daggerAllowed = ExpectedSharedIncomingPool
			.Concat(StartingDeckGeneratorService.GetAutoUpgradeCardIds("dagger"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);
		var hammerAllowed = ExpectedSharedIncomingPool
			.Concat(new[] { "unburdened_strike", "battering_blow" })
			.Concat(StartingDeckGeneratorService.GetAutoUpgradeCardIds("hammer"))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		for (int i = 0; i < 100; i++)
		{
			AssertExchangeIncomingPool(deckKeys, "sword", swordAllowed);
			AssertExchangeIncomingPool(deckKeys, "dagger", daggerAllowed);
			AssertExchangeIncomingPool(deckKeys, "hammer", hammerAllowed);
		}
	}

	[Fact]
	public void GenerateDeckRewardOffer_excludes_upgraded_cards_from_targeting()
	{
		var deckKeys = new List<string>
		{
			"smite|White|Upgraded",
			"reckoning|Black|Upgraded",
			"fervor|Red"
		};

		var offer = QuestCardRewardService.GenerateDeckRewardOffer(deckKeys, "sword", 20);

		Assert.DoesNotContain(offer.options, o => o.outgoingCardKey.Contains("Upgraded", StringComparison.OrdinalIgnoreCase));
	}

	[Fact]
	public void GenerateDeckRewardOffer_after_ten_accepted_mutations_uses_both_offer_compositions()
	{
		var entries = Entries(
			"smite|White",
			"reckoning|Black",
			"fervor|Red",
			"seize|Black");

		var earlyOffer = QuestCardRewardService.GenerateDeckRewardOffer(
			entries, "sword", 20, restrictToCollection: false, acceptedDeckRewardMutations: 9, random: new Random(3));
		Assert.Equal(2, earlyOffer.options.Count(option => option.kind == DeckRewardOfferKinds.Exchange));
		Assert.Equal("smite|White", earlyOffer.options[0].outgoingCardKey);
		Assert.Equal("reckoning|Black", earlyOffer.options[1].outgoingCardKey);

		var exchangeCounts = new HashSet<int>();
		for (int seed = 0; seed < 20; seed++)
		{
			var offer = QuestCardRewardService.GenerateDeckRewardOffer(
				entries, "sword", 20, restrictToCollection: false, acceptedDeckRewardMutations: 10, random: new Random(seed));
			Assert.Equal(3, offer.options.Count);
			exchangeCounts.Add(offer.options.Count(option => option.kind == DeckRewardOfferKinds.Exchange));
		}

		Assert.Equal(new[] { 1, 2 }, exchangeCounts.Order());
	}

	[Fact]
	public void GenerateDeckRewardOffer_after_ten_accepted_mutations_targets_nonstarters_and_upgraded_starters()
	{
		var entries = Entries(
			"smite|White|Upgraded",
			"reckoning|Black|Upgraded",
			"fervor|Red",
			"seize|Black|Upgraded");
		bool foundNonStarter = false;
		bool foundUpgradedStarter = false;

		for (int seed = 0; seed < 100; seed++)
		{
			var offer = QuestCardRewardService.GenerateDeckRewardOffer(
				entries, "sword", 20, restrictToCollection: false, acceptedDeckRewardMutations: 10, random: new Random(seed));
			foreach (var option in offer.options.Where(option => option.kind == DeckRewardOfferKinds.Exchange))
			{
				Assert.False(string.Equals(option.outgoingCardKey, "seize|Black|Upgraded", StringComparison.OrdinalIgnoreCase));
				if (string.Equals(option.outgoingCardKey, "fervor|Red", StringComparison.OrdinalIgnoreCase))
					foundNonStarter = true;
				if (RunDeckService.IsUpgradedCardKey(option.outgoingCardKey))
				{
					foundUpgradedStarter = true;
					Assert.True(RunDeckService.IsUpgradedCardKey(option.incomingCardKey));
				}
			}
		}

		Assert.True(foundNonStarter);
		Assert.True(foundUpgradedStarter);
	}

	[Fact]
	public void ApplyPendingOfferOption_replaces_card_at_same_loadout_position_inherits_restrictions_and_allows_copy_limit_bypass()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = Entries(
			"smite|White",
			"fervor|Red",
			"fervor|White");
		loadout.cards[0].restrictions.Add(RunScopedStateService.RestrictionFrozen);
		SaveCache.SaveLoadout(loadout);
		var outgoingEntryId = loadout.cards[0].entryId;

		SaveCache.SetPendingDeckRewardOffer(new DeckRewardOfferSave
		{
			rewardGold = 20,
			options = new List<DeckRewardOfferOptionSave>
			{
				new()
				{
					kind = DeckRewardOfferKinds.Exchange,
					loadoutIndex = 0,
					outgoingEntryId = outgoingEntryId,
					outgoingCardKey = "smite|White",
					incomingCardKey = "fervor|Red"
				}
			}
		});

		Assert.True(QuestCardRewardService.ApplyPendingOfferOption(0));

		var after = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards;
		Assert.Equal("fervor|Red", after[0].cardKey);
		Assert.NotEqual(outgoingEntryId, after[0].entryId);
		Assert.False(after[0].isStarter);
		Assert.True(after[0].countsAsTraded);
		Assert.Contains(RunScopedStateService.RestrictionFrozen, after[0].restrictions);
		Assert.Equal(3, DeckRules.CountCardIdInDeck(after.Select(entry => entry.cardKey).ToList(), "fervor"));
		Assert.Equal(1, SaveCache.GetAcceptedDeckRewardMutationCount());
		Assert.Null(SaveCache.GetPendingDeckRewardOffer());
	}

	[Fact]
	public void ApplyPendingOfferOption_upgrade_persists_upgraded_key_without_stat_mutation()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = Entries("smite|White");
		loadout.cards[0].countsAsTraded = true;
		loadout.cards[0].restrictions.Add(RunScopedStateService.RestrictionFrozen);
		SaveCache.SaveLoadout(loadout);
		var original = loadout.cards[0];

		SaveCache.SetPendingDeckRewardOffer(new DeckRewardOfferSave
		{
			options = new List<DeckRewardOfferOptionSave>
			{
				new()
				{
					kind = DeckRewardOfferKinds.Upgrade,
					loadoutIndex = 0,
					outgoingEntryId = original.entryId,
					outgoingCardKey = "smite|White",
					upgradedCardKey = "smite|White|Upgraded"
				}
			}
		});

		Assert.True(QuestCardRewardService.ApplyPendingOfferOption(0));

		var upgradedEntry = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.Single();
		Assert.Equal(original.entryId, upgradedEntry.entryId);
		Assert.Equal("smite|White|Upgraded", upgradedEntry.cardKey);
		Assert.True(upgradedEntry.isStarter);
		Assert.True(upgradedEntry.countsAsTraded);
		Assert.Contains(RunScopedStateService.RestrictionFrozen, upgradedEntry.restrictions);
		Assert.Equal(1, SaveCache.GetAcceptedDeckRewardMutationCount());
		var baseCard = Crusaders30XX.ECS.Factories.CardFactory.Create("smite");
		var upgradedCard = Crusaders30XX.ECS.Factories.CardFactory.Create("smite");
		upgradedCard.IsUpgraded = true;
		Assert.Equal(baseCard.Damage, upgradedCard.Damage);
		Assert.EndsWith("+", upgradedCard.DisplayName);
	}

	[Fact]
	public void SkipPendingOffer_clears_offer_without_deck_mutation()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var before = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards
			.Select(entry => (entry.entryId, entry.cardKey))
			.ToList();
		SaveCache.SetPendingDeckRewardOffer(new DeckRewardOfferSave
		{
			options = new List<DeckRewardOfferOptionSave>
			{
				new()
				{
					kind = DeckRewardOfferKinds.Exchange,
					loadoutIndex = 0,
					outgoingEntryId = before[0].entryId,
					outgoingCardKey = before[0].cardKey,
					incomingCardKey = "fervor|Red"
				}
			}
		});

		QuestCardRewardService.SkipPendingOffer();

		Assert.Null(SaveCache.GetPendingDeckRewardOffer());
		Assert.Equal(
			before,
			SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards
				.Select(entry => (entry.entryId, entry.cardKey))
				.ToList());
	}

	[Fact]
	public void PendingOffer_persists_across_save_reload()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var outgoing = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards[0];
		SaveCache.SetPendingDeckRewardOffer(new DeckRewardOfferSave
		{
			rewardGold = 20,
			options = new List<DeckRewardOfferOptionSave>
			{
				new()
				{
					kind = DeckRewardOfferKinds.Upgrade,
					loadoutIndex = 0,
					outgoingEntryId = outgoing.entryId,
					outgoingCardKey = outgoing.cardKey,
					upgradedCardKey = RunDeckService.BuildUpgradedCardKey(outgoing.cardKey),
				}
			}
		});

		SaveCache.Reload();

		var pending = SaveCache.GetPendingDeckRewardOffer();
		Assert.NotNull(pending);
		Assert.Equal(20, pending.rewardGold);
		Assert.Single(pending.options);
		Assert.Equal(outgoing.entryId, pending.options[0].outgoingEntryId);
		Assert.Equal(RunDeckService.BuildUpgradedCardKey(outgoing.cardKey), pending.options[0].upgradedCardKey);
	}

	[Fact]
	public void CardKeyParser_accepts_legacy_and_upgraded_keys()
	{
		Assert.True(RunDeckService.TryParseCardKey("smite|white", out var legacyId, out var legacyColor, out var legacyUpgraded));
		Assert.Equal("smite", legacyId);
		Assert.Equal(Crusaders30XX.ECS.Components.CardData.CardColor.White, legacyColor);
		Assert.False(legacyUpgraded);

		Assert.True(RunDeckService.TryParseCardKey("smite|Red|Upgraded", out var upgradedId, out var upgradedColor, out var upgraded));
		Assert.Equal("smite", upgradedId);
		Assert.Equal(Crusaders30XX.ECS.Components.CardData.CardColor.Red, upgradedColor);
		Assert.True(upgraded);
	}

	[Fact]
	public void OnUpgrade_spawn_path_runs_when_upgraded_card_initializes()
	{
		TestOnUpgradeCard.ResetCounts();
		var entityManager = new EntityManager();
		var entity = entityManager.CreateEntity("TestOnUpgradeCard");
		var card = new TestOnUpgradeCard { IsUpgraded = true };
		card.Initialize(entityManager, entity);

		Assert.Equal(1, TestOnUpgradeCard.SpawnInvokeCount);
		Assert.Equal(0, TestOnUpgradeCard.ApplyInvokeCount);
	}

	[Fact]
	public void OnUpgrade_apply_path_runs_when_upgrade_confirmed()
	{
		TestOnUpgradeCard.ResetCounts();
		CardUpgradeService.InvokeUpgradeConfirmedOnCard(new TestOnUpgradeCard());

		Assert.Equal(0, TestOnUpgradeCard.SpawnInvokeCount);
		Assert.Equal(1, TestOnUpgradeCard.ApplyInvokeCount);
	}

	[Fact]
	public void ApplyPendingOfferOption_upgrade_invokes_upgrade_confirmed_hook()
	{
		CardUpgradeService.UpgradeConfirmedInvokeCountForTests = 0;
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = Entries("smite|White");
		SaveCache.SaveLoadout(loadout);
		var outgoingEntryId = loadout.cards[0].entryId;

		SaveCache.SetPendingDeckRewardOffer(new DeckRewardOfferSave
		{
			options = new List<DeckRewardOfferOptionSave>
			{
				new()
				{
					kind = DeckRewardOfferKinds.Upgrade,
					loadoutIndex = 0,
					outgoingEntryId = outgoingEntryId,
					outgoingCardKey = "smite|White",
					upgradedCardKey = "smite|White|Upgraded"
				}
			}
		});

		Assert.True(QuestCardRewardService.ApplyPendingOfferOption(0));
		var upgraded = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.Single();
		Assert.Equal(outgoingEntryId, upgraded.entryId);
		Assert.Equal("smite|White|Upgraded", upgraded.cardKey);
		Assert.Equal(1, CardUpgradeService.UpgradeConfirmedInvokeCountForTests);
	}

	[Fact]
	public void IsInDefaultStarterPool_recognizes_all_default_starter_cards()
	{
		foreach (var cardId in StartingDeckGeneratorService.DefaultStarterCardPool)
		{
			Assert.True(StartingDeckGeneratorService.IsInDefaultStarterPool(cardId));
		}
	}

	[Fact]
	public void GetAutoUpgradeCardIds_returns_correct_set_for_each_weapon()
	{
		var swordSet = StartingDeckGeneratorService.GetAutoUpgradeCardIds("sword");
		Assert.Contains("mantlet", swordSet);
		Assert.Contains("steadfast_resolve", swordSet);
		Assert.Contains("increase_faith", swordSet);
		Assert.Contains("seize", swordSet);
		Assert.Contains("whirlwind", swordSet);
		Assert.DoesNotContain("fervor", swordSet);
		Assert.DoesNotContain("stab", swordSet);
		Assert.DoesNotContain("smite", swordSet);
		Assert.DoesNotContain("absolution", swordSet);

		var hammerSet = StartingDeckGeneratorService.GetAutoUpgradeCardIds("hammer");
		Assert.Contains("fervor", hammerSet);
		Assert.Contains("stab", hammerSet);
		Assert.Contains("exaltation", hammerSet);
		Assert.Contains("increase_faith", hammerSet);
		Assert.DoesNotContain("mantlet", hammerSet);
		Assert.DoesNotContain("steadfast_resolve", hammerSet);
		Assert.DoesNotContain("smite", hammerSet);

		var daggerSet = StartingDeckGeneratorService.GetAutoUpgradeCardIds("dagger");
		Assert.Contains("mantlet", daggerSet);
		Assert.Contains("fervor", daggerSet);
		Assert.Contains("increase_faith", daggerSet);
		Assert.DoesNotContain("seize", daggerSet);
		Assert.DoesNotContain("whirlwind", daggerSet);
		Assert.DoesNotContain("smite", daggerSet);
	}

	[Fact]
	public void Quest_exchange_auto_upgrades_off_weapon_cards_in_incoming_pool()
	{
		var autoUpgradeIds = StartingDeckGeneratorService.GetAutoUpgradeCardIds("sword");
		var deckKeys = new List<string> { "smite|White", "reckoning|Black" };

		bool foundUpgradedOffWeapon = false;
		for (int i = 0; i < 100; i++)
		{
			var offer = QuestCardRewardService.GenerateDeckRewardOffer(deckKeys, "sword", 20);
			foreach (var option in offer.options.Where(o => o.kind == DeckRewardOfferKinds.Exchange))
			{
				Assert.True(
					RunDeckService.TryParseCardKey(option.incomingCardKey, out var incomingId, out _, out var isUpgraded));
				if (autoUpgradeIds.Contains(incomingId))
				{
					Assert.True(isUpgraded,
						$"Off-weapon card '{incomingId}' should be auto-upgraded but was not: {option.incomingCardKey}");
					foundUpgradedOffWeapon = true;
				}
			}
		}
		Assert.True(foundUpgradedOffWeapon,
			"Expected at least one off-weapon auto-upgraded card to appear in exchange options within 100 iterations");
	}

	[Fact]
	public void Quest_exchange_does_not_auto_upgrade_equipped_weapon_cards()
	{
		var swordAutoUpgradeIds = StartingDeckGeneratorService.GetAutoUpgradeCardIds("sword");
		var deckKeys = new List<string> { "smite|White", "reckoning|Black", "fervor|Red" };

		for (int i = 0; i < 100; i++)
		{
			var offer = QuestCardRewardService.GenerateDeckRewardOffer(deckKeys, "sword", 20);
			foreach (var option in offer.options.Where(o => o.kind == DeckRewardOfferKinds.Exchange))
			{
				Assert.True(
					RunDeckService.TryParseCardKey(option.incomingCardKey, out var incomingId, out _, out var isUpgraded));
				if (!swordAutoUpgradeIds.Contains(incomingId))
				{
					Assert.False(isUpgraded,
						$"Equipped-weapon card '{incomingId}' should NOT be auto-upgraded but was: {option.incomingCardKey}");
				}
			}
		}
	}

	[Fact]
	public void Quest_exchange_does_not_auto_upgrade_starter_rarity_cards()
	{
		var deckKeys = new List<string> { "fervor|Red", "seize|Black" };

		for (int i = 0; i < 100; i++)
		{
			var offer = QuestCardRewardService.GenerateDeckRewardOffer(deckKeys, "sword", 20);
			foreach (var option in offer.options.Where(o => o.kind == DeckRewardOfferKinds.Exchange))
			{
				Assert.True(
					RunDeckService.TryParseCardKey(option.incomingCardKey, out var incomingId, out _, out var isUpgraded));
				if (incomingId == "smite" || incomingId == "absolution" || incomingId == "forge_strike"
					|| incomingId == "reckoning" || incomingId == "litany_of_wrath" || incomingId == "courageous")
				{
					Assert.False(isUpgraded,
						$"Starter rarity card '{incomingId}' should NOT be auto-upgraded but was: {option.incomingCardKey}");
				}
			}
		}
	}

	[Fact]
	public void Auto_upgraded_exchange_invokes_upgrade_confirmed()
	{
		CardUpgradeService.UpgradeConfirmedInvokeCountForTests = 0;
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = Entries("smite|White");
		SaveCache.SaveLoadout(loadout);
		var outgoingEntryId = loadout.cards[0].entryId;

		SaveCache.SetPendingDeckRewardOffer(new DeckRewardOfferSave
		{
			options = new List<DeckRewardOfferOptionSave>
			{
				new()
				{
					kind = DeckRewardOfferKinds.Exchange,
					loadoutIndex = 0,
					outgoingEntryId = outgoingEntryId,
					outgoingCardKey = "smite|White",
					incomingCardKey = "mantlet|Red|Upgraded"
				}
			}
		});

		Assert.True(QuestCardRewardService.ApplyPendingOfferOption(0));
		Assert.Equal(1, CardUpgradeService.UpgradeConfirmedInvokeCountForTests);

		var after = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.Single();
		Assert.Equal("mantlet|Red|Upgraded", after.cardKey);
	}

	[Fact]
	public void Auto_upgraded_exchange_does_not_invoke_upgrade_confirmed_for_non_upgraded_card()
	{
		CardUpgradeService.UpgradeConfirmedInvokeCountForTests = 0;
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		loadout.cards = Entries("smite|White");
		SaveCache.SaveLoadout(loadout);
		var outgoingEntryId = loadout.cards[0].entryId;

		SaveCache.SetPendingDeckRewardOffer(new DeckRewardOfferSave
		{
			options = new List<DeckRewardOfferOptionSave>
			{
				new()
				{
					kind = DeckRewardOfferKinds.Exchange,
					loadoutIndex = 0,
					outgoingEntryId = outgoingEntryId,
					outgoingCardKey = "smite|White",
					incomingCardKey = "fervor|Red"
				}
			}
		});

		Assert.True(QuestCardRewardService.ApplyPendingOfferOption(0));
		Assert.Equal(0, CardUpgradeService.UpgradeConfirmedInvokeCountForTests);
	}

	private static void AssertExchangeIncomingPool(
		IReadOnlyList<string> deckKeys,
		string weaponId,
		HashSet<string> allowed)
	{
		var offer = QuestCardRewardService.GenerateDeckRewardOffer(deckKeys, weaponId, 20);
		foreach (var option in offer.options.Where(o => o.kind == DeckRewardOfferKinds.Exchange))
		{
			Assert.True(
				RunDeckService.TryParseCardKey(option.incomingCardKey, out var incomingId, out _),
				$"Invalid incoming key {option.incomingCardKey}");
			Assert.Contains(incomingId, allowed);
		}
	}

	private static List<LoadoutCardEntry> Entries(params string[] cardKeys)
	{
		return cardKeys.Select((cardKey, index) => new LoadoutCardEntry
		{
			entryId = $"test_card_{index}",
			cardKey = cardKey,
			isStarter = true,
			countsAsTraded = false,
			restrictions = new List<string>(),
		}).ToList();
	}

	private sealed class TestOnUpgradeCard : CardBase
	{
		public static int SpawnInvokeCount;
		public static int ApplyInvokeCount;

		public TestOnUpgradeCard()
		{
			CardId = "test_on_upgrade_card";
			OnUpgrade = (entityManager, card) =>
			{
				if (card != null)
					SpawnInvokeCount++;
				else
					ApplyInvokeCount++;
			};
		}

		public static void ResetCounts()
		{
			SpawnInvokeCount = 0;
			ApplyInvokeCount = 0;
		}
	}
}
