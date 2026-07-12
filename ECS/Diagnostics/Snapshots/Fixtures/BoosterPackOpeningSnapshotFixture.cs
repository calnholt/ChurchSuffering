using System;
using Crusaders30XX.ECS.Systems;
using Crusaders30XX.ECS.Data.Save;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures;

public sealed class BoosterPackOpeningSnapshotFixture : IDisplaySnapshotFixture
{
	private BoosterPackOpeningDisplaySystem _display;
	private BoosterPackOpeningSnapshotVariant _variant;
	private Texture2D _pixel;

	public string Id => "booster-pack-opening";
	public int WarmupFrames => 2;
	public string OutputFileName => $"{_variant?.FileSlug ?? "time-5.14-seed-1337"}.png";

	public void Setup(DisplaySnapshotContext ctx, string[] args)
	{
		_variant = BoosterPackOpeningSnapshotVariant.Parse(args);
		_display = new BoosterPackOpeningDisplaySystem(
			ctx.World.EntityManager,
			ctx.GraphicsDevice,
			ctx.SpriteBatch,
			ctx.ImageAssets,
			random: new Random(_variant.Seed));
		_display.OpenForSnapshot(_variant.TimeSeconds, new BoosterPackSave
		{
			rewards =
			{
				new BoosterPackRewardSave { kind = "card", id = "strike", cardColor = "White" },
				new BoosterPackRewardSave { kind = "medal", id = "st_luke" },
				new BoosterPackRewardSave { kind = "equipment", id = "scarlet_vest" },
			},
		});

		_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
		_pixel.SetData(new[] { Color.White });
	}

	public void Draw(DisplaySnapshotContext ctx)
	{
		ctx.SpriteBatch.Draw(
			_pixel,
			new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
			new Color(51, 43, 39));
		for (int index = 0; index < 9; index++)
		{
			var band = new Rectangle(index * 240 - 140, 0, 100, Game1.VirtualHeight);
			ctx.SpriteBatch.Draw(_pixel, band, new Color(73, 50, 42) * 0.22f);
		}
		_display.Draw();
	}
}
