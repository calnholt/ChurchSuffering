using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class PauseMenuSnapshotFixture : IDisplaySnapshotFixture
	{
		private string _variant = "rumble-on";
		private PauseMenuDisplaySystem _pause;
		private PauseMenuSliderDisplaySystem _sliders;
		private Texture2D _pixel;

		public string Id => "pause-menu";
		public int WarmupFrames => 1;
		public string OutputFileName => _variant;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args ?? Array.Empty<string>());
			SaveCache.SetRumbleEnabled(_variant == "rumble-on");
			ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.Climb;
			_pause = ctx.World.GetSystem<PauseMenuDisplaySystem>()
				?? throw new DisplaySnapshotSetupException("Pause menu system was not registered.");
			_sliders = ctx.World.GetSystem<PauseMenuSliderDisplaySystem>()
				?? throw new DisplaySnapshotSetupException("Pause menu slider system was not registered.");
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			_pause.OpenForSnapshot();
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(34, 24, 30));
			_pause.Draw();
			_sliders.Draw();
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 1 && args[0] is "rumble-on" or "rumble-off") return args[0];
			throw new DisplaySnapshotSetupException("pause-menu expects one variant: rumble-on or rumble-off");
		}
	}
}
