using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Services;
using Xunit;

namespace Crusaders30XX.Tests;

public class AudioSettingsSaveTests
{
	[Fact]
	public void New_save_defaults_audio_levels_to_neutral()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		Assert.Equal(SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL, SaveCache.GetMusicVolumeLevel());
		Assert.Equal(SaveFile.DEFAULT_AUDIO_VOLUME_LEVEL, SaveCache.GetSfxVolumeLevel());
		Assert.True(SaveCache.GetRumbleEnabled());
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
	public void Rumble_setting_persists_and_survives_run_lifecycle_resets()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.SetRumbleEnabled(false);
		SaveCache.Reload();
		Assert.False(SaveCache.GetRumbleEnabled());

		SaveCache.StartNewRun();
		Assert.False(SaveCache.GetRumbleEnabled());

		RunLifecycleService.EndCurrentRun();
		Assert.False(SaveCache.GetRumbleEnabled());
	}
}
