using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class CardBoonTests : IDisposable
{
	public void Dispose()
	{
		SaveCache.DeleteSaveFilesIfPresent();
	}

	[Fact]
	public void Synchronize_applies_stacked_boons_once_and_exposes_tooltips()
	{
		var entityManager = new EntityManager();
		var card = EntityFactory.CreateCardFromDefinition(entityManager, "strike", CardData.CardColor.Red);
		var boons = new List<CardBoonSave>
		{
			new() { type = CardBoonKinds.Overcharged, amount = 2 },
			new() { type = CardBoonKinds.Quickened, amount = 1 },
			new() { type = CardBoonKinds.Honed, amount = 2 },
			new() { type = CardBoonKinds.Guarded, amount = 3 },
		};

		CardBoonApplicator.Synchronize(entityManager, card, boons);
		CardBoonApplicator.Synchronize(entityManager, card, boons);

		var definition = card.GetComponent<CardData>().Card;
		Assert.Equal(15, definition.Damage);
		Assert.Equal(6, definition.Block);
		Assert.Equal(new[] { "Any", "Any" }, definition.Cost);
		Assert.True(definition.IsFreeAction);
		Assert.Equal(4, card.GetComponent<CardBoonComponent>().Boons.Count);

		string tooltip = TooltipTextService.BuildCardTooltip(card, string.Empty, entityManager);
		Assert.Contains("Overcharged 2 - This card has +10 damage and +2 Any cost.", tooltip);
		Assert.Contains("Quickened - This card is a Free Action.", tooltip);
		Assert.Contains("Honed 2 - This card has +2 damage.", tooltip);
		Assert.Contains("Guarded 3 - This card has +3 block.", tooltip);
	}

	[Fact]
	public void Wild_runs_after_authored_upgrade_cost_changes()
	{
		var entityManager = new EntityManager();
		var card = EntityFactory.CreateCardFromDefinition(
			entityManager,
			"iron_covenant",
			CardData.CardColor.Red,
			isUpgraded: true);

		CardBoonApplicator.Synchronize(entityManager, card,
			[new CardBoonSave { type = CardBoonKinds.Wild, amount = 1 }]);

		var costs = card.GetComponent<CardData>().Card.Cost;
		Assert.Equal(6, costs.Count);
		Assert.All(costs, cost => Assert.Equal("Any", cost));
	}

	[Fact]
	public void Saved_boons_survive_upgrade_and_rehydrate_after_reload()
	{
		SaveCache.StartNewRun();
		var added = SaveCache.AddRunDeckEntry(
			RunDeckService.PrimaryLoadoutId,
			"iron_covenant|Red",
			publishChange: false);
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		var entry = loadout.cards.Single(candidate => candidate.entryId == added.entryId);
		entry.boons.Add(new CardBoonSave { type = CardBoonKinds.Wild, amount = 1 });
		SaveCache.SaveLoadout(loadout);

		Assert.True(SaveCache.TryUpgradeRunDeckEntry(
			RunDeckService.PrimaryLoadoutId,
			entry.entryId,
			"iron_covenant|Red|Upgraded",
			out var upgraded));
		Assert.Single(upgraded.boons);

		SaveCache.Reload();
		var entityManager = new EntityManager();
		RunDeckService.EnsureRunDeck(entityManager);
		var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
			.Single(candidate => candidate.GetComponent<RunDeckCard>().EntryId == entry.entryId);
		Assert.All(card.GetComponent<CardData>().Card.Cost, cost => Assert.Equal("Any", cost));
		Assert.Equal(1, card.GetComponent<CardBoonComponent>().Boons.Single().Amount);
	}

	[Fact]
	public void Overcharged_rejects_an_attack_at_four_effective_cost()
	{
		var entry = new LoadoutCardEntry
		{
			entryId = "attack",
			cardKey = "excavate|Black",
			boons = [new CardBoonSave { type = CardBoonKinds.Overcharged, amount = 2 }],
		};

		Assert.Equal(4, CardBoonRules.CreateEffectiveCard(entry).Cost.Count);
		Assert.False(CardBoonRules.IsEligible(CardBoonKinds.Overcharged, entry));
		Assert.True(CardBoonRules.IsEligible(CardBoonKinds.Honed, entry));
	}

	[Fact]
	public void Versatile_rehydrates_existing_dual_color_and_names_it_in_tooltip()
	{
		SaveCache.StartNewRun();
		var added = SaveCache.AddRunDeckEntry(
			RunDeckService.PrimaryLoadoutId,
			"smite|White",
			publishChange: false);
		var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
		var entry = loadout.cards.Single(candidate => candidate.entryId == added.entryId);
		entry.secondaryColor = "Black";
		entry.boons.Add(new CardBoonSave { type = CardBoonKinds.Versatile, amount = 1 });
		SaveCache.SaveLoadout(loadout);
		var entityManager = new EntityManager();

		RunDeckService.EnsureRunDeck(entityManager);

		var card = entityManager.GetEntitiesWithComponent<RunDeckCard>()
			.Single(candidate => candidate.GetComponent<RunDeckCard>().EntryId == entry.entryId);
		Assert.Equal(CardData.CardColor.Black, card.GetComponent<DualColor>().SecondaryColor);
		Assert.Contains(
			"Versatile - This card also counts as Black.",
			TooltipTextService.BuildCardTooltip(card, string.Empty, entityManager));
		Assert.False(CardBoonRules.IsEligible(CardBoonKinds.Versatile, entry));
	}

	[Fact]
	public void Uniform_first_roll_can_select_each_boon_type()
	{
		var loadout = new LoadoutDefinition
		{
			cards = [new LoadoutCardEntry { entryId = "attack", cardKey = "excavate|Black" }],
		};
		var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		for (int seed = 0; seed < 500 && seen.Count < CardBoonKinds.All.Length; seed++)
		{
			seen.Add(ClimbShopService.RollBoon(loadout, new Random(seed)).Type);
		}

		Assert.Equal(CardBoonKinds.All.OrderBy(type => type), seen.OrderBy(type => type));
	}

	[Fact]
	public void Roll_falls_through_until_the_only_applicable_boon()
	{
		var loadout = new LoadoutDefinition
		{
			cards =
			[
				new LoadoutCardEntry
				{
					entryId = "grace",
					cardKey = "abounding_grace|White",
					secondaryColor = "Red",
				},
			],
		};

		for (int seed = 0; seed < 20; seed++)
		{
			var result = ClimbShopService.RollBoon(loadout, new Random(seed));
			Assert.Equal(CardBoonKinds.Guarded, result.Type);
		}
	}
}
