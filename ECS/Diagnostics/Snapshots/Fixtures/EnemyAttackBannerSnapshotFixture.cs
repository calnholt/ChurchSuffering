using System;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.EnemyAttacks;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class EnemyAttackBannerSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "enemy-attack-banner";
		public int WarmupFrames => 2;
		public string OutputFileName => _variant;

		private string _variant = "settled";
		private Texture2D _background;
		private Texture2D _pixel;
		private Texture2D _enemyTexture;
		private EnemyAttackDisplaySystem _display;
		private EnemyAttackBannerLateLayoutSystem _layout;
		private PhaseState _phase;
		private Entity _enemy;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			_variant = ParseVariant(args ?? Array.Empty<string>());
			_background = ctx.Content.Load<Texture2D>("Battle_Backgrounds/gothic-battle-background");
			_enemyTexture = ctx.ImageAssets.GetRequiredTexture("Ogre");
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });

			var phaseEntity = ctx.World.CreateEntity("EnemyAttackBannerSnapshotPhase");
			_phase = new PhaseState { Main = MainPhase.EnemyTurn, Sub = SubPhase.Block };
			ctx.World.AddComponent(phaseEntity, _phase);

			_enemy = ctx.World.CreateEntity("EnemyAttackBannerSnapshotEnemy");
			ctx.World.AddComponent(_enemy, new Enemy { Name = "Snapshot Ogre", CurrentHealth = 80, MaxHealth = 80 });
			ctx.World.AddComponent(_enemy, new HP { Current = 80, Max = 80 });
			ctx.World.AddComponent(_enemy, new Transform { Position = new Vector2(1450f, 430f) });
			var attack = new TreeStomp { Damage = 15 };
			ctx.World.AddComponent(_enemy, new AttackIntent
			{
				Owner = _enemy,
				ActiveAttackSequence = 1337,
				Planned = { new PlannedAttack { AttackId = attack.Id, AttackDefinition = attack } },
			});

			var progressEntity = ctx.World.CreateEntity("EnemyAttackProgress[banner-snapshot]");
			ctx.World.AddComponent(progressEntity, new EnemyAttackProgress
			{
				Enemy = _enemy,
				AttackId = attack.Id,
				AttackSequence = 1337,
				BaseDamage = 15,
				DamageBeforePrevention = 15,
				ActualDamage = 15,
				IsConditionMet = true,
			});

			_display = new EnemyAttackDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch, ctx.ImageAssets);
			_layout = new EnemyAttackBannerLateLayoutSystem(ctx.World.EntityManager);
			Advance(0f);
			EventManager.Publish(new TriggerEnemyAttackDisplayEvent());

			if (_variant == "absorb")
			{
				Advance(0.40f);
				_phase.Sub = SubPhase.EnemyAttack;
				Advance(0.20f);
			}
			else
			{
				Advance(ResolveSampleTime(_variant));
			}

			if (_variant == "hover")
			{
				var ui = ctx.World.EntityManager.GetEntity("UIButton_ConfirmEnemyAttack")?.GetComponent<UIElement>();
				if (ui != null) ui.IsHovered = true;
			}
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			var confirmUi = ctx.World.EntityManager.GetEntity("UIButton_ConfirmEnemyAttack")?.GetComponent<UIElement>();
			if (_variant == "pulse" && confirmUi != null)
			{
				confirmUi.IsInteractable = false;
				confirmUi.IsHovered = false;
			}

			ctx.SpriteBatch.Draw(_background, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), Color.White);
			ctx.SpriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(9, 5, 8, 86));
			ctx.SpriteBatch.Draw(_enemyTexture, _enemy.GetComponent<Transform>().Position, null, Color.White * 0.9f, 0f,
				new Vector2(_enemyTexture.Width * 0.5f, _enemyTexture.Height * 0.5f), 0.35f, SpriteEffects.None, 0f);
			_display.Draw();
		}

		private void Advance(float seconds)
		{
			const float step = 1f / 60f;
			if (seconds <= 0f)
			{
				var zero = new GameTime(TimeSpan.Zero, TimeSpan.Zero);
				_display.Update(zero);
				_layout.Update(zero);
				return;
			}

			float elapsed = 0f;
			while (elapsed + step <= seconds + 0.0001f)
			{
				elapsed += step;
				var time = new GameTime(TimeSpan.FromSeconds(elapsed), TimeSpan.FromSeconds(step));
				_display.Update(time);
				_layout.Update(time);
			}
		}

		private static float ResolveSampleTime(string variant) => variant switch
		{
			"anticipation" => 0.05f,
			"impact" => 0.11f,
			"settled" => 0.40f,
			"hover" => 0.40f,
			"pulse" => 2.30f,
			_ => 0.40f,
		};

		private static string ParseVariant(string[] args)
		{
			if (args.Length == 0) return "settled";
			if (args.Length == 1 && args[0] is "anticipation" or "impact" or "settled" or "hover" or "pulse" or "absorb")
				return args[0];

			throw new DisplaySnapshotSetupException(
				"enemy-attack-banner expects one variant: anticipation, impact, settled, hover, pulse, or absorb");
		}
	}
}
