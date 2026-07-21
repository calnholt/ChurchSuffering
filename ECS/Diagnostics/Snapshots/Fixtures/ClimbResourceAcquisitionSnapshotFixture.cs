using System.Collections.Generic;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class ClimbResourceAcquisitionSnapshotFixture : IDisplaySnapshotFixture
	{
		private readonly ClimbResourceSave _resources = new() { red = 2, white = 1, black = 1 };
		private ClimbResourceAcquisitionDisplaySystem _animation;
		private PlayerResourcesDisplaySystem _resourcesDisplay;
		private string _variant = "fall";

		public string Id => "climb-resource-acquisition";
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

			var climbState = SaveCache.GetClimbState();
			foreach (var slot in climbState?.encounterSlots ?? new List<ClimbEncounterSlotSave>())
			{
				slot.battleLocation = BattleLocation.Desert;
			}
			SaveCache.SaveClimbState(climbState);
			ctx.World.GetSystem<ParallaxLayerSystem>()?.SetActive(false);
			_animation = ctx.World.GetSystem<ClimbResourceAcquisitionDisplaySystem>();
			_resourcesDisplay = ctx.World.GetSystem<PlayerResourcesDisplaySystem>();
			if (_animation == null || _resourcesDisplay == null)
			{
				throw new DisplaySnapshotSetupException("Climb resource acquisition systems were not registered.");
			}

			_animation.SetSnapshotState(_resources, VariantTime(_variant));
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			if (_variant == "pulse")
			{
				_resourcesDisplay.SetResourcePulseForSnapshot(_resources, 0.5f);
			}
			_resourcesDisplay.Draw();
			_animation.Draw();
		}

		private void ConfigureSave()
		{
			SaveCache.StartNewRun();
			var climb = new ClimbSaveState
			{
				time = 5,
				resources = new ClimbResourceSave { red = 2, white = 1, black = 1 },
				shopSlots = new List<ClimbShopSlotSave>(),
				encounterSlots = new List<ClimbEncounterSlotSave>(),
				eventSlots = new List<ClimbEventSlotSave>(),
			};
			SaveCache.SaveClimbState(climb);
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 1 && args[0] is "entry" or "fall" or "catch" or "pulse") return args[0];
			throw new DisplaySnapshotSetupException(
				"climb-resource-acquisition expects one variant: entry, fall, catch, or pulse");
		}

		private static float VariantTime(string variant)
		{
			return variant switch
			{
				"entry" => 0.10f,
				"fall" => 0.47f,
				"catch" => 0.70f,
				_ => 1.24f,
			};
		}
	}
}
