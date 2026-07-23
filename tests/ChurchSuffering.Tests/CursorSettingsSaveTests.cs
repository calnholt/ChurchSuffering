using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public class CursorSettingsSaveTests
{
	[Fact]
	public void New_save_defaults_cursor_speed_levels_to_neutral()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		Assert.Equal(SaveFile.DEFAULT_CURSOR_SPEED_LEVEL, SaveCache.GetCursorSpeedLevel());
		Assert.Equal(SaveFile.DEFAULT_CURSOR_SPEED_LEVEL, SaveCache.GetCursorFastSpeedLevel());
		Assert.Equal(0, SaveCache.GetCursorSpeedLevel());
		Assert.Equal(0, SaveCache.GetCursorFastSpeedLevel());
	}

	[Fact]
	public void Cursor_speed_level_setters_clamp_to_valid_range()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.SetCursorSpeedLevel(-80);
		SaveCache.SetCursorFastSpeedLevel(125);

		Assert.Equal(SaveFile.MIN_CURSOR_SPEED_LEVEL, SaveCache.GetCursorSpeedLevel());
		Assert.Equal(SaveFile.MAX_CURSOR_SPEED_LEVEL, SaveCache.GetCursorFastSpeedLevel());
	}

	[Fact]
	public void Cursor_speed_levels_persist_after_reload()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.SetCursorSpeedLevel(-25);
		SaveCache.SetCursorFastSpeedLevel(35);
		SaveCache.Reload();

		Assert.Equal(-25, SaveCache.GetCursorSpeedLevel());
		Assert.Equal(35, SaveCache.GetCursorFastSpeedLevel());
	}

	[Fact]
	public void Cursor_speed_levels_survive_run_lifecycle_resets()
	{
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.SetCursorSpeedLevel(-20);
		SaveCache.SetCursorFastSpeedLevel(40);

		SaveCache.StartNewRun();
		Assert.Equal(-20, SaveCache.GetCursorSpeedLevel());
		Assert.Equal(40, SaveCache.GetCursorFastSpeedLevel());

		RunLifecycleService.EndCurrentRun();
		Assert.Equal(-20, SaveCache.GetCursorSpeedLevel());
		Assert.Equal(40, SaveCache.GetCursorFastSpeedLevel());
	}

	[Theory]
	[InlineData(-50, 0.5f)]
	[InlineData(0, 1.0f)]
	[InlineData(50, 1.5f)]
	public void Cursor_speed_scale_maps_level_to_half_through_one_and_half(int level, float expectedScale)
	{
		Assert.Equal(expectedScale, SaveCache.CursorSpeedScaleFromLevel(level));
	}
}
