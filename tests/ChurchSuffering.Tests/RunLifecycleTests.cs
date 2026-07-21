using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Services;
using Xunit;

namespace ChurchSuffering.Tests;

public class RunLifecycleTests
{
	[Fact]
	public void Ending_run_persists_inactive_state_until_next_run_starts()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.StartNewRun();
		Assert.True(SaveCache.IsRunActive());
		Assert.Equal(ClimbRuleService.ShopSlotCount, SaveCache.GetClimbState().shopSlots.Count);

		RunLifecycleService.EndCurrentRun();
		Assert.False(SaveCache.IsRunActive());

		SaveCache.Reload();
		Assert.False(SaveCache.IsRunActive());

		SaveCache.StartNewRun();
		Assert.True(SaveCache.IsRunActive());
		Assert.Equal(ClimbRuleService.ShopSlotCount, SaveCache.GetClimbState().shopSlots.Count);
	}
}
