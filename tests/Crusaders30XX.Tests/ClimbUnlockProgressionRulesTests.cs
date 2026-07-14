using System.Linq;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Xunit;

namespace Crusaders30XX.Tests;

public class ClimbUnlockProgressionRulesTests
{
	[Fact]
	public void Unlocks_follow_completed_climb_progression()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		var meta = SaveCache.GetWayStationMeta();

		Assert.False(ClimbUnlockProgressionRules.ShouldShowSettingsModal(meta));
		AssertUnlocked(meta, StartingWeapon.Sword, RunDifficulty.Easy);
		AssertLocked(meta, StartingWeapon.Dagger, RunDifficulty.Easy);
		AssertLocked(meta, StartingWeapon.Hammer, RunDifficulty.Easy);

		Complete(StartingWeapon.Sword, RunDifficulty.Easy);
		meta = SaveCache.GetWayStationMeta();
		Assert.True(ClimbUnlockProgressionRules.ShouldShowSettingsModal(meta));
		AssertUnlocked(meta, StartingWeapon.Dagger, RunDifficulty.Easy);
		AssertUnlocked(meta, StartingWeapon.Sword, RunDifficulty.Normal);
		AssertLocked(meta, StartingWeapon.Sword, RunDifficulty.Hard);
		AssertLocked(meta, StartingWeapon.Dagger, RunDifficulty.Normal);

		Complete(StartingWeapon.Sword, RunDifficulty.Normal);
		AssertUnlocked(SaveCache.GetWayStationMeta(), StartingWeapon.Sword, RunDifficulty.Hard);

		Complete(StartingWeapon.Dagger, RunDifficulty.Easy);
		meta = SaveCache.GetWayStationMeta();
		AssertUnlocked(meta, StartingWeapon.Hammer, RunDifficulty.Easy);
		AssertUnlocked(meta, StartingWeapon.Dagger, RunDifficulty.Normal);
		AssertLocked(meta, StartingWeapon.Dagger, RunDifficulty.Hard);
		AssertLocked(meta, StartingWeapon.Hammer, RunDifficulty.Normal);

		Complete(StartingWeapon.Dagger, RunDifficulty.Normal);
		AssertUnlocked(SaveCache.GetWayStationMeta(), StartingWeapon.Dagger, RunDifficulty.Hard);

		Complete(StartingWeapon.Hammer, RunDifficulty.Easy);
		AssertUnlocked(SaveCache.GetWayStationMeta(), StartingWeapon.Hammer, RunDifficulty.Normal);
		AssertLocked(SaveCache.GetWayStationMeta(), StartingWeapon.Hammer, RunDifficulty.Hard);

		Complete(StartingWeapon.Hammer, RunDifficulty.Normal);
		AssertUnlocked(SaveCache.GetWayStationMeta(), StartingWeapon.Hammer, RunDifficulty.Hard);
	}

	[Fact]
	public void Completed_configurations_are_unique_and_survive_run_lifecycle_and_reload()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		Complete(StartingWeapon.Dagger, RunDifficulty.Easy);
		Complete(StartingWeapon.Dagger, RunDifficulty.Easy);

		Assert.Single(SaveCache.GetWayStationMeta().completedClimbs);

		SaveCache.StartWayStationClimbAttempt();
		SaveCache.MarkRunInactive();
		SaveCache.Reload();

		var meta = SaveCache.GetWayStationMeta();
		Assert.Single(meta.completedClimbs);
		Assert.Equal("dagger", meta.completedClimbs.Single().startingWeaponId);
		Assert.Equal(RunDifficulty.Easy, meta.completedClimbs.Single().difficulty);
		AssertUnlocked(meta, StartingWeapon.Hammer, RunDifficulty.Easy);
	}

	private static void Complete(StartingWeapon weapon, RunDifficulty difficulty)
	{
		SaveCache.StartWayStationClimbAttempt();
		string weaponId = weapon.ToString().ToLowerInvariant();
		SaveCache.ConfigurePrimaryRunSetup(
			weaponId,
			StartingDeckGeneratorService.GetDefaultTemperanceId(weapon),
			difficulty);
		SaveCache.RecordWayStationClimbCompletion();
	}

	private static void AssertUnlocked(WayStationMetaSave meta, StartingWeapon weapon, RunDifficulty difficulty)
	{
		Assert.True(ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, weapon));
		Assert.True(ClimbUnlockProgressionRules.IsDifficultyUnlocked(meta, weapon, difficulty));
	}

	private static void AssertLocked(WayStationMetaSave meta, StartingWeapon weapon, RunDifficulty difficulty)
	{
		Assert.False(ClimbUnlockProgressionRules.IsDifficultyUnlocked(meta, weapon, difficulty));
	}
}
