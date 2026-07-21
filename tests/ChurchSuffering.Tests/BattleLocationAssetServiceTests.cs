using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public class BattleLocationAssetServiceTests
{
	[Fact]
	public void Maps_battle_locations_to_background_assets()
	{
		Assert.Equal("Battle_Backgrounds/desert-battle-background", BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Desert));
		Assert.Equal("Battle_Backgrounds/tundra-battle-background", BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Tundra));
		Assert.Equal("Battle_Backgrounds/jungle-battle-background", BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Jungle));
		Assert.Equal("Battle_Backgrounds/volcano-battle-background", BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Volcano));
		Assert.Equal("Battle_Backgrounds/the-gate-battle-background", BattleLocationAssetService.GetBackgroundAsset(BattleLocation.TheGate));
		Assert.Equal("Battle_Backgrounds/gothic-battle-background", BattleLocationAssetService.GetBackgroundAsset(BattleLocation.Gothic));
	}

	[Fact]
	public void Maps_climb_locations_to_location_background_assets()
	{
		Assert.Equal("desert_background_location", BattleLocationAssetService.GetClimbBackgroundAsset(BattleLocation.Desert));
		Assert.Equal("tundra_background_location", BattleLocationAssetService.GetClimbBackgroundAsset(BattleLocation.Tundra));
		Assert.Equal("jungle_background_location", BattleLocationAssetService.GetClimbBackgroundAsset(BattleLocation.Jungle));
		Assert.Equal("volcano_background_location", BattleLocationAssetService.GetClimbBackgroundAsset(BattleLocation.Volcano));
		Assert.Equal("gothic_background_location", BattleLocationAssetService.GetClimbBackgroundAsset(BattleLocation.Gothic));
		Assert.Equal("desert_background_location", BattleLocationAssetService.GetClimbBackgroundAsset(BattleLocation.TheGate));
	}

	[Fact]
	public void Maps_battle_locations_to_music_tracks()
	{
		Assert.Equal(MusicTrack.DesertBattle, BattleLocationAssetService.GetMusicTrack(BattleLocation.Desert));
		Assert.Equal(MusicTrack.TundraBattle, BattleLocationAssetService.GetMusicTrack(BattleLocation.Tundra));
		Assert.Equal(MusicTrack.JungleBattle, BattleLocationAssetService.GetMusicTrack(BattleLocation.Jungle));
		Assert.Equal(MusicTrack.VolcanoBattle, BattleLocationAssetService.GetMusicTrack(BattleLocation.Volcano));
		Assert.Equal(MusicTrack.TheGateBattle, BattleLocationAssetService.GetMusicTrack(BattleLocation.TheGate));
		Assert.Equal(MusicTrack.GothicBattle, BattleLocationAssetService.GetMusicTrack(BattleLocation.Gothic));
	}

	[Fact]
	public void Rollable_climb_locations_exclude_the_gate()
	{
		Assert.Contains(BattleLocation.Desert, BattleLocationAssetService.ClimbEncounterLocations);
		Assert.Contains(BattleLocation.Tundra, BattleLocationAssetService.ClimbEncounterLocations);
		Assert.Contains(BattleLocation.Jungle, BattleLocationAssetService.ClimbEncounterLocations);
		Assert.Contains(BattleLocation.Volcano, BattleLocationAssetService.ClimbEncounterLocations);
		Assert.Contains(BattleLocation.Gothic, BattleLocationAssetService.ClimbEncounterLocations);
		Assert.DoesNotContain(BattleLocation.TheGate, BattleLocationAssetService.ClimbEncounterLocations);
		Assert.Equal(BattleLocation.TheGate, BattleLocationAssetService.FinalEncounterLocation);
	}
}
