using System;
using System.Linq;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.Diagnostics;
using System.Collections.Generic;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Archival player HUD renderer. PlayerHudCourageDisplaySystem is the registered implementation.
	///
	/// Renders the player's Courage as a red circle with a white outline and centered white text,
	/// anchored just below the player portrait.
	/// </summary>
	[DebugTab("Courage Display")]
	public class CourageDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private readonly Dictionary<(int radius, uint edgeKey), Texture2D> _circleTextures = new();
		private readonly Dictionary<(int outer, int thickness), Texture2D> _ringTextures = new();

		// Debug-adjustable fields
		[DebugEditable(DisplayName = "Circle Radius", Step = 1, Min = 1, Max = 300)]
		public int CircleRadius { get; set; } = 20;

		[DebugEditable(DisplayName = "Outline Thickness", Step = 1, Min = 1, Max = 50)]
		public int OutlineThickness { get; set; } = 2;

		[DebugEditable(DisplayName = "HP Bar Right Padding", Step = 1, Min = -128, Max = 128)]
		public int HpBarRightPadding { get; set; } = 8;

		[DebugEditable(DisplayName = "Vertical Offset From Anchor", Step = 2, Min = -1000, Max = 1000)]
		public int AnchorOffsetY { get; set; } = 224;

		[DebugEditable(DisplayName = "Text Scale Divisor", Step = 1, Min = 1, Max = 200)]
		public int TextScaleDivisor { get; set; } = 88;

		[DebugEditable(DisplayName = "Text Offset X", Step = 1, Min = -500, Max = 500)]
		public int TextOffsetX { get; set; } = 0;

		[DebugEditable(DisplayName = "Text Offset Y", Step = 1, Min = -500, Max = 500)]
		public int TextOffsetY { get; set; } = 0;

		public CourageDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			EventManager.Subscribe<ModifyCourageEvent>(OnModifyCourage);
		}

		private void OnModifyCourage(ModifyCourageEvent evt)
		{
			var hover = EntityManager.GetEntitiesWithComponent<CourageTooltipAnchor>().FirstOrDefault();
			if (hover != null)
			{
				LoggingService.Append("CourageDisplaySystem.OnModifyCourage", new System.Text.Json.Nodes.JsonObject { ["delta"] = evt.Delta });
				EventManager.Publish(new JigglePulseEvent { Target = hover, Config = JigglePulseConfig.Default });
			}
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			// Render courage for the first entity that has Player and Courage
			return EntityManager.GetEntitiesWithComponent<Player>()
				.Where(e => e.HasComponent<Courage>());
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			var playerEntity = GetRelevantEntities().FirstOrDefault();
			if (playerEntity == null) return;

			var courage = playerEntity.GetComponent<Courage>();
			if (courage == null) return;

			// Find the portrait anchor (created by PlayerDisplaySystem)
			var anchor = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (anchor == null) return;
			var anchorTransform = anchor.GetComponent<Transform>();
			var portraitInfo = anchor.GetComponent<PortraitInfo>();
			if (anchorTransform == null || portraitInfo == null || _font == null) return;

			// Fully decouple from portrait breathing and UI scaling: fixed pixel size and fixed offset
			int radius = Math.Max(1, CircleRadius);
			int outlineThickness = Math.Max(1, OutlineThickness);
			if (radius < outlineThickness + 2) radius = outlineThickness + 2; // ensure inner radius stays positive

			// Compute center position. Prefer anchoring to the right of the player's HP bar if available;
			// fall back to portrait anchor with a vertical offset.
			Vector2 center;
			var hpAnchor = playerEntity.GetComponent<HPBarAnchor>();
			if (hpAnchor != null && hpAnchor.Rect.Width > 0 && hpAnchor.Rect.Height > 0)
			{
				int xRight = hpAnchor.Rect.X + hpAnchor.Rect.Width;
				int yMid = hpAnchor.Rect.Y + hpAnchor.Rect.Height / 2;
				center = new Vector2(xRight + Math.Max(-128, HpBarRightPadding) + radius, yMid);
			}
			else
			{
				center = new Vector2(anchorTransform.Position.X, anchorTransform.Position.Y + AnchorOffsetY);
			}

			// Get pulse transform state from the anchor entity
			var hover = EntityManager.GetEntitiesWithComponent<CourageTooltipAnchor>().FirstOrDefault();
			float rotation = 0f;
			Vector2 scale = Vector2.One;
			if (hover != null)
			{
				var ht = hover.GetComponent<Transform>();
				if (ht != null)
				{
					rotation = ht.Rotation;
					scale = ht.Scale;
				}
			}

			// Fetch cached filled circle textures of the required radii
			var outerTex = GetOrCreateCircleTexture(radius, Color.White);
			if (outerTex == null) return;

			// Draw outer white ring (true circular outline)
			var ringTex = GetOrCreateRingTexture(radius, outlineThickness);
			if (ringTex != null)
			{
				_spriteBatch.Draw(
					ringTex,
					position: center,
					sourceRectangle: null,
					color: Color.White,
					rotation: rotation,
					origin: new Vector2(radius, radius),
					scale: scale,
					effects: SpriteEffects.None,
					layerDepth: 0f
				);
			}

			// Draw inner red fill
			int innerRadius = Math.Max(1, radius - outlineThickness);
			var innerTex = GetOrCreateCircleTexture(innerRadius, Color.Red);
			if (innerTex != null)
			{
				_spriteBatch.Draw(
					innerTex,
					position: center,
					sourceRectangle: null,
					color: Color.Red,
					rotation: rotation,
					origin: new Vector2(innerRadius, innerRadius),
					scale: scale,
					effects: SpriteEffects.None,
					layerDepth: 0f
				);
			}

			// Draw centered text (courage amount) in white
			string text = Math.Max(0, courage.Amount).ToString();
			float baseTextScale = Math.Min(1.0f, innerRadius / Math.Max(1f, TextScaleDivisor)); // heuristic scale with size
			Vector2 textOrigin = _font.MeasureString(text) / 2f;
			Vector2 textPos = new Vector2(center.X + TextOffsetX, center.Y + TextOffsetY);
			_spriteBatch.DrawString(_font, text, textPos, Color.White, rotation, textOrigin, baseTextScale * scale, SpriteEffects.None, 0f);

			// Update hoverable UI element bounds over the courage circle for tooltip (entity pre-created in factory)
			if (hover != null)
			{
				int diameter = radius * 2;
				var hitRect = new Rectangle((int)(center.X - radius), (int)(center.Y - radius), diameter, diameter);
				var ui = hover.GetComponent<UIElement>();
				if (ui != null) ui.Bounds = hitRect;
				var ht = hover.GetComponent<Transform>();
				if (ht != null) ht.Position = new Vector2(hitRect.X, hitRect.Y);
			}
		}

		private Texture2D GetOrCreateCircleTexture(int radius, Color edgeColor)
		{
			if (radius <= 0) return null;
			var key = (radius, edgeColor.PackedValue);
			if (_circleTextures.TryGetValue(key, out var tex) && tex != null) return tex;
			tex = CreateFilledCircleTexture(_graphicsDevice, radius, edgeColor);
			_circleTextures[key] = tex;
			return tex;
		}

		private Texture2D GetOrCreateRingTexture(int outerRadius, int thickness)
		{
			if (outerRadius <= 0 || thickness <= 0) return null;
			var key = (outerRadius, thickness);
			if (_ringTextures.TryGetValue(key, out var tex) && tex != null) return tex;
			tex = CreateRingTexture(_graphicsDevice, outerRadius, thickness);
			_ringTextures[key] = tex;
			return tex;
		}

		private static Texture2D CreateFilledCircleTexture(GraphicsDevice device, int radius, Color edgeColor)
		{
			int diameter = radius * 2;
			var texture = new Texture2D(device, diameter, diameter);
			var data = new Color[diameter * diameter];
			int rSq = radius * radius;
			for (int y = 0; y < diameter; y++)
			{
				int dy = y - radius;
				for (int x = 0; x < diameter; x++)
				{
					int dx = x - radius;
					int idx = y * diameter + x;
					bool inside = (dx * dx + dy * dy) <= rSq;
					if (inside)
					{
						data[idx] = Color.White; // opaque fill; final tint applied at draw time
					}
					else
					{
						// Fully transparent outside the circle to avoid any solid square fill
						data[idx] = Color.Transparent;
					}
				}
			}
			texture.SetData(data);
			return texture;
		}

		private static Texture2D CreateRingTexture(GraphicsDevice device, int outerRadius, int thickness)
		{
			int diameter = outerRadius * 2;
			int innerRadius = Math.Max(1, outerRadius - thickness);
			int outerSq = outerRadius * outerRadius;
			int innerSq = innerRadius * innerRadius;
			var texture = new Texture2D(device, diameter, diameter);
			var data = new Color[diameter * diameter];
			for (int y = 0; y < diameter; y++)
			{
				int dy = y - outerRadius;
				for (int x = 0; x < diameter; x++)
				{
					int dx = x - outerRadius;
					int d2 = dx * dx + dy * dy;
					int idx = y * diameter + x;
					if (d2 <= outerSq && d2 >= innerSq)
					{
						data[idx] = Color.White;
					}
					else
					{
						data[idx] = Color.Transparent;
					}
				}
			}
			texture.SetData(data);
			return texture;
		}
	}
}

