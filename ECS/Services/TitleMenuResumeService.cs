using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.Diagnostics;

namespace Crusaders30XX.ECS.Services
{
	public static class TitleMenuResumeService
	{
		public static SceneId? ResolveDirectTransitionScene()
		{
			ApplySkipTutorialsOption();
			if (!SaveCache.IsGuidedTutorialCompleted()) return null;
			if (!SaveCache.IsRunActive()) return SceneId.WayStation;
			if (SaveCache.TryGetResumableBattleNode(out _)) return null;
			return SceneId.Climb;
		}

		public static void OnTitleMenuClicked(World world)
		{
			ApplySkipTutorialsOption();
			if (!SaveCache.IsGuidedTutorialCompleted())
			{
				GuidedTutorialService.Start(world);
				return;
			}

			if (!SaveCache.IsRunActive())
			{
				EventManager.Publish(new ShowTransition { Scene = SceneId.WayStation, SkipHold = true });
				return;
			}

			if (SaveCache.TryGetResumableBattleNode(out var nodeId))
			{
				WayStationRunSetupService.BeginBattleFromNodeId(world, nodeId);
				return;
			}

			EventManager.Publish(new ShowTransition { Scene = SceneId.Climb, SkipHold = true });
		}

		private static void ApplySkipTutorialsOption()
		{
			if (TutorialLaunchOptions.SkipTutorials && !SaveCache.IsGuidedTutorialCompleted())
			{
				SaveCache.CompleteGuidedTutorial();
			}
		}
	}
}
