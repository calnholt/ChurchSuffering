using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class RunSetupUnlockTests : IDisposable
{
	public RunSetupUnlockTests()
	{
		SaveCache.DeleteSaveFilesIfPresent();
	}

	public void Dispose()
	{
		SaveCache.DeleteSaveFilesIfPresent();
	}

	[Fact]
	public void UnlockAllRunSetupOptions_unlocks_every_choice_and_preserves_other_progress()
	{
		var collection = SaveCache.GetCollection();
		collection.cardIds = new List<string> { "strike" };
		collection.medalIds = new List<string> { "st_luke" };
		collection.equipmentIds = new List<string> { "scarlet_vest" };
		collection.totalPoints = 37;
		SaveCache.SaveCollection(collection);

		var save = SaveCache.GetAll();
		save.isRunActive = true;
		save.gold = 12;
		save.lastLocation = "waystation";
		save.waystation.climbAttempts = 4;
		save.waystation.deferredNpcDialogueCounter = 2;
		save.waystation.purchasedMedalIds = new List<string> { "st_luke" };
		SaveCache.SaveCollection(collection);

		SaveCache.UnlockAllRunSetupOptions();
		SaveCache.Reload();

		var meta = SaveCache.GetWayStationMeta();
		foreach (var weapon in Enum.GetValues<StartingWeapon>())
		{
			Assert.True(ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon));
			foreach (var difficulty in Enum.GetValues<RunDifficulty>())
			{
				Assert.True(ClimbUnlockProgressionRules.IsDifficultyUnlocked(meta, weapon, difficulty));
			}
		}

		var preservedCollection = SaveCache.GetCollection();
		Assert.Equal(collection.cardIds, preservedCollection.cardIds);
		Assert.Equal(collection.medalIds, preservedCollection.medalIds);
		Assert.Equal(collection.equipmentIds, preservedCollection.equipmentIds);
		Assert.Equal(37, preservedCollection.totalPoints);
		Assert.Equal(4, meta.climbAttempts);
		Assert.Equal(2, meta.deferredNpcDialogueCounter);
		Assert.Equal(new[] { "st_luke" }, meta.purchasedMedalIds);
		Assert.True(SaveCache.GetAll().isRunActive);
		Assert.Equal(12, SaveCache.GetAll().gold);
		Assert.Equal("waystation", SaveCache.GetAll().lastLocation);
	}

	[Fact]
	public void UnlockAllRunSetupOptions_is_idempotent()
	{
		SaveCache.UnlockAllRunSetupOptions();
		var first = SaveCache.GetWayStationMeta();

		SaveCache.UnlockAllRunSetupOptions();
		var second = SaveCache.GetWayStationMeta();

		Assert.Equal(first.climbCompletions, second.climbCompletions);
		Assert.Equal(
			first.completedClimbs.Select(CompletionKey).OrderBy(key => key),
			second.completedClimbs.Select(CompletionKey).OrderBy(key => key));
	}

	private static string CompletionKey(CompletedClimbSave completion)
	{
		return $"{completion.startingWeaponId}|{completion.difficulty}";
	}
}
