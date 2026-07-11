using System;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.VisualEffects;
using Crusaders30XX.ECS.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.Diagnostics.Snapshots.Fixtures
{
	public sealed class ModularFxSnapshotFixture : IDisplaySnapshotFixture
	{
		public string Id => "modular-fx";
		public int WarmupFrames => 2;
		public string OutputFileName => _moduleSlug != null
			? $"module-{_moduleSlug}-{_sampleSlug}-{_directionSlug}-seed-{_seed}"
			: $"{_presetSlug}-{_sampleSlug}{(_explicitSeed ? $"-seed-{_seed}" : string.Empty)}{(_explicitDirection ? $"-{_directionSlug}" : string.Empty)}";

		private static readonly Vector2 PlayerAnchor = new(520f, 575f);
		private static readonly Vector2 EnemyAnchor = new(1380f, 540f);

		private string _presetSlug = "heavy-hammer";
		private string _sampleSlug = "impact";
		private string _moduleSlug;
		private string _directionSlug = "auto";
		private int _seed = 1337;
		private bool _explicitSeed;
		private bool _explicitDirection;
		private Texture2D _pixel;
		private Texture2D _playerTexture;
		private Texture2D _enemyTexture;
		private ModularEffectScreenDisplaySystem _screenDisplay;
		private ModularEffectPrimitiveDisplaySystem _primitiveDisplay;
		private ModularEffectParticleDisplaySystem _particleDisplay;
		private ModularEffectActorPresentationSystem _actorDisplay;
		private BattlePresentationTransform _battleTransform;
		private Entity _player;
		private Entity _enemy;

		public void Setup(DisplaySnapshotContext ctx, string[] args)
		{
			ParseArgs(args ?? Array.Empty<string>());
			var recipe = _moduleSlug != null ? ResolveModuleRecipe(_moduleSlug) : ResolveRecipe(_presetSlug);
			var timing = VisualEffectTimingProfileResolver.Resolve(recipe.Timing);

			_pixel = new Texture2D(ctx.GraphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			_playerTexture = ctx.ImageAssets.GetRequiredTexture("crusader_hammer");
			_enemyTexture = ctx.ImageAssets.GetRequiredTexture("Skeleton");

			_player = CreateActor(ctx, "SnapshotPlayer", PlayerAnchor, isPlayer: true, _playerTexture, 0.36f);
			_enemy = CreateActor(ctx, "Enemy", EnemyAnchor, isPlayer: false, _enemyTexture, 0.62f);

			var source = recipe.TargetRole == VisualEffectTargetRole.Player ? _enemy : _player;
			var target = recipe.TargetRole == VisualEffectTargetRole.Player ? _player : _enemy;
			if (_directionSlug == "right")
			{
				source = _player;
				target = _enemy;
			}
			else if (_directionSlug == "left")
			{
				source = _enemy;
				target = _player;
			}
			else
			{
				_directionSlug = ReferenceEquals(target, _enemy) ? "right" : "left";
			}
			var activeEntity = ctx.World.CreateEntity("SnapshotActiveVisualEffect");
			ctx.World.AddComponent(activeEntity, new ActiveVisualEffect
			{
				RequestId = GuidFromSeed(_seed),
				Recipe = recipe.Clone(),
				Timing = timing,
				Source = source,
				Target = target,
				SourceAnchor = source.GetComponent<Transform>().Position,
				TargetAnchor = target.GetComponent<Transform>().Position,
				ImpactAnchor = target.GetComponent<Transform>().Position,
				DirectionSign = target.GetComponent<Transform>().Position.X >= source.GetComponent<Transform>().Position.X ? 1 : -1,
				ElapsedSeconds = ResolveElapsedSeconds(timing, _sampleSlug),
				IsPreview = true,
				SourceKind = recipe.TargetRole == VisualEffectTargetRole.Player ? VisualEffectSourceKind.EnemyAttack : VisualEffectSourceKind.Card,
				SourceId = _moduleSlug ?? _presetSlug,
				DisplayName = _moduleSlug ?? _presetSlug
			});

			_screenDisplay = new ModularEffectScreenDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch);
			_primitiveDisplay = new ModularEffectPrimitiveDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch);
			_particleDisplay = new ModularEffectParticleDisplaySystem(ctx.World.EntityManager, ctx.GraphicsDevice, ctx.SpriteBatch);
			_actorDisplay = new ModularEffectActorPresentationSystem(ctx.World.EntityManager);
			var sampleTime = new GameTime(TimeSpan.Zero, TimeSpan.Zero);
			_actorDisplay.Update(sampleTime);
			_screenDisplay.Update(sampleTime);
			_particleDisplay.Update(sampleTime);
			_battleTransform = ctx.World.EntityManager.GetEntity("BattlePresentationTransform")?.GetComponent<BattlePresentationTransform>();
		}

		public void Draw(DisplaySnapshotContext ctx)
		{
			DrawBackdrop(ctx);
			DrawActor(ctx.SpriteBatch, _player, _playerTexture, 0.36f);
			DrawActor(ctx.SpriteBatch, _enemy, _enemyTexture, 0.62f);
			_screenDisplay.Draw();
			_primitiveDisplay.Draw();
			_particleDisplay.Draw();
		}

		private static Entity CreateActor(DisplaySnapshotContext ctx, string name, Vector2 position, bool isPlayer, Texture2D texture, float scale)
		{
			var actor = ctx.World.CreateEntity(name);
			ctx.World.AddComponent(actor, new Transform { Position = position, Scale = Vector2.One });
			ctx.World.AddComponent(actor, new PortraitInfo
			{
				TextureWidth = texture.Width,
				TextureHeight = texture.Height,
				BaseScale = scale,
				CurrentScale = scale,
				LastDrawCenter = position
			});
			if (isPlayer) ctx.World.AddComponent(actor, new Player());
			else ctx.World.AddComponent(actor, new Enemy());
			return actor;
		}

		private void DrawBackdrop(DisplaySnapshotContext ctx)
		{
			_spriteDraw(ctx.SpriteBatch, new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight), new Color(16, 13, 16));
			DrawBand(ctx.SpriteBatch, 0, 0, Game1.VirtualHeight, new Color(10, 8, 10), new Color(46, 34, 30));
			_spriteDraw(ctx.SpriteBatch, new Rectangle(0, 720, Game1.VirtualWidth, 360), Color.Black * 0.38f);
			_spriteDraw(ctx.SpriteBatch, new Rectangle(0, 724, Game1.VirtualWidth, 3), new Color(255, 245, 223) * 0.08f);
		}

		private void DrawActor(SpriteBatch spriteBatch, Entity actor, Texture2D texture, float scale)
		{
			var presentation = actor.GetComponent<ActorPresentationState>();
			var position = actor.GetComponent<Transform>().Position + (presentation?.DrawOffset ?? Vector2.Zero) + (_battleTransform?.Offset ?? Vector2.Zero);
			var drawScale = new Vector2(scale) * (presentation?.ScaleMultiplier ?? Vector2.One) * (_battleTransform?.Scale ?? Vector2.One);
			spriteBatch.Draw(
				texture,
				position,
				null,
				Color.White,
				0f,
				new Vector2(texture.Width * 0.5f, texture.Height * 0.5f),
				drawScale,
				SpriteEffects.None,
				0f);
		}

		private void DrawBand(SpriteBatch spriteBatch, int x, int y, int height, Color top, Color bottom)
		{
			const int strips = 48;
			for (int i = 0; i < strips; i++)
			{
				float t = i / (float)(strips - 1);
				var color = Color.Lerp(top, bottom, t);
				int stripY = y + (int)MathF.Round(i * height / (float)strips);
				int stripH = Math.Max(1, (int)MathF.Ceiling(height / (float)strips));
				_spriteDraw(spriteBatch, new Rectangle(x, stripY, Game1.VirtualWidth, stripH + 1), color);
			}
		}

		private void _spriteDraw(SpriteBatch spriteBatch, Rectangle rect, Color color)
		{
			spriteBatch.Draw(_pixel, rect, color);
		}

		private static VisualEffectRecipe ResolveRecipe(string slug)
		{
			return slug switch
			{
				"heavy-hammer" => VisualEffectPresets.HeavyHammer(),
				"holy-strike" => VisualEffectPresets.HolyStrike(),
				"enemy-rock-blast" => VisualEffectPresets.EnemyRockBlast(),
				"enemy-bite" => VisualEffectPresets.EnemyBite(),
				"enemy-slash" => VisualEffectPresets.EnemySlash(),
				"light-slash" => VisualEffectPresets.LightSlash(),
				_ => throw new DisplaySnapshotSetupException(
					$"Unknown modular-fx preset '{slug}'. Expected heavy-hammer, holy-strike, enemy-rock-blast, enemy-bite, enemy-slash, or light-slash.")
			};
		}

		private static VisualEffectRecipe ResolveModuleRecipe(string slug)
		{
			string enumToken = slug.Replace("-", string.Empty, StringComparison.Ordinal);
			if (!Enum.TryParse(enumToken, true, out VisualEffectModule module))
			{
				throw new DisplaySnapshotSetupException($"Unknown modular-fx module '{slug}'.");
			}
			var entry = VisualEffectModuleDebugCatalog.All.FirstOrDefault(candidate => candidate.Module == module);
			if (!VisualEffectModuleDebugCatalog.All.Any(candidate => candidate.Module == module))
			{
				throw new DisplaySnapshotSetupException($"Module '{slug}' is not available in the debug catalog.");
			}
			return VisualEffectModuleDebugCatalog.BuildRecipe(entry);
		}

		private void ParseArgs(string[] args)
		{
			int positional = 0;
			for (int i = 0; i < args.Length; i++)
			{
				string arg = args[i];
				if (arg == "--module")
				{
					_moduleSlug = RequireValue(args, ref i, arg).ToLowerInvariant();
				}
				else if (arg == "--sample")
				{
					_sampleSlug = RequireValue(args, ref i, arg).ToLowerInvariant();
				}
				else if (arg == "--seed")
				{
					string value = RequireValue(args, ref i, arg);
					if (!int.TryParse(value, out _seed)) throw new DisplaySnapshotSetupException($"Invalid modular-fx seed '{value}'.");
					_explicitSeed = true;
				}
				else if (arg == "--direction")
				{
					_directionSlug = RequireValue(args, ref i, arg).ToLowerInvariant();
					if (_directionSlug is not ("left" or "right")) throw new DisplaySnapshotSetupException("Modular-fx direction must be left or right.");
					_explicitDirection = true;
				}
				else if (arg.StartsWith("--", StringComparison.Ordinal))
				{
					throw new DisplaySnapshotSetupException($"Unknown modular-fx option '{arg}'.");
				}
				else if (positional++ == 0)
				{
					_presetSlug = arg.ToLowerInvariant();
				}
				else if (positional == 2)
				{
					_sampleSlug = arg.ToLowerInvariant();
				}
				else
				{
					throw new DisplaySnapshotSetupException($"Unexpected modular-fx argument '{arg}'.");
				}
			}
			_ = ResolveElapsedSeconds(VisualEffectTimingProfileResolver.Resolve(VisualEffectTimingProfile.SnapImpact), _sampleSlug);
		}

		private static string RequireValue(string[] args, ref int index, string option)
		{
			if (++index >= args.Length) throw new DisplaySnapshotSetupException($"Missing value for modular-fx option '{option}'.");
			return args[index];
		}

		private static Guid GuidFromSeed(int seed)
		{
			var bytes = new byte[16];
			uint value = unchecked((uint)seed);
			for (int i = 0; i < bytes.Length; i++)
			{
				value = value * 1664525u + 1013904223u;
				bytes[i] = (byte)(value >> 24);
			}
			return new Guid(bytes);
		}

		private static float ResolveElapsedSeconds(VisualEffectTiming timing, string sampleSlug)
		{
			return sampleSlug switch
			{
				"start" => timing.DurationSeconds * 0.12f,
				"impact" => Math.Min(timing.DurationSeconds - 0.001f, timing.ImpactTimeSeconds + Math.Max(0.04f, timing.DurationSeconds * 0.08f)),
				"late" => timing.DurationSeconds * 0.72f,
				_ => throw new DisplaySnapshotSetupException(
					$"Unknown modular-fx sample '{sampleSlug}'. Expected start, impact, or late.")
			};
		}
	}
}
