using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Draws the seal.png texture over cards that have the Sealed component.
	/// Also displays the remaining seal count on the seal.
	/// </summary>
	[DebugTab("Seal Display")]
	public class SealDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly GraphicsDevice _graphicsDevice;
		private Texture2D _sealTexture;
		private CardGeometrySettings _settings;
		private float _elapsedTime = 0f;

		[DebugEditable(DisplayName = "Seal Alpha", Step = 5, Min = 0, Max = 255)]
		public int SealAlpha { get; set; } = 255;

		[DebugEditable(DisplayName = "Seal Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float SealScale { get; set; } = 0.75f;

		[DebugEditable(DisplayName = "Seal Offset X", Step = 1f, Min = -100f, Max = 100f)]
		public float SealOffsetX { get; set; } = 0f;

		[DebugEditable(DisplayName = "Seal Offset Y", Step = 1f, Min = -100f, Max = 100f)]
		public float SealOffsetY { get; set; } = 0f;

		[DebugEditable(DisplayName = "Icon Rotation Speed", Step = 0.1f, Min = 0f, Max = 10f)]
		public float IconRotationSpeed { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Pulse Speed", Step = 0.1f, Min = 0f, Max = 10f)]
		public float PulseSpeed { get; set; } = 0.6f;

		[DebugEditable(DisplayName = "Min Alpha", Step = 5, Min = 0, Max = 255)]
		public int MinAlpha { get; set; } = 90;

		[DebugEditable(DisplayName = "Max Alpha", Step = 5, Min = 0, Max = 255)]
		public int MaxAlpha { get; set; } = 165;

		[DebugEditable(DisplayName = "Text Scale", Step = 0.01f, Min = 0.05f, Max = 3.0f)]
		public float TextScale { get; set; } = 0.15f;

		[DebugEditable(DisplayName = "Text Offset Y", Step = 1f, Min = -100f, Max = 100f)]
		public float TextOffsetY { get; set; } = -82f;

		public SealDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, Texture2D sealTexture)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_sealTexture = sealTexture;
			EventManager.Subscribe<CardRenderEvent>(OnCardRenderEvent);
			EventManager.Subscribe<CardRenderScaledEvent>(OnCardRenderScaledEvent);
			EventManager.Subscribe<CardRenderScaledRotatedEvent>(OnCardRenderScaledRotatedEvent);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Sealed>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			_elapsedTime += (float)gameTime.ElapsedGameTime.TotalSeconds;
		}

		public void Draw()
		{
			// Drawing is handled via event subscriptions
		}

		private void OnCardRenderEvent(CardRenderEvent evt)
		{
			if (!ShouldRenderSeal(evt.Card)) return;

			var ui = evt.Card.GetComponent<UIElement>();
			if (ui == null) return;

			var geometry = CardGeometryService.GetVisualGeometry(EntityManager, evt.Card, evt.Position);
			var center = geometry.Center;
			center.X += SealOffsetX * geometry.Scale;
			center.Y += SealOffsetY * geometry.Scale;

			var sealedComp = evt.Card.GetComponent<Sealed>();
			DrawSealOverlay(center, geometry.Bounds.Width, geometry.Bounds.Height, geometry.Scale, geometry.Rotation, sealedComp?.Seals ?? 0);
		}

		private void OnCardRenderScaledEvent(CardRenderScaledEvent evt)
		{
			if (!ShouldRenderSeal(evt.Card)) return;
			LoggingService.Append("SealDisplaySystem.OnCardRenderScaledEvent", new System.Text.Json.Nodes.JsonObject
			{
				["cardId"] = evt.Card?.Id ?? -1,
				["scale"] = evt.Scale,
				["seals"] = evt.Card?.GetComponent<Sealed>()?.Seals ?? 0
			});
			using var clip = CardRenderClipScope.Apply(_graphicsDevice, evt.ClipRect);

			var geometry = CardGeometryService.GetVisualGeometry(
				EntityManager,
				evt.Card,
				evt.Position,
				evt.Scale,
				evt.Rotation);
			var center = geometry.Center;
			center.X += SealOffsetX * geometry.Scale;
			center.Y += SealOffsetY * geometry.Scale;

			_settings ??= CardGeometryService.GetSettings(EntityManager);
			int cardWidth = _settings?.CardWidth ?? CardGeometrySettings.DefaultWidth;
			int cardHeight = _settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
			var sealedComp = evt.Card.GetComponent<Sealed>();
			DrawSealOverlay(center, cardWidth, cardHeight, geometry.Scale, evt.Rotation, sealedComp?.Seals ?? 0);
		}

		private void OnCardRenderScaledRotatedEvent(CardRenderScaledRotatedEvent evt)
		{
			if (!ShouldRenderSeal(evt.Card)) return;

			var geometry = CardGeometryService.GetVisualGeometry(
				EntityManager,
				evt.Card,
				evt.Position,
				evt.Scale);
			var center = geometry.Center;
			center.X += SealOffsetX * geometry.Scale;
			center.Y += SealOffsetY * geometry.Scale;

			_settings ??= CardGeometryService.GetSettings(EntityManager);
			int cardWidth = _settings?.CardWidth ?? CardGeometrySettings.DefaultWidth;
			int cardHeight = _settings?.CardHeight ?? CardGeometrySettings.DefaultHeight;
			var sealedComp = evt.Card.GetComponent<Sealed>();
			DrawSealOverlay(center, cardWidth, cardHeight, geometry.Scale, geometry.Rotation, sealedComp?.Seals ?? 0);
		}

		private bool ShouldRenderSeal(Entity card)
		{
			return card != null
				&& card.GetComponent<Sealed>() != null
				&& card.GetComponent<SuppressCardVisualEffects>() == null
				&& _sealTexture != null;
		}

		private void DrawSealOverlay(Vector2 center, float cardWidth, float cardHeight, float scale, float rotation, int seals)
		{
			// Calculate pulsing alpha using sine wave
			float alphaPulse = (float)Math.Sin(_elapsedTime * PulseSpeed * Math.PI) * 0.5f + 0.5f;
			int currentAlpha = (int)MathHelper.Lerp(MinAlpha, MaxAlpha, alphaPulse);
			float finalAlpha = (currentAlpha / 255f) * (SealAlpha / 255f);
			finalAlpha = MathHelper.Clamp(finalAlpha, 0f, 1f);

			// Calculate rotation offset for the seal icon
			float rotationOffset = _elapsedTime * IconRotationSpeed;

			// Use uniform scale (no stretching) - fit to card width
			float uniformScale = (cardWidth / (float)_sealTexture.Width) * SealScale * scale;

			// Draw the seal texture centered on the card with uniform scale and rotation
			_spriteBatch.Draw(
				_sealTexture,
				center,
				null,
				Color.White * finalAlpha,
				rotation + rotationOffset,
				new Vector2(_sealTexture.Width / 2f, _sealTexture.Height / 2f),
				uniformScale,
				SpriteEffects.None,
				0f
			);

			// Draw seal count text at bottom right of card (without rotation offset)
			DrawSealCount(center, cardWidth, cardHeight, scale, rotation, seals);
		}

		private void DrawSealCount(Vector2 center, float cardWidth, float cardHeight, float scale, float rotation, int seals)
		{
			var font = FontSingleton.ContentFont;
			if (font == null) return;

			string text = $"{seals}";
			var textSize = font.MeasureString(text);
			float textScaleFinal = TextScale * scale;

			// Calculate text position at bottom right of card (before rotation)
			float padding = 8f * scale;
			var localOffset = new Vector2(
				(cardWidth * scale / 2f) - (textSize.X * textScaleFinal) - padding,
				(cardHeight * scale / 2f) - (textSize.Y * textScaleFinal) - padding + TextOffsetY * scale
			);

			// Rotate the offset around the center
			float cos = (float)Math.Cos(rotation);
			float sin = (float)Math.Sin(rotation);
			var rotatedOffset = new Vector2(
				localOffset.X * cos - localOffset.Y * sin,
				localOffset.X * sin + localOffset.Y * cos
			);

			var textPos = center + rotatedOffset;

			// Color #00ffbf (cyan/teal)
			var textColor = new Color(0x00, 0xff, 0xbf);

			// Draw text with shadow for readability, rotated to match card
			var shadowOffset = new Vector2(2 * scale, 2 * scale);
			// Rotate shadow offset too
			var rotatedShadow = new Vector2(
				shadowOffset.X * cos - shadowOffset.Y * sin,
				shadowOffset.X * sin + shadowOffset.Y * cos
			);
			_spriteBatch.DrawString(font, text, textPos + rotatedShadow, Color.Black * 0.7f, rotation, Vector2.Zero, textScaleFinal, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(font, text, textPos, textColor, rotation, Vector2.Zero, textScaleFinal, SpriteEffects.None, 0f);
		}
	}
}
