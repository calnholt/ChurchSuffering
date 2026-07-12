using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class ClimbResourceAcquisitionSnapshotFixture : IDisplaySnapshotFixture
	{
		private readonly ClimbResourceSave _resources = new() { red = 2, white = 1, black = 1 };
		private ClimbSceneSystem _climbScene;
		private ClimbResourceAcquisitionDisplaySystem _animation;
		private ClimbHeaderDisplaySystem _header;
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
			var columns = ctx.World.GetSystem<ClimbColumnDisplaySystem>();
			if (columns != null)
			{
				columns.PortraitParallaxMultiplierX = 0f;
				columns.PortraitParallaxMultiplierY = 0f;
			}
			_climbScene = ctx.World.GetSystem<ClimbSceneSystem>();
			_animation = ctx.World.GetSystem<ClimbResourceAcquisitionDisplaySystem>();
			_header = ctx.World.GetSystem<ClimbHeaderDisplaySystem>();
			if (_climbScene == null || _animation == null || _header == null)
			{
				throw new DisplaySnapshotSetupException("Climb resource acquisition systems were not registered.");
			}

			_animation.SetSnapshotState(_resources, VariantTime(_variant));
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			if (_variant == "pulse")
			{
				_header.SetResourcePulseForSnapshot(_resources, 0.5f);
			}
			_climbScene.Draw();
		}

		private void ConfigureSave()
		{
			const int seed = 30030;
			SaveCache.StartNewRun();
			var save = SaveCache.GetAll();
			save.isRunActive = true;
			save.runMapSeed = seed;
			var loadout = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId);
			var climb = ClimbRuleService.CreateInitialState(seed, loadout);
			climb.time = 5;
			climb.resources = new ClimbResourceSave { red = 2, white = 1, black = 1 };
			climb.eventSlots = new List<ClimbEventSlotSave>();
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
