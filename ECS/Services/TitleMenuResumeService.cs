using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Dialog;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.Diagnostics;

namespace ChurchSuffering.ECS.Services
{
	public static class TitleMenuResumeService
	{
		public static SceneId? ResolveDirectTransitionScene()
		{
			ApplySkipTutorialsOption();
			if (!SaveCache.IsGuidedTutorialCompleted()) return null;
			if (!SaveCache.IsRunActive()) return SceneId.WayStation;
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

			EventManager.Publish(new ShowTransition { Scene = SceneId.Climb, SkipHold = true });
		}

		/// <summary>
		/// When <c>skip-tutorials</c> is set, persist guided-tutorial completion and Keeper intro
		/// so a later launch without the flag skips both. Idempotent.
		/// </summary>
		public static void PersistSkipTutorialsIfRequested()
		{
			if (!TutorialLaunchOptions.SkipTutorials) return;

			if (!SaveCache.IsGuidedTutorialCompleted())
			{
				SaveCache.CompleteGuidedTutorial();
			}

			if (!SaveCache.HasSeenWayStationDialogueSegment(
				WayStationDialogueCatalog.KeeperCharacterId,
				WayStationDialogueCatalog.KeeperIntroSegmentId))
			{
				SaveCache.MarkWayStationDialogueSegmentSeen(
					WayStationDialogueCatalog.KeeperCharacterId,
					WayStationDialogueCatalog.KeeperIntroSegmentId);
			}
		}

		private static void ApplySkipTutorialsOption()
		{
			PersistSkipTutorialsIfRequested();
		}
	}
}
