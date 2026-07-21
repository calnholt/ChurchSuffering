using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.ECS.Data.Ids;
using ChurchSuffering.ECS.Data.Loadouts;
using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Objects.Cards;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class ReparationServiceTests
{
	private static readonly HashSet<string> AllowedRestrictions = new(StringComparer.Ordinal)
	{
		RunScopedStateService.RestrictionThorned,
		RunScopedStateService.RestrictionScorched,
		RunScopedStateService.RestrictionCursed,
		RunScopedStateService.RestrictionFrozen,
		RunScopedStateService.RestrictionBrittle,
	};

	[Fact]
	public void Reparation_is_deterministic_and_obeys_candidate_rules()
	{
		var collection = new PlayerCollectionSave
		{
			cardIds = CardFactory.GetAllCards().Keys.Select(id => id.ToKey()).ToList(),
		};
		var first = StartingDeckGeneratorService.BuildStartingLoadout("sword", 7721, "first");
		var second = StartingDeckGeneratorService.BuildStartingLoadout("sword", 7721, "first");
		var originalIds = first.cards
			.Select(entry => DeckRules.ParseBaseCardId(entry.cardKey))
			.ToHashSet(StringComparer.OrdinalIgnoreCase);

		int firstCount = ReparationService.Apply(first, "sword", 7721, 24, collection, 8);
		int secondCount = ReparationService.Apply(second, "sword", 7721, 24, collection, 8);

		Assert.Equal(8, firstCount);
		Assert.Equal(8, secondCount);
		Assert.Equal(
			first.cards.Select(Describe),
			second.cards.Select(Describe));

		var replacements = first.cards.Where(entry => !entry.isStarter).ToList();
		Assert.Equal(8, replacements.Count);
		Assert.All(replacements, entry =>
		{
			string cardId = DeckRules.ParseBaseCardId(entry.cardKey);
			var card = CardFactory.Create(cardId);
			Assert.DoesNotContain(cardId, originalIds);
			Assert.NotNull(card);
			Assert.True(card.CanAddToLoadout);
			Assert.True(card.IsEligibleForWeapon("sword"));
			Assert.False(card.IsWeapon);
			Assert.False(card.IsToken);
			Assert.NotEqual(Rarity.Starter, card.Rarity);
			Assert.Single(entry.restrictions);
			Assert.Contains(entry.restrictions[0], AllowedRestrictions);
		});
		Assert.All(
			first.cards.GroupBy(entry => DeckRules.ParseBaseCardId(entry.cardKey), StringComparer.OrdinalIgnoreCase),
			group => Assert.True(group.Count() <= DeckRules.MaxCopiesPerCardId));
	}

	[Fact]
	public void Fresh_collection_supports_all_eight_reparation_stacks()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			var loadout = StartingDeckGeneratorService.BuildStartingLoadout("sword", 30030, "fresh");
			int applied = ReparationService.Apply(loadout, "sword", 30030, 24, SaveCache.GetCollection(), 8);
			Assert.Equal(8, applied);
			Assert.Equal(8, loadout.cards.Count(entry => !entry.isStarter));
		}
		finally
		{
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	private static string Describe(ChurchSuffering.ECS.Data.Loadouts.LoadoutCardEntry entry)
	{
		return $"{entry.entryId}|{entry.cardKey}|{string.Join(',', entry.restrictions)}";
	}
}
