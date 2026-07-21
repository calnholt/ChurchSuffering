using System;
using System.Linq;
using ChurchSuffering.ECS.Core;

using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Factories;
using ChurchSuffering.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ChurchSuffering.Diagnostics;
using System.Collections.Generic;
using ChurchSuffering.ECS.Singletons;

namespace ChurchSuffering.ECS.Systems
{
	/// <summary>
	/// Archival player HUD renderer. PlayerHudTemperanceDisplaySystem is the registered implementation.
	///
	/// Displays the player's Temperance as a chunked meter composed of slanted parallelogram chunks.
	/// Number of chunks equals the equipped Temperance ability's threshold. Filled chunks are white with
	/// black outline; empty chunks are black with black outline and a white slant indicator.
	/// Positioned next to the Courage display using the portrait anchor for reference.
	/// </summary>
	[DebugTab("Temperance Display")]
	public class TemperanceDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font = FontSingleton.ContentFont;
		private readonly System.Collections.Generic.Dictionary<string, Texture2D> _chunkFilledCache = new();
		private readonly System.Collections.Generic.Dictionary<string, Texture2D> _chunkEmptyCache = new();

		// Debug-adjustable fields
		[DebugEditable(DisplayName = "Chunk Width", Step = 1, Min = 4, Max = 512)]
		public int ChunkWidth { get; set; } = 14;
		[DebugEditable(DisplayName = "Chunk Height", Step = 1, Min = 4, Max = 512)]
		public int ChunkHeight { get; set; } = 22;
		[DebugEditable(DisplayName = "Chunk Gap", Step = 1, Min = -128, Max = 128)]
		public int ChunkGap { get; set; } = 0;
		[DebugEditable(DisplayName = "Chunk Slant (px)", Step = 1, Min = 0, Max = 128)]
		public int ChunkSlant { get; set; } = 16;

		[DebugEditable(DisplayName = "Outline Thickness", Step = 1, Min = 1, Max = 50)]
		public int OutlineThickness { get; set; } = 2;

		[DebugEditable(DisplayName = "HP Bar Left Padding", Step = 1, Min = -128, Max = 128)]
		public int HpBarLeftPadding { get; set; } = 4;

		[DebugEditable(DisplayName = "Anchor Offset X", Step = 2, Min = -2000, Max = 2000)]
		public int AnchorOffsetX { get; set; } = -180; // to the right of Courage by default

		[DebugEditable(DisplayName = "Anchor Offset Y", Step = 2, Min = -2000, Max = 2000)]
		public int AnchorOffsetY { get; set; } = 188; // align with Courage default

		public TemperanceDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			EventManager.Subscribe<ModifyTemperanceEvent>(OnModifyTemperance);
			EventManager.Subscribe<SetTemperanceEvent>(OnSetTemperance);
		}

		private void OnModifyTemperance(ModifyTemperanceEvent evt)
		{
			LoggingService.Append("TemperanceDisplaySystem.OnModifyTemperance", new System.Text.Json.Nodes.JsonObject
			{
				["delta"] = evt.Delta
			});
			TriggerPulse();
		}

		private void OnSetTemperance(SetTemperanceEvent evt)
		{
			LoggingService.Append("TemperanceDisplaySystem.OnSetTemperance", new System.Text.Json.Nodes.JsonObject
			{
				["amount"] = evt.Amount
			});
			TriggerPulse();
		}

		private void TriggerPulse()
		{
			var hover = EntityManager.GetEntitiesWithComponent<TemperanceTooltipAnchor>().FirstOrDefault();
			if (hover != null)
			{
				EventManager.Publish(new JigglePulseEvent { Target = hover, Config = JigglePulseConfig.Default });
			}
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Player>().Where(e => e.HasComponent<Temperance>());
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			var player = GetRelevantEntities().FirstOrDefault();
			if (player == null) return;
			var temperance = player.GetComponent<Temperance>();
			if (temperance == null) return;

			var anchor = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (anchor == null) return;
			var t = anchor.GetComponent<Transform>();
			var info = anchor.GetComponent<PortraitInfo>();
			if (t == null || info == null) return;

			// Get pulse transform state from the anchor entity
			var hover = EntityManager.GetEntitiesWithComponent<TemperanceTooltipAnchor>().FirstOrDefault();
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

			int outline = Math.Max(1, OutlineThickness);
			int wChunk = Math.Max(4, ChunkWidth);
			int hChunk = Math.Max(4, ChunkHeight);
			int slant = Math.Max(0, ChunkSlant);

			Vector2 center; // computed after threshold is known

			// Resolve threshold from equipped ability (default to 3 if unavailable)
			int threshold = 3;
			var equipped = player.GetComponent<EquippedTemperanceAbility>();
			if (equipped != null && !string.IsNullOrEmpty(equipped.AbilityId))
			{
				var ability = TemperanceFactory.Create(equipped.AbilityId);
				if (ability != null)
				{
					threshold = Math.Max(1, ability.Threshold);
				}
			}
			int filled = Math.Max(0, Math.Min(temperance.Amount, threshold));

			// Position to the left of the player's HP bar using HPBarAnchor if available; fall back to portrait anchor
			var playerEntity = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			var hpAnchor = playerEntity?.GetComponent<HPBarAnchor>();
			if (hpAnchor != null && hpAnchor.Rect.Width > 0 && hpAnchor.Rect.Height > 0)
			{
				// Compute preview width using the same formula as actual draw so the right edge aligns
				int meterWidthPreview = threshold * wChunk + Math.Max(0, ChunkSlant) + Math.Max(0, (threshold - 1) * Math.Max(0, ChunkGap));
				int xLeft = hpAnchor.Rect.X - Math.Max(-128, HpBarLeftPadding) - meterWidthPreview; // padding left of HP bar
				int yMid = hpAnchor.Rect.Y + hpAnchor.Rect.Height / 2;
				center = new Vector2(xLeft + meterWidthPreview / 2f, yMid);
			}
			else
			{
				center = new Vector2(t.Position.X + AnchorOffsetX, t.Position.Y + AnchorOffsetY);
			}

			// Prepare textures for chunks
			int texW = wChunk + slant; // bounding width of slanted shape
			int texH = hChunk;
			string key = $"{texW}x{texH}s{slant}o{outline}";
			var texFilled = GetOrCreateParallelogramTexture(key + ":filled", texW, texH, slant, Color.White, Color.Black, false);
			var texEmpty = GetOrCreateParallelogramTexture(key + ":empty:slant", texW, texH, slant, Color.Black, Color.Black, true);
			var texEmptyFirst = GetOrCreateParallelogramTexture(key + ":empty:noslant", texW, texH, slant, Color.Black, Color.Black, false);
			if (texFilled == null || texEmpty == null) return;

			// Layout horizontally centered on center.X
			int stepX = wChunk + ChunkGap; // step equals visible width plus configured gap; when gap=0, chunks butt together
			int totalW = threshold * wChunk + Math.Max(0, ChunkSlant) + Math.Max(0, (threshold - 1) * ChunkGap);
			int startX = (int)Math.Round(center.X - totalW / 2f);
			int startY = (int)Math.Round(center.Y - texH / 2f);

			Vector2 origin = new Vector2(texW / 2f, texH / 2f);

			for (int i = 0; i < threshold; i++)
			{
				int x = startX + i * stepX;
				// Base center position of this chunk
				Vector2 chunkCenter = new Vector2(x + texW / 2f, startY + texH / 2f);

				// Offset from meter center
				Vector2 relPos = chunkCenter - center;

				// Rotate and scale the offset
				Vector2 transformedRelPos = Vector2.Transform(relPos * scale, Matrix.CreateRotationZ(rotation));
				Vector2 drawPos = center + transformedRelPos;

				var tex = (i < filled) ? texFilled : (i == 0 ? texEmptyFirst : texEmpty);
				_spriteBatch.Draw(tex, drawPos, null, Color.White, rotation, origin, scale, SpriteEffects.None, 0f);
			}

			// Update hoverable UI element bounds over the temperance meter for tooltip (entity pre-created in factory)
			if (hover != null)
			{
				var hitRect = new Rectangle(startX, startY, totalW, texH);
				var ui = hover.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.Bounds = hitRect;
					// Tooltip content is initialized in the factory; bounds only are updated here
				}
				var ht = hover.GetComponent<Transform>();
				if (ht != null) ht.Position = new Vector2(hitRect.X, hitRect.Y);
			}
		}

		private Texture2D GetOrCreateParallelogramTexture(string key, int width, int height, int slant, Color fill, Color outline, bool drawWhiteSlantOnEmpty)
		{
			var dict = (fill == Color.White) ? _chunkFilledCache : _chunkEmptyCache;
			if (dict.TryGetValue(key, out var existing) && existing != null) return existing;
			var tex = new Texture2D(_graphicsDevice, width, height);
			var data = new Color[width * height];
			for (int y = 0; y < height; y++)
			{
				// left edge shifts right as y decreases (top slants right)
				float t = (height <= 1) ? 0f : (1f - (y / (float)(height - 1)));
				int left = (int)Math.Round(slant * t);
				int right = left + (width - 1 - slant);
				for (int x = 0; x < width; x++)
				{
					int idx = y * width + x;
					bool inside = (x >= left && x <= right);
					if (!inside) { data[idx] = Color.Transparent; continue; }
					bool isOutline = (x == left || x == right || y == 0 || y == height - 1);
					if (isOutline)
					{
						data[idx] = outline;
					}
					else
					{
						data[idx] = fill;
					}
				}
			}
			// Optional white slant indicator on empty chunk: draw a diagonal along the slanted edge
			if (drawWhiteSlantOnEmpty && fill == Color.Black)
			{
				for (int yy = 1; yy < height - 1; yy++)
				{
					float t = (height <= 1) ? 0f : (1f - (yy / (float)(height - 1)));
					int xDiag = (int)Math.Round(slant * t) + 1;
					int idx = yy * width + Math.Max(1, Math.Min(width - 2, xDiag));
					data[idx] = Color.White;
				}
			}
			tex.SetData(data);
			dict[key] = tex;
			return tex;
		}
	}
}
