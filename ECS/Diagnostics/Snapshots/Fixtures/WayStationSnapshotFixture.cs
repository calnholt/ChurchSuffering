using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Systems;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class WayStationSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "waystation";
		public int WarmupFrames => 2;
		public string OutputFileName => "default";

		private WayStationDisplaySystem _wayStation;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			WayStationRunSetupSingleton.SelectedWeapon = StartingWeapon.Sword;
			WayStationRunSetupSingleton.SelectedDifficulty = RunDifficulty.Easy;

			_wayStation = new WayStationDisplaySystem(
				ctx.World,
				ctx.GraphicsDevice,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			ctx.World.AddSystem(_wayStation);
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			_wayStation.Draw();
		}
	}
}
