using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Dialog;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.Diagnostics;
using Xunit;

namespace Crusaders30XX.Tests;

public class TitleMenuResumeRoutingTests
{
	[Fact]
	public void Fresh_profile_is_inactive_and_routes_to_guided_tutorial()
	{
		TutorialLaunchOptions.ConfigureFromArgs([]);
		SaveCache.DeleteSaveFilesIfPresent();
		_ = SaveCache.GetAll();

		Assert.False(SaveCache.IsRunActive());
		Assert.Null(TitleMenuResumeService.ResolveDirectTransitionScene());
	}

	[Fact]
	public void Active_run_routes_to_climb()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();

		Assert.True(SaveCache.IsRunActive());
		Assert.Equal(SceneId.Climb, TitleMenuResumeService.ResolveDirectTransitionScene());
	}

	[Fact]
	public void Completing_guided_tutorial_keeps_profile_inactive_and_routes_to_WayStation()
	{
		TutorialLaunchOptions.ConfigureFromArgs([]);
		SaveCache.DeleteSaveFilesIfPresent();

		SaveCache.CompleteGuidedTutorial();

		Assert.True(SaveCache.IsGuidedTutorialCompleted());
		Assert.False(SaveCache.IsRunActive());
		Assert.Equal(SceneId.WayStation, TitleMenuResumeService.ResolveDirectTransitionScene());
	}

	[Fact]
	public void Skip_tutorials_persists_completion_and_covered_keys()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		TutorialLaunchOptions.ConfigureFromArgs(["skip-tutorials"]);
		try
		{
			TitleMenuResumeService.PersistSkipTutorialsIfRequested();

			SaveCache.Reload();
			Assert.True(SaveCache.IsGuidedTutorialCompleted());
			Assert.False(SaveCache.IsRunActive());
			Assert.Contains("teach_pledge", SaveCache.GetAll().seenTutorials);
			Assert.Contains("guided_tutorial", SaveCache.GetAll().seenTutorials);
			Assert.True(SaveCache.HasSeenWayStationDialogueSegment(
				WayStationDialogueCatalog.KeeperCharacterId,
				WayStationDialogueCatalog.KeeperIntroSegmentId));
			Assert.Null(WayStationDialoguePlanner.TryGetAutoDialogue(SaveCache.GetWayStationMeta()));

			TutorialLaunchOptions.ConfigureFromArgs([]);
			Assert.Equal(SceneId.WayStation, TitleMenuResumeService.ResolveDirectTransitionScene());
			Assert.Null(WayStationDialoguePlanner.TryGetAutoDialogue(SaveCache.GetWayStationMeta()));
		}
		finally
		{
			TutorialLaunchOptions.ConfigureFromArgs([]);
		}
	}

	[Fact]
	public void Inactive_run_routes_to_WayStation_even_when_climb_state_existed()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();
		SaveCache.MarkRunInactive();

		Assert.False(SaveCache.IsRunActive());
		Assert.Equal(SceneId.WayStation, TitleMenuResumeService.ResolveDirectTransitionScene());
	}
}
