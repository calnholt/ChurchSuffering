using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class PassiveApplicationSnapshotFixture : IDisplaySnapshotFixture
	{
		private static readonly Vector2 PlayerAnchor = new(520f, 575f);
		private static readonly Vector2 EnemyAnchor = new(1380f, 540f);

		private AppliedPassiveType _type = AppliedPassiveType.Burn;
		private string _typeSlug = "burn";
		private string _sample = "hold";
		private string _target = "player";
		private string _mode = "single";
		private Texture2D _pixel;
		private Texture2D _playerTexture;
		private Texture2D _enemyTexture;
		private Entity _player;
		private Entity _enemy;
		private PassiveApplicationAnimationDisplaySystem _statusDisplay;
		private ModularEffectPrimitiveDisplaySystem _attackDisplay;

		public string Id => "passive-application";
		public int WarmupFrames => 2;
		public string OutputFileName => $"{_typeSlug}-{_sample}-{_target}-{_mode}";

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			ParseArgs(args ?? Array.Empty<string>());
			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			_playerTexture = ctx.ImageAssets.GetRequiredTexture("crusader_hammer");
			_enemyTexture = ctx.ImageAssets.GetRequiredTexture("Enemies/Skeleton");
			_player = CreateActor(ctx, "SnapshotPlayer", PlayerAnchor, true, _playerTexture, .36f);
			_enemy = CreateActor(ctx, "Enemy", EnemyAnchor, false, _enemyTexture, .62f);

			_statusDisplay = new PassiveApplicationAnimationDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch)
			{
				AudioVolume = 0f,
			};
			var target = _target == "enemy" ? _enemy : _player;
			EventManager.Publish(new ApplyPassiveEvent { Target = target, Type = _type, Delta = 1 });
			if (_mode == "multi")
			{
				EventManager.Publish(new ApplyPassiveEvent { Target = target, Type = AppliedPassiveType.Armor, Delta = 2 });
				EventManager.Publish(new ApplyPassiveEvent { Target = target, Type = AppliedPassiveType.Fear, Delta = 1 });
			}

			if (_mode == "attack")
			{
				var attackRecipe = VisualEffectPresets.EnemySlash();
				var timing = VisualEffectTimingProfileResolver.Resolve(attackRecipe.Timing);
				var active = ctx.World.CreateEntity("SnapshotConcurrentAttack");
				ctx.World.AddComponent(active, new ActiveVisualEffect
				{
					RequestId = Guid.Parse("13371337-1337-1337-1337-133713371337"),
					Recipe = attackRecipe,
					Timing = timing,
					Source = _enemy,
					Target = _player,
					SourceAnchor = EnemyAnchor,
					TargetAnchor = PlayerAnchor,
					ImpactAnchor = PlayerAnchor,
					DirectionSign = -1,
					ElapsedSeconds = timing.ImpactTimeSeconds,
					IsPreview = true,
					SourceKind = VisualEffectSourceKind.EnemyAttack,
				});
				var resources = new ModularEffectRenderResources(ctx.GraphicsDevice, ctx.ImageAssets.GetPixel(Color.White));
				_attackDisplay = new ModularEffectPrimitiveDisplaySystem(ctx.World.EntityManager, ctx.SpriteBatch, resources);
			}

			_statusDisplay.Update(new GameTime(TimeSpan.Zero, TimeSpan.FromSeconds(SampleSeconds())));
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			DrawBackdrop(ctx.SpriteBatch);
			DrawActor(ctx.SpriteBatch, _player, _playerTexture, .36f);
			DrawActor(ctx.SpriteBatch, _enemy, _enemyTexture, .62f);
			_attackDisplay?.Draw();
			_statusDisplay.Draw();
			DrawPassiveChip(ctx.SpriteBatch, _target == "enemy" ? EnemyAnchor : PlayerAnchor);
		}

		private void ParseArgs(string[] args)
		{
			if (args.Length > 4) throw new DisplaySnapshotSetupException("passive-application accepts: [type] [entry|hold|exit] [player|enemy] [single|multi|attack].");
			if (args.Length > 0)
			{
				string normalized = Normalize(args[0]);
				if (!Enum.GetValues<AppliedPassiveType>().Any(type => Normalize(type.ToString()) == normalized))
					throw new DisplaySnapshotSetupException($"Unknown passive type '{args[0]}'.");
				_type = Enum.GetValues<AppliedPassiveType>().First(type => Normalize(type.ToString()) == normalized);
				_typeSlug = ToSlug(_type.ToString());
			}
			if (args.Length > 1)
			{
				_sample = args[1].ToLowerInvariant();
				if (_sample is not ("entry" or "hold" or "exit")) throw new DisplaySnapshotSetupException("Passive sample must be entry, hold, or exit.");
			}
			if (args.Length > 2)
			{
				_target = args[2].ToLowerInvariant();
				if (_target is not ("player" or "enemy")) throw new DisplaySnapshotSetupException("Passive target must be player or enemy.");
			}
			if (args.Length > 3)
			{
				_mode = args[3].ToLowerInvariant();
				if (_mode is not ("single" or "multi" or "attack")) throw new DisplaySnapshotSetupException("Passive mode must be single, multi, or attack.");
			}
		}

		private float SampleSeconds()
		{
			float duration = PassiveApplicationRecipeCatalog.Get(_type).DurationSeconds;
			return _sample switch
			{
				"entry" => duration * .18f,
				"exit" => duration * .82f,
				_ => _mode == "multi" ? Math.Max(duration * .46f, .28f) : duration * .46f,
			};
		}

		private static Entity CreateActor(DisplaySnapshotContext ctx, string name, Vector2 position, bool player, Texture2D texture, float scale)
		{
			var actor = ctx.World.CreateEntity(name);
			ctx.World.AddComponent(actor, new Transform { Position = position });
			ctx.World.AddComponent(actor, new PortraitInfo
			{
				TextureWidth = texture.Width,
				TextureHeight = texture.Height,
				BaseScale = scale,
				CurrentScale = scale,
				LastDrawCenter = position,
			});
			if (player) ctx.World.AddComponent(actor, new Player());
			else ctx.World.AddComponent(actor, new Enemy());
			return actor;
		}

		private void DrawBackdrop(SpriteBatch spriteBatch)
		{
			spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(16, 13, 16));
			spriteBatch.Draw(_pixel, new Rectangle(0, 720, Game1.VirtualWidth, 360), Color.Black * .38f);
			spriteBatch.Draw(_pixel, new Rectangle(0, 724, Game1.VirtualWidth, 3), new Color(255, 245, 223) * .08f);
		}

		private static void DrawActor(SpriteBatch spriteBatch, Entity actor, Texture2D texture, float scale)
		{
			spriteBatch.Draw(texture, actor.GetComponent<Transform>().Position, null, Color.White, 0f, new Vector2(texture.Width * .5f, texture.Height * .5f), scale, SpriteEffects.None, 0f);
		}

		private void DrawPassiveChip(SpriteBatch spriteBatch, Vector2 actorCenter)
		{
			var rect = new Rectangle((int)actorCenter.X - 80, (int)actorCenter.Y + 205, 160, 34);
			spriteBatch.Draw(_pixel, rect, Color.Black * .92f);
			spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, 3), VisualEffectPaletteResolver.Resolve(PassiveApplicationRecipeCatalog.Get(_type).Palette).Primary);
		}

		private static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

		private static string ToSlug(string value)
		{
			return string.Concat(value.Select((c, i) => i > 0 && char.IsUpper(c) ? $"-{char.ToLowerInvariant(c)}" : char.ToLowerInvariant(c).ToString()));
		}
	}
}
