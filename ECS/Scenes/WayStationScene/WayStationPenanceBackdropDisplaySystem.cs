using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("WayStation Penance Backdrop")]
	public sealed class WayStationPenanceBackdropDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ContentManager _content;
		private readonly Texture2D _pixel;
		private WayStationPenanceBackdropOverlay _overlay;
		private bool _effectLoadAttempted;
		private Vector2 _cursor;
		private bool _hasCursor;
		private Vector2 _parallaxPixels;
		private float _lifecycleAlpha;

		[DebugEditable(DisplayName = "Base Zoom", Step = 0.001f, Min = 1f, Max = 1.2f)] public float BaseZoom { get; set; } = 1f;
		[DebugEditable(DisplayName = "Modal Zoom", Step = 0.001f, Min = 1f, Max = 1.2f)] public float ModalZoom { get; set; } = 1.004f;
		[DebugEditable(DisplayName = "Base Saturation", Step = 0.01f, Min = 0f, Max = 2f)] public float BaseSaturation { get; set; } = 0.90f;
		[DebugEditable(DisplayName = "Modal Saturation", Step = 0.01f, Min = 0f, Max = 2f)] public float ModalSaturation { get; set; } = 0.62f;
		[DebugEditable(DisplayName = "Base Contrast", Step = 0.01f, Min = 0f, Max = 2f)] public float BaseContrast { get; set; } = 1.02f;
		[DebugEditable(DisplayName = "Modal Contrast", Step = 0.01f, Min = 0f, Max = 2f)] public float ModalContrast { get; set; } = 1.05f;
		[DebugEditable(DisplayName = "Vignette", Step = 0.01f, Min = 0f, Max = 2f)] public float VignetteStrength { get; set; } = 1f;
		[DebugEditable(DisplayName = "Dim", Step = 0.01f, Min = 0f, Max = 2f)] public float DimStrength { get; set; } = 1f;
		[DebugEditable(DisplayName = "Parallax X", Step = 0.0001f, Min = 0f, Max = 0.2f)] public float ParallaxMultiplierX { get; set; } = 0.0018f;
		[DebugEditable(DisplayName = "Parallax Y", Step = 0.0001f, Min = 0f, Max = 0.2f)] public float ParallaxMultiplierY { get; set; } = 0.0018f;
		[DebugEditable(DisplayName = "Parallax Max", Step = 0.1f, Min = 0, Max = 120)] public float ParallaxMaxOffset { get; set; } = 2.4f;
		[DebugEditable(DisplayName = "Parallax Smooth", Step = 0.01f, Min = 0f, Max = 1f)] public float ParallaxSmoothTime { get; set; } = 0.24f;
		[DebugEditable(DisplayName = "Zoom Entrance Seconds", Step = 0.01f, Min = 0.1f, Max = 2f)] public float ZoomEntranceSeconds { get; set; } = 0.8f;
		[DebugEditable(DisplayName = "Zoom Exit Seconds", Step = 0.01f, Min = 0.1f, Max = 2f)] public float ZoomExitSeconds { get; set; } = 0.47f;
		[DebugEditable(DisplayName = "Fallback Edge", Step = 0.01f, Min = 0f, Max = 1f)] public float FallbackEdgeAlpha { get; set; } = 0.42f;

		public WayStationPenanceBackdropDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ContentManager content,
			ImageAssetService assets) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			_pixel = assets.GetPixel(Color.White);
			EventManager.Subscribe<CursorStateEvent>(OnCursorState);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>()?.Current;
			if (scene is not (SceneId.WayStation or SceneId.Snapshot))
			{
				_parallaxPixels = Vector2.Zero;
				_lifecycleAlpha = 0f;
				return;
			}

			float delta = (float)Math.Max(0, gameTime.ElapsedGameTime.TotalSeconds);
			var state = EntityManager.GetEntity(WayStationSceneConstants.ModalRootName)?.GetComponent<WayStationPenanceModalState>();
			float lifecycleProgress = state?.Phase switch
			{
				WayStationPenanceModalPhase.Entering => MathHelper.Clamp(state.ElapsedSeconds / Math.Max(0.001f, ZoomEntranceSeconds), 0f, 1f),
				WayStationPenanceModalPhase.Visible => 1f,
				WayStationPenanceModalPhase.Exiting => 1f - MathHelper.Clamp(state.ElapsedSeconds / Math.Max(0.001f, ZoomExitSeconds), 0f, 1f),
				_ => 0f,
			};
			_lifecycleAlpha = SmootherStep(lifecycleProgress);

			Vector2 target = Vector2.Zero;
			if (_hasCursor)
			{
				Vector2 center = new(Game1.VirtualWidth / 2f, Game1.VirtualHeight / 2f);
				target = new Vector2((center.X - _cursor.X) * ParallaxMultiplierX, (center.Y - _cursor.Y) * ParallaxMultiplierY);
				float length = target.Length();
				if (length > ParallaxMaxOffset && length > 0f) target *= ParallaxMaxOffset / length;
			}
			float smoothing = Math.Max(0f, ParallaxSmoothTime);
			float blend = smoothing <= 0f ? 1f : 1f - MathF.Exp(-delta / smoothing);
			_parallaxPixels = Vector2.Lerp(_parallaxPixels, target, MathHelper.Clamp(blend, 0f, 1f));

			if (_lifecycleAlpha > 0f && ShaderRuntimeOptions.ShadersEnabled) EnsureEffectLoaded();
		}

		public void DrawUnderlay(Action drawUnderlay)
		{
			if (drawUnderlay == null) return;
			if (_lifecycleAlpha <= 0.001f)
			{
				drawUnderlay();
				return;
			}

			if (!ShaderRuntimeOptions.ShadersEnabled || _overlay?.IsAvailable != true)
			{
				drawUnderlay();
				DrawFallback();
				return;
			}

			CompositeUnderlay(drawUnderlay);
		}

		private void CompositeUnderlay(Action drawUnderlay)
		{
			RenderTargetBinding[] previousTargets = _graphicsDevice.GetRenderTargets();
			var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
			_spriteBatch.End();
			using var sourceLease = FullScreenRenderTargetPool.Acquire(
				_graphicsDevice,
				Game1.Display.RenderWidth,
				Game1.Display.RenderHeight,
				RenderTargetUsage.PreserveContents);

			try
			{
				_graphicsDevice.SetRenderTarget(sourceLease.Target);
				_graphicsDevice.Clear(Color.Transparent);
				_spriteBatch.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.AnisotropicClamp, DepthStencilState.None, state.RasterizerState, null, Game1.Display.SpriteBatchTransform);
				drawUnderlay();
				_spriteBatch.End();

				SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, previousTargets);
				ConfigureOverlay();
				_overlay.Begin(_spriteBatch);
				_overlay.Draw(_spriteBatch, sourceLease.Target);
				_overlay.End(_spriteBatch);
			}
			finally
			{
				SpriteBatchRenderTargetCompositor.RestoreRenderTargets(_graphicsDevice, previousTargets);
				_graphicsDevice.Textures[0] = null;
				_graphicsDevice.SamplerStates[0] = state.SamplerState;
				_graphicsDevice.ScissorRectangle = state.ScissorRectangle;
				SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(_graphicsDevice, _spriteBatch, state);
			}
		}

		private void ConfigureOverlay()
		{
			_overlay.LifecycleAlpha = _lifecycleAlpha;
			_overlay.BaseZoom = BaseZoom;
			_overlay.ModalZoom = ModalZoom;
			_overlay.BaseSaturation = BaseSaturation;
			_overlay.ModalSaturation = ModalSaturation;
			_overlay.BaseContrast = BaseContrast;
			_overlay.ModalContrast = ModalContrast;
			_overlay.ParallaxOffset = new Vector2(
				_parallaxPixels.X / Math.Max(1f, Game1.VirtualWidth),
				_parallaxPixels.Y / Math.Max(1f, Game1.VirtualHeight));
			_overlay.VignetteStrength = Math.Max(0f, VignetteStrength);
			_overlay.DimStrength = Math.Max(0f, DimStrength);
		}

		private void DrawFallback()
		{
			float alpha = _lifecycleAlpha;
			for (int y = 0; y < Game1.VirtualHeight; y += 4)
			{
				float normalized = y / (float)Game1.VirtualHeight;
				float dim = normalized < 0.18f
					? MathHelper.Lerp(0.78f, 0.42f, normalized / 0.18f)
					: normalized < 0.80f
						? MathHelper.Lerp(0.42f, 0.46f, (normalized - 0.18f) / 0.62f)
						: MathHelper.Lerp(0.46f, 0.86f, (normalized - 0.80f) / 0.20f);
				_spriteBatch.Draw(_pixel, new Rectangle(0, y, Game1.VirtualWidth, 4), Color.Black * (dim * DimStrength * alpha));
			}
			int layers = 12;
			for (int i = 0; i < layers; i++)
			{
				int insetX = i * 34;
				int insetY = i * 20;
				var rect = new Rectangle(insetX, insetY, Game1.VirtualWidth - insetX * 2, Game1.VirtualHeight - insetY * 2);
				WayStationPenanceDraw.Border(_spriteBatch, _pixel, rect, Color.Black * (FallbackEdgeAlpha * alpha / layers), 20);
			}
		}

		private void EnsureEffectLoaded()
		{
			if (_effectLoadAttempted) return;
			_effectLoadAttempted = true;
			try
			{
				_overlay = new WayStationPenanceBackdropOverlay(_content.Load<Effect>("Shaders/WayStationPenanceBackdrop"));
			}
			catch (Exception ex)
			{
				LoggingService.Append("WayStationPenanceBackdropDisplaySystem.EffectLoadFailed", new JsonObject { ["Message"] = ex.Message });
			}
		}

		private void OnCursorState(CursorStateEvent evt)
		{
			_cursor = evt.Position;
			_hasCursor = true;
		}

		private static float SmootherStep(float value)
		{
			value = MathHelper.Clamp(value, 0f, 1f);
			return value * value * value * (value * (value * 6f - 15f) + 10f);
		}
	}
}
