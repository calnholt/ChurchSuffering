using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Equipment Display")]
	public class EquipmentDisplaySystem : Core.System
	{
		public const string RootEntityName = "UI_EquipmentDisplayRoot";
		public const string TooltipEntityName = "UI_EquipmentTooltip";

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private readonly Dictionary<(int Width, int Height, int Radius), Texture2D> _roundedRectCache = new();
		private readonly Dictionary<int, float> _pulseSeconds = new();
		private Vector2? _lastConfiguredAnchor;

		[DebugEditable(DisplayName = "Left Margin", Step = 1, Min = 0, Max = 2000)]
		public int LeftMargin { get; set; } = 30;

		[DebugEditable(DisplayName = "Top Margin", Step = 1, Min = 0, Max = 2000)]
		public int TopMargin { get; set; } = 200;

		[DebugEditable(DisplayName = "Panel Width", Step = 1, Min = 60, Max = 300)]
		public int PanelWidth { get; set; } = 108;

		[DebugEditable(DisplayName = "Panel Height", Step = 1, Min = 70, Max = 300)]
		public int PanelHeight { get; set; } = 116;

		[DebugEditable(DisplayName = "Panel Border", Step = 1, Min = 1, Max = 12)]
		public int PanelBorderThickness { get; set; } = 2;

		[DebugEditable(DisplayName = "Panel Radius", Step = 1, Min = 0, Max = 40)]
		public int PanelCornerRadius { get; set; } = 8;

		[DebugEditable(DisplayName = "Slot Height", Step = 1, Min = 30, Max = 200)]
		public int SlotHeight { get; set; } = 76;

		[DebugEditable(DisplayName = "Slot Icon Size", Step = 1, Min = 12, Max = 160)]
		public int SlotIconSize { get; set; } = 52;

		[DebugEditable(DisplayName = "Column Gap", Step = 1, Min = 0, Max = 100)]
		public int ColumnGap { get; set; } = 8;

		[DebugEditable(DisplayName = "Row Gap", Step = 1, Min = 0, Max = 100)]
		public int RowGap { get; set; } = 12;

		[DebugEditable(DisplayName = "Label Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float LabelFontScale { get; set; } = 0.06f;

		[DebugEditable(DisplayName = "Value Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ValueFontScale { get; set; } = 0.14f;

		[DebugEditable(DisplayName = "Ability Star Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float AbilityStarScale { get; set; } = 0.13f;

		[DebugEditable(DisplayName = "Disabled Brightness", Step = 0.01f, Min = 0f, Max = 1f)]
		public float DisabledBrightness { get; set; } = 0.4f;

		[DebugEditable(DisplayName = "Pulse Seconds", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float PulseDurationSeconds { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Pulse Scale", Step = 0.01f, Min = 1f, Max = 1.5f)]
		public float PulseScale { get; set; } = 1.12f;

		public EquipmentDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			if (graphicsDevice != null)
			{
				_pixel = _imageAssets.GetPixel(Color.White);
			}
			EventManager.Subscribe<EquipmentAbilityTriggered>(OnEquipmentAbilityTriggered);
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			if (!IsEquipmentScene())
			{
				DestroyDisplayHierarchy();
				return;
			}

			UpdatePulses((float)gameTime.ElapsedGameTime.TotalSeconds);
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null)
			{
				DestroyDisplayHierarchy();
				return;
			}

			var equipment = GetPlayerEquipment(player);
			if (equipment.Count == 0)
			{
				DestroyDisplayHierarchy();
				return;
			}

			CaptureLastRenderedPanelCenters(equipment);
			var configuredAnchor = new Vector2(LeftMargin, TopMargin);
			Vector2 anchorDelta = _lastConfiguredAnchor.HasValue
				? configuredAnchor - _lastConfiguredAnchor.Value
				: Vector2.Zero;
			var root = EnsureRoot();
			EnsureTooltip(root);
			LayoutPanels(root, equipment, anchorDelta);
			_lastConfiguredAnchor = configuredAnchor;
		}

		public void Draw()
		{
			if (_graphicsDevice == null || _spriteBatch == null || _pixel == null) return;
			var player = EntityManager.GetEntitiesWithComponent<Player>().FirstOrDefault();
			if (player == null) return;

			foreach (var entity in GetPlayerEquipment(player))
			{
				var zone = entity.GetComponent<EquipmentZone>();
				if ((zone?.Zone ?? EquipmentZoneType.Default) != EquipmentZoneType.Default) continue;
				DrawPanel(entity);
			}
		}

		public Rectangle GetPanelWorldBounds(Entity equipmentEntity)
		{
			var ui = equipmentEntity?.GetComponent<UIElement>();
			return ui == null
				? Rectangle.Empty
				: TransformResolverService.ResolveUIBounds(EntityManager, equipmentEntity, ui);
		}

		private bool IsEquipmentScene()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>();
			return scene == null || scene.Current == SceneId.Battle || scene.Current == SceneId.Snapshot;
		}

		private List<Entity> GetPlayerEquipment(Entity player)
		{
			return EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Where(entity => entity.GetComponent<EquippedEquipment>()?.EquippedOwner == player)
				.OrderBy(entity => SlotOrder(entity.GetComponent<EquippedEquipment>().Equipment.Slot))
				.ThenBy(entity => entity.Id)
				.ToList();
		}

		private Entity EnsureRoot()
		{
			var roots = EntityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>()
				.OrderBy(entity => entity.Id)
				.ToList();
			var root = roots.FirstOrDefault();
			foreach (var duplicate in roots.Skip(1))
			{
				EntityManager.DestroyEntity(duplicate.Id);
			}

			if (root == null)
			{
				root = EntityManager.CreateEntity(RootEntityName);
				EntityManager.AddComponent(root, new EquipmentDisplayRoot());
				EntityManager.AddComponent(root, new Transform());
				EntityManager.AddComponent(root, new UIElement());
			}

			root.Name = RootEntityName;
			var transform = root.GetComponent<Transform>();
			transform.Position = new Vector2(LeftMargin, TopMargin);
			transform.Scale = Vector2.One;
			transform.Rotation = 0f;
			transform.ZOrder = 10001;
			var ui = root.GetComponent<UIElement>();
			ui.Bounds = Rectangle.Empty;
			ui.IsInteractable = false;
			ui.IsHidden = true;
			ui.TooltipType = TooltipType.None;

			if (root.HasComponent<ParentTransform>())
			{
				EntityManager.RemoveComponent<ParentTransform>(root);
			}
			if (!root.HasComponent<ParallaxLayer>())
			{
				EntityManager.AddComponent(root, ParallaxLayer.GetUIParallaxLayer());
			}
			return root;
		}

		private Entity EnsureTooltip(Entity root)
		{
			var tooltips = EntityManager.GetEntitiesWithComponent<EquipmentTooltipState>()
				.OrderBy(entity => entity.Id)
				.ToList();
			var tooltip = tooltips.FirstOrDefault();
			foreach (var duplicate in tooltips.Skip(1))
			{
				EntityManager.DestroyEntity(duplicate.Id);
			}

			if (tooltip == null)
			{
				tooltip = EntityManager.CreateEntity(TooltipEntityName);
				EntityManager.AddComponent(tooltip, new EquipmentTooltipState());
				EntityManager.AddComponent(tooltip, new Transform { ZOrder = 10002 });
				EntityManager.AddComponent(tooltip, new UIElement
				{
					IsInteractable = false,
					IsHidden = true,
					TooltipType = TooltipType.None,
					ShowHoverHighlight = false,
				});
			}

			tooltip.Name = TooltipEntityName;
			EnsureParent(tooltip, root);
			if (tooltip.HasComponent<ParallaxLayer>())
			{
				EntityManager.RemoveComponent<ParallaxLayer>(tooltip);
			}
			return tooltip;
		}

		private void LayoutPanels(
			Entity root,
			IReadOnlyList<Entity> equipment,
			Vector2 anchorDelta)
		{
			EquipmentSlot[] slots =
			[
				EquipmentSlot.Head,
				EquipmentSlot.Chest,
				EquipmentSlot.Arms,
				EquipmentSlot.Legs,
			];

			int y = 0;
			foreach (var slot in slots)
			{
				int x = 0;
				foreach (var entity in equipment.Where(item =>
					item.GetComponent<EquippedEquipment>().Equipment.Slot == slot))
				{
					if (entity.HasComponent<ParallaxLayer>())
					{
						EntityManager.RemoveComponent<ParallaxLayer>(entity);
					}

					var zone = entity.GetComponent<EquipmentZone>();
					if (zone == null)
					{
						zone = new EquipmentZone();
						EntityManager.AddComponent(entity, zone);
					}
					if (zone.Zone != EquipmentZoneType.Default)
					{
						continue;
					}

					EnsureParent(entity, root);
					var transform = entity.GetComponent<Transform>();
					if (transform == null)
					{
						transform = new Transform();
						EntityManager.AddComponent(entity, transform);
					}
					transform.Position = new Vector2(x, y);
					transform.Scale = Vector2.One;
					transform.Rotation = 0f;
					transform.ZOrder = 10001;

					var ui = entity.GetComponent<UIElement>();
					if (ui == null)
					{
						ui = new UIElement();
						EntityManager.AddComponent(entity, ui);
					}
					ui.Bounds = new Rectangle(0, 0, PanelWidth, PanelHeight);
					ui.IsInteractable = true;
					ui.IsHidden = false;
					ui.Tooltip = string.Empty;
					ui.TooltipType = TooltipType.Equipment;
					ui.TooltipPosition = TooltipPosition.Right;
					ui.TooltipOffsetPx = 20;
					ui.EventType = UIElementEventType.None;
					ui.ShowHoverHighlight = entity.GetComponent<EquippedEquipment>().Equipment.HasUses;

					Rectangle worldBounds = TransformResolverService.ResolveUIBounds(EntityManager, entity, ui);
					if (zone.LastPanelCenter == Vector2.Zero)
					{
						zone.LastPanelCenter = new Vector2(worldBounds.Center.X, worldBounds.Center.Y);
					}
					else if (anchorDelta != Vector2.Zero)
					{
						zone.LastPanelCenter += anchorDelta;
					}
					x += PanelWidth + ColumnGap;
				}
				y += PanelHeight + RowGap;
			}
		}

		private void DrawPanel(Entity entity)
		{
			var equipped = entity.GetComponent<EquippedEquipment>();
			if (equipped?.Equipment == null) return;

			Rectangle stableBounds = GetPanelWorldBounds(entity);
			if (stableBounds.Width <= 0 || stableBounds.Height <= 0) return;
			Rectangle drawBounds = ScaleAroundCenter(stableBounds, GetPulseScale(entity.Id));
			bool exhausted = !equipped.Equipment.HasUses;
			Color border = GetPanelBorder(equipped.Equipment.Color, exhausted);
			Color socket = GetSocketColor(equipped.Equipment.Color, exhausted);

			DrawRoundedRect(drawBounds, border);
			var inner = Inset(drawBounds, PanelBorderThickness);
			DrawRoundedRect(inner, new Color(8, 8, 8, 240));

			int footerHeight = Math.Max(1, PanelHeight - SlotHeight);
			var socketBounds = new Rectangle(
				inner.X,
				inner.Y,
				inner.Width,
				Math.Max(1, inner.Height - footerHeight));
			_spriteBatch.Draw(_pixel, socketBounds, socket);

			var footer = new Rectangle(inner.X, socketBounds.Bottom, inner.Width, footerHeight);
			int split = footer.Width / 2;
			var blockRect = new Rectangle(footer.X, footer.Y, split, footer.Height);
			var usesRect = new Rectangle(footer.X + split, footer.Y, footer.Width - split, footer.Height);
			_spriteBatch.Draw(_pixel, blockRect, exhausted ? new Color(17, 30, 38) : new Color(42, 74, 94));
			_spriteBatch.Draw(_pixel, usesRect, new Color(10, 10, 10));
			_spriteBatch.Draw(_pixel, new Rectangle(footer.X, footer.Y, footer.Width, 1), new Color(255, 255, 255, 36));
			_spriteBatch.Draw(_pixel, new Rectangle(usesRect.X, usesRect.Y, 1, usesRect.Height), new Color(255, 255, 255, 30));

			DrawSlotIcon(equipped.Equipment.Slot, socketBounds, exhausted);
			DrawFooterStat(blockRect, "BLOCK", equipped.Equipment.Block.ToString(), exhausted);
			DrawFooterStat(
				usesRect,
				"USES",
				$"{Math.Max(0, equipped.Equipment.RemainingUses)}/{equipped.Equipment.Uses}",
				exhausted);
			if (equipped.Equipment.CanActivateDuringActionPhase)
			{
				DrawAbilityStar(drawBounds, exhausted);
			}
		}

		private void DrawSlotIcon(EquipmentSlot slot, Rectangle socketBounds, bool exhausted)
		{
			Texture2D texture = GetIcon(slot);
			if (texture == null) return;
			int size = Math.Min(SlotIconSize, Math.Min(socketBounds.Width, socketBounds.Height));
			var destination = new Rectangle(
				socketBounds.Center.X - size / 2,
				socketBounds.Center.Y - size / 2,
				size,
				size);
			_spriteBatch.Draw(texture, destination, exhausted ? new Color(102, 102, 102) : Color.White);
		}

		private void DrawFooterStat(Rectangle bounds, string label, string value, bool exhausted)
		{
			var labelFont = FontSingleton.ChakraPetchFont;
			var valueFont = FontSingleton.TitleFont;
			if (labelFont == null || valueFont == null) return;

			Color color = exhausted ? new Color(136, 136, 136) : Color.White;
			Vector2 labelSize = labelFont.MeasureString(label) * LabelFontScale;
			Vector2 valueSize = valueFont.MeasureString(value) * ValueFontScale;
			float totalHeight = labelSize.Y + valueSize.Y - 2f;
			var labelPos = new Vector2(
				bounds.Center.X - labelSize.X / 2f,
				bounds.Center.Y - totalHeight / 2f);
			var valuePos = new Vector2(
				bounds.Center.X - valueSize.X / 2f,
				labelPos.Y + labelSize.Y - 2f);
			_spriteBatch.DrawString(labelFont, label, labelPos, color, 0f, Vector2.Zero, LabelFontScale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(valueFont, value, valuePos, color, 0f, Vector2.Zero, ValueFontScale, SpriteEffects.None, 0f);
		}

		private void DrawAbilityStar(Rectangle bounds, bool exhausted)
		{
			var font = FontSingleton.TitleFont;
			if (font == null) return;
			Color color = exhausted ? new Color(102, 102, 102, 115) : new Color(196, 30, 58);
			_spriteBatch.DrawString(
				font,
				"*",
				new Vector2(bounds.X + 4, bounds.Y - 1),
				color,
				0f,
				Vector2.Zero,
				AbilityStarScale,
				SpriteEffects.None,
				0f);
		}

		private void DrawRoundedRect(Rectangle bounds, Color color)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;
			var key = (bounds.Width, bounds.Height, Math.Min(PanelCornerRadius, Math.Min(bounds.Width, bounds.Height) / 2));
			if (!_roundedRectCache.TryGetValue(key, out var texture))
			{
				texture = RoundedRectTextureFactory.CreateRoundedRect(
					_graphicsDevice,
					key.Width,
					key.Height,
					key.Item3);
				_roundedRectCache[key] = texture;
			}
			_spriteBatch.Draw(texture, bounds, color);
		}

		private Texture2D GetIcon(EquipmentSlot slot)
		{
			string key = slot.ToString().ToLowerInvariant();
			return _imageAssets.TryGetTexture(key);
		}

		private void EnsureParent(Entity child, Entity root)
		{
			var parent = child.GetComponent<ParentTransform>();
			if (parent == null)
			{
				EntityManager.AddComponent(child, new ParentTransform { Parent = root });
			}
			else
			{
				parent.Parent = root;
			}
		}

		private void DestroyDisplayHierarchy()
		{
			var root = EntityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>().FirstOrDefault();
			if (root == null)
			{
				_lastConfiguredAnchor = null;
				return;
			}
			foreach (var equipment in EntityManager.GetEntitiesWithComponent<EquippedEquipment>())
			{
				var parent = equipment.GetComponent<ParentTransform>();
				if (parent?.Parent == root)
				{
					Vector2 worldPosition = TransformResolverService.ResolveWorldPosition(EntityManager, equipment);
					EntityManager.RemoveComponent<ParentTransform>(equipment);
					equipment.GetComponent<Transform>().Position = worldPosition;
				}
			}
			foreach (var tooltip in EntityManager.GetEntitiesWithComponent<EquipmentTooltipState>().ToList())
			{
				EntityManager.DestroyEntity(tooltip.Id);
			}
			EntityManager.DestroyEntity(root.Id);
			_lastConfiguredAnchor = null;
		}

		private void CaptureLastRenderedPanelCenters(IReadOnlyList<Entity> equipment)
		{
			foreach (var entity in equipment)
			{
				var zone = entity.GetComponent<EquipmentZone>();
				var ui = entity.GetComponent<UIElement>();
				if (zone?.Zone != EquipmentZoneType.Default
					|| ui == null
					|| !entity.HasComponent<ParentTransform>())
				{
					continue;
				}

				Rectangle bounds = TransformResolverService.ResolveUIBounds(
					EntityManager,
					entity,
					ui);
				if (bounds.Width > 0 && bounds.Height > 0)
				{
					zone.LastPanelCenter = new Vector2(bounds.Center.X, bounds.Center.Y);
				}
			}
		}

		private void OnEquipmentAbilityTriggered(EquipmentAbilityTriggered evt)
		{
			if (evt?.Equipment != null)
			{
				_pulseSeconds[evt.Equipment.Id] = PulseDurationSeconds;
			}
		}

		private void OnDeleteCaches(DeleteCachesEvent evt)
		{
			_roundedRectCache.Clear();
			_pulseSeconds.Clear();
		}

		private void UpdatePulses(float elapsedSeconds)
		{
			foreach (int entityId in _pulseSeconds.Keys.ToList())
			{
				float remaining = Math.Max(0f, _pulseSeconds[entityId] - elapsedSeconds);
				if (remaining <= 0f)
				{
					_pulseSeconds.Remove(entityId);
				}
				else
				{
					_pulseSeconds[entityId] = remaining;
				}
			}
		}

		private float GetPulseScale(int entityId)
		{
			if (!_pulseSeconds.TryGetValue(entityId, out float remaining) || PulseDurationSeconds <= 0f)
			{
				return 1f;
			}
			float progress = 1f - remaining / PulseDurationSeconds;
			return 1f + (PulseScale - 1f) * (float)Math.Sin(progress * Math.PI);
		}

		private static int SlotOrder(EquipmentSlot slot)
		{
			return slot switch
			{
				EquipmentSlot.Head => 0,
				EquipmentSlot.Chest => 1,
				EquipmentSlot.Arms => 2,
				EquipmentSlot.Legs => 3,
				_ => 4,
			};
		}

		private Color GetPanelBorder(CardData.CardColor color, bool exhausted)
		{
			Color baseColor = color switch
			{
				CardData.CardColor.Red => new Color(204, 34, 34, 140),
				CardData.CardColor.White => new Color(200, 192, 180, 217),
				CardData.CardColor.Black => new Color(85, 85, 85, 180),
				_ => new Color(255, 255, 255, 217),
			};
			return exhausted ? Dim(baseColor, 0.5f) : baseColor;
		}

		private Color GetSocketColor(CardData.CardColor color, bool exhausted)
		{
			Color baseColor = color switch
			{
				CardData.CardColor.Red => new Color(78, 12, 12, 220),
				CardData.CardColor.White => new Color(190, 185, 176, 210),
				CardData.CardColor.Black => new Color(19, 19, 19, 242),
				_ => new Color(35, 35, 35, 242),
			};
			return exhausted ? Dim(baseColor, DisabledBrightness) : baseColor;
		}

		private static Color Dim(Color color, float brightness)
		{
			brightness = MathHelper.Clamp(brightness, 0f, 1f);
			return new Color(
				(byte)(color.R * brightness),
				(byte)(color.G * brightness),
				(byte)(color.B * brightness),
				color.A);
		}

		private static Rectangle Inset(Rectangle bounds, int amount)
		{
			amount = Math.Max(0, amount);
			return new Rectangle(
				bounds.X + amount,
				bounds.Y + amount,
				Math.Max(1, bounds.Width - amount * 2),
				Math.Max(1, bounds.Height - amount * 2));
		}

		private static Rectangle ScaleAroundCenter(Rectangle bounds, float scale)
		{
			if (Math.Abs(scale - 1f) < 0.001f) return bounds;
			int width = Math.Max(1, (int)Math.Round(bounds.Width * scale));
			int height = Math.Max(1, (int)Math.Round(bounds.Height * scale));
			return new Rectangle(
				bounds.Center.X - width / 2,
				bounds.Center.Y - height / 2,
				width,
				height);
		}
	}
}
