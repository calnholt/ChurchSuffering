using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class EnemyDamageMeterSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "enemy-damage-meter";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant;

		private string _variant = "initial";
		private Texture2D _pixel;
		private EnemyDamageMeterDisplaySystem _display;
		private UIElement _anchorUi;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args);
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });

			var phaseEntity = ctx.World.CreateEntity("DamageMeterSnapshotPhase");
			var phase = new PhaseState { Main = MainPhase.EnemyTurn, Sub = SubPhase.Block };
			ctx.World.AddComponent(phaseEntity, phase);

			var enemy = ctx.World.CreateEntity("DamageMeterSnapshotEnemy");
			ctx.World.AddComponent(enemy, new AttackIntent
			{
				Owner = enemy,
				ActiveAttackSequence = 1,
				Planned = { new PlannedAttack() },
			});

			var progressEntity = ctx.World.CreateEntity("EnemyAttackProgress[damage-meter-snapshot]");
			var progress = new EnemyAttackProgress
			{
				Enemy = enemy,
				AttackSequence = 1,
				DamageBeforePrevention = 12,
				ActualDamage = 9,
				AegisTotal = 3,
			};
			ctx.World.AddComponent(progressEntity, progress);

			var anchor = ctx.World.CreateEntity("DamageMeterSnapshotAnchor");
			ctx.World.AddComponent(anchor, new EnemyAttackBannerAnchor());
			ctx.World.AddComponent(anchor, new Transform { Position = new Vector2(Game1.VirtualWidth / 2f, 300f) });
			_anchorUi = new UIElement
			{
				Bounds = new Rectangle(Game1.VirtualWidth / 2 - 330, 190, 660, 220),
				IsInteractable = false,
			};
			ctx.World.AddComponent(anchor, _anchorUi);

			_display = new EnemyDamageMeterDisplaySystem(
				ctx.World.EntityManager,
				ctx.GraphicsDevice,
				ctx.SpriteBatch);

			Advance(1f);

			if (_variant is "transition" or "settled" or "absorb")
			{
				progress.AssignedBlockTotal = 5;
				progress.ActualDamage = 2;
				progress.PreventedDamageFromBlockCondition = 2;
				Advance(_variant == "transition" ? 0.1f : 1f);
			}

			if (_variant == "absorb")
			{
				phase.Sub = SubPhase.EnemyAttack;
				_anchorUi.Bounds = new Rectangle(Game1.VirtualWidth / 2 - 250, 220, 500, 165);
				Advance(0.2f);
			}
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			ctx.SpriteBatch.Draw(
				_pixel,
				new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight),
				new Color(32, 26, 25));
			ctx.SpriteBatch.Draw(_pixel, _anchorUi.Bounds, new Color(20, 20, 20, 220));
			DrawBorder(ctx.SpriteBatch, _anchorUi.Bounds, Color.White, 2);
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

		private void DrawBorder(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
		{
			spriteBatch.Draw(_pixel, new Rectangle(rect.Left, rect.Top, rect.Width, thickness), color);
			spriteBatch.Draw(_pixel, new Rectangle(rect.Left, rect.Bottom - thickness, rect.Width, thickness), color);
			spriteBatch.Draw(_pixel, new Rectangle(rect.Left, rect.Top, thickness, rect.Height), color);
			spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Top, thickness, rect.Height), color);
		}

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 0) return "initial";
			if (args.Length == 1 && args[0] is "initial" or "transition" or "settled" or "absorb")
				return args[0];

			throw new DisplaySnapshotSetupException(
				"enemy-damage-meter expects one variant: initial, transition, settled, or absorb");
		}
	}
}
