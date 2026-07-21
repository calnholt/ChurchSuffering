using System;
using System.Collections.Generic;
using System.Linq;
using ChurchSuffering.Diagnostics;
using ChurchSuffering.ECS.Components;
using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Events;
using ChurchSuffering.ECS.Objects.Equipment;
using ChurchSuffering.ECS.Rendering;
using ChurchSuffering.ECS.Services;
using ChurchSuffering.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace ChurchSuffering.ECS.Systems
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
		private readonly Dictionary<(int Width, int Height, int RadiusTL, int RadiusTR, int RadiusBR, int RadiusBL), Texture2D> _perCornerRoundedRectCache = new();
		private readonly Dictionary<int, float> _pulseSeconds = new();
		private Vector2? _lastConfiguredAnchor;
		private static readonly Vector2[] AbilityStarPoints =
		[
			new(0.5000f, 0.0938f),
			new(0.6125f, 0.3750f),
			new(0.9063f, 0.3750f),
			new(0.6688f, 0.5500f),
			new(0.7625f, 0.8438f),
			new(0.5000f, 0.7000f),
			new(0.2375f, 0.8438f),
			new(0.3313f, 0.5500f),
			new(0.0938f, 0.3750f),
			new(0.3875f, 0.3750f),
		];

		[DebugEditable(DisplayName = "Left Margin", Step = 1, Min = 0, Max = 2000)]
		public int LeftMargin { get; set; } = 30;

		[DebugEditable(DisplayName = "Top Margin", Step = 1, Min = 0, Max = 2000)]
		public int TopMargin { get; set; } = 200;

		[DebugEditable(DisplayName = "Panel Width", Step = 1, Min = 60, Max = 300)]
		public int PanelWidth { get; set; } = 108;

		[DebugEditable(DisplayName = "Panel Height", Step = 1, Min = 70, Max = 300)]
		public int PanelHeight { get; set; } = 133;

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
		public float ValueFontScale { get; set; } = 0.13f;

		[DebugEditable(DisplayName = "Footer Padding", Step = 1, Min = 0, Max = 40)]
		public int FooterPadding { get; set; } = 8;

		[DebugEditable(DisplayName = "Footer Gap", Step = 1, Min = 0, Max = 30)]
		public int FooterGap { get; set; } = 6;

		[DebugEditable(DisplayName = "Chip Label Height", Step = 1, Min = 6, Max = 30)]
		public int ChipLabelHeight { get; set; } = 13;

		[DebugEditable(DisplayName = "Chip Value Height", Step = 1, Min = 10, Max = 60)]
		public int ChipValueHeight { get; set; } = 28;

		[DebugEditable(DisplayName = "Chip Corner Radius", Step = 1, Min = 0, Max = 20)]
		public int ChipCornerRadius { get; set; } = 3;

		[DebugEditable(DisplayName = "Shadow Offset Y", Step = 1, Min = 0, Max = 30)]
		public int ShadowOffsetY { get; set; } = 6;

		[DebugEditable(DisplayName = "Shadow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ShadowAlpha { get; set; } = 0.55f;

		[DebugEditable(DisplayName = "Ability Mark Size", Step = 1, Min = 6, Max = 40)]
		public int AbilityMarkSize { get; set; } = 14;

		[DebugEditable(DisplayName = "Ability Mark Offset X", Step = 1, Min = -20, Max = 40)]
		public int AbilityMarkOffsetX { get; set; } = 4;

		[DebugEditable(DisplayName = "Ability Mark Offset Y", Step = 1, Min = -20, Max = 40)]
		public int AbilityMarkOffsetY { get; set; } = 4;

		[DebugEditable(DisplayName = "Used Opacity", Step = 0.01f, Min = 0f, Max = 1f)]
		public float UsedOpacity { get; set; } = 0.4f;

		[DebugEditable(DisplayName = "Pulse Seconds", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float PulseDurationSeconds { get; set; } = 0.18f;

		[DebugEditable(DisplayName = "Pulse Scale", Step = 0.01f, Min = 1f, Max = 1.5f)]
		public float PulseScale { get; set; } = 1.12f;

		[DebugEditable(DisplayName = "Use Pip Size", Step = 1, Min = 2, Max = 32)]
		public int UsePipSize { get; set; } = 8;

		[DebugEditable(DisplayName = "Use Pip Gap", Step = 1, Min = 0, Max = 32)]
		public int UsePipGap { get; set; } = 1;

		[DebugEditable(DisplayName = "Use Pip Inset Right", Step = 1, Min = 0, Max = 40)]
		public int UsePipInsetRight { get; set; } = 6;

		[DebugEditable(DisplayName = "Use Pip Inset Bottom", Step = 1, Min = 0, Max = 40)]
		public int UsePipInsetBottom { get; set; } = 6;

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
			var tooltip = EntityManager.GetEntity(TooltipEntityName);
			if (tooltip?.GetComponent<EquipmentTooltipState>() == null)
			{
				if (tooltip != null)
				{
					EntityManager.DestroyEntity(tooltip.Id);
				}

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
					ui.ShowHoverHighlight = entity.GetComponent<EquippedEquipment>().Equipment.IsAvailable;

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
			float opacity = equipped.Equipment.IsAvailable ? 1f : UsedOpacity;
			Color background = CardPalette.Background(equipped.Equipment.Color);
			Color socket = CardPalette.Gutter(equipped.Equipment.Color);

			var shadow = new Rectangle(
				drawBounds.X,
				drawBounds.Y + ShadowOffsetY,
				drawBounds.Width,
				Math.Max(1, drawBounds.Height - ShadowOffsetY));
			DrawRoundedRect(shadow, Color.Black * ShadowAlpha * opacity);
			DrawRoundedRect(drawBounds, background * opacity);

			bool hasBlock = equipped.Equipment.Block > 0;
			int footerHeight = hasBlock
				? Math.Max(1, drawBounds.Height - SlotHeight)
				: 0;
			var socketBounds = new Rectangle(
				drawBounds.X,
				drawBounds.Y,
				drawBounds.Width,
				hasBlock ? Math.Max(1, drawBounds.Height - footerHeight) : drawBounds.Height);
			DrawRoundedRectPerCorner(
				socketBounds,
				socket * opacity,
				PanelCornerRadius,
				PanelCornerRadius,
				hasBlock ? 0 : PanelCornerRadius,
				hasBlock ? 0 : PanelCornerRadius);

			DrawEquipmentIcon(equipped.Equipment, socketBounds, opacity);
			DrawUsePips(equipped.Equipment, socketBounds, opacity);
			if (hasBlock)
			{
				var footer = new Rectangle(drawBounds.X, socketBounds.Bottom, drawBounds.Width, footerHeight);
				int chipHeight = Math.Max(1, ChipLabelHeight + ChipValueHeight);
				int chipWidth = Math.Max(1, footer.Width - FooterPadding * 2);
				int chipY = Math.Max(footer.Y, footer.Bottom - FooterPadding - chipHeight);
				var blockRect = new Rectangle(
					footer.Center.X - chipWidth / 2,
					chipY,
					chipWidth,
					chipHeight);
				DrawFooterStatChip(
					blockRect,
					"BLOCK",
					equipped.Equipment.Block.ToString(),
					CardPalette.BlockLabelSlabBackground(equipped.Equipment.Color),
					CardPalette.BlockLabelSlabText(equipped.Equipment.Color),
					CardPalette.BlockChipBackground(equipped.Equipment.Color),
					CardPalette.BlockChipText(equipped.Equipment.Color),
					opacity);
			}
			if (equipped.Equipment.CanActivateDuringActionPhase)
			{
				DrawAbilityStar(drawBounds, opacity);
			}
		}

		private void DrawEquipmentIcon(EquipmentBase equipment, Rectangle socketBounds, float opacity)
		{
			Texture2D texture = EquipmentArtService.GetTexture(_imageAssets, equipment);
			if (texture == null) return;
			int size = Math.Min(SlotIconSize, Math.Min(socketBounds.Width, socketBounds.Height));
			var destination = EquipmentArtService.GetContainedBounds(
				texture,
				new Rectangle(socketBounds.Center.X - size / 2, socketBounds.Center.Y - size / 2, size, size));
			_spriteBatch.Draw(texture, destination, Color.White * opacity);
		}

		private void DrawUsePips(EquipmentBase equipment, Rectangle socketBounds, float opacity)
		{
			if (_imageAssets == null || _graphicsDevice == null || equipment == null) return;
			int count = Math.Max(0, equipment.MaxUses);
			if (count == 0) return;

			int pipSize = Math.Max(2, UsePipSize);
			int stride = pipSize + Math.Max(0, UsePipGap);
			int totalWidth = count * pipSize + Math.Max(0, count - 1) * Math.Max(0, UsePipGap);
			int right = socketBounds.Right - UsePipInsetRight;
			int bottom = socketBounds.Bottom - UsePipInsetBottom;
			int startX = right - totalWidth;
			int y = bottom - pipSize;

			int radius = Math.Max(1, pipSize / 2);
			Texture2D circle = _imageAssets.GetAntiAliasedCircle(radius);
			int holeThickness = Math.Max(1, pipSize / 4);
			Texture2D holeRing = PrimitiveTextureFactory.GetAntialiasedRingMask(
				_graphicsDevice,
				pipSize,
				pipSize,
				holeThickness);
			Color pipColor = equipment.Color == CardData.CardColor.White ? Color.Black : Color.White;
			int remaining = Math.Max(0, equipment.RemainingUses);

			for (int i = 0; i < count; i++)
			{
				var pip = new Rectangle(startX + i * stride, y, pipSize, pipSize);
				if (i < remaining)
				{
					int inset = Math.Max(1, pipSize / 8);
					int fill = Math.Max(1, pipSize - inset * 2);
					_spriteBatch.Draw(circle, pip, pipColor * (0.18f * opacity));
					_spriteBatch.Draw(circle, new Rectangle(pip.X + inset, pip.Y + inset, fill, fill), pipColor * opacity);
				}
				else
				{
					_spriteBatch.Draw(holeRing, pip, pipColor * (0.62f * opacity));
				}
			}
		}

		private void DrawFooterStatChip(
			Rectangle bounds,
			string label,
			string value,
			Color labelFill,
			Color labelText,
			Color valueFill,
			Color valueText,
			float opacity)
		{
			var labelFont = FontSingleton.ChakraPetchFont;
			var valueFont = FontSingleton.TitleFont;
			if (labelFont == null || valueFont == null) return;

			var labelBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, Math.Min(ChipLabelHeight, bounds.Height));
			var valueBounds = new Rectangle(
				bounds.X,
				labelBounds.Bottom,
				bounds.Width,
				Math.Max(1, bounds.Bottom - labelBounds.Bottom));
			DrawRoundedRectPerCorner(
				labelBounds,
				labelFill * opacity,
				ChipCornerRadius,
				ChipCornerRadius,
				0,
				0);
			DrawRoundedRectPerCorner(
				valueBounds,
				valueFill * opacity,
				0,
				0,
				ChipCornerRadius,
				ChipCornerRadius);

			Vector2 labelSize = labelFont.MeasureString(label) * LabelFontScale;
			Vector2 valueSize = valueFont.MeasureString(value) * ValueFontScale;
			var labelPos = new Vector2(
				bounds.Center.X - labelSize.X / 2f,
				labelBounds.Center.Y - labelSize.Y / 2f);
			var valuePos = new Vector2(
				bounds.Center.X - valueSize.X / 2f,
				valueBounds.Center.Y - valueSize.Y / 2f);
			_spriteBatch.DrawString(labelFont, label, labelPos, labelText * opacity, 0f, Vector2.Zero, LabelFontScale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(valueFont, value, valuePos, valueText * opacity, 0f, Vector2.Zero, ValueFontScale, SpriteEffects.None, 0f);
		}

		private void DrawAbilityStar(Rectangle bounds, float opacity)
		{
			int size = Math.Max(1, AbilityMarkSize);
			var texture = PrimitiveTextureFactory.GetAntialiasedPolygonMask(
				_graphicsDevice,
				size,
				size,
				"equipment-ability-star-v1",
				AbilityStarPoints);
			Color color = CardPalette.AbilityRed * opacity;
			_spriteBatch.Draw(
				texture,
				new Rectangle(bounds.X + AbilityMarkOffsetX, bounds.Y + AbilityMarkOffsetY, size, size),
				color);
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

		private void DrawRoundedRectPerCorner(
			Rectangle bounds,
			Color color,
			int radiusTL,
			int radiusTR,
			int radiusBR,
			int radiusBL)
		{
			if (bounds.Width <= 0 || bounds.Height <= 0) return;
			int maxRadius = Math.Min(bounds.Width, bounds.Height) / 2;
			var key = (
				Width: bounds.Width,
				Height: bounds.Height,
				RadiusTL: Math.Min(Math.Max(0, radiusTL), maxRadius),
				RadiusTR: Math.Min(Math.Max(0, radiusTR), maxRadius),
				RadiusBR: Math.Min(Math.Max(0, radiusBR), maxRadius),
				RadiusBL: Math.Min(Math.Max(0, radiusBL), maxRadius));
			if (!_perCornerRoundedRectCache.TryGetValue(key, out var texture))
			{
				texture = RoundedRectTextureFactory.CreateRoundedRectPerCorner(
					_graphicsDevice,
					key.Width,
					key.Height,
					key.RadiusTL,
					key.RadiusTR,
					key.RadiusBR,
					key.RadiusBL);
				_perCornerRoundedRectCache[key] = texture;
			}
			_spriteBatch.Draw(texture, bounds, color);
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
			var battleTooltip = EntityManager.GetEntity(TooltipEntityName);
			if (battleTooltip?.GetComponent<EquipmentTooltipState>() != null)
			{
				EntityManager.DestroyEntity(battleTooltip.Id);
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
			_perCornerRoundedRectCache.Clear();
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
