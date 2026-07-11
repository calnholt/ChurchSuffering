using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Services;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Displays equipped equipment icons (head/chest/arms/legs) on the left side of the screen,
	/// grouped by type in vertical order: Head, Chest, Arms, Legs. Multiple items of the same
	/// type are drawn in a row for that type.
	/// </summary>
	[Obsolete("Legacy equipment renderer. Use EquipmentDisplaySystem.")]
	[DebugTab("Equipment Display")]
	public class EquipmentDisplaySystemLegacy : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ContentManager _content;
		private SpriteFont _font = FontSingleton.ContentFont;
		private readonly Dictionary<string, Texture2D> _iconCache = new();
		private readonly Dictionary<(int w, int h, int r), Texture2D> _roundedRectCache = new();
		private readonly Dictionary<int, Entity> _tooltipByEquipEntityId = new();

		private readonly Dictionary<int, double> _pulseTimeByEntityId = new();
		private const double PulseDurationSeconds = 0.18; // similar to draw pile
		private const float PulseAmplitude = 0.12f; // 12% size bump
		private double _lastDt = 0.0;
		// Root anchor removed; each equipment item will own its own transform base

		// Layout constants (pixels)
		[DebugEditable(DisplayName = "Left Margin", Step = 2, Min = 0, Max = 2000)]
		public int LeftMargin { get; set; } = 30;
		[DebugEditable(DisplayName = "Top Margin", Step = 2, Min = 0, Max = 2000)]
		public int TopMargin { get; set; } = 200;
		[DebugEditable(DisplayName = "Icon Size", Step = 1, Min = 8, Max = 512)]
		public int IconSize { get; set; } = 60;
		[DebugEditable(DisplayName = "Column Gap", Step = 1, Min = 0, Max = 128)]
		public int ColGap { get; set; } = 8;
		[DebugEditable(DisplayName = "Row Gap", Step = 1, Min = 0, Max = 128)]
		public int RowGap { get; set; } = 12;
		[DebugEditable(DisplayName = "Background Corner Radius", Step = 1, Min = 0, Max = 64)]
		public int BgCornerRadius { get; set; } = 23;
		[DebugEditable(DisplayName = "Background Border Thickness", Step = 1, Min = 0, Max = 16)]
		public int BgBorderThickness { get; set; } = 0;
		[DebugEditable(DisplayName = "Background Padding", Step = 1, Min = 0, Max = 64)]
		public int BgPadding { get; set; } = 28;
		[DebugEditable(DisplayName = "Icon Padding X", Step = 1, Min = 0, Max = 256)]
		public int IconPaddingX { get; set; } = 8;
		[DebugEditable(DisplayName = "Icon Padding Y", Step = 1, Min = 0, Max = 256)]
		public int IconPaddingY { get; set; } = 8;
		[DebugEditable(DisplayName = "Block Text Scale", Step = 0.05f, Min = 0.2f, Max = 3f)]
		public float BlockTextScale { get; set; } = 0.188f;
		[DebugEditable(DisplayName = "Shield Icon Height", Step = 1, Min = 8, Max = 128)]
		public int ShieldIconHeight { get; set; } = 36;
		[DebugEditable(DisplayName = "Shield Gap", Step = 1, Min = 0, Max = 64)]
		public int ShieldGap { get; set; } = 2;
		[DebugEditable(DisplayName = "Background Opacity", Step = 0.05f, Min = 0f, Max = 1f)]
		public float BgOpacity { get; set; } = 0.75f;
		[DebugEditable(DisplayName = "Shield Offset X", Step = 1, Min = -200, Max = 200)]
		public int ShieldOffsetX { get; set; } = 0;
		[DebugEditable(DisplayName = "Shield Offset Y", Step = 1, Min = -200, Max = 200)]
		public int ShieldOffsetY { get; set; } = -2;
		[DebugEditable(DisplayName = "Checkmark Size", Step = 1, Min = 6, Max = 256)]
		public int CheckmarkSize { get; set; } = 35;
		[DebugEditable(DisplayName = "Checkmark Offset X", Step = 1, Min = -200, Max = 200)]
		public int CheckmarkOffsetX { get; set; } = 9;
		[DebugEditable(DisplayName = "Checkmark Offset Y", Step = 1, Min = -200, Max = 200)]
		public int CheckmarkOffsetY { get; set; } = -9;

		public EquipmentDisplaySystemLegacy(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ContentManager content)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_content = content;
			EventManager.Subscribe<EquipmentAbilityTriggered>(OnEquipmentAbilityTriggered);
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCachesEvent);
		}

		private void OnEquipmentAbilityTriggered(EquipmentAbilityTriggered evt)
		{
			// Trigger a brief pulse for the specific equipment that triggered
			if (evt.Equipment != null)
			{
				_pulseTimeByEntityId[evt.Equipment.Id] = PulseDurationSeconds;
			}
		}

		private void OnDeleteCachesEvent(DeleteCachesEvent evt)
		{
			_tooltipByEquipEntityId.Clear();
			_iconCache.Clear();
			_roundedRectCache.Clear();
			_pulseTimeByEntityId.Clear();
		}

		private float GetCurrentPulseScale(int entityId)
		{
			if (!_pulseTimeByEntityId.TryGetValue(entityId, out var pulseTimeRemaining) || pulseTimeRemaining <= 0.0)
			{
				return 1f;
			}
			float t = (float)(1.0 - (pulseTimeRemaining / PulseDurationSeconds));
			float wave = (float)Math.Sin(t * Math.PI);
			return 1f + PulseAmplitude * wave;
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<Player>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			// Layout equipment positions so parallax can process them
			var allEquipmentForPlayer = EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Where(e => e.GetComponent<EquippedEquipment>().EquippedOwner == entity)
				.Select(e => e.GetComponent<EquippedEquipment>())
				.ToList();
			var equipment = allEquipmentForPlayer
				.Where(eq => (eq.Owner.GetComponent<EquipmentZone>()?.Zone ?? EquipmentZoneType.Default) == EquipmentZoneType.Default)
				.ToList();
			if (equipment.Count == 0) return;

			EquipmentSlot[] order = [EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Arms, EquipmentSlot.Legs];
			int baseX = LeftMargin;
			int y = TopMargin;
			int rowHeight = (IconSize + BgPadding * 2) + RowGap;
			foreach (var type in order)
			{
				var items = equipment.Where(eq => eq.Equipment.Slot == type).ToList();
				if (items.Count > 0)
				{
					int x = baseX;
					foreach (var item in items)
					{
						var tEquip = item.Owner.GetComponent<Transform>();
						if (tEquip != null)
						{
							tEquip.Position = new Vector2(x, y);
							tEquip.ZOrder = 10001;
						}
						x += (IconSize + BgPadding * 2) + ColGap;
					}
					y += rowHeight;
				}
			}
		}

		public override void Update(GameTime gameTime)
		{
			_lastDt = gameTime.ElapsedGameTime.TotalSeconds;
			base.Update(gameTime);
		}

		public void Draw()
		{
			// Update pulse timers for all entities
			if (_lastDt > 0)
			{
				var keys = _pulseTimeByEntityId.Keys.ToList();
				foreach (var entityId in keys)
				{
					var remaining = _pulseTimeByEntityId[entityId];
					if (remaining > 0.0)
					{
						_pulseTimeByEntityId[entityId] = Math.Max(0.0, remaining - _lastDt);
					}
				}
			}
			var player = GetRelevantEntities().FirstOrDefault();
			if (player == null) return;

			// Gather equipment for this player
			var allEquipmentForPlayer = EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Where(e => e.GetComponent<EquippedEquipment>().EquippedOwner == player)
				.Select(e => e.GetComponent<EquippedEquipment>())
				.ToList();
			var equipment = allEquipmentForPlayer
				.Where(eq => (eq.Owner.GetComponent<EquipmentZone>()?.Zone ?? EquipmentZoneType.Default) == EquipmentZoneType.Default)
				.ToList();

			if (equipment.Count == 0) return;

			// Group and order types
			EquipmentSlot[] order = [EquipmentSlot.Head, EquipmentSlot.Chest, EquipmentSlot.Arms, EquipmentSlot.Legs];
			int baseX = LeftMargin;
			int y = TopMargin;
			int rowHeight = (IconSize + BgPadding * 2) + RowGap;
			foreach (var type in order)
			{
				var items = equipment.Where(eq => eq.Equipment.Slot == type).ToList();
				// Draw items in a row if any are visible
				if (items.Count > 0)
				{
					int x = baseX;
					foreach (var item in items)
					{
						// Resolve equipment definition for visuals and tooltip
						int bgW = IconSize + BgPadding * 2;
						int bgH = IconSize + BgPadding * 2;
						var tEquip = item.Owner.GetComponent<Transform>();
						// Read current Position (parallax-adjusted from Update)
						var curPos = tEquip != null ? tEquip.Position : new Vector2(x, y);
						var uiEquip = item.Owner.GetComponent<UIElement>();
						var bgRect = new Rectangle((int)System.Math.Round(curPos.X), (int)System.Math.Round(curPos.Y), bgW, bgH);
						uiEquip.Bounds = new Rectangle(bgRect.X, bgRect.Y, bgRect.Width, bgRect.Height);
						// Persist panel center for return animations
						var zoneState = item.Owner.GetComponent<EquipmentZone>();
						if (zoneState == null)
						{
							zoneState = new EquipmentZone();
							EntityManager.AddComponent(item.Owner, zoneState);
						}
						zoneState.LastPanelCenter = new Vector2(bgRect.X + bgRect.Width * 0.5f, bgRect.Y + bgRect.Height * 0.5f);
						var fillColor = ResolveFillColor(item);
						bool disabledNow = IsDisabledForBlock(item);
						// Update tooltip and hover state before publishing highlight so hover is accurate
						UpdateTooltip(item, bgRect);
						UpdateClickable(item, bgRect);
						// Publish highlight event on hover unless disabled; during Player Action phase only if item has Activate ability
						var phaseNow = EntityManager.GetEntitiesWithComponent<PhaseState>().FirstOrDefault()?.GetComponent<PhaseState>();
						bool isPlayerAction = phaseNow != null && phaseNow.Main == MainPhase.PlayerTurn && phaseNow.Sub == SubPhase.Action;
						// Now draw background and contents, with optional pulse
						float pulseScale = GetCurrentPulseScale(item.Owner.Id);
						if (pulseScale != 1f)
						{
							int scaledW = (int)Math.Round(bgRect.Width * pulseScale);
							int scaledH = (int)Math.Round(bgRect.Height * pulseScale);
							var center = new Vector2(bgRect.X + bgRect.Width / 2f, bgRect.Y + bgRect.Height / 2f);
							var scaled = new Rectangle((int)(center.X - scaledW / 2f), (int)(center.Y - scaledH / 2f), scaledW, scaledH);
							DrawRoundedBackground(scaled, disabledNow ? DisabledFill(fillColor) : fillColor);
							bgRect = scaled;
						}
						else
						{
							DrawRoundedBackground(bgRect, disabledNow ? DisabledFill(fillColor) : fillColor);
						}

						// Draw icon within padding, preserving aspect ratio
						var tex = GetOrLoadIcon(item.Equipment.Slot.ToString());
						if (tex != null)
						{
							int targetH = IconSize;
							int targetW = IconSize;
							if (tex.Width > 0 && tex.Height > 0)
							{
								float aspect = tex.Width / (float)tex.Height;
								if (aspect >= 1f)
								{
									targetW = IconSize;
									targetH = (int)Math.Round(IconSize / aspect);
								}
								else
								{
									targetH = IconSize;
									targetW = (int)Math.Round(IconSize * aspect);
								}
							}
							int innerX = bgRect.X + IconPaddingX;
							int innerY = bgRect.Y + IconPaddingY;
							// Center the icon within the padded area if one dimension is smaller due to aspect fit
							int padBoxW = IconSize;
							int padBoxH = IconSize;
							int drawX = innerX + (padBoxW - targetW) / 2;
							int drawY = innerY + (padBoxH - targetH) / 2;
							var iconRect = new Rectangle(drawX, drawY, targetW, targetH);
							_spriteBatch.Draw(tex, iconRect, disabledNow ? Color.Gray : Color.White);
						}

						// Draw block value and shield icon at bottom-left
						DrawBlockAndShield(item, bgRect, fillColor);
						// Overlay yellow X if destroyed
						x += bgW + ColGap;
					}
				}
				y += rowHeight; // reserve row space for this equipment type to prevent vertical shifting
			}
		}

		private bool IsDisabledForBlock(EquippedEquipment item)
		{
			return item.Equipment.IsUsed;
		}

		private Color DisabledFill(Color baseFill)
		{
			// Dim the fill color to indicate disabled
			return new Color((byte)(baseFill.R * 0.4f), (byte)(baseFill.G * 0.4f), (byte)(baseFill.B * 0.4f), (byte)(MathHelper.Clamp(BgOpacity * 255f, 0f, 255f)));
		}

		private void UpdateClickable(EquippedEquipment item, Rectangle rect)
		{
			var ui = item.Owner.GetComponent<UIElement>();
			ui.Bounds = rect;
			ui.IsInteractable = true;
			ui.Tooltip = BuildTooltipText(item);
			ui.TooltipPosition = TooltipPosition.Right;
			// Panel (default zone) items should use direct click handling, not delegate routing
			ui.EventType = UIElementEventType.None;
			// Transform.Position already reflects parallax; ZOrder handled above
		}

		private void DrawRoundedBackground(Rectangle rect, Color fill)
		{
			int w = rect.Width;
			int h = rect.Height;
			int rOuter = Math.Max(0, BgCornerRadius);
			int rInner = Math.Max(0, BgCornerRadius - BgBorderThickness);
			var outer = GetRoundedRectTexture(w, h, rOuter);
			var inner = GetRoundedRectTexture(Math.Max(1, w - BgBorderThickness * 2), Math.Max(1, h - BgBorderThickness * 2), rInner);
			var center = new Vector2(rect.X + w / 2f, rect.Y + h / 2f);
			if (BgBorderThickness > 0)
			{
				_spriteBatch.Draw(outer, center, null, Color.Black, 0f, new Vector2(outer.Width / 2f, outer.Height / 2f), 1f, SpriteEffects.None, 0f);
			}
			byte a = (byte)(MathHelper.Clamp(BgOpacity, 0f, 1f) * 255f);
			var fillWithAlpha = new Color(fill.R, fill.G, fill.B, a);
			_spriteBatch.Draw(inner, center, null, fillWithAlpha, 0f, new Vector2(inner.Width / 2f, inner.Height / 2f), 1f, SpriteEffects.None, 0f);
		}

		private Texture2D GetRoundedRectTexture(int width, int height, int radius)
		{
			var key = (width, height, radius);
			if (_roundedRectCache.TryGetValue(key, out var tex) && tex != null) return tex;
			var texture = RoundedRectTextureFactory.CreateRoundedRect(_graphicsDevice, width, height, radius);
			_roundedRectCache[key] = texture;
			return texture;
		}

		private void DrawBlockAndShield(EquippedEquipment item, Rectangle bgRect, Color fillColor)
		{
			if (_font == null) return;
			int block = 0;
			try
			{
				block = Math.Max(0, item.Equipment.Block);
			}
			catch { }
			if (block <= 0) return;

			var textColor = GetTextColorForFill(fillColor);
			string blockText = block.ToString();
			var textSize = _font.MeasureString(blockText) * BlockTextScale;
			float marginX = 8f;
			float marginY = 8f;
			float baselineY = bgRect.Y + bgRect.Height - marginY;
			float numberX = bgRect.X + marginX;
			float numberY = baselineY - textSize.Y;
			_spriteBatch.DrawString(_font, blockText, new Vector2(numberX, numberY), textColor, 0f, Vector2.Zero, BlockTextScale, SpriteEffects.None, 0f);

			var shield = SafeLoadTexture("shield");
			if (shield != null)
			{
				float iconH = Math.Max(8, ShieldIconHeight);
				float iconW = shield.Height > 0 ? iconH * (shield.Width / (float)shield.Height) : iconH;
				float gap = Math.Max(0, ShieldGap);
				float iconX = numberX + textSize.X + gap + ShieldOffsetX;
				float iconY = baselineY - iconH + ShieldOffsetY;
				_spriteBatch.Draw(shield, new Rectangle((int)iconX, (int)iconY, (int)iconW, (int)iconH), Color.White);
			}
		}

		private void UpdateTooltip(EquippedEquipment item, Rectangle rect)
		{
			var uiEntity = item.Owner;
			var tr = uiEntity.GetComponent<Transform>();
			// Do not overwrite Transform.Position here; it's owned by Parallax/motion
			var ui = uiEntity.GetComponent<UIElement>();
			if (ui != null)
			{
				ui.Bounds = rect;
				ui.Tooltip = BuildTooltipText(item);
				ui.TooltipPosition = TooltipPosition.Right;
			}
		}

		private string BuildTooltipText(EquippedEquipment item)
		{
			return EquipmentService.GetTooltipText(item.Equipment, EquipmentTooltipType.Battle);
		}

		private Texture2D SafeLoadTexture(string asset)
		{
			try { return _content.Load<Texture2D>(asset); } catch { return null; }
		}

		private Color ResolveFillColor(EquippedEquipment item)
		{
			switch (item.Equipment.Color)
			{
				case CardData.CardColor.Red: return Color.DarkRed;
				case CardData.CardColor.White: return Color.White;
				case CardData.CardColor.Black: return Color.Black;
				default: return Color.Gray;
			}
		}

		private Color GetTextColorForFill(Color fill)
		{
			// Match card logic: black text on white fill, white otherwise
			if (fill == Color.White) return Color.Black;
			return Color.White;
		}

		private Texture2D GetOrLoadIcon(string type)
		{
			string key = type.ToLowerInvariant();
			if (_iconCache.TryGetValue(key, out var t) && t != null) return t;
			string assetName = key; // assumes head.png, chest.png, arms.png, legs.png in Content root
			try
			{
				var tex = _content.Load<Texture2D>(assetName);
				_iconCache[key] = tex;
				return tex;
			}
			catch
			{
				LoggingService.Append("EquipmentDisplaySystem.GetOrCreateIcon.missing", new System.Text.Json.Nodes.JsonObject { ["type"] = type, ["assetName"] = assetName });
				_iconCache[key] = null;
				return null;
			}
		}


	}
}

