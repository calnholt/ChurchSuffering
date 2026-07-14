using System.Collections.Generic;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class WayStationSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "waystation";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant.FileSlug();

		private WayStationSnapshotVariant _variant;

		private WayStationBackgroundDisplaySystem _wayStationBackground;
		private IncenseDisplaySystem _incense;
		private WayStationPoiDisplaySystem _wayStationPoi;
		private WayStationDialogueSystem _wayStationDialogue;
		private WayStationClimbSettingsModalSystem _wayStationClimbSettingsModal;
		private LocationNameDisplaySystem _locationName;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = WayStationSnapshotVariantParser.Parse(args);
			ConfigureProgression();
			ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.WayStation;

			_locationName = new LocationNameDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);
			ctx.World.AddSystem(_locationName);

			_wayStationBackground = new WayStationBackgroundDisplaySystem(
				ctx.World.EntityManager,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			ctx.World.AddSystem(_wayStationBackground);

			_incense = new IncenseDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch,
				ctx.Content);
			ctx.World.AddSystem(_incense);

			_wayStationPoi = new WayStationPoiDisplaySystem(
				ctx.World.EntityManager,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			ctx.World.AddSystem(_wayStationPoi);

			_wayStationDialogue = new WayStationDialogueSystem(
				ctx.World.EntityManager,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			ctx.World.AddSystem(_wayStationDialogue);

			_wayStationClimbSettingsModal = new WayStationClimbSettingsModalSystem(
				ctx.World,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			ctx.World.AddSystem(_wayStationClimbSettingsModal);

			EventManager.Publish(new LoadSceneEvent { Scene = SceneId.WayStation, PreviousScene = SceneId.Snapshot });
			EventManager.Publish(new UpdateLocationNameEvent { Title = "Waystation" });
			if (_variant != WayStationSnapshotVariant.Default)
			{
				EventManager.Publish(new OpenWayStationClimbSettingsModalEvent());
				PinModalVisible(ctx);
			}
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			if (_variant != WayStationSnapshotVariant.Default) PinModalVisible(ctx);
			_wayStationBackground.Draw();
			_incense.Draw();
			_wayStationPoi.Draw();
			_wayStationDialogue.Draw();
			_wayStationClimbSettingsModal.Draw();
			_locationName.Draw();
		}

		private static void PinModalVisible(DisplaySnapshotContext ctx)
		{
			var animation = ctx.World.EntityManager
				.GetEntity(WayStationSceneConstants.ModalRootName)
				?.GetComponent<ModalAnimation>();
			if (animation == null)
				throw new DisplaySnapshotSetupException("Waystation climb modal did not open.");
			animation.RequestedVisible = true;
			animation.Phase = ModalAnimationPhase.Visible;
			animation.ElapsedSeconds = 0f;
		}

		private void ConfigureProgression()
		{
			WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Sword;
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;
			SaveCache.GetAll().collection.pendingClimbPoints = _variant == WayStationSnapshotVariant.Default ? 1 : 0;
			var completed = new List<CompletedClimbSave>();
			int completions = 0;

			if (_variant != WayStationSnapshotVariant.Default)
			{
				completions = 1;
				completed.Add(Completed(StartingWeapon.Sword, RunDifficulty.Easy));
			}
			if (_variant is WayStationSnapshotVariant.ModalHammer or WayStationSnapshotVariant.ModalFull)
			{
				completions = 2;
				completed.Add(Completed(StartingWeapon.Dagger, RunDifficulty.Easy));
				WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Dagger;
			}
			if (_variant == WayStationSnapshotVariant.ModalFull)
			{
				completions = 6;
				completed.Add(Completed(StartingWeapon.Sword, RunDifficulty.Normal));
				completed.Add(Completed(StartingWeapon.Dagger, RunDifficulty.Normal));
				completed.Add(Completed(StartingWeapon.Hammer, RunDifficulty.Easy));
				completed.Add(Completed(StartingWeapon.Hammer, RunDifficulty.Normal));
				WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Hammer;
				WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Hard;
			}

			SaveCache.GetAll().waystation = new WayStationMetaSave
			{
				climbCompletions = completions,
				completedClimbs = completed,
			};
		}

		private static CompletedClimbSave Completed(StartingWeapon weapon, RunDifficulty difficulty)
		{
			return new CompletedClimbSave
			{
				startingWeaponId = weapon.ToString().ToLowerInvariant(),
				difficulty = difficulty,
			};
		}
	}
}
