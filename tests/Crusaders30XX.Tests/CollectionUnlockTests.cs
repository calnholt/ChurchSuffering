using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class CollectionUnlockTests : IDisposable
{
	public CollectionUnlockTests()
	{
		SaveCache.DeleteSaveFilesIfPresent();
	}

	public void Dispose()
	{
		SaveCache.DeleteSaveFilesIfPresent();
	}

	[Fact]
	public void UnlockAllCollectionItems_adds_every_collectible_and_preserves_progress()
	{
		var collection = SaveCache.GetCollection();
		collection.cardIds = new List<string> { "strike" };
		collection.medalIds = new List<string> { "st_luke" };
		collection.equipmentIds = new List<string> { "scarlet_vest" };
		collection.totalPoints = 37;
		collection.pendingClimbPoints = 4;
		collection.processedRewardLevels = 1;
		SaveCache.SaveCollection(collection);

		SaveCache.UnlockAllCollectionItems();
		SaveCache.Reload();

		var unlocked = SaveCache.GetCollection();
		Assert.Equal(
			CardFactory.GetAllCards()
				.Where(entry => entry.Value != null && entry.Value.CanAddToLoadout && !entry.Value.IsWeapon && !entry.Value.IsToken)
				.Select(entry => entry.Key.ToKey())
				.OrderBy(id => id, System.StringComparer.OrdinalIgnoreCase),
			unlocked.cardIds.OrderBy(id => id, System.StringComparer.OrdinalIgnoreCase));
		Assert.Equal(
			MedalFactory.GetAllMedals().Keys.Select(id => id.ToKey()).OrderBy(id => id, System.StringComparer.OrdinalIgnoreCase),
			unlocked.medalIds.OrderBy(id => id, System.StringComparer.OrdinalIgnoreCase));
		Assert.Equal(
			EquipmentFactory.GetAllEquipment().Keys.Select(id => id.ToKey()).OrderBy(id => id, System.StringComparer.OrdinalIgnoreCase),
			unlocked.equipmentIds.OrderBy(id => id, System.StringComparer.OrdinalIgnoreCase));
		Assert.Equal(37, unlocked.totalPoints);
		Assert.Equal(4, unlocked.pendingClimbPoints);
		Assert.Equal(1, unlocked.processedRewardLevels);

		SaveCache.UnlockAllCollectionItems();
		var unlockedAgain = SaveCache.GetCollection();
		Assert.Equal(unlocked.cardIds.Count, unlockedAgain.cardIds.Count);
		Assert.Equal(unlocked.medalIds.Count, unlockedAgain.medalIds.Count);
		Assert.Equal(unlocked.equipmentIds.Count, unlockedAgain.equipmentIds.Count);
	}
}
