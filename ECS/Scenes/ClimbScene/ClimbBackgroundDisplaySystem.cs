using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Climb Background")]
	public class ClimbBackgroundDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ContentManager _content;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private const int LayeredBackgroundTextureSlotCount = 3;
		private LayeredHolesOverlay _overlay;
		private bool _overlayFailed;
		private bool _wasClimbScene;
		private float _timeSeconds;

		[DebugEditable(DisplayName = "Background Dim Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float BackgroundDimAlpha { get; set; } = 0f;

		[DebugEditable(DisplayName = "Background Anchor Y", Step = 1, Min = -400, Max = 400)]
		public int BackgroundAnchorY { get; set; } = 0;

		[DebugEditable(DisplayName = "Time Scale", Step = 0.01f, Min = 0f, Max = 5f)]
		public float TimeScale { get; set; } = 0.5f;

		[DebugEditable(DisplayName = "Hole Count", Step = 1, Min = 1, Max = 30)]
		public int HoleCount { get; set; } = 5;

		[DebugEditable(DisplayName = "Period Min", Step = 0.01f, Min = 0.01f, Max = 60f)]
		public float HolePeriodMin { get; set; } = 10f;

		[DebugEditable(DisplayName = "Period Max", Step = 0.01f, Min = 0.01f, Max = 60f)]
		public float HolePeriodMax { get; set; } = 20f;

		[DebugEditable(DisplayName = "Life Min", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float HoleLifeMin { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Life Max", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float HoleLifeMax { get; set; } = 0.75f;

		[DebugEditable(DisplayName = "Open Frac", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float HoleOpenFrac { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Close Frac", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float HoleCloseFrac { get; set; } = 0.45f;

		[DebugEditable(DisplayName = "Radius Min", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float HoleRadiusMin { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Radius Max", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float HoleRadiusMax { get; set; } = 0.50f;

		[DebugEditable(DisplayName = "Radius Flux Amp", Step = 0.01f, Min = 0f, Max = 1f)]
		public float RadiusFluxAmp { get; set; } = 0.13f;

		[DebugEditable(DisplayName = "Radius Flux Rate", Step = 0.01f, Min = 0f, Max = 10f)]
		public float RadiusFluxRate { get; set; } = 2.3f;

		[DebugEditable(DisplayName = "Hole Margin", Step = 0.01f, Min = 0f, Max = 0.50f)]
		public float HoleMargin { get; set; } = 0.02f;

		[DebugEditable(DisplayName = "Hole Feather", Step = 0.01f, Min = 0.001f, Max = 0.20f)]
		public float HoleFeather { get; set; } = 0.071f;

		[DebugEditable(DisplayName = "Feather Vary", Step = 0.01f, Min = 0f, Max = 1f)]
		public float FeatherVary { get; set; } = 0.70f;

		[DebugEditable(DisplayName = "Rim Warp Amp", Step = 0.01f, Min = 0f, Max = 1f)]
		public float RimWarpAmp { get; set; } = 0f;

		[DebugEditable(DisplayName = "Rim Warp Scale", Step = 0.01f, Min = 0.01f, Max = 20f)]
		public float RimWarpScale { get; set; } = 2.13f;

		[DebugEditable(DisplayName = "Rim Warp Speed", Step = 0.01f, Min = 0f, Max = 2f)]
		public float RimWarpSpeed { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Reveal Refract", Step = 0.01f, Min = 0f, Max = 2f)]
		public float RevealRefract { get; set; } = 0f;

		[DebugEditable(DisplayName = "Reveal Darken", Step = 0.01f, Min = 0f, Max = 1f)]
		public float RevealDarken { get; set; } = 0f;

		public ClimbBackgroundDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			bool isClimbScene = IsClimbScene();
			if (!isClimbScene)
			{
				_wasClimbScene = false;
				_timeSeconds = 0f;
				return;
			}

			if (!_wasClimbScene)
			{
				_timeSeconds = 0f;
			}

			_wasClimbScene = true;
			var plan = BuildLayerPlan(GetEncounterSlots());
			if (plan.UseShader && ShaderRuntimeOptions.ShadersEnabled)
			{
				EnsureOverlayLoaded();
				_timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds)
					* MathHelper.Max(0f, TimeScale);
			}
		}

		public void Draw()
		{
			Draw(undimmed: false);
		}

		public void Draw(bool undimmed)
		{
			if (!IsClimbScene()) return;

			var dest = new Rectangle(0, 0, Game1.VirtualWidth, Game1.VirtualHeight);
			var plan = BuildLayerPlan(GetEncounterSlots());
			var drawDestination = GetBackgroundDestination(dest, LoadClimbBackground(plan.TopLocation));
			bool drewBackground = false;

			if (plan.UseShader && ShaderRuntimeOptions.ShadersEnabled && _overlay?.IsAvailable == true)
			{
				var top = LoadClimbBackground(plan.TopLocation);
				var middle = LoadClimbBackground(plan.MiddleLocation);
				var bottom = LoadClimbBackground(plan.BottomLocation);
				if (top != null && middle != null && bottom != null)
				{
					DrawLayeredBackground(top, middle, bottom, drawDestination, plan.LayerSplit);
					drewBackground = true;
				}
			}

			if (!drewBackground)
			{
				DrawStaticBackground(plan.TopLocation, dest);
			}

			if (!undimmed)
			{
				_spriteBatch.Draw(_pixel, dest, Color.Black * MathHelper.Clamp(BackgroundDimAlpha, 0f, 1f));
			}
		}

		internal static ClimbBackgroundLayerPlan BuildLayerPlan(IEnumerable<ClimbEncounterSlotSave> slots)
		{
			var locations = new List<BattleLocation>(3);
			if (slots != null)
			{
				foreach (var slot in slots)
				{
					if (slot == null || slot.isCompleted || string.IsNullOrWhiteSpace(slot.enemyId)) continue;

					BattleLocation location = NormalizeClimbBackgroundLocation(slot.battleLocation);
					if (locations.Contains(location)) continue;

					locations.Add(location);
					if (locations.Count >= 3) break;
				}
			}

			if (locations.Count == 0)
			{
				locations.Add(BattleLocation.Desert);
			}

			return new ClimbBackgroundLayerPlan(locations.ToArray());
		}

		private void DrawLayeredBackground(Texture2D top, Texture2D middle, Texture2D bottom, Rectangle destination, float layerSplit)
		{
			ConfigureOverlay(middle, bottom, layerSplit);

			BlendState savedBlend = _graphicsDevice.BlendState;
			var savedSamplers = CaptureSamplerStates();
			var savedTextures = CaptureTextureSlots();
			DepthStencilState savedDepth = _graphicsDevice.DepthStencilState;
			RasterizerState savedRasterizer = _graphicsDevice.RasterizerState;

			_spriteBatch.End();
			bool overlayBatchStarted = false;
			try
			{
				_overlay.Begin(_spriteBatch);
				overlayBatchStarted = true;
				_overlay.Draw(_spriteBatch, top, destination);
			}
			finally
			{
				try
				{
					if (overlayBatchStarted)
					{
						_overlay.End(_spriteBatch);
					}
				}
				finally
				{
					RestoreTextureSlots(savedTextures);
					ClearExtraTextureSlots();
					RestoreSamplerStates(savedSamplers);
					_spriteBatch.Begin(
						SpriteSortMode.Immediate,
						savedBlend,
						savedSamplers[0],
						savedDepth,
						savedRasterizer,
						null,
						Game1.Display.SpriteBatchTransform);
				}
			}
		}

		private Texture[] CaptureTextureSlots()
		{
			var textures = new Texture[LayeredBackgroundTextureSlotCount];
			for (int i = 0; i < textures.Length; i++)
			{
				textures[i] = _graphicsDevice.Textures[i];
			}

			return textures;
		}

		private SamplerState[] CaptureSamplerStates()
		{
			var samplers = new SamplerState[LayeredBackgroundTextureSlotCount];
			for (int i = 0; i < samplers.Length; i++)
			{
				samplers[i] = _graphicsDevice.SamplerStates[i];
			}

			return samplers;
		}

		private void RestoreTextureSlots(Texture[] textures)
		{
			for (int i = 0; i < textures.Length; i++)
			{
				_graphicsDevice.Textures[i] = textures[i];
			}
		}

		private void ClearExtraTextureSlots()
		{
			for (int i = 1; i < LayeredBackgroundTextureSlotCount; i++)
			{
				_graphicsDevice.Textures[i] = null;
			}
		}

		private void RestoreSamplerStates(SamplerState[] samplers)
		{
			for (int i = 0; i < samplers.Length; i++)
			{
				_graphicsDevice.SamplerStates[i] = samplers[i];
			}
		}

		private void DrawStaticBackground(BattleLocation location, Rectangle dest)
		{
			var background = LoadClimbBackground(location) ?? LoadClimbBackground(BattleLocation.Desert);
			if (background != null)
			{
				_spriteBatch.Draw(background, GetBackgroundDestination(dest, background), Color.White);
			}
			else
			{
				_spriteBatch.Draw(_pixel, dest, ClimbSceneDrawHelpers.Black1);
			}
		}

		private Rectangle GetBackgroundDestination(Rectangle dest, Texture2D background)
		{
			if (background == null) return dest;

			float scale = Math.Max(dest.Width / (float)background.Width, dest.Height / (float)background.Height);
			int width = (int)Math.Ceiling(background.Width * scale);
			int height = (int)Math.Ceiling(background.Height * scale);
			return new Rectangle((dest.Width - width) / 2, BackgroundAnchorY, width, height);
		}

		private void ConfigureOverlay(Texture2D middle, Texture2D bottom, float layerSplit)
		{
			_overlay.Time = _timeSeconds;
			_overlay.HoleCount = Math.Clamp(HoleCount, 1, 30);
			_overlay.HolePeriodMin = Math.Min(HolePeriodMin, HolePeriodMax);
			_overlay.HolePeriodMax = Math.Max(HolePeriodMin, HolePeriodMax);
			_overlay.HoleLifeMin = MathHelper.Clamp(Math.Min(HoleLifeMin, HoleLifeMax), 0.01f, 1f);
			_overlay.HoleLifeMax = MathHelper.Clamp(Math.Max(HoleLifeMin, HoleLifeMax), 0.01f, 1f);
			_overlay.HoleOpenFrac = MathHelper.Clamp(HoleOpenFrac, 0.01f, 1f);
			_overlay.HoleCloseFrac = MathHelper.Clamp(HoleCloseFrac, 0.01f, 1f);
			_overlay.HoleRadiusMin = Math.Max(0.001f, Math.Min(HoleRadiusMin, HoleRadiusMax));
			_overlay.HoleRadiusMax = Math.Max(0.001f, Math.Max(HoleRadiusMin, HoleRadiusMax));
			_overlay.RadiusFluxAmp = MathHelper.Clamp(RadiusFluxAmp, 0f, 1f);
			_overlay.RadiusFluxRate = Math.Max(0f, RadiusFluxRate);
			_overlay.HoleMargin = MathHelper.Clamp(HoleMargin, 0f, 0.50f);
			_overlay.HoleFeather = MathHelper.Clamp(HoleFeather, 0.001f, 0.20f);
			_overlay.FeatherVary = MathHelper.Clamp(FeatherVary, 0f, 1f);
			_overlay.RimWarpAmp = MathHelper.Clamp(RimWarpAmp, 0f, 1f);
			_overlay.RimWarpScale = Math.Max(0.01f, RimWarpScale);
			_overlay.RimWarpSpeed = Math.Max(0f, RimWarpSpeed);
			_overlay.RevealRefract = MathHelper.Clamp(RevealRefract, 0f, 2f);
			_overlay.LayerSplit = MathHelper.Clamp(layerSplit, 0f, 1f);
			_overlay.RevealDarken = MathHelper.Clamp(RevealDarken, 0f, 1f);
			_overlay.MiddleTexture = middle;
			_overlay.BottomTexture = bottom;
		}

		private bool EnsureOverlayLoaded()
		{
			if (_overlayFailed) return false;
			if (_overlay != null) return _overlay.IsAvailable;

			try
			{
				var effect = _content.Load<Effect>("Shaders/LayeredHoles");
				_overlay = new LayeredHolesOverlay(effect);
			}
			catch (Exception exception)
			{
				_overlayFailed = true;
				LoggingService.Append(
					"ClimbBackgroundDisplaySystem.EnsureOverlayLoaded",
					new System.Text.Json.Nodes.JsonObject
					{
						["error"] = "Failed to load shader",
						["exception"] = exception.Message,
					});
			}

			return _overlay?.IsAvailable == true;
		}

		private Texture2D LoadClimbBackground(BattleLocation location)
		{
			return _imageAssets.TryGetTexture(BattleLocationAssetService.GetClimbBackgroundAsset(location));
		}

		private IEnumerable<ClimbEncounterSlotSave> GetEncounterSlots()
		{
			return SaveCache.GetClimbState()?.encounterSlots ?? Enumerable.Empty<ClimbEncounterSlotSave>();
		}

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}

		private static BattleLocation NormalizeClimbBackgroundLocation(BattleLocation location)
		{
			return location switch
			{
				BattleLocation.Tundra => BattleLocation.Tundra,
				BattleLocation.Jungle => BattleLocation.Jungle,
				BattleLocation.Volcano => BattleLocation.Volcano,
				BattleLocation.Gothic => BattleLocation.Gothic,
				_ => BattleLocation.Desert,
			};
		}
	}

	internal readonly struct ClimbBackgroundLayerPlan
	{
		private readonly BattleLocation[] _locations;

		public ClimbBackgroundLayerPlan(BattleLocation[] locations)
		{
			_locations = locations == null || locations.Length == 0
				? new[] { BattleLocation.Desert }
				: locations;
		}

		public IReadOnlyList<BattleLocation> Locations => _locations ?? Array.Empty<BattleLocation>();
		public int LocationCount => _locations?.Length ?? 0;
		public bool UseShader => LocationCount > 1;
		public BattleLocation TopLocation => LocationCount > 0 ? _locations[0] : BattleLocation.Desert;
		public BattleLocation MiddleLocation => LocationCount > 1 ? _locations[1] : TopLocation;
		public BattleLocation BottomLocation => LocationCount > 2 ? _locations[2] : MiddleLocation;
		public float LayerSplit => LocationCount == 2 ? 1f : 0.5f;
	}
}
