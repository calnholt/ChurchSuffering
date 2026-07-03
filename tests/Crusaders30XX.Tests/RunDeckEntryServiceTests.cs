using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class RunDeckEntryServiceTests
{
	[Fact]
	public void Duplicate_keys_create_distinct_entities_and_reload_independent_restrictions()
	{
		EventManager.Clear();
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var first = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.First();
			var second = SaveCache.AddRunDeckEntry(
				RunDeckService.PrimaryLoadoutId,
				first.cardKey,
				publishChange: false);
			Assert.NotNull(second);
			Assert.True(SaveCache.AddRunDeckEntryRestriction(
				RunDeckService.PrimaryLoadoutId,
				first.entryId,
				RunScopedStateService.RestrictionFrozen));

			SaveCache.Reload();
			var entityManager = new EntityManager();
			RunDeckService.EnsureRunDeck(entityManager);
			var duplicates = entityManager.GetEntitiesWithComponent<RunDeckCard>()
				.Where(entity => entity.IsActive
					&& entity.GetComponent<RunDeckCard>().CardKey == first.cardKey)
				.ToList();

			Assert.Equal(2, duplicates.Count);
			Assert.Equal(2, duplicates.Select(entity => entity.GetComponent<RunDeckCard>().EntryId).Distinct().Count());
			Assert.True(duplicates.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == first.entryId).HasComponent<Frozen>());
			Assert.False(duplicates.Single(entity => entity.GetComponent<RunDeckCard>().EntryId == second.entryId).HasComponent<Frozen>());
		}
		finally
		{
			EventManager.Clear();
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Upgrade_preserves_entry_identity_provenance_and_restrictions()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var original = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId).cards.First();
			Assert.True(SaveCache.AddRunDeckEntryRestriction(
				RunDeckService.PrimaryLoadoutId,
				original.entryId,
				RunScopedStateService.RestrictionBrittle));
			string upgradedKey = RunDeckService.BuildUpgradedCardKey(original.cardKey);

			Assert.True(SaveCache.TryUpgradeRunDeckEntry(
				RunDeckService.PrimaryLoadoutId,
				original.entryId,
				upgradedKey,
				out var upgraded));

			Assert.Equal(original.entryId, upgraded.entryId);
			Assert.Equal(original.isStarter, upgraded.isStarter);
			Assert.Equal(original.countsAsTraded, upgraded.countsAsTraded);
			Assert.Contains(RunScopedStateService.RestrictionBrittle, upgraded.restrictions);
		}
		finally
		{
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}

	[Fact]
	public void Replacement_keeps_position_but_allocates_unrestricted_traded_entry()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		try
		{
			SaveCache.StartNewRun();
			var before = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			var outgoing = before.cards[1];
			Assert.True(SaveCache.AddRunDeckEntryRestriction(
				RunDeckService.PrimaryLoadoutId,
				outgoing.entryId,
				RunScopedStateService.RestrictionColorless));

			Assert.True(SaveCache.TryReplaceRunDeckEntry(
				RunDeckService.PrimaryLoadoutId,
				outgoing.entryId,
				"strike|Red",
				out var replacement));

			var after = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			Assert.Equal(replacement.entryId, after.cards[1].entryId);
			Assert.NotEqual(outgoing.entryId, replacement.entryId);
			Assert.Equal("strike|Red", replacement.cardKey);
			Assert.False(replacement.isStarter);
			Assert.True(replacement.countsAsTraded);
			Assert.Empty(replacement.restrictions);
			Assert.Null(SaveCache.GetRunDeckEntry(RunDeckService.PrimaryLoadoutId, outgoing.entryId));
		}
		finally
		{
			SaveCache.DeleteSaveFilesIfPresent();
		}
	}
}
