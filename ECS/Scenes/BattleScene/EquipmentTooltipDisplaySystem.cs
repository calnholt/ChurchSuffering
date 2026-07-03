using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
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

		// v1 mockup .tooltip .tag
		private static readonly Color TagFill = new Color(255, 255, 255, 15);
		private static readonly Color TagBorder = new Color(255, 255, 255, 46);
		private static readonly Color TagText = new Color(200, 192, 184);

		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private readonly Dictionary<(int Width, int Height, int Radius), Texture2D> _roundedRectCache = new();
		private readonly string _tooltipEntityName;

		[DebugEditable(DisplayName = "Tooltip Width", Step = 1, Min = 180, Max = 600)]
		public int TooltipWidth { get; set; } = 300;

		[DebugEditable(DisplayName = "Tooltip Min Height", Step = 1, Min = 80, Max = 400)]
		public int TooltipMinHeight { get; set; } = 148;

		[DebugEditable(DisplayName = "Tooltip Gap", Step = 1, Min = 0, Max = 100)]
		public int TooltipGap { get; set; } = 20;

		[DebugEditable(DisplayName = "Tooltip Border", Step = 1, Min = 1, Max = 12)]
		public int BorderThickness { get; set; } = 2;

		[DebugEditable(DisplayName = "Tooltip Radius", Step = 1, Min = 0, Max = 40)]
		public int CornerRadius { get; set; } = 8;

		[DebugEditable(DisplayName = "Color Stripe Width", Step = 1, Min = 1, Max = 30)]
		public int StripeWidth { get; set; } = 6;

		[DebugEditable(DisplayName = "Stat Gutter Width", Step = 1, Min = 30, Max = 120)]
		public int GutterWidth { get; set; } = 54;

		[DebugEditable(DisplayName = "Gutter Icon Size", Step = 1, Min = 12, Max = 100)]
		public int GutterIconSize { get; set; } = 38;

		[DebugEditable(DisplayName = "Chip Size", Step = 1, Min = 20, Max = 100)]
		public int ChipSize { get; set; } = 38;

		[DebugEditable(DisplayName = "Body Padding", Step = 1, Min = 0, Max = 60)]
		public int BodyPadding { get; set; } = 12;

		[DebugEditable(DisplayName = "Title Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float TitleFontScale { get; set; } = 0.16f;

		[DebugEditable(DisplayName = "Body Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float BodyFontScale { get; set; } = 0.10f;

		[DebugEditable(DisplayName = "Chip Value Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ChipValueFontScale { get; set; } = 0.14f;

		[DebugEditable(DisplayName = "Chip Label Font Scale", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ChipLabelFontScale { get; set; } = 0.05f;

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
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return Array.Empty<Entity>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			var tooltipEntity = GetTooltipEntity();
			var root = string.IsNullOrWhiteSpace(_tooltipEntityName)
				? EntityManager.GetEntitiesWithComponent<EquipmentDisplayRoot>().FirstOrDefault()
				: null;
			if (tooltipEntity == null) return;

			var state = tooltipEntity.GetComponent<EquipmentTooltipState>();
			var hovered = FindHoveredEquipment();
			state.TargetVisible = hovered != null;
			if (hovered != null)
			{
				state.EquipmentEntity = hovered;
				LayoutTooltip(root, tooltipEntity, hovered, state);
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

		private Entity FindHoveredEquipment()
		{
			return EntityManager.GetEntitiesWithComponent<EquippedEquipment>()
				.Where(entity =>
				{
					var zone = entity.GetComponent<EquipmentZone>();
					var ui = entity.GetComponent<UIElement>();
					return (zone?.Zone ?? EquipmentZoneType.Default) == EquipmentZoneType.Default
						&& ui?.IsHovered == true
						&& !ui.IsHidden
						&& ui.TooltipType == TooltipType.Equipment;
				})
				.OrderByDescending(entity => entity.GetComponent<Transform>()?.ZOrder ?? 0)
				.ThenByDescending(entity => entity.Id)
				.FirstOrDefault();
		}

		private void LayoutTooltip(
			Entity root,
			Entity tooltipEntity,
			Entity equipmentEntity,
			EquipmentTooltipState state)
		{
			var equipment = equipmentEntity.GetComponent<EquippedEquipment>().Equipment;
			int height = CalculateHeight(equipment);
			Rectangle panelBounds = TransformResolverService.ResolveUIBounds(
				EntityManager,
				equipmentEntity,
				equipmentEntity.GetComponent<UIElement>());
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

		private Entity GetTooltipEntity()
		{
			var tooltips = EntityManager.GetEntitiesWithComponent<EquipmentTooltipState>();
			if (string.IsNullOrWhiteSpace(_tooltipEntityName))
			{
				return tooltips.FirstOrDefault();
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

			int bodyWidth = Math.Max(
				1,
				TooltipWidth - StripeWidth - GutterWidth - BodyPadding * 2);
			float height = BodyPadding;
			height += titleFont.MeasureString(equipment.Name ?? string.Empty).Y * TitleFontScale;
			height += 10;
			height += MeasureWrappedHeight(bodyFont, equipment.Text, BodyFontScale, bodyWidth);
			if (!string.IsNullOrWhiteSpace(equipment.Text)
				&& !string.IsNullOrWhiteSpace(equipment.FlavorText))
			{
				height += 6;
			}
			height += MeasureWrappedHeight(flavorFont, equipment.FlavorText, BodyFontScale, bodyWidth);
			if (equipment.CanActivateDuringActionPhase)
			{
				height += TagRowPaddingTop
					+ TagPaddingY * 2
					+ bodyFont.MeasureString(FreeActionTagText).Y * TagFontScale;
			}
			height += BodyPadding;
			return Math.Max(TooltipMinHeight, (int)Math.Ceiling(height));
		}

		private void DrawTooltip(Rectangle bounds, EquippedEquipment equipped, float alpha)
		{
			var equipment = equipped.Equipment;
			Color border = new Color(255, 255, 255, 217) * alpha;
			DrawRoundedRect(bounds, border);
			var inner = Inset(bounds, BorderThickness);
			DrawRoundedRect(inner, new Color(8, 8, 8, 240) * alpha);

			var stripe = new Rectangle(inner.X, inner.Y, Math.Min(StripeWidth, inner.Width), inner.Height);
			_spriteBatch.Draw(_pixel, stripe, GetStripeColor(equipment.Color) * alpha);

			var gutter = new Rectangle(
				stripe.Right,
				inner.Y,
				Math.Min(GutterWidth, Math.Max(1, inner.Right - stripe.Right)),
				inner.Height);
			_spriteBatch.Draw(_pixel, gutter, new Color(0, 0, 0, 70) * alpha);
			DrawGutter(gutter, equipment, alpha);

			var body = new Rectangle(
				gutter.Right,
				inner.Y,
				Math.Max(1, inner.Right - gutter.Right),
				inner.Height);
			DrawBody(body, equipment, alpha);
		}

		private void DrawGutter(
			Rectangle gutter,
			EquipmentBase equipment,
			float alpha)
		{
			int x = gutter.Center.X;
			int y = gutter.Y + 10;
			var icon = GetIcon(equipment.Slot);
			if (icon != null)
			{
				var iconBounds = new Rectangle(
					x - GutterIconSize / 2,
					y,
					GutterIconSize,
					GutterIconSize);
				_spriteBatch.Draw(icon, iconBounds, Color.White * alpha);
			}
			y += GutterIconSize + 8;

			var blockChip = new Rectangle(x - ChipSize / 2, y, ChipSize, ChipSize);
			DrawChip(blockChip, equipment.Block.ToString(), "BLOCK", new Color(42, 74, 94), alpha);
			y += ChipSize + 8;
			var usesChip = new Rectangle(x - ChipSize / 2, y, ChipSize, ChipSize);
			DrawChip(
				usesChip,
				Math.Max(0, equipment.RemainingUses).ToString(),
				"USES",
				new Color(29, 29, 29),
				alpha,
				new Color(255, 255, 255, 50));
		}

		private void DrawChip(
			Rectangle bounds,
			string value,
			string label,
			Color fill,
			float alpha,
			Color? border = null)
		{
			_spriteBatch.Draw(_pixel, bounds, fill * alpha);
			if (border.HasValue)
			{
				DrawBorder(bounds, border.Value * alpha, 1);
			}

			var valueFont = FontSingleton.TitleFont;
			var labelFont = FontSingleton.ChakraPetchFont;
			if (valueFont == null || labelFont == null) return;
			Vector2 valueSize = valueFont.MeasureString(value) * ChipValueFontScale;
			Vector2 labelSize = labelFont.MeasureString(label) * ChipLabelFontScale;
			float totalHeight = valueSize.Y + labelSize.Y - 2f;
			var valuePos = new Vector2(
				bounds.Center.X - valueSize.X / 2f,
				bounds.Center.Y - totalHeight / 2f);
			var labelPos = new Vector2(
				bounds.Center.X - labelSize.X / 2f,
				valuePos.Y + valueSize.Y - 2f);
			_spriteBatch.DrawString(valueFont, value, valuePos, Color.White * alpha, 0f, Vector2.Zero, ChipValueFontScale, SpriteEffects.None, 0f);
			_spriteBatch.DrawString(labelFont, label, labelPos, Color.White * alpha, 0f, Vector2.Zero, ChipLabelFontScale, SpriteEffects.None, 0f);
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

			int contentWidth = Math.Max(1, body.Width - BodyPadding * 2);
			float x = body.X + BodyPadding;
			float y = body.Y + BodyPadding;
			string name = equipment.Name ?? equipment.Id ?? string.Empty;
			_spriteBatch.DrawString(
				titleFont,
				name,
				new Vector2(x, y),
				Color.White * alpha,
				0f,
				Vector2.Zero,
				TitleFontScale,
				SpriteEffects.None,
				0f);
			y += titleFont.MeasureString(name).Y * TitleFontScale + 5f;
			_spriteBatch.Draw(
				_pixel,
				new Rectangle((int)x, (int)y, contentWidth, 2),
				GetStripeColor(equipment.Color) * alpha);
			y += 8f;

			y = DrawWrappedText(
				bodyFont,
				equipment.Text,
				new Vector2(x, y),
				contentWidth,
				new Color(184, 176, 168) * alpha);
			if (!string.IsNullOrWhiteSpace(equipment.Text)
				&& !string.IsNullOrWhiteSpace(equipment.FlavorText))
			{
				y += 6f;
			}
			y = DrawWrappedText(
				flavorFont,
				equipment.FlavorText,
				new Vector2(x, y),
				contentWidth,
				new Color(200, 192, 184) * alpha);

			if (equipment.CanActivateDuringActionPhase)
			{
				DrawFreeActionTag(body, bodyFont, x, y, alpha);
			}
		}

		private void DrawFreeActionTag(
			Rectangle body,
			SpriteFont bodyFont,
			float contentX,
			float contentY,
			float alpha)
		{
			var tagBounds = ComputeFreeActionTagBounds(bodyFont, body, contentX, contentY);
			DrawRoundedFilledBordered(tagBounds, TagCornerRadius, 1, TagFill, TagBorder, alpha);
			_spriteBatch.DrawString(
				bodyFont,
				FreeActionTagText,
				new Vector2(tagBounds.X + TagPaddingX, tagBounds.Y + TagPaddingY),
				TagText * alpha,
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
			int pillY = body.Bottom - BodyPadding - pillH;
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

		private float DrawWrappedText(
			SpriteFont font,
			string text,
			Vector2 position,
			int maxWidth,
			Color color)
		{
			if (string.IsNullOrWhiteSpace(text)) return position.Y;
			var lines = TextUtils.WrapText(font, text, BodyFontScale, maxWidth);
			float y = position.Y;
			float lineHeight = font.LineSpacing * BodyFontScale;
			foreach (string line in lines)
			{
				_spriteBatch.DrawString(
					font,
					line,
					new Vector2(position.X, y),
					color,
					0f,
					Vector2.Zero,
					BodyFontScale,
					SpriteEffects.None,
					0f);
				y += lineHeight;
			}
			return y;
		}

		private static float MeasureWrappedHeight(
			SpriteFont font,
			string text,
			float scale,
			int maxWidth)
		{
			if (string.IsNullOrWhiteSpace(text)) return 0f;
			return TextUtils.WrapText(font, text, scale, maxWidth).Count * font.LineSpacing * scale;
		}

		private Texture2D GetIcon(EquipmentSlot slot)
		{
			string key = slot.ToString().ToLowerInvariant();
			return _imageAssets.TryGetTexture(key);
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

		private void DrawBorder(Rectangle bounds, Color color, int thickness)
		{
			_spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, bounds.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Bottom - thickness, bounds.Width, thickness), color);
			_spriteBatch.Draw(_pixel, new Rectangle(bounds.X, bounds.Y, thickness, bounds.Height), color);
			_spriteBatch.Draw(_pixel, new Rectangle(bounds.Right - thickness, bounds.Y, thickness, bounds.Height), color);
		}

		private static Rectangle Inset(Rectangle bounds, int amount)
		{
			return new Rectangle(
				bounds.X + amount,
				bounds.Y + amount,
				Math.Max(1, bounds.Width - amount * 2),
				Math.Max(1, bounds.Height - amount * 2));
		}

		private static Color GetStripeColor(CardData.CardColor color)
		{
			return color switch
			{
				CardData.CardColor.Red => new Color(204, 34, 34),
				CardData.CardColor.White => new Color(153, 153, 153),
				CardData.CardColor.Black => new Color(51, 51, 51),
				_ => new Color(128, 128, 128),
			};
		}
	}
}
