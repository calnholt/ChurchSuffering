using System.Linq;
using Crusaders30XX.ECS.Components;
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
		Assert.False(SaveCache.IsStartQuestCompleted());
		Assert.Null(TitleMenuResumeService.ResolveDirectTransitionScene());
	}

	[Fact]
	public void Active_run_with_incomplete_start_quest_routes_to_climb()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();

		Assert.True(SaveCache.IsRunActive());
		Assert.False(SaveCache.IsStartQuestCompleted());
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
		Assert.Empty(SaveCache.GetRunMapNodes());
		Assert.Equal(SceneId.WayStation, TitleMenuResumeService.ResolveDirectTransitionScene());
	}

	[Fact]
	public void Skip_tutorials_persists_completion_and_covered_keys()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		TutorialLaunchOptions.ConfigureFromArgs(["skip-tutorials"]);
		try
		{
			Assert.Equal(SceneId.WayStation, TitleMenuResumeService.ResolveDirectTransitionScene());
			Assert.True(SaveCache.IsGuidedTutorialCompleted());
			Assert.False(SaveCache.IsRunActive());
			Assert.Contains("teach_pledge", SaveCache.GetAll().seenTutorials);
			Assert.Contains("guided_tutorial", SaveCache.GetAll().seenTutorials);
		}
		finally
		{
			TutorialLaunchOptions.ConfigureFromArgs([]);
		}
	}

	[Fact]
	public void Active_run_with_completed_start_quest_routes_to_Climb()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();
		SaveCache.SetQuestCompleted(null, SaveCache.GetStartNodeId(), true);

		Assert.True(SaveCache.IsRunActive());
		Assert.True(SaveCache.IsStartQuestCompleted());
		Assert.Equal(SceneId.Climb, TitleMenuResumeService.ResolveDirectTransitionScene());
	}

	[Fact]
	public void Inactive_run_routes_to_WayStation_even_when_start_quest_was_completed()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();
		SaveCache.SetQuestCompleted(null, SaveCache.GetStartNodeId(), true);
		SaveCache.MarkRunInactive();

		Assert.False(SaveCache.IsRunActive());
		Assert.Equal(SceneId.WayStation, TitleMenuResumeService.ResolveDirectTransitionScene());
	}

	[Fact]
	public void Active_run_with_pending_incomplete_battle_routes_to_battle_path()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();
		SaveCache.SetQuestCompleted(null, SaveCache.GetStartNodeId(), true);

		var nodes = SaveCache.GetRunMapNodes().ToList();
		var startNode = nodes[0];
		Assert.NotEmpty(startNode.childIndices);
		string childId = nodes[startNode.childIndices[0]].id;
		SaveCache.SetRunNodeRevealed(childId, true);
		SaveCache.SetPendingBattleNode(childId);

		Assert.Null(TitleMenuResumeService.ResolveDirectTransitionScene());
		Assert.True(SaveCache.TryGetResumableBattleNode(out var nodeId));
		Assert.Equal(childId, nodeId);
	}

	[Fact]
	public void TryGetResumableBattleNode_clears_stale_flag_when_node_completed()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();
		string nodeId = SaveCache.GetStartNodeId();
		SaveCache.SetPendingBattleNode(nodeId);
		SaveCache.SetQuestCompleted(null, nodeId, true);

		Assert.False(SaveCache.TryGetResumableBattleNode(out _));
		Assert.Empty(SaveCache.GetAll().pendingBattleNodeId);
	}

	[Fact]
	public void TryGetResumableBattleNode_clears_stale_flag_when_node_missing()
	{
		SaveCache.DeleteSaveFilesIfPresent();
		SaveCache.CompleteGuidedTutorial();
		SaveCache.StartNewRun();
		SaveCache.SetPendingBattleNode("nonexistent_node");

		Assert.False(SaveCache.TryGetResumableBattleNode(out _));
		Assert.Empty(SaveCache.GetAll().pendingBattleNodeId);
	}
}
