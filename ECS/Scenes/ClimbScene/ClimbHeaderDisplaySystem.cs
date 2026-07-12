using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Climb Header")]
	public class ClimbHeaderDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly Texture2D _pixel;
		private readonly Texture2D _cardBackArt;
		private float _resourcePreviewAlpha;
		private ClimbResourceSave _lastPreviewResources = new ClimbResourceSave();
		private ClimbResourceSave _pulseResources = new ClimbResourceSave();
		private float _resourcePulseElapsed = float.MaxValue;
		private readonly Action<ClimbResourceHeaderPulseRequested> _pulseHandler;

		[DebugEditable(DisplayName = "Header Height", Step = 1, Min = 40, Max = 180)]
		public int HeaderHeight { get; set; } = 90;
		[DebugEditable(DisplayName = "Header Padding X", Step = 1, Min = 0, Max = 120)]
		public int HeaderPaddingX { get; set; } = 32;
		[DebugEditable(DisplayName = "Header Padding Top", Step = 1, Min = 0, Max = 80)]
		public int HeaderPaddingTop { get; set; } = 10;
		[DebugEditable(DisplayName = "Header Padding Bottom", Step = 1, Min = 0, Max = 80)]
		public int HeaderPaddingBottom { get; set; } = 12;
		[DebugEditable(DisplayName = "Header Gap", Step = 1, Min = 0, Max = 80)]
		public int HeaderGap { get; set; } = 16;
		[DebugEditable(DisplayName = "Header Control Height", Step = 1, Min = 32, Max = 160)]
		public int HeaderControlHeight { get; set; } = 67;
		[DebugEditable(DisplayName = "Overview Button Width", Step = 1, Min = 72, Max = 220)]
		public int OverviewButtonWidth { get; set; } = 120;
		[DebugEditable(DisplayName = "Timeline Padding X", Step = 1, Min = 0, Max = 40)]
		public int TimelinePaddingX { get; set; } = 10;
		[DebugEditable(DisplayName = "Timeline Padding Y", Step = 1, Min = 0, Max = 40)]
		public int TimelinePaddingY { get; set; } = 10;
		[DebugEditable(DisplayName = "Timeline Slot Gap", Step = 1, Min = 0, Max = 12)]
		public int TimelineSlotGap { get; set; } = 0;
		[DebugEditable(DisplayName = "Timeline Shop Icon Size", Step = 1, Min = 8, Max = 32)]
		public int TimelineShopIconSize { get; set; } = 18;
		[DebugEditable(DisplayName = "Timeline Shop Icon Gap", Step = 1, Min = 0, Max = 16)]
		public int TimelineShopIconGap { get; set; } = 4;
		[DebugEditable(DisplayName = "Resource Amount Font Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float ResourceAmountFontScale { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Resource Icon Size", Step = 1, Min = 6, Max = 48)]
		public int ResourceIconSize { get; set; } = 24;
		[DebugEditable(DisplayName = "Resource Fade Seconds", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ResourceFadeSeconds { get; set; } = 0.2f;
		[DebugEditable(DisplayName = "Resource Pulse Duration", Step = 0.01f, Min = 0.05f, Max = 1f)]
		public float ResourcePulseDuration { get; set; } = 0.28f;
		[DebugEditable(DisplayName = "Resource Pulse Max Scale", Step = 0.01f, Min = 1f, Max = 1.5f)]
		public float ResourcePulseMaxScale { get; set; } = 1.18f;
		[DebugEditable(DisplayName = "Resource Pulse Highlight", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ResourcePulseHighlight { get; set; } = 0.65f;
		[DebugEditable(DisplayName = "Resource Bar Border Thickness", Step = 1, Min = 1, Max = 6)]
		public int ResourceBarBorderThickness { get; set; } = 2;
		[DebugEditable(DisplayName = "Timeline Hourglass Width", Step = 1, Min = 3, Max = 24)]
		public int TimelineHourglassWidth { get; set; } = 24;
		[DebugEditable(DisplayName = "Timeline Hourglass Height", Step = 1, Min = 4, Max = 32)]
		public int TimelineHourglassHeight { get; set; } = 24;
		[DebugEditable(DisplayName = "Empty Hourglass Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float EmptyHourglassAlpha { get; set; } = 0.2f;
		[DebugEditable(DisplayName = "Hourglass Red Glow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float HourglassRedGlowAlpha { get; set; } = 0.65f;
		[DebugEditable(DisplayName = "Hourglass White Meter Glow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float HourglassWhiteMeterGlowAlpha { get; set; } = 0.25f;
		[DebugEditable(DisplayName = "Hourglass Glow Radius", Step = 1, Min = 1, Max = 8)]
		public int HourglassGlowRadius { get; set; } = 2;
		[DebugEditable(DisplayName = "Header Shadow Offset Y", Step = 1, Min = 0, Max = 20)]
		public int HeaderShadowOffsetY { get; set; } = 4;
		[DebugEditable(DisplayName = "Header Shadow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float HeaderShadowAlpha { get; set; } = 0.45f;
		[DebugEditable(DisplayName = "Header Bottom Border Thickness", Step = 1, Min = 1, Max = 4)]
		public int HeaderBottomBorderThickness { get; set; } = 1;
		[DebugEditable(DisplayName = "Timeline Border Thickness", Step = 1, Min = 1, Max = 4)]
		public int TimelineBorderThickness { get; set; } = 1;
		[DebugEditable(DisplayName = "Timeline Min Slot Width", Step = 1, Min = 1, Max = 16)]
		public int TimelineMinSlotWidth { get; set; } = 4;
		[DebugEditable(DisplayName = "Resource Bar Border Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ResourceBarBorderAlpha { get; set; } = 0.85f;
		[DebugEditable(DisplayName = "Resource Icon Text Gap", Step = 1, Min = 0, Max = 16)]
		public int ResourceIconTextGap { get; set; } = 4;
		[DebugEditable(DisplayName = "Resource Amount Text Y Offset", Step = 1, Min = -8, Max = 8)]
		public int ResourceAmountTextYOffset { get; set; } = -3;
		[DebugEditable(DisplayName = "Resource Bar Width", Step = 1, Min = 100, Max = 320)]
		public int ResourceBarWidth { get; set; } = 228;
		[DebugEditable(DisplayName = "Resource Cell Padding", Step = 1, Min = 0, Max = 20)]
		public int ResourceCellPadding { get; set; } = 8;
		[DebugEditable(DisplayName = "Resource Content Y Offset", Step = 1, Min = -12, Max = 12)]
		public int ResourceContentYOffset { get; set; } = 7;
		[DebugEditable(DisplayName = "Resource Delta Font Scale", Step = 0.01f, Min = 0.03f, Max = 0.2f)]
		public float ResourceDeltaFontScale { get; set; } = 0.075f;
		[DebugEditable(DisplayName = "Resource Delta Badge Padding X", Step = 1, Min = 0, Max = 12)]
		public int ResourceDeltaBadgePaddingX { get; set; } = 4;
		[DebugEditable(DisplayName = "Resource Delta Badge Padding Y", Step = 1, Min = 0, Max = 8)]
		public int ResourceDeltaBadgePaddingY { get; set; } = 2;
		[DebugEditable(DisplayName = "Resource Delta Lift", Step = 1, Min = 0, Max = 12)]
		public int ResourceDeltaLift { get; set; } = 4;
		[DebugEditable(DisplayName = "Resource Cell Glow Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float ResourceCellGlowAlpha { get; set; } = 0.32f;
		[DebugEditable(DisplayName = "Overview Button Border Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float OverviewButtonBorderAlpha { get; set; } = 0.85f;
		[DebugEditable(DisplayName = "Overview Button Border Thickness", Step = 1, Min = 1, Max = 6)]
		public int OverviewButtonBorderThickness { get; set; } = 2;
		[DebugEditable(DisplayName = "Overview Card Width", Step = 1, Min = 12, Max = 60)]
		public int OverviewCardWidth { get; set; } = 28;
		[DebugEditable(DisplayName = "Overview Card Height", Step = 1, Min = 16, Max = 64)]
		public int OverviewCardHeight { get; set; } = 40;
		[DebugEditable(DisplayName = "Overview Hover Lift", Step = 1, Min = 0, Max = 8)]
		public int OverviewHoverLift { get; set; } = 2;
		[DebugEditable(DisplayName = "Overview Label Font Scale", Step = 0.01f, Min = 0.03f, Max = 0.2f)]
		public float OverviewLabelFontScale { get; set; } = 0.075f;

		internal static int HeaderHeightValue { get; private set; } = 90;
		internal static int HeaderPaddingXValue { get; private set; } = 32;
		internal static int HeaderPaddingTopValue { get; private set; } = 10;
		internal static int HeaderGapValue { get; private set; } = 16;
		internal static int HeaderControlHeightValue { get; private set; } = 67;
		internal static int OverviewButtonWidthValue { get; private set; } = 120;
		internal static int ResourceBarWidthValue { get; private set; } = 228;

		public ClimbHeaderDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
			_cardBackArt = _imageAssets.TryGetTexture("card_back");
			_pulseHandler = OnResourcePulseRequested;
			EventManager.Subscribe(_pulseHandler);
			ClimbSceneDrawHelpers.EnsureHourglassTextures(_imageAssets);
			ClimbSceneDrawHelpers.EnsureResourceTextures(_imageAssets);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime)
		{
			HeaderHeightValue = HeaderHeight;
			HeaderPaddingXValue = HeaderPaddingX;
			HeaderPaddingTopValue = HeaderPaddingTop;
			HeaderGapValue = HeaderGap;
			HeaderControlHeightValue = HeaderControlHeight;
			OverviewButtonWidthValue = OverviewButtonWidth;
			ResourceBarWidthValue = ResourceBarWidth;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			HeaderHeightValue = HeaderHeight;
			HeaderPaddingXValue = HeaderPaddingX;
			HeaderPaddingTopValue = HeaderPaddingTop;
			HeaderGapValue = HeaderGap;
			HeaderControlHeightValue = HeaderControlHeight;
			OverviewButtonWidthValue = OverviewButtonWidth;
			ResourceBarWidthValue = ResourceBarWidth;
			if (IsClimbScene())
			{
				UpdateResourcePreviewFade(gameTime);
				_resourcePulseElapsed += Math.Max(0f, (float)gameTime.ElapsedGameTime.TotalSeconds);
			}
		}

		public void Draw()
		{
			if (!IsClimbScene()) return;

			var climb = SaveCache.GetClimbState();
			var preview = GetPreview();
			var header = GetBounds(ClimbHeaderLayoutSystem.HeaderName);
			if (header.Width <= 0) header = new Rectangle(0, 0, Game1.VirtualWidth, HeaderHeight);

			_spriteBatch.Draw(_pixel, new Rectangle(header.X, header.Y + HeaderShadowOffsetY, header.Width, header.Height), Color.Black * HeaderShadowAlpha);
			_spriteBatch.Draw(_pixel, header, ClimbSceneDrawHelpers.HeaderFill);
			ClimbSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, new Rectangle(0, header.Bottom - HeaderBottomBorderThickness, Game1.VirtualWidth, HeaderBottomBorderThickness), ClimbSceneDrawHelpers.Black4, HeaderBottomBorderThickness);

			DrawTimeline(GetBounds(ClimbHeaderLayoutSystem.TimelineName), climb, preview);
			DrawResources(GetBounds(ClimbHeaderLayoutSystem.ResourceBarName), climb);
			DrawOverviewButton(GetBounds(ClimbHeaderLayoutSystem.LoadoutButtonName));
		}

		private void DrawTimeline(Rectangle rect, ClimbSaveState climb, ClimbPreviewState preview)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;

			int used = ClimbRuleService.ClampTime(climb?.time ?? 0);
			int projected = preview?.IsActive == true ? preview.ProjectedUsedTime : used;
			int delta = Math.Max(0, projected - used);

			_spriteBatch.Draw(_pixel, rect, ClimbSceneDrawHelpers.Black2);
			ClimbSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, rect, ClimbSceneDrawHelpers.Black4, TimelineBorderThickness);

			int rowX = rect.X + TimelinePaddingX;
			int hourglassY = rect.Y + TimelinePaddingY;
			int rowW = rect.Width - TimelinePaddingX * 2;
			int gapTotal = TimelineSlotGap * (ClimbRuleService.MaxTime - 1);
			int slotW = Math.Max(TimelineMinSlotWidth, (rowW - gapTotal) / ClimbRuleService.MaxTime);
			var glow = new HourglassGlowTuning
			{
				RedGlowAlpha = HourglassRedGlowAlpha,
				WhiteMeterGlowAlpha = HourglassWhiteMeterGlowAlpha,
				GlowRadius = HourglassGlowRadius,
			};

			for (int i = 0; i < ClimbRuleService.MaxTime; i++)
			{
				int slotX = rowX + i * (slotW + TimelineSlotGap);

				bool isUsed = i < used;
				bool isPreview = delta > 0 && i >= used && i < projected;
				bool isEmpty = !isUsed && !isPreview;
				float alpha = isEmpty ? EmptyHourglassAlpha : 1f;
				var iconRect = new Rectangle(
					slotX + (slotW - TimelineHourglassWidth) / 2,
					hourglassY,
					TimelineHourglassWidth,
					TimelineHourglassHeight);
				var style = isPreview
					? HourglassIconStyle.Red
					: isUsed
						? HourglassIconStyle.WhiteMeter
						: HourglassIconStyle.WhiteFaded;
				ClimbSceneDrawHelpers.DrawHourglassIcon(
					_spriteBatch,
					iconRect,
					style,
					isPreview ? ClimbSceneDrawHelpers.Red2 : ClimbSceneDrawHelpers.White3,
					isPreview ? ClimbSceneDrawHelpers.Red2 : ClimbSceneDrawHelpers.White2,
					isUsed || isPreview,
					alpha,
					glow);

				bool shopMarker = (i + 1) % ClimbRuleService.ShopRefreshInterval == 0 && i + 1 < ClimbRuleService.MaxTime;
				if (!shopMarker) continue;

				int refreshAt = i + 1;
				bool expiring = delta > 0 && projected >= refreshAt && used < refreshAt;
				var shopRect = new Rectangle(
					slotX + (slotW - TimelineShopIconSize) / 2,
					hourglassY + TimelineHourglassHeight + TimelineShopIconGap,
					TimelineShopIconSize,
					TimelineShopIconSize);
				ClimbSceneDrawHelpers.DrawShopTitleIcon(
					_spriteBatch,
					_pixel,
					shopRect,
					expiring ? ClimbSceneDrawHelpers.Red2 : ClimbSceneDrawHelpers.White1);
			}
		}

		private void DrawResources(Rectangle rect, ClimbSaveState climb)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			var baseResources = climb?.resources ?? new ClimbResourceSave();
			var previewResources = _lastPreviewResources ?? baseResources;

			_spriteBatch.Draw(_pixel, rect, ClimbSceneDrawHelpers.ResourceBarFill);
			ClimbSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, rect, Color.White * ResourceBarBorderAlpha, ResourceBarBorderThickness);

			int firstWidth = rect.Width / 3;
			int secondWidth = rect.Width / 3;
			var redRect = new Rectangle(rect.X, rect.Y, firstWidth, rect.Height);
			var whiteRect = new Rectangle(redRect.Right, rect.Y, secondWidth, rect.Height);
			var blackRect = new Rectangle(whiteRect.Right, rect.Y, rect.Right - whiteRect.Right, rect.Height);
			DrawResourceCell(redRect, ClimbResourceType.Red, baseResources.red, previewResources.red, ClimbSceneDrawHelpers.Red3);
			DrawResourceCell(whiteRect, ClimbResourceType.White, baseResources.white, previewResources.white, ClimbSceneDrawHelpers.White1);
			DrawResourceCell(blackRect, ClimbResourceType.Black, baseResources.black, previewResources.black, ClimbSceneDrawHelpers.White3);
			_spriteBatch.Draw(_pixel, new Rectangle(redRect.Right, rect.Y + 1, 1, Math.Max(1, rect.Height - 2)), ClimbSceneDrawHelpers.White1 * 0.28f);
			_spriteBatch.Draw(_pixel, new Rectangle(whiteRect.Right, rect.Y + 1, 1, Math.Max(1, rect.Height - 2)), ClimbSceneDrawHelpers.White1 * 0.28f);
		}

		private void DrawResourceCell(Rectangle cell, ClimbResourceType type, int amount, int previewAmount, Color color)
		{
			var amountText = amount.ToString();
			var previewText = previewAmount.ToString();
			int delta = previewAmount - amount;
			string deltaText = delta > 0 ? $"+{delta}" : delta.ToString();

			float previewAlpha = MathHelper.Clamp(_resourcePreviewAlpha, 0f, 1f);
			bool showPreviewDelta = previewAlpha > 0.001f && delta != 0;

			float pulse = GetResourcePulse(type);
			if (pulse > 0f)
			{
				var glow = new Rectangle(cell.X + 2, cell.Y + 2, Math.Max(1, cell.Width - 4), Math.Max(1, cell.Height - 4));
				_spriteBatch.Draw(_pixel, glow, color * (pulse * ResourceCellGlowAlpha));
			}

			int iconSize = Math.Max(1, (int)Math.Round(ResourceIconSize * MathHelper.Lerp(1f, ResourcePulseMaxScale, pulse)));
			int baseIconCenterX = cell.X + ResourceCellPadding + ResourceIconSize / 2;
			int iconX = baseIconCenterX - iconSize / 2;
			int iconY = cell.Center.Y + ResourceContentYOffset - iconSize / 2;
			var iconPos = new Vector2(iconX, iconY);
			var pulseColor = Color.Lerp(color, Color.White, pulse * ResourcePulseHighlight);
			ClimbSceneDrawHelpers.DrawResourceIcon(_spriteBatch, _graphicsDevice, _pixel, iconPos, type, iconSize, pulseColor);

			int textX = cell.X + ResourceCellPadding + ResourceIconSize + ResourceIconTextGap;
			int availableTextWidth = Math.Max(1, cell.Right - ResourceCellPadding - textX);
			float fontScale = FitResourceAmountScale(amountText, previewText, availableTextWidth);
			var amountSize = ClimbSceneDrawHelpers.MeasureBodyText(amountText, fontScale);
			var previewSize = ClimbSceneDrawHelpers.MeasureBodyText(previewText, fontScale);
			float textHeight = Math.Max(amountSize.Y, previewSize.Y);
			var textPos = new Vector2(textX, cell.Center.Y + ResourceContentYOffset - textHeight / 2f + ResourceAmountTextYOffset);
			var textColor = Color.Lerp(ClimbSceneDrawHelpers.White1, Color.White, pulse * ResourcePulseHighlight);

			if (!showPreviewDelta)
			{
				ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, amountText, textPos, fontScale, textColor);
				return;
			}

			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, amountText, textPos, fontScale, textColor * (1f - previewAlpha));
			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, previewText, textPos, fontScale, textColor * previewAlpha);
			var deltaColor = delta > 0 ? ClimbSceneDrawHelpers.GreenPositive : ClimbSceneDrawHelpers.Red2;
			var deltaSize = ClimbSceneDrawHelpers.MeasureBodyText(deltaText, ResourceDeltaFontScale);
			int badgeW = (int)Math.Ceiling(deltaSize.X) + ResourceDeltaBadgePaddingX * 2;
			int badgeH = (int)Math.Ceiling(deltaSize.Y) + ResourceDeltaBadgePaddingY * 2;
			int settledY = cell.Y + ResourceCellPadding;
			int badgeY = settledY + (int)Math.Round((1f - previewAlpha) * ResourceDeltaLift);
			var badge = new Rectangle(cell.Right - ResourceCellPadding - badgeW, badgeY, badgeW, badgeH);
			_spriteBatch.Draw(_pixel, badge, deltaColor * (0.24f * previewAlpha));
			ClimbSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, badge, deltaColor * (0.9f * previewAlpha), 1);
			var deltaPos = new Vector2(badge.X + ResourceDeltaBadgePaddingX, badge.Y + ResourceDeltaBadgePaddingY);
			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, deltaText, deltaPos, ResourceDeltaFontScale, deltaColor * previewAlpha);
		}

		private float FitResourceAmountScale(string amountText, string previewText, int availableWidth)
		{
			float widest = Math.Max(
				ClimbSceneDrawHelpers.MeasureBodyText(amountText, ResourceAmountFontScale).X,
				ClimbSceneDrawHelpers.MeasureBodyText(previewText, ResourceAmountFontScale).X);
			if (widest <= availableWidth || widest <= 0f) return ResourceAmountFontScale;
			return Math.Max(0.05f, ResourceAmountFontScale * availableWidth / widest);
		}

		private void OnResourcePulseRequested(ClimbResourceHeaderPulseRequested evt)
		{
			if (evt?.Resources == null) return;
			_pulseResources = CloneResources(evt.Resources);
			_resourcePulseElapsed = 0f;
		}

		private float GetResourcePulse(ClimbResourceType type)
		{
			int amount = type switch
			{
				ClimbResourceType.Red => _pulseResources?.red ?? 0,
				ClimbResourceType.White => _pulseResources?.white ?? 0,
				_ => _pulseResources?.black ?? 0,
			};
			if (amount <= 0 || ResourcePulseDuration <= 0f || _resourcePulseElapsed >= ResourcePulseDuration) return 0f;
			float progress = MathHelper.Clamp(_resourcePulseElapsed / ResourcePulseDuration, 0f, 1f);
			return MathF.Sin(MathF.PI * progress);
		}

		public void Shutdown()
		{
			EventManager.Unsubscribe(_pulseHandler);
		}

		internal void SetResourcePulseForSnapshot(ClimbResourceSave resources, float progress)
		{
			_pulseResources = CloneResources(resources);
			_resourcePulseElapsed = MathHelper.Clamp(progress, 0f, 1f) * ResourcePulseDuration;
		}

		internal void SetResourcePreviewForSnapshot(ClimbResourceSave resources, float alpha)
		{
			_lastPreviewResources = CloneResources(resources);
			_resourcePreviewAlpha = MathHelper.Clamp(alpha, 0f, 1f);
		}

		private void UpdateResourcePreviewFade(GameTime gameTime)
		{
			var climb = SaveCache.GetClimbState();
			var preview = GetPreview();
			var current = climb?.resources ?? new ClimbResourceSave();
			bool previewingResources = preview?.IsActive == true && !ResourcesEqual(current, preview.ProjectedResources);
			if (previewingResources)
			{
				_lastPreviewResources = CloneResources(preview.ProjectedResources);
			}

			float target = previewingResources ? 1f : 0f;
			float elapsed = (float)gameTime.ElapsedGameTime.TotalSeconds;
			float delta = ResourceFadeSeconds <= 0f ? 1f : elapsed / ResourceFadeSeconds;
			_resourcePreviewAlpha = MathHelper.Clamp(
				_resourcePreviewAlpha + (target > _resourcePreviewAlpha ? delta : -delta),
				0f,
				1f);
		}

		private static bool ResourcesEqual(ClimbResourceSave a, ClimbResourceSave b)
		{
			return (a?.red ?? 0) == (b?.red ?? 0)
				&& (a?.white ?? 0) == (b?.white ?? 0)
				&& (a?.black ?? 0) == (b?.black ?? 0);
		}

		private static ClimbResourceSave CloneResources(ClimbResourceSave resources)
		{
			return new ClimbResourceSave
			{
				red = resources?.red ?? 0,
				white = resources?.white ?? 0,
				black = resources?.black ?? 0,
			};
		}

		private void DrawOverviewButton(Rectangle rect)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			var ui = EntityManager.GetEntity(ClimbHeaderLayoutSystem.LoadoutButtonName)?.GetComponent<UIElement>();
			bool hovered = ui?.IsHovered == true;
			var fill = hovered ? ClimbSceneDrawHelpers.Red2 * 0.22f : ClimbSceneDrawHelpers.Black2;
			var border = hovered ? ClimbSceneDrawHelpers.Red2 : Color.White * OverviewButtonBorderAlpha;
			_spriteBatch.Draw(_pixel, rect, fill);
			ClimbSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, rect, border, OverviewButtonBorderThickness);

			int lift = hovered ? OverviewHoverLift : 0;
			int cardX = rect.X + 13;
			int cardY = rect.Center.Y - OverviewCardHeight / 2 - lift;
			DrawOverviewCard(new Rectangle(cardX - 4, cardY + 3, OverviewCardWidth, OverviewCardHeight), ClimbSceneDrawHelpers.White1 * 0.45f);
			DrawOverviewCard(new Rectangle(cardX, cardY, OverviewCardWidth, OverviewCardHeight), hovered ? Color.White : ClimbSceneDrawHelpers.White3);

			int labelX = cardX + OverviewCardWidth + 10;
			var labelColor = hovered ? Color.White : ClimbSceneDrawHelpers.White3;
			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, "RUN", new Vector2(labelX, rect.Y + 17 - lift), OverviewLabelFontScale, labelColor);
			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, "OVERVIEW", new Vector2(labelX, rect.Y + 34 - lift), OverviewLabelFontScale, labelColor);
		}

		private void DrawOverviewCard(Rectangle rect, Color tint)
		{
			_spriteBatch.Draw(_pixel, rect, ClimbSceneDrawHelpers.Black4);
			if (_cardBackArt != null)
			{
				float scale = Math.Min(rect.Width / (float)_cardBackArt.Width, rect.Height / (float)_cardBackArt.Height);
				int width = Math.Max(1, (int)Math.Round(_cardBackArt.Width * scale));
				int height = Math.Max(1, (int)Math.Round(_cardBackArt.Height * scale));
				var fitted = new Rectangle(rect.Center.X - width / 2, rect.Center.Y - height / 2, width, height);
				_spriteBatch.Draw(_cardBackArt, fitted, tint);
			}
			ClimbSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, rect, tint, 1);
		}

		private Rectangle GetBounds(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
		}

		private ClimbPreviewState GetPreview()
		{
			return EntityManager.GetEntity(ClimbHeaderLayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
		}

		private bool IsClimbScene()
		{
			return EntityManager.GetEntitiesWithComponent<SceneState>()
				.FirstOrDefault()
				?.GetComponent<SceneState>()
				?.Current == SceneId.Climb;
		}
	}
}
