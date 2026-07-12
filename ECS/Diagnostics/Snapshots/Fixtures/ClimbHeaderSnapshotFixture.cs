using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class ClimbHeaderSnapshotFixture : IDisplaySnapshotFixture
	{
		private ClimbHeaderDisplaySystem _header;
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
			var columns = ctx.World.GetSystem<ClimbColumnDisplaySystem>();
			if (columns != null)
			{
				columns.PortraitParallaxMultiplierX = 0f;
				columns.PortraitParallaxMultiplierY = 0f;
			}

			_header = ctx.World.GetSystem<ClimbHeaderDisplaySystem>();
			if (_header == null)
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
				_header.SetResourcePreviewForSnapshot(new ClimbResourceSave { red = 14, white = 5, black = 103 }, 1f);
			}
			else if (_variant == "pulse")
			{
				_header.SetResourcePulseForSnapshot(new ClimbResourceSave { red = 1, black = 1 }, 0.5f);
			}
			else if (_variant == "overview-hover")
			{
				var ui = ctx.World.EntityManager
					.GetEntity(ClimbHeaderLayoutSystem.LoadoutButtonName)
					?.GetComponent<UIElement>();
				if (ui != null) ui.IsHovered = true;
			}

			_header.Draw();
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
