using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Equipment;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Utils;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Equipment Tooltip")]
	public class EquipmentTooltipDisplaySystem : Core.System
	{
		private const string FreeActionTagText = "FREE ACTION";

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private readonly Dictionary<(int Width, int Height, int Radius), Texture2D> _roundedRectCache = new();
		private readonly Dictionary<(int Width, int Height, int RadiusTL, int RadiusTR, int RadiusBR, int RadiusBL), Texture2D> _perCornerRoundedRectCache = new();
		private readonly string _tooltipEntityName;

		[DebugEditable(DisplayName = "Tooltip Width", Step = 1, Min = 180, Max = 600)]
		public int TooltipWidth { get; set; } = 300;

		[DebugEditable(DisplayName = "Tooltip Min Height", Step = 1, Min = 80, Max = 400)]
		public int TooltipMinHeight { get; set; } = 148;

		[DebugEditable(DisplayName = "Tooltip Gap", Step = 1, Min = 0, Max = 100)]
		public int TooltipGap { get; set; } = 20;

		[DebugEditable(DisplayName = "Tooltip Radius", Step = 1, Min = 0, Max = 40)]
		public int CornerRadius { get; set; } = 8;

		[DebugEditable(DisplayName = "Stat Gutter Width", Step = 1, Min = 30, Max = 120)]
		public int GutterWidth { get; set; } = 54;

		[DebugEditable(DisplayName = "Gutter Icon Size", Step = 1, Min = 12, Max = 100)]
		public int GutterIconSize { get; set; } = 38;

		[DebugEditable(DisplayName = "Chip Size", Step = 1, Min = 20, Max = 100)]
		public int ChipSize { get; set; } = 38;

		[DebugEditable(DisplayName = "Chip Label Height", Step = 1, Min = 6, Max = 30)]
		public int ChipLabelHeight { get; set; } = 13;

		[DebugEditable(DisplayName = "Chip Value Height", Step = 1, Min = 10, Max = 60)]
		public int ChipValueHeight { get; set; } = 25;

		[DebugEditable(DisplayName = "Chip Corner Radius", Step = 1, Min = 0, Max = 20)]
		public int ChipCornerRadius { get; set; } = 3;

		[DebugEditable(DisplayName = "Gutter Padding Top", Step = 1, Min = 0, Max = 40)]
		public int GutterPaddingTop { get; set; } = 10;

		[DebugEditable(DisplayName = "Gutter Padding Bottom", Step = 1, Min = 0, Max = 40)]
		public int GutterPaddingBottom { get; set; } = 12;

		[DebugEditable(DisplayName = "Gutter Gap", Step = 1, Min = 0, Max = 40)]
		public int GutterGap { get; set; } = 8;

		[DebugEditable(DisplayName = "Body Padding Top", Step = 1, Min = 0, Max = 60)]
		public int BodyPaddingTop { get; set; } = 12;

		[DebugEditable(DisplayName = "Body Padding Right", Step = 1, Min = 0, Max = 60)]
		public int BodyPaddingRight { get; set; } = 14;

		[DebugEditable(DisplayName = "Body Padding Bottom", Step = 1, Min = 0, Max = 60)]
		public int BodyPaddingBottom { get; set; } = 12;

		[DebugEditable(DisplayName = "Body Padding Left", Step = 1, Min = 0, Max = 60)]
		public int BodyPaddingLeft { get; set; } = 10;

		[DebugEditable(DisplayName = "Body Gap", Step = 1, Min = 0, Max = 30)]
		public int BodyGap { get; set; } = 6;

		[DebugEditable(DisplayName = "Rule Height", Step = 1, Min = 1, Max = 8)]
		public int RuleHeight { get; set; } = 2;

		[DebugEditable(DisplayName = "Text Block Padding X", Step = 1, Min = 0, Max = 30)]
		public int TextBlockPaddingX { get; set; } = 6;

		[DebugEditable(DisplayName = "Text Block Padding Y", Step = 1, Min = 0, Max = 30)]
		public int TextBlockPaddingY { get; set; } = 4;

		[DebugEditable(DisplayName = "Text Block Radius", Step = 1, Min = 0, Max = 20)]
		public int TextBlockCornerRadius { get; set; } = 3;

		[DebugEditable(DisplayName = "Body Line Height", Step = 0.01f, Min = 0.5f, Max = 3f)]
		public float BodyLineHeightMultiplier { get; set; } = 1f;

		[DebugEditable(DisplayName = "Shadow Offset Y", Step = 1, Min = 0, Max = 30)]
		public int ShadowOffsetY { get; set; } = 6;

		[DebugEditable(DisplayName = "Shadow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ShadowAlpha { get; set; } = 0.55f;

		[DebugEditable(DisplayName = "Title Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float TitleFontScale { get; set; } = 0.14f;

		[DebugEditable(DisplayName = "Body Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float BodyFontScale { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Chip Value Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ChipValueFontScale { get; set; } = 0.13f;

		[DebugEditable(DisplayName = "Chip Label Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ChipLabelFontScale { get; set; } = 0.06f;

		[DebugEditable(DisplayName = "Tag Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float TagFontScale { get; set; } = 0.070f;

		[DebugEditable(DisplayName = "Tag Corner Radius", Step = 1, Min = 0, Max = 20)]
		public int TagCornerRadius { get; set; } = 3;

		[DebugEditable(DisplayName = "Tag Padding X", Step = 1, Min = 0, Max = 40)]
		public int TagPaddingX { get; set; } = 8;

		[DebugEditable(DisplayName = "Tag Padding Y", Step = 1, Min = 0, Max = 40)]
		public int TagPaddingY { get; set; } = 3;

		[DebugEditable(DisplayName = "Tag Row Padding Top", Step = 1, Min = 0, Max = 40)]
		public int TagRowPaddingTop { get; set; } = 4;

		[DebugEditable(DisplayName = "Fade Seconds", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float FadeSeconds { get; set; } = 0.10f;

		public EquipmentTooltipDisplaySystem(
			EntityManager entityManager,
			GraphicsDevice graphicsDevice,
			SpriteBatch spriteBatch,
			ImageAssetService imageAssets,
			string tooltipEntityName = null) : base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_tooltipEntityName = tooltipEntityName ?? string.Empty;
			if (graphicsDevice != null)
			{
				_pixel = _imageAssets.GetPixel(Color.White);
			}
			EventManager.Subscribe<DeleteCachesEvent>(OnDeleteCaches);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			var tooltipEntity = GetTooltipEntity();
			if (tooltipEntity == null) return;

			var root = ResolveLayoutRoot(tooltipEntity);
			var state = tooltipEntity.GetComponent<EquipmentTooltipState>();
			var hovered = FindHoveredEquipment();
			state.TargetVisible = hovered != null;
			if (hovered != null)
			{
				state.EquipmentEntity = hovered.EquipmentEntity;
				state.AnchorEntity = hovered.AnchorEntity;
				LayoutTooltip(root, tooltipEntity, hovered.AnchorEntity, hovered.EquipmentEntity, state);
			}

			float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
			float delta = FadeSeconds <= 0f ? 1f : elapsed / FadeSeconds;
			state.Alpha01 = MathHelper.Clamp(
				state.Alpha01 + (state.TargetVisible ? delta : -delta),
				0f,
				1f);

			var ui = tooltipEntity.GetComponent<UIElement>();
			ui.Bounds = new Rectangle(0, 0, state.Bounds.Width, state.Bounds.Height);
			ui.IsHidden = state.Alpha01 <= 0f;
			ui.IsInteractable = false;
		}

		public void Draw()
		{
			if (_graphicsDevice == null || _spriteBatch == null || _pixel == null) return;
			var tooltipEntity = GetTooltipEntity();
			var state = tooltipEntity?.GetComponent<EquipmentTooltipState>();
			var equipped = state?.EquipmentEntity?.GetComponent<EquippedEquipment>();
			if (tooltipEntity == null || state == null || equipped?.Equipment == null || state.Alpha01 <= 0f)
			{
				return;
			}

			var localBounds = new Rectangle(0, 0, state.Bounds.Width, state.Bounds.Height);
			Rectangle bounds = TransformResolverService.ResolveLocalBounds(
				EntityManager,
				tooltipEntity,
				localBounds);
			DrawTooltip(bounds, equipped, state.Alpha01);
		}

		public Rectangle GetTooltipWorldBounds()
		{
			var tooltipEntity = GetTooltipEntity();
			var state = tooltipEntity?.GetComponent<EquipmentTooltipState>();
			return tooltipEntity == null || state == null
				? Rectangle.Empty
				: TransformResolverService.ResolveLocalBounds(
					EntityManager,
					tooltipEntity,
					new Rectangle(0, 0, state.Bounds.Width, state.Bounds.Height));
		}

		private HoveredEquipment FindHoveredEquipment()
		{
			var explicitSources = EntityManager.GetEntitiesWithComponent<EquipmentTooltipSource>()
				.Select(entity => new
				{
					Anchor = entity,
					Source = entity.GetComponent<EquipmentTooltipSource>(),
					Ui = entity.GetComponent<UIElement>(),
				})
				.Where(item => item.Source?.EquipmentEntity?.GetComponent<EquippedEquipment>()?.Equipment != null
					&& string.Equals(item.Source.TooltipEntityName, _tooltipEntityName, StringComparison.OrdinalIgnoreCase)
					&& item.Ui?.IsHovered == true
					&& !item.Ui.IsHidden
					&& item.Ui.TooltipType == TooltipType.Equipment)
				.Select(item => new HoveredEquipment(item.Anchor, item.Source.EquipmentEntity));

			var directSources = EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Where(entity =>
				{
					var zone = entity.GetComponent<EquipmentZone>();
					var ui = entity.GetComponent<UIElement>();
					return entity.GetComponent<EquipmentTooltipSource>() == null
						&& (zone == null
							|| zone.Zone is EquipmentZoneType.Default or EquipmentZoneType.AssignedBlock)
						&& ui?.IsHovered == true
						&& !ui.IsHidden
						&& ui.TooltipType == TooltipType.Equipment;
				})
				.Select(entity => new HoveredEquipment(entity, entity));

			return explicitSources
				.Concat(directSources)
				.OrderByDescending(item => item.AnchorEntity.GetComponent<Transform>()?.ZOrder ?? 0)
				.ThenByDescending(item => item.AnchorEntity.Id)
				.FirstOrDefault();
		}

		private void LayoutTooltip(
			Entity root,
			Entity tooltipEntity,
			Entity anchorEntity,
			Entity equipmentEntity,
			EquipmentTooltipState state)
		{
			var equipment = equipmentEntity.GetComponent<EquippedEquipment>().Equipment;
			int height = CalculateHeight(equipment);
			Rectangle panelBounds = TransformResolverService.ResolveUIBounds(
				EntityManager,
				anchorEntity,
				anchorEntity.GetComponent<UIElement>());
			Vector2 rootWorld = root == null
				? Vector2.Zero
				: TransformResolverService.ResolveWorldPosition(EntityManager, root);
			int worldX = panelBounds.Right + TooltipGap;
			int worldY = panelBounds.Center.Y - height / 2;
			worldX = Math.Max(0, Math.Min(worldX, Game1.VirtualWidth - TooltipWidth));
			worldY = Math.Max(0, Math.Min(worldY, Game1.VirtualHeight - height));

			var transform = tooltipEntity.GetComponent<Transform>();
			transform.Position = new Vector2(worldX - rootWorld.X, worldY - rootWorld.Y);
			transform.Scale = Vector2.One;
			transform.Rotation = 0f;
			transform.ZOrder = 10002;
			state.Bounds = new Rectangle(0, 0, TooltipWidth, height);
		}

		private Entity ResolveLayoutRoot(Entity tooltipEntity)
		{
			var parent = tooltipEntity?.GetComponent<ParentTransform>()?.Parent;
			return parent?.GetComponent<EquipmentDisplayRoot>() != null ? parent : null;
		}

		private Entity GetTooltipEntity()
		{
			var tooltips = EntityManager.GetEntitiesWithComponent<EquipmentTooltipState>();
			if (string.IsNullOrWhiteSpace(_tooltipEntityName))
			{
				return tooltips.FirstOrDefault(entity =>
						string.Equals(
							entity.Name,
							EquipmentDisplaySystem.TooltipEntityName,
							StringComparison.OrdinalIgnoreCase))
					?? tooltips.FirstOrDefault();
			}
			return tooltips.FirstOrDefault(entity =>
				string.Equals(entity.Name, _tooltipEntityName, StringComparison.OrdinalIgnoreCase));
		}

		private int CalculateHeight(EquipmentBase equipment)
		{
			var titleFont = FontSingleton.TitleFont;
			var bodyFont = FontSingleton.ChakraPetchFont;
			var flavorFont = FontSingleton.ChakraPetchBoldItalicFont;
			if (titleFont == null || bodyFont == null || flavorFont == null) return TooltipMinHeight;

			int bodyWidth = Math.Max(1, TooltipWidth - GutterWidth - BodyPaddingLeft - BodyPaddingRight);
			float bodyHeight = BodyPaddingTop;
			bodyHeight += titleFont.MeasureString(equipment.Name ?? string.Empty).Y * TitleFontScale;
			bodyHeight += BodyGap;
			bodyHeight += RuleHeight;
			bodyHeight += BodyGap;
			bodyHeight += MeasureTextBlockHeight(bodyFont, equipment.Text, BodyFontScale, bodyWidth);
			if (!string.IsNullOrWhiteSpace(equipment.Text)
				&& !string.IsNullOrWhiteSpace(equipment.FlavorText))
			{
				bodyHeight += BodyGap;
			}
			bodyHeight += MeasureTextBlockHeight(flavorFont, equipment.FlavorText, BodyFontScale, bodyWidth);
			if (equipment.CanActivateDuringActionPhase)
			{
				bodyHeight += TagRowPaddingTop
					+ TagPaddingY * 2
					+ bodyFont.MeasureString(FreeActionTagText).Y * TagFontScale;
			}
			bodyHeight += BodyPaddingBottom;

			float gutterHeight = GutterPaddingTop
				+ GutterIconSize
				+ GutterPaddingBottom;
			if (equipment.Block > 0)
			{
				gutterHeight += GutterGap + ChipLabelHeight + ChipValueHeight;
			}
			return Math.Max(TooltipMinHeight, (int)Math.Ceiling(Math.Max(bodyHeight, gutterHeight)));
		}

		private void DrawTooltip(Rectangle bounds, EquippedEquipment equipped, float alpha)
		{
			var equipment = equipped.Equipment;
			var shadow = new Rectangle(
				bounds.X,
				bounds.Y + ShadowOffsetY,
				bounds.Width,
				Math.Max(1, bounds.Height - ShadowOffsetY));
			DrawRoundedRect(shadow, Color.Black * ShadowAlpha * alpha);
			DrawRoundedRect(bounds, CardPalette.Background(equipment.Color) * alpha);

			var gutter = new Rectangle(
				bounds.X,
				bounds.Y,
				Math.Min(GutterWidth, bounds.Width),
				bounds.Height);
			DrawRoundedRectPerCorner(
				gutter,
				CardPalette.Gutter(equipment.Color) * alpha,
				CornerRadius,
				0,
				0,
				CornerRadius);
			DrawGutter(gutter, equipment, alpha);

			var body = new Rectangle(
				gutter.Right,
				bounds.Y,
				Math.Max(1, bounds.Right - gutter.Right),
				bounds.Height);
			DrawBody(body, equipment, alpha);
		}

		private void DrawGutter(
			Rectangle gutter,
			EquipmentBase equipment,
			float alpha)
		{
			int x = gutter.Center.X;
			int y = gutter.Y + GutterPaddingTop;
			var icon = EquipmentArtService.GetTexture(_imageAssets, equipment);
			if (icon != null)
			{
				var iconBounds = EquipmentArtService.GetContainedBounds(
					icon,
					new Rectangle(x - GutterIconSize / 2, y, GutterIconSize, GutterIconSize));
				_spriteBatch.Draw(icon, iconBounds, Color.White * alpha);
			}
			y += GutterIconSize + GutterGap;

			if (equipment.Block > 0)
			{
				int chipHeight = Math.Max(1, ChipLabelHeight + ChipValueHeight);
				var blockChip = new Rectangle(x - ChipSize / 2, y, ChipSize, chipHeight);
				DrawChipStack(
					blockChip,
					equipment.Block.ToString(),
					"BLOCK",
					CardPalette.BlockLabelSlabBackground(equipment.Color),
					CardPalette.BlockLabelSlabText(equipment.Color),
					CardPalette.BlockChipBackground(equipment.Color),
					CardPalette.BlockChipText(equipment.Color),
					alpha);
			}
		}

		private void DrawChipStack(
			Rectangle bounds,
			string value,
			string label,
			Color labelFill,
			Color labelText,
			Color valueFill,
			Color valueText,
			float alpha)
		{
			var valueFont = FontSingleton.TitleFont;
			var labelFont = FontSingleton.ChakraPetchFont;
			if (valueFont == null || labelFont == null) return;
			var labelBounds = new Rectangle(bounds.X, bounds.Y, bounds.Width, Math.Min(ChipLabelHeight, bounds.Height));
			var valueBounds = new Rectangle(
				bounds.X,
				labelBounds.Bottom,
				bounds.Width,
				Math.Max(1, bounds.Bottom - labelBounds.Bottom));
			DrawRoundedRectPerCorner(
				labelBounds,
				labelFill * alpha,
				ChipCornerRadius,
				ChipCornerRadius,
				0,
				0);
			DrawRoundedRectPerCorner(
				valueBounds,
				valueFill * alpha,
				0,
				0,
				ChipCornerRadius,
				ChipCornerRadius);

			Vector2 valueSize = valueFont.MeasureString(value) * ChipValueFontScale;
			Vector2 labelSize = labelFont.MeasureString(label) * ChipLabelFontScale;
			var labelPos = new Vector2(
				bounds.Center.X - labelSize.X / 2f,
				labelBounds.Center.Y - labelSize.Y / 2f);
			var valuePos = new Vector2(
				bounds.Center.X - valueSize.X / 2f,
				valueBounds.Center.Y - valueSize.Y / 2f);
			_spriteBatch.DrawString(labelFont, label, labelPos, labelText * alpha, 0f, Vector2.Zero, ChipLabelFontScale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(valueFont, value, valuePos, valueText * alpha, 0f, Vector2.Zero, ChipValueFontScale, SpriteEffects.None, 0f);
		}

		private void DrawBody(
			Rectangle body,
			EquipmentBase equipment,
			float alpha)
		{
			var titleFont = FontSingleton.TitleFont;
			var bodyFont = FontSingleton.ChakraPetchFont;
			var flavorFont = FontSingleton.ChakraPetchBoldItalicFont;
			if (titleFont == null || bodyFont == null || flavorFont == null) return;

			int contentWidth = Math.Max(1, body.Width - BodyPaddingLeft - BodyPaddingRight);
			float x = body.X + BodyPaddingLeft;
			float y = body.Y + BodyPaddingTop;
			string name = equipment.Name ?? equipment.Id ?? string.Empty;
			_spriteBatch.DrawString(
				titleFont,
				name,
				new Vector2(x, y),
				CardPalette.NameText(equipment.Color) * alpha,
				0f,
				Vector2.Zero,
				TitleFontScale,
				SpriteEffects.None,
				0f);
			y += titleFont.MeasureString(name).Y * TitleFontScale + BodyGap;
			_spriteBatch.Draw(
				_pixel,
				new Rectangle((int)x, (int)y, contentWidth, RuleHeight),
				CardPalette.RuleLine(equipment.Color) * alpha);
			y += RuleHeight + BodyGap;

			y = DrawTextBlock(
				bodyFont,
				equipment.Text,
				new Vector2(x, y),
				contentWidth,
				CardPalette.NameText(equipment.Color) * alpha,
				CardPalette.TextBackground(equipment.Color) * alpha,
				BodyFontScale);
			if (!string.IsNullOrWhiteSpace(equipment.Text)
				&& !string.IsNullOrWhiteSpace(equipment.FlavorText))
			{
				y += BodyGap;
			}
			y = DrawTextBlock(
				flavorFont,
				equipment.FlavorText,
				new Vector2(x, y),
				contentWidth,
				CardPalette.NameText(equipment.Color) * alpha,
				CardPalette.TextBackground(equipment.Color) * alpha,
				BodyFontScale);

			if (equipment.CanActivateDuringActionPhase)
			{
				DrawFreeActionTag(body, bodyFont, x, y, equipment.Color, alpha);
			}
		}

		private void DrawFreeActionTag(
			Rectangle body,
			SpriteFont bodyFont,
			float contentX,
			float contentY,
			CardData.CardColor color,
			float alpha)
		{
			var tagBounds = ComputeFreeActionTagBounds(bodyFont, body, contentX, contentY);
			DrawRoundedFilledBordered(
				tagBounds,
				TagCornerRadius,
				1,
				CardPalette.Background(color),
				CardPalette.FreeChipBorder(color),
				alpha);
			_spriteBatch.DrawString(
				bodyFont,
				FreeActionTagText,
				new Vector2(tagBounds.X + TagPaddingX, tagBounds.Y + TagPaddingY),
				CardPalette.FreeChipText(color) * alpha,
				0f,
				Vector2.Zero,
				TagFontScale,
				SpriteEffects.None,
				0f);
		}

		private Rectangle ComputeFreeActionTagBounds(
			SpriteFont font,
			Rectangle body,
			float contentX,
			float contentY)
		{
			Vector2 textSize = font.MeasureString(FreeActionTagText) * TagFontScale;
			int pillW = (int)Math.Ceiling(textSize.X) + TagPaddingX * 2;
			int pillH = (int)Math.Ceiling(textSize.Y) + TagPaddingY * 2;
			int pillY = body.Bottom - BodyPaddingBottom - pillH;
			pillY = Math.Max((int)(contentY + TagRowPaddingTop), pillY);
			return new Rectangle((int)contentX, pillY, pillW, pillH);
		}

		private void DrawRoundedFilledBordered(
			Rectangle bounds,
			int radius,
			int borderThickness,
			Color fill,
			Color border,
			float alpha)
		{
			DrawRoundedRectWithRadius(bounds, border * alpha, radius);
			var inner = Inset(bounds, borderThickness);
			if (inner.Width <= 0 || inner.Height <= 0) return;
			DrawRoundedRectWithRadius(inner, fill * alpha, Math.Max(0, radius - borderThickness));
		}

		private float DrawTextBlock(
			SpriteFont font,
			string text,
			Vector2 position,
			int maxWidth,
			Color textColor,
			Color fillColor,
			float scale)
		{
			if (string.IsNullOrWhiteSpace(text)) return position.Y;
			int innerWidth = Math.Max(1, maxWidth - TextBlockPaddingX * 2);
			var lines = TextUtils.WrapText(font, text, scale, innerWidth);
			float lineHeight = font.LineSpacing * scale * BodyLineHeightMultiplier;
			int blockHeight = Math.Max(
				1,
				(int)Math.Ceiling(lines.Count * lineHeight + TextBlockPaddingY * 2));
			var blockBounds = new Rectangle(
				(int)position.X,
				(int)position.Y,
				maxWidth,
				blockHeight);
			DrawRoundedRectWithRadius(blockBounds, fillColor, TextBlockCornerRadius);

			float y = position.Y + TextBlockPaddingY;
			foreach (string line in lines)
			{
				_spriteBatch.DrawString(
					font,
					line,
					new Vector2(position.X + TextBlockPaddingX, y),
					textColor,
					0f,
					Vector2.Zero,
					scale,
					SpriteEffects.None,
					0f);
				y += lineHeight;
			}
			return blockBounds.Bottom;
		}

		private float MeasureTextBlockHeight(
			SpriteFont font,
			string text,
			float scale,
			int maxWidth)
		{
			if (string.IsNullOrWhiteSpace(text)) return 0f;
			int innerWidth = Math.Max(1, maxWidth - TextBlockPaddingX * 2);
			int lineCount = TextUtils.WrapText(font, text, scale, innerWidth).Count;
			return lineCount * font.LineSpacing * scale * BodyLineHeightMultiplier + TextBlockPaddingY * 2;
		}

		private void DrawRoundedRect(Rectangle bounds, Color color)
		{
			int radius = Math.Min(CornerRadius, Math.Min(bounds.Width, bounds.Height) / 2);
			DrawRoundedRectWithRadius(bounds, color, radius);
		}

		private void DrawRoundedRectWithRadius(Rectangle bounds, Color color, int radius)
		{
			radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
			var key = (Width: bounds.Width, Height: bounds.Height, Radius: radius);
			if (!_roundedRectCache.TryGetValue(key, out var texture))
			{
				texture = RoundedRectTextureFactory.CreateRoundedRect(
					_graphicsDevice,
					key.Width,
					key.Height,
					key.Radius);
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

		private static Rectangle Inset(Rectangle bounds, int amount)
		{
			return new Rectangle(
				bounds.X + amount,
				bounds.Y + amount,
				Math.Max(1, bounds.Width - amount * 2),
				Math.Max(1, bounds.Height - amount * 2));
		}

		private sealed record HoveredEquipment(Entity AnchorEntity, Entity EquipmentEntity);

		private void OnDeleteCaches(DeleteCachesEvent evt)
		{
			_roundedRectCache.Clear();
			_perCornerRoundedRectCache.Clear();
		}
	}
}
