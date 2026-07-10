using System;
using System.Text.Json.Nodes;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Draws the frost.png texture over cards that have the Frozen component.
	/// </summary>
	[DebugTab("Frozen Display")]
	public class FrozenCardDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly GraphicsDevice _graphicsDevice;
		private Texture2D _frostTexture;
		private CardGeometrySettings _settings;

		[DebugEditable(DisplayName = "Frost Alpha", Step = 5, Min = 0, Max = 255)]
		public int FrostAlpha { get; set; } = 255;

		[DebugEditable(DisplayName = "Frost Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float FrostScale { get; set; } = 1.0f;

		[DebugEditable(DisplayName = "Frost Offset X", Step = 1f, Min = -100f, Max = 100f)]
		public float FrostOffsetX { get; set; } = 0f;

		[DebugEditable(DisplayName = "Frost Offset Y", Step = 1f, Min = -100f, Max = 100f)]
		public float FrostOffsetY { get; set; } = 0f;

		private float _elapsedTime = 0f;

	public FrozenCardDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D frostTexture)
		: base(entityManager)
	{
		_graphicsDevice = graphicsDevice;
		_spriteBatch = spriteBatch;
		_frostTexture = frostTexture;
		// Subscribe to CardRenderEvent to draw frost overlay right after each card is drawn
		EventManager.Subscribe<CardRenderEvent>(evt => FrameProfiler.Measure("FrozenCardDisplaySystem.OnCardRenderEvent", () => OnCardRenderEvent(evt)));
		// Subscribe to CardRenderScaledEvent for cards in modals/overlays
		EventManager.Subscribe<CardRenderScaledEvent>(evt => {
			LoggingService.Append("FrozenCardDisplaySystem.OnCardRenderScaledEvent", new JsonObject {
				{ "CardId", evt.Card?.Name },
				{ "Scale", evt.Scale }
			});
			FrameProfiler.Measure("FrozenCardDisplaySystem.OnCardRenderScaledEvent", () => OnCardRenderScaledEvent(evt));
		});
		EventManager.Subscribe<CardRenderScaledRotatedEvent>(evt =>
			FrameProfiler.Measure(
				"FrozenCardDisplaySystem.OnCardRenderScaledRotatedEvent",
				() => OnCardRenderScaledRotatedEvent(evt)));
	}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Frozen>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			// Track time for pulse animation
			if (entity == null) return;
			_elapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
		}

		public void Draw()
		{
		}

	private void OnCardRenderEvent(CardRenderEvent evt)
	{
		if (!ShouldRenderFrost(evt.Card)) return;

		var geometry = CardGeometryService.GetVisualGeometry(EntityManager, evt.Card, evt.Position);
		var center = geometry.Center;
		center.X += FrostOffsetX * geometry.Scale;
		center.Y += FrostOffsetY * geometry.Scale;

		DrawFrostOverlay(center, geometry.Bounds.Width, geometry.Bounds.Height, geometry.Scale, geometry.Rotation);
	}

	private void OnCardRenderScaledEvent(CardRenderScaledEvent evt)
	{
		if (!ShouldRenderFrost(evt.Card)) return;
		using var clip = CardRenderClipScope.Apply(_graphicsDevice, evt.ClipRect);

		var geometry = CardGeometryService.GetVisualGeometry(
			EntityManager,
			evt.Card,
			evt.Position,
			evt.Scale,
			evt.Rotation);
		var center = geometry.Center;
		center.X += FrostOffsetX * geometry.Scale;
		center.Y += FrostOffsetY * geometry.Scale;

		_settings ??= CardGeometryService.GetSettings(EntityManager);
		int cardWidth = _settings?.CardWidth ?? CardGeometrySettings.DefaultWidth;
		int cardHeight = _settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
		DrawFrostOverlay(center, cardWidth, cardHeight, geometry.Scale, evt.Rotation);
	}

	private void OnCardRenderScaledRotatedEvent(CardRenderScaledRotatedEvent evt)
	{
		if (!ShouldRenderFrost(evt.Card)) return;

		var geometry = CardGeometryService.GetVisualGeometry(
			EntityManager,
			evt.Card,
			evt.Position,
			evt.Scale);
		var center = geometry.Center;
		center.X += FrostOffsetX * geometry.Scale;
		center.Y += FrostOffsetY * geometry.Scale;

		_settings ??= CardGeometryService.GetSettings(EntityManager);
		int cardWidth = _settings?.CardWidth ?? CardGeometrySettings.DefaultWidth;
		int cardHeight = _settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
		DrawFrostOverlay(center, cardWidth, cardHeight, geometry.Scale, geometry.Rotation);
	}

	private bool ShouldRenderFrost(Entity card)
	{
		return !ShaderRuntimeOptions.ShadersEnabled
			&& card != null
			&& card.GetComponent<Frozen>() != null
			&& card.GetComponent<SuppressCardVisualEffects>() == null
			&& _frostTexture != null;
	}

	private void DrawFrostOverlay(Vector2 center, float cardWidth, float cardHeight, float scale, float rotation)
	{
		// Calculate final alpha
		float finalAlpha = FrostAlpha / 255f;
		finalAlpha = MathHelper.Clamp(finalAlpha, 0f, 1f);

		// Calculate final scale based on card size and scale factor
		float scaleX = (cardWidth / (float)_frostTexture.Width) * FrostScale * scale;
		float scaleY = (cardHeight / (float)_frostTexture.Height) * FrostScale * scale;
		var spriteScale = new Vector2(scaleX, scaleY);

		// Draw the frost texture centered on the card
		_spriteBatch.Draw(
			_frostTexture,
			center,
			null,
			Color.White * finalAlpha,
			rotation,
			new Vector2(_frostTexture.Width / 2f, _frostTexture.Height / 2f),
			spriteScale,
			SpriteEffects.None,
			0f
		);
	}
	}
}
