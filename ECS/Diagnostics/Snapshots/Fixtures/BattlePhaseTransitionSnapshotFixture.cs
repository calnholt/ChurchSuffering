using System;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.Diagnostics.Snapshots.Fixtures
{
	public sealed class BattlePhaseTransitionSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "battle-phase-transition";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant;

		private string _variant = "block-hold";
		private Texture2D _background;
		private Texture2D _pixel;
		private BattlePhaseDisplaySystem _display;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args);
			_background = ctx.Content.Load<Texture2D>("Battle_Backgrounds/gothic-battle-background");
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });

			var phaseEntity = ctx.World.CreateEntity("BattlePhaseTransitionSnapshotPhase");
			var phase = new PhaseState
			{
				Main = MainPhase.PlayerTurn,
				Sub = ResolveSubPhase(_variant),
				TurnNumber = 1
			};
			ctx.World.AddComponent(phaseEntity, phase);

			_display = new BattlePhaseDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);

			if (_variant == "start-hold")
				EventManager.Publish(new ShowStartOfBattleAnimationEvent());
			else if (_variant == "victory-hold")
				EventManager.Publish(new ShowVictoryAnimationEvent());

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

		private static SubPhase ResolveSubPhase(string variant)
		{
			if (variant.StartsWith("block", StringComparison.Ordinal)) return SubPhase.Block;
			if (variant.StartsWith("action", StringComparison.Ordinal)) return SubPhase.Action;
			if (variant.StartsWith("pledge", StringComparison.Ordinal)) return SubPhase.Pledge;
			return SubPhase.StartBattle;
		}

		private static float ResolveSampleTime(string variant)
		{
			return variant switch
			{
				"block-entry" => 0.12f,
				"action-exit" => 0.74f,
				_ => 0.32f
			};
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 0) return "block-hold";
			if (args.Length == 1 && args[0] is
				"start-hold" or
				"block-entry" or
				"block-hold" or
				"action-hold" or
				"action-exit" or
				"pledge-hold" or
				"victory-hold")
			{
				return args[0];
			}

			throw new DisplaySnapshotSetupException(
				"battle-phase-transition expects one variant: start-hold, block-entry, block-hold, action-hold, action-exit, pledge-hold, or victory-hold");
		}
	}
}
