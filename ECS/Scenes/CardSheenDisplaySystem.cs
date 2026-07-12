using System;
using System.Collections.Generic;
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

namespace Crusaders30XX.ECS.Systems;

[DebugTab("Card Sheen")]
public sealed class CardSheenDisplaySystem : Core.System
{
	private const int RenderPriority = -200;

	private readonly GraphicsDevice _graphicsDevice;
	private readonly SpriteBatch _spriteBatch;
	private readonly ContentManager _content;
	private readonly Texture2D _pixel;

	private Effect _effect;
	private CardSheenOverlay _overlay;
	private bool _failed;
	private float _timeSeconds;
	private readonly HashSet<int> _locallyCompositedCards = new();

	[DebugEditable(DisplayName = "Angle Degrees", Step = 1f, Min = 0f, Max = 360f)]
	public float AngleDegrees { get; set; } = 105f;

	[DebugEditable(DisplayName = "Band Width", Step = 0.01f, Min = 0.01f, Max = 0.5f)]
	public float BandWidthNormalized { get; set; } = 0.11f;

	[DebugEditable(DisplayName = "Feather", Step = 0.01f, Min = 0.001f, Max = 0.3f)]
	public float FeatherNormalized { get; set; } = 0.06f;

	[DebugEditable(DisplayName = "Core Width", Step = 0.01f, Min = 0.001f, Max = 0.2f)]
	public float CoreWidthNormalized { get; set; } = 0.025f;

	[DebugEditable(DisplayName = "Corner Radius Px", Step = 1f, Min = 0f, Max = 80f)]
	public float CornerRadiusPx { get; set; } = 11f;

	[DebugEditable(DisplayName = "Intensity", Step = 0.01f, Min = 0f, Max = 2f)]
	public float Intensity { get; set; } = 0.55f;

	[DebugEditable(DisplayName = "Sheen Duration Seconds", Step = 0.01f, Min = 0.01f, Max = 3f)]
	public float SheenDurationSeconds { get; set; } = 0.84f;

	[DebugEditable(DisplayName = "Repeat Delay Seconds", Step = 0.01f, Min = 0f, Max = 10f)]
	public float RepeatDelaySeconds { get; set; } = 2f;

	[DebugEditable(DisplayName = "Gold Fringe Strength", Step = 0.01f, Min = 0f, Max = 1f)]
	public float GoldFringeStrength { get; set; } = 0.28f;

	[DebugEditable(DisplayName = "Blue Fringe Strength", Step = 0.01f, Min = 0f, Max = 1f)]
	public float BlueFringeStrength { get; set; } = 0.18f;

	[DebugEditable(DisplayName = "Core Strength", Step = 0.01f, Min = 0f, Max = 1f)]
	public float CoreStrength { get; set; } = 0.55f;

	public CardSheenDisplaySystem(
		EntityManager entityManager,
		GraphicsDevice graphicsDevice,
		SpriteBatch spriteBatch,
		ContentManager content,
		ImageAssetService imageAssets)
		: base(entityManager)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		_content = content;
		_pixel = imageAssets.GetPixel(Color.White);

		EventManager.Subscribe<CardBaseRenderCompletedEvent>(OnCardBaseRenderCompleted, RenderPriority);
		EventManager.Subscribe<CardShaderPassEvent>(OnCardShaderPass, 50);
		EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
	}

	protected override IEnumerable<Entity> GetRelevantEntities()
	{
		return EntityManager.GetEntitiesWithComponent<CardSheen>();
	}

	protected override void UpdateEntity(Entity entity, GameTime gameTime)
	{
		var sheen = entity.GetComponent<CardSheen>();
		if (sheen == null) return;
		if (!sheen.IsActive)
		{
			sheen.HasActivationTime = false;
			return;
		}

		if (!sheen.HasActivationTime)
		{
			sheen.ActivationTimeSeconds = _timeSeconds;
			sheen.HasActivationTime = true;
		}
	}

	public override void Update(GameTime gameTime)
	{
		_timeSeconds += MathHelper.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
		base.Update(gameTime);

		if (!ShaderRuntimeOptions.ShadersEnabled || _failed || _overlay != null) return;
		foreach (var entity in GetRelevantEntities())
		{
			EnsureLoaded();
			return;
		}
	}

	private void OnCardBaseRenderCompleted(CardBaseRenderCompletedEvent evt)
	{
		if (evt?.Card != null && _locallyCompositedCards.Remove(evt.Card.Id)) return;
		if (!ShaderRuntimeOptions.ShadersEnabled || _overlay?.IsAvailable != true || evt?.Card == null) return;
		var sheen = evt.Card.GetComponent<CardSheen>();
		if (sheen?.IsActive != true || !sheen.HasActivationTime) return;

		var geometry = CardGeometryService.GetVisualGeometry(
			EntityManager,
			evt.Card,
			evt.Position,
			evt.Scale,
			evt.Rotation);

		ConfigureOverlay(sheen, evt.Scale);

		DrawOverlay(geometry.Center, new Vector2(geometry.Bounds.Width, geometry.Bounds.Height), evt.Rotation);
	}

	private void OnCardShaderPass(CardShaderPassEvent evt)
	{
		CardShaderPassContext context = evt?.Context;
		if (context == null || !ShaderRuntimeOptions.ShadersEnabled || !EnsureLoaded()) return;
		CardSheen sheen = context.Card.GetComponent<CardSheen>();
		if (sheen?.IsActive != true || !sheen.HasActivationTime) return;

		CardVisualGeometry geometry = CardGeometryService.GetVisualGeometry(
			EntityManager,
			context.Card,
			context.Position,
			context.Scale,
			context.Rotation);
		ConfigureOverlay(sheen, context.Scale);
		_overlay.Resolution = context.LogicalSize;
		_overlay.CardCenter = context.ToSurface(geometry.Center);
		_overlay.CardRotation = context.Rotation;
		Vector2 size = new(Math.Max(1f, geometry.Bounds.Width), Math.Max(1f, geometry.Bounds.Height));
		context.Apply("CardSheen", (spriteBatch, source) =>
		{
			_overlay.BeginComposite(spriteBatch);
			_overlay.DrawComposite(spriteBatch, source, size);
			_overlay.End(spriteBatch);
		});
		_locallyCompositedCards.Add(context.Card.Id);
	}

	private void ConfigureOverlay(CardSheen sheen, float scale)
	{
		_overlay.TimeSeconds = MathHelper.Max(0f, _timeSeconds - sheen.ActivationTimeSeconds);
		_overlay.SheenDurationSeconds = MathHelper.Max(0.001f, SheenDurationSeconds);
		_overlay.RepeatDelaySeconds = MathHelper.Max(0f, RepeatDelaySeconds);
		_overlay.AngleRadians = MathHelper.ToRadians(AngleDegrees);
		_overlay.BandWidthNormalized = BandWidthNormalized;
		_overlay.FeatherNormalized = FeatherNormalized;
		_overlay.CoreWidthNormalized = CoreWidthNormalized;
		_overlay.CornerRadiusPx = CornerRadiusPx * scale;
		_overlay.Intensity = Intensity;
		_overlay.GoldFringeColor = new Vector3(1f, 240f / 255f, 164f / 255f) * GoldFringeStrength;
		_overlay.BlueFringeColor = new Vector3(101f / 255f, 209f / 255f, 1f) * BlueFringeStrength;
		_overlay.CoreColor = new Vector3(1f, 254f / 255f, 240f / 255f) * CoreStrength;
	}

	private void DrawOverlay(Vector2 center, Vector2 size, float rotation)
	{
		var state = SpriteBatchRenderTargetCompositor.CaptureState(_graphicsDevice);
		Texture previousTexture = _graphicsDevice.Textures[0];
		bool sceneBatchEnded = false;
		bool effectBatchBegun = false;
		try
		{
			_spriteBatch.End();
			sceneBatchEnded = true;
			_overlay.Begin(_spriteBatch);
			effectBatchBegun = true;
			_overlay.Draw(_spriteBatch, _pixel, center, size, rotation);
			_overlay.End(_spriteBatch);
			effectBatchBegun = false;
		}
		catch (Exception exception)
		{
			LoggingService.Append("CardSheenDisplaySystem.DrawOverlay", new JsonObject
			{
				["error"] = "Failed to draw card sheen",
				["exception"] = exception.Message,
			});
			_failed = true;
		}
		finally
		{
			if (effectBatchBegun)
			{
				try { _spriteBatch.End(); }
				catch { }
			}

			if (sceneBatchEnded)
			{
				_graphicsDevice.BlendState = state.BlendState;
				_graphicsDevice.SamplerStates[0] = state.SamplerState;
				_graphicsDevice.DepthStencilState = state.DepthStencilState;
				_graphicsDevice.RasterizerState = state.RasterizerState;
				_graphicsDevice.ScissorRectangle = state.ScissorRectangle;
				_graphicsDevice.Textures[0] = previousTexture;
				SpriteBatchRenderTargetCompositor.RestoreSpriteBatch(
					_graphicsDevice,
					_spriteBatch,
					state);
				// Prime MonoGame's stock SpriteEffect after leaving a custom effect batch.
				// Without this near-transparent draw, DrawString can retain the prior pixel shader
				// when several sheen passes occur back-to-back.
				_spriteBatch.Draw(_pixel, Vector2.Zero, new Color(0, 0, 0, 1));
			}
		}
	}

	private bool EnsureLoaded()
	{
		if (!ShaderRuntimeOptions.ShadersEnabled || _failed) return false;
		try
		{
			_effect ??= _content.Load<Effect>("Shaders/CardSheen");
			_overlay ??= new CardSheenOverlay(_effect);
			return _overlay.IsAvailable;
		}
		catch (Exception exception)
		{
			LoggingService.Append("CardSheenDisplaySystem.EnsureLoaded", new JsonObject
			{
				["error"] = "Failed to load shader",
				["exception"] = exception.Message,
			});
			_failed = true;
			return false;
		}
	}

	private void OnDeleteCaches(DeleteCachesEvent evt)
	{
		_effect = null;
		_overlay = null;
		_failed = false;
	}
}
