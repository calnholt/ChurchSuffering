using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class WayStationSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "waystation";
		public int WarmupFrames => 2;
		public string OutputFileName => "default";

		private WayStationBackgroundDisplaySystem _wayStationBackground;
		private IncenseDisplaySystem _incense;
		private WayStationPoiDisplaySystem _wayStationPoi;
		private WayStationDialogueSystem _wayStationDialogue;
		private WayStationClimbSettingsModalSystem _wayStationClimbSettingsModal;
		private LocationNameDisplaySystem _locationName;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Sword;
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;

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

			EventManager.Publish(new UpdateLocationNameEvent { Title = "Waystation" });
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			_wayStationBackground.Draw();
			_incense.Draw();
			_wayStationPoi.Draw();
			_wayStationDialogue.Draw();
			_wayStationClimbSettingsModal.Draw();
			_locationName.Draw();
		}
	}
}
