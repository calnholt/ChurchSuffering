using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class NarrativeEventModalSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "narrative-event-modal";
		public int WarmupFrames => 2;

		private NarrativeEventModalDisplaySystem _modal;
		private NarrativeEventSnapshotVariant _variant;
		private Texture2D _pixel;

		public string OutputFileName => _variant?.FileSlug ?? "default";

		private static readonly Color BackdropColor = new(40, 44, 48);

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = NarrativeEventSnapshotVariant.Parse(args);

			if (EventFactory.Create(_variant.EventTypeId) == null)
			{
				throw new DisplaySnapshotSetupException(
					$"Failed to create narrative event: '{_variant.EventTypeId}'");
			}

			_modal = new NarrativeEventModalDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch,
				ctx.ImageAssets);
			ctx.World.AddSystem(_modal);

			_modal.OpenForSnapshot(_variant.EventTypeId, _variant.VisibleOptionCount);

			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			int vw = Game1.VirtualWidth;
			int vh = Game1.VirtualHeight;
			ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, vw, vh), BackdropColor);
			_modal.Draw();
		}
	}
}
