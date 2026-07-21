using ChurchSuffering.ECS.Data.RunSetup;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public class ClimbUnlockProgressionRulesTests
{
	[Fact]
	public void Progression_advances_current_level_and_unlocks_weapons_in_order()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		var meta = SaveCache.GetWayStationMeta();
		Assert.Equal(0, ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, StartingWeapon.Sword));
		Assert.False(ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, StartingWeapon.Dagger));
		Assert.False(ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, StartingWeapon.Hammer));

		Complete(StartingWeapon.Sword, 0);
		meta = SaveCache.GetWayStationMeta();
		Assert.Equal(1, ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, StartingWeapon.Sword));
		Assert.True(ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, StartingWeapon.Dagger));
		Assert.Equal(0, ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, StartingWeapon.Dagger));

		Complete(StartingWeapon.Dagger, 0);
		meta = SaveCache.GetWayStationMeta();
		Assert.Equal(1, ClimbUnlockProgressionRules.GetHighestUnlockedPenance(meta, StartingWeapon.Dagger));
		Assert.True(ClimbUnlockProgressionRules.IsWeaponUnlocked(meta, StartingWeapon.Hammer));
	}

	[Fact]
	public void Replaying_lower_penance_does_not_advance_highest()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		Complete(StartingWeapon.Sword, 0);
		Complete(StartingWeapon.Sword, 0);

		Assert.Equal(1, ClimbUnlockProgressionRules.GetHighestUnlockedPenance(
			SaveCache.GetWayStationMeta(), StartingWeapon.Sword));
	}

	[Fact]
	public void Penance_progression_caps_at_twenty_four()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.UnlockAllRunSetupOptions();
		Complete(StartingWeapon.Hammer, 24);

		Assert.Equal(24, ClimbUnlockProgressionRules.GetHighestUnlockedPenance(
			SaveCache.GetWayStationMeta(), StartingWeapon.Hammer));
	}

	[Fact]
	public void Meta_dictionary_and_active_penance_survive_reload()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		Complete(StartingWeapon.Sword, 0);
		SaveCache.StartWayStationClimbAttempt();
		SaveCache.ConfigurePrimaryRunSetup("dagger", StartingDeckGeneratorService.GetDefaultTemperanceId(StartingWeapon.Dagger), 0);

		SaveCache.Reload();

		Assert.Equal(1, SaveCache.GetWayStationMeta().highestPenanceByWeapon["sword"]);
		Assert.Equal(0, SaveCache.GetWayStationMeta().highestPenanceByWeapon["dagger"]);
		Assert.Equal("dagger", SaveCache.GetClimbState().startingWeaponId);
		Assert.Equal(0, SaveCache.GetClimbState().penanceLevel);
	}

	private static void Complete(StartingWeapon weapon, int penanceLevel)
	{
		SaveCache.StartWayStationClimbAttempt();
		SaveCache.ConfigurePrimaryRunSetup(
			PenanceRules.GetWeaponId(weapon),
			StartingDeckGeneratorService.GetDefaultTemperanceId(weapon),
			penanceLevel);
		SaveCache.RecordWayStationClimbCompletion();
	}
}
