using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Location Select")]
	public class LocationSelectDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly SpriteFont _font;
		private readonly ContentManager _content;
		private readonly Dictionary<string, Texture2D> _textureCache = new();
		private readonly Dictionary<string, Entity> _locationEntitiesById = new();
		private Texture2D _pixel;
		private Texture2D _customizeRoundedCache;


		[DebugEditable(DisplayName = "Tile Size (% of min screen dim)", Step = 0.005f, Min = 0.05f, Max = 0.9f)]
		public float TileSize { get; set; } = 0.25f;

		[DebugEditable(DisplayName = "Tile Padding (fraction of tile)", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float TilePadding { get; set; } = 0.09f;

		[DebugEditable(DisplayName = "Rect Padding (fraction of tile)", Step = 0.01f, Min = 0f, Max = 0.5f)]
		public float RectPadding { get; set; } = 0.03f;

		[DebugEditable(DisplayName = "Rows", Step = 1, Min = 1, Max = 12)]
		public int Rows { get; set; } = 3;

		[DebugEditable(DisplayName = "Columns", Step = 1, Min = 1, Max = 12)]
		public int Columns { get; set; } = 5;

		[DebugEditable(DisplayName = "Total Slots", Step = 1, Min = 1, Max = 100)]
		public int TotalSlots { get; set; } = 15;

		[DebugEditable(DisplayName = "Y Offset (% of screen height)", Step = 0.01f, Min = -1f, Max = 1f)]
		public float YOffset { get; set; } = 0f;

		[DebugEditable(DisplayName = "Label Scale", Step = 0.05f, Min = 0.25f, Max = 2f)]
		public float LabelScale { get; set; } = 0.25f;

		[DebugEditable(DisplayName = "Image Scale", Step = 0.05f, Min = 0.1f, Max = 2f)]
		public float ImageScale { get; set; } = 2.0f;

		[DebugEditable(DisplayName = "Circle Radius (fraction of tile)", Step = 0.01f, Min = 0.05f, Max = 0.5f)]
		public float CircleRadius { get; set; } = 0.16f;

		[DebugEditable(DisplayName = "Circle Offset X (fraction of tile)", Step = 0.01f, Min = -1f, Max = 1f)]
		public float CircleOffsetX { get; set; } = -0.1f;

		[DebugEditable(DisplayName = "Circle Offset Y (fraction of tile)", Step = 0.01f, Min = -1f, Max = 1f)]
		public float CircleOffsetY { get; set; } = 0.1f;

		[DebugEditable(DisplayName = "Locked Circle Scale (radius)", Step = 0.01f, Min = 0.05f, Max = 0.5f)]
		public float LockedCircleScale { get; set; } = 0.35f;

		[DebugEditable(DisplayName = "Locked '?' Scale", Step = 0.05f, Min = 0.2f, Max = 2f)]
		public float LockedQuestionScale { get; set; } = .8f;

		// Customize button debug settings
		[DebugEditable(DisplayName = "Customize Btn Width", Step = 2, Min = 40, Max = 800)]
		public int CustomizeButtonWidth { get; set; } = 200;

		[DebugEditable(DisplayName = "Customize Btn Height", Step = 2, Min = 24, Max = 300)]
		public int CustomizeButtonHeight { get; set; } = 56;

		[DebugEditable(DisplayName = "Customize Btn Margin", Step = 1, Min = 0, Max = 120)]
		public int CustomizeButtonMargin { get; set; } = 16;

		[DebugEditable(DisplayName = "Customize Btn Radius", Step = 1, Min = 0, Max = 64)]
		public int CustomizeButtonCornerRadius { get; set; } = 12;

		[DebugEditable(DisplayName = "Customize Label Scale", Step = 0.05f, Min = 0.1f, Max = 2.0f)]
		public float CustomizeLabelScale { get; set; } = 0.2f;

		public LocationSelectDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content, SpriteFont font)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			_font = font;
			_pixel = new Texture2D(graphicsDevice, 1, 1);
			_pixel.SetData(new[] { Color.White });
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
		}

		protected override System.Collections.Generic.IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>();
		}

		private void OnDeleteCaches(DeleteCachesEvent evt)
		{
			_locationEntitiesById.Clear();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			var scene = entity.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.WorldMap)
			{
				return;
			}

			LayoutAndSyncLocationEntities(Game1.VirtualWidth, Game1.VirtualHeight);

			var i = 0;
			foreach (var kv in _locationEntitiesById)
			{
				var locEnt = kv.Value;
				if (locEnt == null) continue;
				var t = locEnt.GetComponent<Transform>();
				var ui = locEnt.GetComponent<UIElement>();
				if (t != null && ui != null)
				{
					var rect = GetRect(Game1.VirtualWidth, Game1.VirtualHeight, TotalSlots, i);
					var position = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
					t.Position = position;
					i++;
				}
			}
		}

			private Rectangle GetRect(int viewportW, int viewportH, int displayCount, int index)
			{
				int minDim = System.Math.Min(viewportW, viewportH);
				int tile = System.Math.Max(32, (int)System.Math.Round(minDim * MathHelper.Clamp(TileSize, 0.05f, 0.9f)));
				int pad = System.Math.Max(0, (int)System.Math.Round(tile * MathHelper.Clamp(TilePadding, 0f, 0.5f)));
				int cell = tile + pad;
				int cols = System.Math.Max(1, Columns > 0 ? Columns : viewportW / cell);
				int rows = System.Math.Max(1, Rows > 0 ? Rows : (int)System.Math.Ceiling(displayCount / (float)cols));

				int gridW = cols * cell - pad;
				int gridH = rows * cell - pad;
				int startX = (viewportW - gridW) / 2;
				int startY = (viewportH - gridH) / 2 + (int)System.Math.Round(viewportH * MathHelper.Clamp(YOffset, -1f, 1f));
				int r = index / cols;
				int c = index % cols;
				var rect = new Rectangle(startX + c * cell, startY + r * cell, tile, tile);
				return rect;
			}

		private void LayoutAndSyncLocationEntities(int viewportW, int viewportH)
		{
			var all = LocationDefinitionCache.GetAll();
			var list = all?.Values?.OrderBy(d => d?.name ?? d?.id).ToList() ?? new List<LocationDefinition>();
			int displayCount = System.Math.Max(1, TotalSlots);

			for (int i = 0; i < displayCount; i++)
			{
				LocationDefinition def = (i < list.Count) ? list[i] : null;
				var rect = GetRect(viewportW, viewportH, displayCount, i);

				string key = def?.id ?? $"locked_{i}";
				var position = new Vector2(rect.X + rect.Width / 2f, rect.Y + rect.Height / 2f);
				if (!_locationEntitiesById.TryGetValue(key, out var e) || e == null)
				{
					e = EntityManager.CreateEntity($"Location_{key}");
					EntityManager.AddComponent(e, new Transform { Position = position, ZOrder = 0 });
					EntityManager.AddComponent(e, new UIElement { Bounds = rect, IsInteractable = true, TooltipType = TooltipType.Quests });
					EntityManager.AddComponent(e, ParallaxLayer.GetUIParallaxLayer());
					_locationEntitiesById[key] = e;
				}
			}

			// Customize button bottom-right
			EnsureCustomizeButton(viewportW, viewportH);

			// Handle clicks to start selected quest (only for unlocked slots)
			foreach (var kv in _locationEntitiesById)
			{
				var key = kv.Key;
				var ent = kv.Value;
				var ui = ent?.GetComponent<UIElement>();
				if (ui == null) continue;
				if (ui.IsClicked)
				{
					if (key.StartsWith("locked_")) break;
					var def = list.FirstOrDefault(d => d.id == key);
					if (def == null) break;
					bool unlocked = def.id == "desert"; // assume only desert unlocked
					if (!unlocked) break;

					int completed = SaveCache.GetValueOrDefault(key, 0);
					int clampedIdx = System.Math.Max(0, System.Math.Min(completed, (def.pointsOfInterest?.Count ?? 1) - 1));
					var qsEntity = EntityManager.GetEntitiesWithComponent<QuestSelectState>().FirstOrDefault();
					var qs = qsEntity?.GetComponent<QuestSelectState>();
					int chosenIndex = (qs != null && qs.LocationId == key)
						? System.Math.Max(0, System.Math.Min(qs.SelectedQuestIndex, (def.pointsOfInterest?.Count ?? 1) - 1))
						: clampedIdx;

					// Build queued events from selected quest and transition to Battle
					var qeEntity = EntityManager.GetEntitiesWithComponent<QueuedEvents>().FirstOrDefault();
					if (qeEntity == null)
					{
						qeEntity = EntityManager.CreateEntity("QueuedEvents");
						EntityManager.AddComponent(qeEntity, new QueuedEvents());
						EntityManager.AddComponent(qeEntity, new DontDestroyOnLoad());
					}
					var qe = qeEntity.GetComponent<QueuedEvents>();
					qe.CurrentIndex = -1;
					qe.Events.Clear();
					// Record quest context for dialog lookup
					qe.LocationId = def.id;
					qe.QuestIndex = chosenIndex;
					var questDefs = def.pointsOfInterest[chosenIndex];
					foreach (var q in questDefs.events)
					{
						var type = ParseQueuedEventType(q?.type);
						if (!string.IsNullOrEmpty(q?.id))
						{
							var queuedEvent = new QueuedEvent { EventId = q.id, EventType = type };
							if (q.modifications != null)
							{
								queuedEvent.Modifications = new List<EnemyModification>(q.modifications);
							}
							qe.Events.Add(queuedEvent);
						}
					}
					if (qe.Events.Count > 0)
					{
						// Announce the quest that was chosen so global systems can prepare dialog
						EventManager.Publish(new QuestSelected { LocationId = def.id, QuestIndex = chosenIndex, QuestId = questDefs.id });
						EventManager.Publish(new ShowTransition { Scene = SceneId.Battle });
					}
					break;
				}
			}
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.WorldMap) return;

			// Skip drawing when quest overlay is open
			var qsOpen = EntityManager.GetEntitiesWithComponent<QuestSelectState>().FirstOrDefault()?.GetComponent<QuestSelectState>()?.IsOpen ?? false;
			if (qsOpen) return;

			var all = LocationDefinitionCache.GetAll();
			var list = all?.Values?.OrderBy(d => d?.name ?? d?.id).ToList() ?? new List<LocationDefinition>();
			int displayCount = System.Math.Max(1, TotalSlots);

			for (int i = 0; i < 1; i++)
			{
				LocationDefinition def = (i < list.Count) ? list[i] : null;
				string key = def?.id ?? $"locked_{i}";
				if (!_locationEntitiesById.TryGetValue(key, out var e) || e == null) continue;
				var ui = e.GetComponent<UIElement>();
				if (ui == null) continue;
				var tEnt = e.GetComponent<Transform>();
				if (tEnt == null) continue;

				int tileW = ui.Bounds.Width;
				int tileH = ui.Bounds.Height;
				var dst = new Rectangle(
					(int)System.Math.Round(tEnt.Position.X - tileW / 2f),
					(int)System.Math.Round(tEnt.Position.Y - tileH / 2f),
					tileW,
					tileH);
				// Keep UI bounds aligned with parallax-adjusted transform for hit-testing/tooltips
				// ui.Bounds = dst;

				// Container background (rounded rect)
				DrawContainer(dst);

				// Compute inner padded rect for content within the container
				int rectPad = System.Math.Max(0, (int)System.Math.Round(dst.Width * MathHelper.Clamp(RectPadding, 0f, 0.5f)));
				var inner = new Rectangle(dst.X + rectPad, dst.Y + rectPad, System.Math.Max(1, dst.Width - 2 * rectPad), System.Math.Max(1, dst.Height - 2 * rectPad));

				bool unlocked = def != null && def.id == "desert"; // assume only desert unlocked
				if (def != null && unlocked)
				{
					// Tile image
					Texture2D tex = LoadTextureSafe(def.id);
					// Scale proportionally to fit within the tile, preserving aspect ratio
					float scaleFit = System.Math.Min(inner.Width / (float)tex.Width, inner.Height / (float)tex.Height);
					float scale = ImageScale;
					int drawW = System.Math.Max(1, (int)System.Math.Round(tex.Width * scale));
					int drawH = System.Math.Max(1, (int)System.Math.Round(tex.Height * scale));
					int drawX = inner.X + (inner.Width - drawW) / 2;
					int drawY = inner.Y + (inner.Height - drawH) / 2;
					var drawRect = new Rectangle(drawX, drawY, drawW, drawH);
					_spriteBatch.Draw(tex, drawRect, Color.White);
					ui.IsInteractable = true;
					// Label (inside the container)
					string label = def.name ?? def.id;
					var size = _font.MeasureString(label) * LabelScale;
					var pos = new Vector2(inner.X + inner.Width / 2f - size.X / 2f, inner.Bottom - size.Y - 4);
					_spriteBatch.DrawString(_font, label, pos, Color.White, 0f, Vector2.Zero, LabelScale, SpriteEffects.None, 0f);
					// Progress badge in top-right
					DrawProgressBadge(def, dst);
				}
				else
				{
					// Locked or empty slot: draw placeholder if desired; disable interaction
					ui.IsInteractable = false;
					// int minSide = System.Math.Min(inner.Width, inner.Height);
					// int radius = System.Math.Max(4, (int)System.Math.Round(minSide * System.Math.Max(0.05f, System.Math.Min(0.5f, LockedCircleScale))));
					// var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
					// var circleDst = new Rectangle(inner.X + inner.Width / 2 - radius, inner.Y + inner.Height / 2 - radius, radius * 2, radius * 2);
					// _spriteBatch.Draw(circle, circleDst, Color.White);
					// string qm = "?";
					// var qSize = _font.MeasureString(qm);
					// float qScale = System.Math.Min((radius * 1.6f) / System.Math.Max(1f, qSize.X), 1.0f) * System.Math.Max(0.2f, System.Math.Min(2f, LockedQuestionScale));
					// var qPos = new Vector2(circleDst.Center.X - (qSize.X * qScale) / 2f, circleDst.Center.Y - (qSize.Y * qScale) / 2f + 1);
					// _spriteBatch.DrawString(_font, qm, qPos, Color.Black, 0f, Vector2.Zero, qScale, SpriteEffects.None, 0f);
				}
			}

			// Draw Customize button
			DrawCustomizeButton();
		}

		private void DrawContainer(Rectangle dst)
		{
			// Black rounded rect underlay with a faint border
			int radius = System.Math.Max(4, System.Math.Min(dst.Width, dst.Height) / 12);
			var rounded = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, dst.Width, dst.Height, radius);
			_spriteBatch.Draw(rounded, new Rectangle(dst.X, dst.Y, dst.Width, dst.Height), Color.Black);
		}

		private void DrawProgressBadge(LocationDefinition def, Rectangle container)
		{
			int completed = SaveCache.GetValueOrDefault(def.id, 0);
			int total = def?.pointsOfInterest?.Count ?? 0;
			float pct = (total > 0) ? MathHelper.Clamp((completed / (float)total) * 100f, 0f, 100f) : 0f;

			int baseLen = System.Math.Min(container.Width, container.Height);
			int radius = System.Math.Max(4, (int)System.Math.Round(baseLen * MathHelper.Clamp(CircleRadius, 0.05f, 0.5f)));
			var circle = PrimitiveTextureFactory.GetAntiAliasedCircle(_graphicsDevice, radius);
			// Center the circle on the top-right corner of the rounded rectangle, with optional offsets
			int offsetX = (int)System.Math.Round(baseLen * MathHelper.Clamp(CircleOffsetX, -1f, 1f));
			int offsetY = (int)System.Math.Round(baseLen * MathHelper.Clamp(CircleOffsetY, -1f, 1f));
			int cx = container.Right + offsetX;
			int cy = container.Top + offsetY;
			var dst = new Rectangle(cx - radius, cy - radius, radius * 2, radius * 2);
			_spriteBatch.Draw(circle, dst, new Color(110, 0, 0));

			string text = ((int)System.Math.Round(pct)).ToString() + "%";
			var size = _font.MeasureString(text);
			float scale = System.Math.Min((radius * 1.6f) / System.Math.Max(1f, size.X), 0.8f);
			var pos = new Vector2(cx - (size.X * scale) / 2f, cy - (size.Y * scale) / 2f + 1);
			_spriteBatch.DrawString(_font, text, pos, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private void EnsureCustomizeButton(int viewportW, int viewportH)
		{
			int btnW = System.Math.Max(40, CustomizeButtonWidth);
			int btnH = System.Math.Max(24, CustomizeButtonHeight);
			int margin = System.Math.Max(0, CustomizeButtonMargin);
			var rect = new Rectangle(viewportW - btnW - margin, viewportH - btnH - margin, btnW, btnH);
			var ent = EntityManager.GetEntitiesWithComponent<LocationCustomizeButton>().FirstOrDefault();
			if (ent == null)
			{
				ent = EntityManager.CreateEntity("LocationCustomizeButton");
				EntityManager.AddComponent(ent, new LocationCustomizeButton());
				EntityManager.AddComponent(ent, new UIElement { Bounds = rect, IsInteractable = true });
			}
			else
			{
				var ui = ent.GetComponent<UIElement>();
				if (ui == null) EntityManager.AddComponent(ent, new UIElement { Bounds = rect, IsInteractable = true });
				else { ui.Bounds = rect; ui.IsInteractable = true; }
			}
		}

		private void DrawCustomizeButton()
		{
			var ent = EntityManager.GetEntitiesWithComponent<LocationCustomizeButton>().FirstOrDefault();
			var ui = ent?.GetComponent<UIElement>();
			if (ui == null) return;
			var r = ui.Bounds;
			var tex = _customizeRoundedCache;
			if (tex == null || tex.Width != r.Width || tex.Height != r.Height)
			{
				int radius = System.Math.Max(0, CustomizeButtonCornerRadius);
				tex = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, r.Width, r.Height, radius);
				_customizeRoundedCache = tex;
			}
			_spriteBatch.Draw(tex, r, new Color(0, 0, 0));
			string text = "Customize";
			var size = _font.MeasureString(text);
			float scale = CustomizeLabelScale;
			var pos = new Vector2(r.X + r.Width / 2f - (size.X * scale) / 2f, r.Y + r.Height / 2f - (size.Y * scale) / 2f);
			_spriteBatch.DrawString(_font, text, pos, Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
		}

		private Texture2D LoadTextureSafe(string assetName)
		{
			if (string.IsNullOrEmpty(assetName)) return null;
			if (_textureCache.TryGetValue(assetName, out var cached)) return cached;
			try
			{
				var tex = _content.Load<Texture2D>(assetName);
				_textureCache[assetName] = tex;
				return tex;
			}
			catch
			{
				_textureCache[assetName] = null;
				return null;
			}
		}

		private static QueuedEventType ParseQueuedEventType(string type)
		{
			if (string.IsNullOrEmpty(type)) return QueuedEventType.Enemy;
			switch (type.ToLowerInvariant())
			{
				case "enemy": return QueuedEventType.Enemy;
				case "event": return QueuedEventType.Event;
				case "shop": return QueuedEventType.Shop;
				case "church": return QueuedEventType.Church;
				default: return QueuedEventType.Enemy;
			}
		}
	}
}
