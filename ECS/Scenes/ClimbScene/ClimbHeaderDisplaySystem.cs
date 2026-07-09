using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
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
		private Texture2D _weaponArt;
		private string _weaponArtKey = string.Empty;
		private float _resourcePreviewAlpha;
		private ClimbResourceSave _lastPreviewResources = new ClimbResourceSave();

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
		[DebugEditable(DisplayName = "Weapon Button Size", Step = 1, Min = 32, Max = 160)]
		public int WeaponButtonSize { get; set; } = 67;
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
		[DebugEditable(DisplayName = "Resource Label Font Scale", Step = 0.01f, Min = 0.03f, Max = 0.3f)]
		public float ResourceLabelFontScale { get; set; } = 0.075f;
		[DebugEditable(DisplayName = "Resource Amount Font Scale", Step = 0.01f, Min = 0.03f, Max = 0.4f)]
		public float ResourceAmountFontScale { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Resource Icon Size", Step = 1, Min = 6, Max = 48)]
		public int ResourceIconSize { get; set; } = 24;
		[DebugEditable(DisplayName = "Resource Fade Seconds", Step = 0.01f, Min = 0.01f, Max = 1f)]
		public float ResourceFadeSeconds { get; set; } = 0.2f;
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
		[DebugEditable(DisplayName = "Resource Label Padding X", Step = 1, Min = 0, Max = 40)]
		public int ResourceLabelPaddingX { get; set; } = 12;
		[DebugEditable(DisplayName = "Resource Label Padding Y", Step = 1, Min = 0, Max = 40)]
		public int ResourceLabelPaddingY { get; set; } = 8;
		[DebugEditable(DisplayName = "Resource Icon Bottom Padding", Step = 1, Min = 0, Max = 40)]
		public int ResourceIconBottomPadding { get; set; } = 13;
		[DebugEditable(DisplayName = "Resource Row Padding Right", Step = 1, Min = 0, Max = 40)]
		public int ResourceRowPaddingRight { get; set; } = 0;
		[DebugEditable(DisplayName = "Resource Group Spacing", Step = 1, Min = 0, Max = 40)]
		public int ResourceGroupSpacing { get; set; } = 2;
		[DebugEditable(DisplayName = "Resource Icon Text Gap", Step = 1, Min = 0, Max = 16)]
		public int ResourceIconTextGap { get; set; } = 4;
		[DebugEditable(DisplayName = "Resource Amount Text Y Offset", Step = 1, Min = -8, Max = 8)]
		public int ResourceAmountTextYOffset { get; set; } = -3;
		[DebugEditable(DisplayName = "Resource Amount Delta Gap", Step = 1, Min = 0, Max = 16)]
		public int ResourceAmountDeltaGap { get; set; } = 2;
		[DebugEditable(DisplayName = "Resource Bar Width", Step = 1, Min = 100, Max = 320)]
		public int ResourceBarWidth { get; set; } = 320;
		[DebugEditable(DisplayName = "Weapon Button Border Alpha", Step = 0.01f, Min = 0f, Max = 1f)]
		public float WeaponButtonBorderAlpha { get; set; } = 0.85f;
		[DebugEditable(DisplayName = "Weapon Button Border Thickness", Step = 1, Min = 1, Max = 6)]
		public int WeaponButtonBorderThickness { get; set; } = 2;

		internal static int HeaderHeightValue { get; private set; } = 90;
		internal static int HeaderPaddingXValue { get; private set; } = 32;
		internal static int HeaderPaddingTopValue { get; private set; } = 10;
		internal static int HeaderGapValue { get; private set; } = 16;
		internal static int WeaponButtonSizeValue { get; private set; } = 67;
		internal static int ResourceBarWidthValue { get; private set; } = 320;

		public ClimbHeaderDisplaySystem(EntityManager entityManager, GraphicsDevice graphicsDevice, SpriteBatch spriteBatch, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = graphicsDevice;
			_spriteBatch = spriteBatch;
			_imageAssets = imageAssets;
			_pixel = _imageAssets.GetPixel(Color.White);
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
			WeaponButtonSizeValue = WeaponButtonSize;
			ResourceBarWidthValue = ResourceBarWidth;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			HeaderHeightValue = HeaderHeight;
			HeaderPaddingXValue = HeaderPaddingX;
			HeaderPaddingTopValue = HeaderPaddingTop;
			HeaderGapValue = HeaderGap;
			WeaponButtonSizeValue = WeaponButtonSize;
			ResourceBarWidthValue = ResourceBarWidth;
			if (IsClimbScene())
			{
				SyncWeaponArt();
				UpdateResourcePreviewFade(gameTime);
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
			DrawResources(GetBounds(ClimbHeaderLayoutSystem.ResourceBarName), climb, preview);
			DrawWeaponButton(GetBounds(ClimbHeaderLayoutSystem.LoadoutButtonName));
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

		private void DrawResources(Rectangle rect, ClimbSaveState climb, ClimbPreviewState preview)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			var baseResources = climb?.resources ?? new ClimbResourceSave();
			var previewResources = _lastPreviewResources ?? baseResources;

			_spriteBatch.Draw(_pixel, rect, ClimbSceneDrawHelpers.ResourceBarFill);
			ClimbSceneDrawHelpers.DrawBorder(_spriteBatch, _pixel, rect, Color.White * ResourceBarBorderAlpha, ResourceBarBorderThickness);
			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, "RESOURCES", new Vector2(rect.X + ResourceLabelPaddingX, rect.Y + ResourceLabelPaddingY), ResourceLabelFontScale, ClimbSceneDrawHelpers.White3);

			int iconY = rect.Bottom - ResourceIconBottomPadding - ResourceIconSize;
			int x = rect.Right - ResourceRowPaddingRight;
			x = DrawResourceAmountRightAligned(x, iconY, ClimbResourceType.Black, baseResources.black, previewResources.black, ClimbSceneDrawHelpers.White3);
			x -= ResourceGroupSpacing;
			x = DrawResourceAmountRightAligned(x, iconY, ClimbResourceType.White, baseResources.white, previewResources.white, ClimbSceneDrawHelpers.White1);
			x -= ResourceGroupSpacing;
			DrawResourceAmountRightAligned(x, iconY, ClimbResourceType.Red, baseResources.red, previewResources.red, ClimbSceneDrawHelpers.Red3);
		}

		private int DrawResourceAmountRightAligned(int rightX, int iconY, ClimbResourceType type, int amount, int previewAmount, Color color)
		{
			var amountText = amount.ToString();
			var previewText = previewAmount.ToString();
			int delta = previewAmount - amount;
			string deltaText = delta == 0
				? string.Empty
				: delta > 0
					? $" (+{delta})"
					: $" ({delta})";

			float previewAlpha = MathHelper.Clamp(_resourcePreviewAlpha, 0f, 1f);
			bool showPreviewDelta = previewAlpha > 0.001f && delta != 0;

			var amountSize = ClimbSceneDrawHelpers.MeasureBodyText(amountText, ResourceAmountFontScale);
			var previewSize = ClimbSceneDrawHelpers.MeasureBodyText(previewText, ResourceAmountFontScale);
			float amountWidth = Math.Max(amountSize.X, previewSize.X);
			float reservedDeltaWidth = GetReservedDeltaSuffixWidth();
			float textWidth = amountWidth + reservedDeltaWidth;
			int groupW = ResourceIconSize + ResourceIconTextGap + (int)Math.Ceiling(textWidth);
			int iconX = rightX - groupW;
			var iconPos = new Vector2(iconX, iconY);
			ClimbSceneDrawHelpers.DrawResourceIcon(_spriteBatch, _graphicsDevice, _pixel, iconPos, type, ResourceIconSize, color);
			var textPos = new Vector2(iconPos.X + ResourceIconSize + ResourceIconTextGap, iconPos.Y + ResourceAmountTextYOffset);

			if (!showPreviewDelta)
			{
				ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, amountText, textPos, ResourceAmountFontScale, ClimbSceneDrawHelpers.White1);
				return iconX;
			}

			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, amountText, textPos, ResourceAmountFontScale, ClimbSceneDrawHelpers.White1 * (1f - previewAlpha));
			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, previewText, textPos, ResourceAmountFontScale, ClimbSceneDrawHelpers.White1 * previewAlpha);
			var deltaPos = new Vector2(textPos.X + amountWidth + ResourceAmountDeltaGap, textPos.Y);
			var deltaColor = delta > 0 ? ClimbSceneDrawHelpers.GreenPositive : ClimbSceneDrawHelpers.Red2;
			ClimbSceneDrawHelpers.DrawBodyText(_spriteBatch, deltaText, deltaPos, ResourceAmountFontScale, deltaColor * previewAlpha);
			return iconX;
		}

		private float GetReservedDeltaSuffixWidth()
		{
			var positive = ClimbSceneDrawHelpers.MeasureBodyText(" (+9)", ResourceAmountFontScale);
			var negative = ClimbSceneDrawHelpers.MeasureBodyText(" (-9)", ResourceAmountFontScale);
			return ResourceAmountDeltaGap + Math.Max(positive.X, negative.X);
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

		private void DrawWeaponButton(Rectangle rect)
		{
			if (rect.Width <= 0 || rect.Height <= 0) return;
			var ui = EntityManager.GetEntity(ClimbHeaderLayoutSystem.LoadoutButtonName)?.GetComponent<UIElement>();
			ClimbSceneDrawHelpers.DrawCircularFramedImage(
				_spriteBatch,
				_graphicsDevice,
				_pixel,
				_weaponArt,
				rect,
				Color.White * WeaponButtonBorderAlpha,
				WeaponButtonBorderThickness,
				ui?.IsHovered == true);
		}

		private Rectangle GetBounds(string name)
		{
			return EntityManager.GetEntity(name)?.GetComponent<UIElement>()?.Bounds ?? Rectangle.Empty;
		}

		private ClimbPreviewState GetPreview()
		{
			return EntityManager.GetEntity(ClimbHeaderLayoutSystem.RootName)?.GetComponent<ClimbPreviewState>();
		}

		private void SyncWeaponArt()
		{
			string weaponId = SaveCache.GetLoadout(RunDeckService.PrimaryLoadoutId)?.weaponId ?? "sword";
			string asset = CrusaderPortraitAssets.ResolveWeaponCardArtAsset(weaponId);
			if (_weaponArt != null && _weaponArtKey == asset) return;
			_weaponArtKey = asset;
			_weaponArt = _imageAssets.TryGetTexture(asset);
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
