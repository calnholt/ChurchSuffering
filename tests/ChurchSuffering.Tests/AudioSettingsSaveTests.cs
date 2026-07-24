using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public class AudioSettingsSaveTests
{
	[Fact]
	public void New_save_defaults_audio_levels_to_neutral()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		Assert.Equal(SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL, SaveCache.GetMusicVolumeLevel());
		Assert.Equal(SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL, SaveCache.GetSfxVolumeLevel());
		Assert.Equal(SaveFile.DEFAULT_RUMBLE_LEVEL, SaveCache.GetRumbleLevel());
	}

	[Fact]
	public void Audio_level_setters_clamp_to_valid_range()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.SetMusicVolumeLevel(-10);
		SaveCache.SetSfxVolumeLevel(125);

		Assert.Equal(0, SaveCache.GetMusicVolumeLevel());
		Assert.Equal(100, SaveCache.GetSfxVolumeLevel());
	}

	[Fact]
	public void Audio_levels_persist_after_reload()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.SetMusicVolumeLevel(35);
		SaveCache.SetSfxVolumeLevel(80);
		SaveCache.Reload();

		Assert.Equal(35, SaveCache.GetMusicVolumeLevel());
		Assert.Equal(80, SaveCache.GetSfxVolumeLevel());
	}

	[Fact]
	public void Audio_levels_survive_run_lifecycle_resets()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.SetMusicVolumeLevel(25);
		SaveCache.SetSfxVolumeLevel(75);

		SaveCache.StartNewRun();
		Assert.Equal(25, SaveCache.GetMusicVolumeLevel());
		Assert.Equal(75, SaveCache.GetSfxVolumeLevel());

		RunLifecycleService.EndCurrentRun();
		Assert.Equal(25, SaveCache.GetMusicVolumeLevel());
		Assert.Equal(75, SaveCache.GetSfxVolumeLevel());
	}

	[Fact]
	public void Rumble_level_persists_and_survives_run_lifecycle_resets()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.SetRumbleLevel(0);
		SaveCache.Reload();
		Assert.Equal(0, SaveCache.GetRumbleLevel());

		SaveCache.StartNewRun();
		Assert.Equal(0, SaveCache.GetRumbleLevel());

		RunLifecycleService.EndCurrentRun();
		Assert.Equal(0, SaveCache.GetRumbleLevel());
	}

	[Fact]
	public void Rumble_level_setters_clamp_to_valid_range()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.SetRumbleLevel(-10);
		Assert.Equal(0, SaveCache.GetRumbleLevel());

		SaveCache.SetRumbleLevel(150);
		Assert.Equal(100, SaveCache.GetRumbleLevel());
	}
}
