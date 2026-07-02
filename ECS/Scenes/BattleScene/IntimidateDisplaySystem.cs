using System;
using System.Linq;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.ECS.Singletons;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Draws "INTIMIDATED!" diagonally over cards that have the Intimidated component.
	/// </summary>
	[DebugTab("Intimidate Display")]
	public class IntimidateDisplaySystem : Core.System
	{
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private readonly GraphicsDevice _graphicsDevice;
		private Texture2D _pixel;
		private readonly System.Collections.Generic.Dictionary<(int w, int h, int r), Texture2D> _roundedRectCache = new();
		private CardGeometrySettings _settings;
		private readonly System.Collections.Generic.Dictionary<int, AnimState> _animByEntityId = new();

		[DebugEditable(DisplayName = "Text Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float TextScale { get; set; } = 0.3f;

		[DebugEditable(DisplayName = "Rotation (Degrees)", Step = 1f, Min = -180f, Max = 180f)]
		public float RotationDegrees { get; set; } = -55f;

		[DebugEditable(DisplayName = "Overlay Alpha", Step = 5, Min = 0, Max = 255)]
		public int OverlayAlpha { get; set; } = 155;

		[DebugEditable(DisplayName = "Overlay Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int OverlayCornerRadius { get; set; } = 10;

		[DebugEditable(DisplayName = "Shadow Red", Step = 5, Min = 0, Max = 255)]
		public int ShadowRed { get; set; } = 0;

		[DebugEditable(DisplayName = "Shadow Green", Step = 5, Min = 0, Max = 255)]
		public int ShadowGreen { get; set; } = 0;

		[DebugEditable(DisplayName = "Shadow Blue", Step = 5, Min = 0, Max = 255)]
		public int ShadowBlue { get; set; } = 0;

		[DebugEditable(DisplayName = "Shadow Offset X", Step = 0.5f, Min = -10f, Max = 10f)]
		public float ShadowOffsetX { get; set; } = 5f;

		[DebugEditable(DisplayName = "Shadow Offset Y", Step = 0.5f, Min = -10f, Max = 10f)]
		public float ShadowOffsetY { get; set; } = 0f;

		// Animation settings
		[DebugEditable(DisplayName = "Slam Duration (s)", Step = 0.01f, Min = 0.01f, Max = 1.0f)]
		public float SlamDurationSeconds { get; set; } = 1.0f;
		[DebugEditable(DisplayName = "Slam Start Scale", Step = 0.05f, Min = 0.05f, Max = 2.0f)]
		public float SlamStartScale { get; set; } = 0.25f;
		[DebugEditable(DisplayName = "Slam Overshoot Scale", Step = 0.05f, Min = 1.0f, Max = 2.5f)]
		public float SlamOvershootScale { get; set; } = 1.25f;
		[DebugEditable(DisplayName = "Base Scale After Slam", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float SlamEndBaseScale { get; set; } = 1.0f;
		[DebugEditable(DisplayName = "Slam Start Alpha", Step = 0.05f, Min = 0.0f, Max = 1.0f)]
		public float SlamStartAlpha { get; set; } = 0.0f;
		[DebugEditable(DisplayName = "Slam End Alpha", Step = 0.05f, Min = 0.0f, Max = 1.0f)]
		public float SlamEndAlpha { get; set; } = 1.0f;
		[DebugEditable(DisplayName = "Idle Pulse Scale Amp", Step = 0.005f, Min = 0.0f, Max = 0.5f)]
		public float IdlePulseScaleAmplitude { get; set; } = 0.02f;
		[DebugEditable(DisplayName = "Idle Pulse Alpha Amp", Step = 0.01f, Min = 0.0f, Max = 1.0f)]
		public float IdlePulseAlphaAmplitude { get; set; } = 0.10f;
		[DebugEditable(DisplayName = "Idle Pulse Speed (Hz)", Step = 0.05f, Min = 0.05f, Max = 3.0f)]
		public float IdlePulseSpeedHz { get; set; } = 0.15f;

		public string IntimidateText { get; set; } = "INTIMIDATED!";

		private class AnimState
		{
			public float Elapsed;
			public float CurrentScale = 1f;
			public float CurrentAlpha = 1f;
		}

		public IntimidateDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch) 
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			
			// Create a single pixel texture for backgrounds/overlays
			_pixel = new Texture2D(_graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });

			// Draw intimidate overlay/text right after each card is drawn so higher-Z cards can occlude it
			EventManager.Subscribe<CardRenderEvent>(evt => FrameProfiler.Measure("IntimidateDisplaySystem.OnCardRenderEvent", () => OnCardRenderEvent(evt)));
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Intimidated>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			if (entity == null) return;
			float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
			if (!_animByEntityId.TryGetValue(entity.Id, out var anim))
			{
				anim = new AnimState();
				_animByEntityId[entity.Id] = anim;
			}
			anim.Elapsed += dt;
			float idleT = anim.Elapsed - SlamDurationSeconds;
			float w = MathHelper.TwoPi * MathHelper.Clamp(IdlePulseSpeedHz, 0.01f, 10f);
			float osc = (float)Math.Sin(idleT * w);
			anim.CurrentScale = SlamEndBaseScale * (1f + IdlePulseScaleAmplitude * osc);
			float pulse = (1f + osc) * 0.5f; // 0..1
			float alphaPulse = MathHelper.Lerp(1f - IdlePulseAlphaAmplitude * 0.5f, 1f + IdlePulseAlphaAmplitude * 0.5f, pulse);
			anim.CurrentAlpha = MathHelper.Clamp(SlamEndAlpha * alphaPulse, 0f, 1f);
		}

	public void Draw()
	{
		var intimidatedCards = GetRelevantEntities().ToList();
		if (intimidatedCards.Count == 0) return;

		{
			var alive = new System.Collections.Generic.HashSet<int>(intimidatedCards.Select(e => e.Id));
			foreach (var key in _animByEntityId.Keys.ToList())
			{
				if (!alive.Contains(key)) _animByEntityId.Remove(key);
			}
		}
	}

	private void OnCardRenderEvent(CardRenderEvent evt)
		{
			// Only draw overlay for intimidated cards
			var card = evt.Card;
			if (card == null
				|| card.GetComponent<Intimidated>() == null
				|| card.GetComponent<SuppressCardVisualEffects>() != null) return;
			var ui = card.GetComponent<UIElement>();
			if (ui == null) return;

			var geometry = CardGeometryService.GetVisualGeometry(EntityManager, card, evt.Position);
			var bounds = geometry.Bounds;
			var center = geometry.Center;

			// Measure text size (unscaled) so the origin remains centered regardless of TextScale
			var textSizeUnscaled = _font.MeasureString(IntimidateText);

			// Get or create rounded rect texture for overlay (match the card's visual radius)
			_settings ??= CardGeometryService.GetSettings(EntityManager);
			int baseRadiusSetting = _settings?.CardCornerRadius ?? CardGeometrySettings.DefaultCornerRadius;
			int baseRadius = OverlayCornerRadius > 0
				? (int)Math.Round(OverlayCornerRadius * geometry.Scale)
				: (int)Math.Round(baseRadiusSetting * geometry.Scale);
			var overlayKey = (bounds.Width, bounds.Height, baseRadius);
			if (!_roundedRectCache.TryGetValue(overlayKey, out var roundedRect))
			{
				roundedRect = RoundedRectTextureFactory.CreateRoundedRect(
					_graphicsDevice,
					bounds.Width,
					bounds.Height,
					baseRadius
				);
				_roundedRectCache[overlayKey] = roundedRect;
			}

			// Draw semi-transparent dark overlay with rounded corners on top of this card
			var overlayColor = new Color(0, 0, 0, OverlayAlpha);
			_spriteBatch.Draw(
				roundedRect,
				center,
				null,
				overlayColor,
				geometry.Rotation,
				new Vector2(bounds.Width / 2f, bounds.Height / 2f),
				1f,
				SpriteEffects.None,
				0f
			);

			// Calculate rotation (combine card rotation with the intimidate rotation)
			float rotation = geometry.Rotation + MathHelper.ToRadians(RotationDegrees);

			// Text position (centered on card)
			var textOrigin = textSizeUnscaled / 2f;

			// Resolve animated scale and alpha
			_animByEntityId.TryGetValue(card.Id, out var anim);
			float scaleNow = TextScale * geometry.Scale * (anim?.CurrentScale > 0f ? anim.CurrentScale : 1f);
			float alphaNow = MathHelper.Clamp(anim?.CurrentAlpha ?? 1f, 0f, 1f);
			var shadowOffset = new Vector2(ShadowOffsetX * geometry.Scale, ShadowOffsetY * geometry.Scale);

			// Draw shadow/bold layer first
			var shadowColor = new Color(ShadowRed, ShadowGreen, ShadowBlue) * alphaNow;
			_spriteBatch.DrawString(
				_font,
				IntimidateText,
				center + shadowOffset,
				shadowColor,
				rotation,
				textOrigin,
				scaleNow,
				SpriteEffects.None,
				0f
			);

			// Draw text with main color
			_spriteBatch.DrawString(
				_font,
				IntimidateText,
				center,
				Color.DarkRed * alphaNow,
				rotation,
				textOrigin,
				scaleNow,
				SpriteEffects.None,
				0f
			);
		}
	}
}
