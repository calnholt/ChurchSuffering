using System.Linq;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public class StarterDeckSaveTests
{
	[Fact]
	public void StartingDeckGenerator_builds_full_starting_deck()
	{
		var deck = StartingDeckGeneratorService.Generate(
			StartingDeckGeneratorService.DefaultStarterCardPool,
			12345);

		Assert.Equal(DeckRules.StartingDeckSize, deck.Count);
	}

	[Fact]
	public void Generate_enforces_single_copy_limit_for_overlay_cards()
	{
		var pool = new[] { "smite", "fervor", "courageous", "reckoning" };
		var singleCopy = new[] { "smite" };

		for (int seed = 0; seed < 100; seed++)
		{
			var deck = StartingDeckGeneratorService.Generate(pool, seed, singleCopy);
			Assert.True(DeckRules.CountCardIdInDeck(deck, "smite") <= 1);
		}
	}

	[Fact]
	public void Generate_includes_single_copy_ids_not_in_main_pool()
	{
		var pool = new[] { "fervor", "courageous" };
		var singleCopy = new[] { "smite" };

		for (int seed = 0; seed < 50; seed++)
		{
			var deck = StartingDeckGeneratorService.Generate(pool, seed, singleCopy);
			Assert.Equal(1, DeckRules.CountCardIdInDeck(deck, "smite"));
		}
	}

	[Fact]
	public void Weapon_starter_pools_include_guaranteed_single_copy_cards()
	{
		for (int seed = 0; seed < 100; seed++)
		{
			var swordDeck = StartingDeckGeneratorService.Generate(
				StartingDeckGeneratorService.GetSwordStarterCardPool(),
				seed,
				StartingDeckGeneratorService.GetSwordSingleCopyStarterCardPool());
			Assert.Equal(1, DeckRules.CountCardIdInDeck(swordDeck, "exaltation"));
			Assert.Equal(1, DeckRules.CountCardIdInDeck(swordDeck, "increase_faith"));

			var daggerDeck = StartingDeckGeneratorService.Generate(
				StartingDeckGeneratorService.GetDaggerStarterCardPool(),
				seed,
				StartingDeckGeneratorService.GetDaggerSingleCopyStarterCardPool());
			Assert.Equal(1, DeckRules.CountCardIdInDeck(daggerDeck, "razor_storm"));
			Assert.Equal(1, DeckRules.CountCardIdInDeck(daggerDeck, "increase_faith"));

			var hammerDeck = StartingDeckGeneratorService.Generate(
				StartingDeckGeneratorService.GetHammerStarterCardPool(),
				seed,
				StartingDeckGeneratorService.GetHammerSingleCopyStarterCardPool());
			Assert.Equal(1, DeckRules.CountCardIdInDeck(hammerDeck, "unburdened_strike"));
			Assert.Equal(1, DeckRules.CountCardIdInDeck(hammerDeck, "increase_faith"));
		}
	}

	[Fact]
	public void StartNewRun_persists_structured_starter_entries_with_stable_ids()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();

		var save = SaveCache.GetAll();
		var loadout = save.loadouts?.FirstOrDefault(l => l.id == "loadout_1");

		Assert.NotNull(loadout);
		Assert.Equal(DeckRules.StartingDeckSize, loadout.cards.Count);
		Assert.Equal(loadout.cards.Count, loadout.cards.Select(entry => entry.entryId).Distinct().Count());
		Assert.Equal(loadout.cards.Count, save.nextRunDeckEntryId);
		for (int i = 0; i < loadout.cards.Count; i++)
		{
			var entry = loadout.cards[i];
			Assert.Equal($"run_card_{i}", entry.entryId);
			Assert.False(string.IsNullOrWhiteSpace(entry.cardKey));
			Assert.True(entry.isStarter);
			Assert.False(entry.countsAsTraded);
			Assert.Empty(entry.restrictions);
		}
	}
}
