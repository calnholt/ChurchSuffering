using System;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class EnemyDefeatBurstSnapshotFixture : IDisplaySnapshotFixture
	{
		private static readonly Vector2 PortraitCenter = new(1380f, 460f);

		public string Id => "enemy-defeat-burst";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant;

		private string _variant = "peak-jitter";
		private Texture2D _background;
		private Texture2D _pixel;
		private PixelBurstDisplaySystem _display;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args ?? Array.Empty<string>());
			_background = ctx.Content.Load<Texture2D>("Battle_Backgrounds/gothic-battle-background");
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			var portrait = ctx.ImageAssets.GetRequiredTexture("Enemies/Skeleton");
			const float portraitScale = 0.35f;

			_display = new PixelBurstDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch,
				new Random(1337));

			EventManager.Publish(new PixelBurstAnimationRequested
			{
				Texture = portrait,
				Center = PortraitCenter,
				DrawTopLeft = PortraitPixelBurstLayout.ComputeTopLeft(
					PortraitCenter,
					portrait.Width,
					portrait.Height,
					portraitScale),
				DrawScale = new Vector2(portraitScale),
				SourceEntityId = 1337,
				BurstId = Guid.Parse("13371337-1337-1337-1337-133713371337"),
				IsPreview = true
			});

			Advance(ResolveSampleTime(_variant));
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ctx.SpriteBatch.Draw(
				_background,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				Color.White);
			ctx.SpriteBatch.Draw(
				_pixel,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				new Color(8, 6, 8, 78));
			_display.Draw();
		}

		private void Advance(float seconds)
		{
			const float step = 1f / 60f;
			float elapsed = 0f;
			while (elapsed + step <= seconds + 0.0001f)
			{
				elapsed += step;
				_display.Update(new GameTime(
					TimeSpan.FromSeconds(elapsed),
					TimeSpan.FromSeconds(step)));
			}
		}

		private static float ResolveSampleTime(string variant)
		{
			return variant switch
			{
				"assembled" => 0f,
				"peak-jitter" => 11f / 60f,
				"exploding" => 19f / 60f,
				_ => 0f
			};
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 0) return "peak-jitter";
			if (args.Length == 1 && args[0] is "assembled" or "peak-jitter" or "exploding")
			{
				return args[0];
			}

			throw new DisplaySnapshotSetupException(
				"enemy-defeat-burst expects one variant: assembled, peak-jitter, or exploding");
		}
	}
}
