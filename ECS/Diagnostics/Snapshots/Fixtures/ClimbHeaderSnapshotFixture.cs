using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class ClimbHeaderSnapshotFixture : IDisplaySnapshotFixture
	{
		private ClimbV2TitleDisplaySystem _title;
		private DistanceClimbedTimelineDisplaySystem _timeline;
		private PlayerResourcesDisplaySystem _resources;
		private ClimbOverviewButtonDisplaySystem _overview;
		private string _variant = "normal";

		public string Id => "climb-header";
		public int WarmupFrames => 10;
		public string OutputFileName => _variant;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args);
			ConfigureSave();
			ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.Climb;
			EventManager.Publish(new LoadSceneEvent
			{
				Scene = SceneId.Climb,
				PreviousScene = SceneId.Snapshot,
			});

			ctx.World.GetSystem<ParallaxLayerSystem>()?.SetActive(false);
			_title = ctx.World.GetSystem<ClimbV2TitleDisplaySystem>();
			_timeline = ctx.World.GetSystem<DistanceClimbedTimelineDisplaySystem>();
			_resources = ctx.World.GetSystem<PlayerResourcesDisplaySystem>();
			_overview = ctx.World.GetSystem<ClimbOverviewButtonDisplaySystem>();
			if (_title == null || _timeline == null || _resources == null || _overview == null)
			{
				throw new DisplaySnapshotSetupException("Climb header systems were not registered.");
			}
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			var climb = SaveCache.GetClimbState();
			climb.resources = new ClimbResourceSave { red = 12, white = 7, black = 103 };
			SaveCache.SaveClimbState(climb);

			if (_variant == "preview-delta")
			{
				_resources.SetResourcePreviewForSnapshot(new ClimbResourceSave { red = 14, white = 5, black = 103 }, 1f);
			}
			else if (_variant == "pulse")
			{
				_resources.SetResourcePulseForSnapshot(new ClimbResourceSave { red = 1, black = 1 }, 0.5f);
			}
			else if (_variant == "overview-hover")
			{
				var ui = ctx.World.EntityManager
					.GetEntity(ClimbV2LayoutSystem.OverviewName)
					?.GetComponent<UIElement>();
				if (ui != null) ui.IsHovered = true;
			}

			_title.Draw();
			_timeline.Draw();
			_resources.Draw();
			_overview.Draw();
		}

		private static void ConfigureSave()
		{
			SaveCache.StartNewRun();
			var climb = new ClimbSaveState
			{
				time = 5,
				resources = new ClimbResourceSave { red = 12, white = 7, black = 103 },
				shopSlots = new List<ClimbShopSlotSave>(),
				encounterSlots = new List<ClimbEncounterSlotSave>(),
				eventSlots = new List<ClimbEventSlotSave>(),
			};
			SaveCache.SaveClimbState(climb);
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 1 && args[0] is "normal" or "preview-delta" or "pulse" or "overview-hover")
			{
				return args[0];
			}

			throw new DisplaySnapshotSetupException(
				"climb-header expects one variant: normal, preview-delta, pulse, or overview-hover");
		}
	}
}
