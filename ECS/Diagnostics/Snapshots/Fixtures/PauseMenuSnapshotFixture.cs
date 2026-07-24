using System;
using System.Linq;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Data.Save;
using ChurchSuffering.ECS.Input;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class PauseMenuSnapshotFixture : IDisplaySnapshotFixture
	{
		private string _variant = "rumble-50";
		private PauseMenuDisplaySystem _pause;
		private PauseMenuSliderDisplaySystem _sliders;
		private Texture2D _pixel;

		public string Id => "pause-menu";
		public int WarmupFrames => 1;
		public string OutputFileName => _variant;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args ?? Array.Empty<string>());
			SaveCache.SetRumbleLevel(_variant == "rumble-50" ? 50 : 0);
			SaveCache.SetCursorSpeedLevel(SaveFile.DEFAULT_CURSOR_SPEED_LEVEL);
			SaveCache.SetCursorFastSpeedLevel(SaveFile.DEFAULT_CURSOR_SPEED_LEVEL);
			ctx.SceneEntity.GetComponent<SceneState>().Current = SceneId.Climb;
			_pause = ctx.World.GetSystem<PauseMenuDisplaySystem>()
				?? throw new DisplaySnapshotSetupException("Pause menu system was not registered.");
			_sliders = ctx.World.GetSystem<PauseMenuSliderDisplaySystem>()
				?? throw new DisplaySnapshotSetupException("Pause menu slider system was not registered.");
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			ForceGamepadInput(ctx);
			_pause.OpenForSnapshot();
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ForceGamepadInput(ctx);
			_pause.OpenForSnapshot();
			ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(34, 24, 30));
			_pause.Draw();
			_sliders.Draw();
		}

		private static void ForceGamepadInput(DisplaySnapshotContext ctx)
		{
			var state = ctx.World.EntityManager
				.GetEntitiesWithComponent<PlayerInputState>()
				.FirstOrDefault()
				?.GetComponent<PlayerInputState>();
			if (state == null)
			{
				var entity = ctx.World.EntityManager.GetEntity("PlayerInput")
					?? ctx.World.EntityManager.CreateEntity("PlayerInput");
				state = entity.GetComponent<PlayerInputState>();
				if (state == null)
				{
					state = new PlayerInputState();
					ctx.World.EntityManager.AddComponent(entity, state);
				}
			}

			state.Frame = default(PlayerInputFrame) with
			{
				IsWindowActive = true,
				IsGamepadConnected = true,
				Device = PlayerInputDevice.Gamepad,
				PreviousDevice = PlayerInputDevice.Gamepad,
			};
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 1 && args[0] is "rumble-50" or "rumble-0") return args[0];
			throw new DisplaySnapshotSetupException("pause-menu expects one variant: rumble-50 or rumble-0");
		}
	}
}
