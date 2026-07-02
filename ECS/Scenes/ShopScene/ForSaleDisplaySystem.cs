using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Rendering;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Shop For-Sale Grid")]
	public class ForSaleDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private readonly SpriteFont _font = FontSingleton.ContentFont;

		private string _currentShopTitle = "Shop";
		private string _currentShopId = string.Empty;
		private bool _needsRebuild = true;

		private Texture2D _goldIcon;
		private readonly Dictionary<string, Entity> _cardPreviewCache = new();

		// Layout
		[DebugEditable(DisplayName = "Max Columns", Step = 1, Min = 1, Max = 8)]
		public int MaxColumns { get; set; } = 4;
		[DebugEditable(DisplayName = "Tile Width", Step = 5, Min = 80, Max = 800)]
		public int TileWidth { get; set; } = 490;
		[DebugEditable(DisplayName = "Tile Height", Step = 5, Min = 80, Max = 800)]
		public int TileHeight { get; set; } = 280;
		[DebugEditable(DisplayName = "Horizontal Gap", Step = 2, Min = 0, Max = 200)]
		public int HorizontalGap { get; set; } = 32;
		[DebugEditable(DisplayName = "Vertical Gap", Step = 2, Min = 0, Max = 200)]
		public int VerticalGap { get; set; } = 72;
		[DebugEditable(DisplayName = "Panel Margin X", Step = 2, Min = 0, Max = 400)]
		public int PanelMarginX { get; set; } = 60;
		[DebugEditable(DisplayName = "Panel Margin Top", Step = 2, Min = 0, Max = 800)]
		public int PanelMarginTop { get; set; } = 256;
		[DebugEditable(DisplayName = "Padding X", Step = 1, Min = 0, Max = 100)]
		public int PaddingX { get; set; } = 12;
		[DebugEditable(DisplayName = "Padding Y", Step = 1, Min = 0, Max = 100)]
		public int PaddingY { get; set; } = 12;

		// Text and content
		[DebugEditable(DisplayName = "Name Text Scale", Step = 0.01f, Min = 0.05f, Max = 1.5f)]
		public float NameTextScale { get; set; } = 0.15f;
		[DebugEditable(DisplayName = "Price Text Scale", Step = 0.01f, Min = 0.05f, Max = 1.5f)]
		public float PriceTextScale { get; set; } = 0.3f;
		[DebugEditable(DisplayName = "Content Scale", Step = 0.01f, Min = 0.05f, Max = 2f)]
		public float ContentScale { get; set; } = 1f;
		[DebugEditable(DisplayName = "Icon Size", Step = 2, Min = 16, Max = 512)]
		public int IconSize { get; set; } = 148;
		[DebugEditable(DisplayName = "Price Icon Scale", Step = 0.05f, Min = 0.1f, Max = 3f)]
		public float PriceIconScale { get; set; } = 0.3f;
		[DebugEditable(DisplayName = "Name Offset Y", Step = 2, Min = -100, Max = 200)]
		public int NameOffsetY { get; set; } = 6;
		[DebugEditable(DisplayName = "Content Offset X", Step = 2, Min = -200, Max = 300)]
		public int ContentOffsetX { get; set; } = 50;
		[DebugEditable(DisplayName = "Content Offset Y", Step = 2, Min = -200, Max = 300)]
		public int ContentOffsetY { get; set; } = 162;
		[DebugEditable(DisplayName = "Price Offset Y", Step = 2, Min = -200, Max = 300)]
		public int PriceOffsetY { get; set; } = 140;

		// Color fan for card previews
		[DebugEditable(DisplayName = "Color Fan Enabled", Step = 1)]
		public bool ColorFanEnabled { get; set; } = true;
		[DebugEditable(DisplayName = "Color Fan Angle (deg)", Step = 1f, Min = 0f, Max = 30f)]
		public float ColorFanAngleStepDeg { get; set; } = 8f;
		[DebugEditable(DisplayName = "Color Fan Spacing X", Step = 2, Min = 0, Max = 200)]
		public int ColorFanSpacingX { get; set; } = 28;
		[DebugEditable(DisplayName = "Color Fan Offset Y", Step = 2, Min = -100, Max = 100)]
		public int ColorFanSpacingY { get; set; } = 0;

		// Trapezoid background (single shape with per-edge A/B and mix)
		[DebugEditable(DisplayName = "Left Side Offset", Step = 1f, Min = 0f, Max = 100f)]
		public float LeftSideOffset { get; set; } = 14f;
		[DebugEditable(DisplayName = "Top Angle A", Step = 1f, Min = -45f, Max = 45f)]
		public float TopAngleA { get; set; } = -6f;
		[DebugEditable(DisplayName = "Top Angle B", Step = 1f, Min = -45f, Max = 45f)]
		public float TopAngleB { get; set; } = 36f;
		[DebugEditable(DisplayName = "Right Angle A", Step = 1f, Min = -45f, Max = 45f)]
		public float RightAngleA { get; set; } = -16f;
		[DebugEditable(DisplayName = "Right Angle B", Step = 1f, Min = -45f, Max = 45f)]
		public float RightAngleB { get; set; } = -16f;
		[DebugEditable(DisplayName = "Bottom Angle A", Step = 1f, Min = -45f, Max = 45f)]
		public float BottomAngleA { get; set; } = -8f;
		[DebugEditable(DisplayName = "Bottom Angle B", Step = 1f, Min = -45f, Max = 45f)]
		public float BottomAngleB { get; set; } = -2f;
		[DebugEditable(DisplayName = "Left Angle A", Step = 1f, Min = -45f, Max = 45f)]
		public float LeftAngleA { get; set; } = -45f;
		[DebugEditable(DisplayName = "Left Angle B", Step = 1f, Min = -45f, Max = 45f)]
		public float LeftAngleB { get; set; } = 9f;
		[DebugEditable(DisplayName = "Angle Mix (0..1)", Step = 0.05f, Min = 0f, Max = 1f)]
		public float AngleMix { get; set; } = 0f;
		[DebugEditable(DisplayName = "Alternate Angles A/B", Step = 1)]
		public bool AlternateAngles { get; set; } = false;

		public ForSaleDisplaySystem(EntityManager em, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets)
			: base(em)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_imageAssets = imageAssets;

			EventManager.Subscribe<LoadSceneEvent>(_ =>
			{
				if (_.Scene != SceneId.Shop) return;
				_needsRebuild = true;
			});
			EventManager.Subscribe<SetShopTitle>(_ =>
			{
				_currentShopTitle = string.IsNullOrWhiteSpace(_.Title) ? "Shop" : _.Title;
				_currentShopId = _.ShopId ?? string.Empty;
				_needsRebuild = true;
			});
			EventManager.Subscribe<HotKeyHoldCompletedEvent>(OnHotKeyHoldCompleted);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			return EntityManager.GetEntitiesWithComponent<ForSaleItem>();
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			UpdateShopPositions();
		}

		private void UpdateShopPositions()
		{
			EnsureInventoryEntities();
			var items = EntityManager.GetEntitiesWithComponent<ForSaleItem>()
				.Select(e => new { E = e, T = e.GetComponent<Transform>(), FS = e.GetComponent<ForSaleItem>() })
				.Where(x => x.FS != null)
				.ToList();
			if (items.Count == 0) return;

			int cols = System.Math.Max(1, MaxColumns);
			for (int i = 0; i < items.Count; i++)
			{
				var x = items[i];
				int row = i / cols;
				int col = i % cols;
				int px = PanelMarginX + col * (TileWidth + HorizontalGap);
				int py = PanelMarginTop + row * (TileHeight + VerticalGap);
				var baseCenter = new Vector2(px + TileWidth / 2f, py + TileHeight / 2f);
				if (x.T != null)
				{
					x.T.Position = baseCenter;
					x.T.ZOrder = 10002;
				}
			}
		}

		private void OnHotKeyHoldCompleted(HotKeyHoldCompletedEvent evt)
		{
			try
			{
				var e = evt?.Entity;
				if (e == null) return;
				var fs = e.GetComponent<ForSaleItem>();
				var ui = e.GetComponent<UIElement>();
				if (fs == null || ui == null) return;
				if (fs.IsPurchased) return;
				if (!ui.IsInteractable) return;
				if (!PurchaseItemService.CanAfford(fs.Price)) return;

				var result = PurchaseItemService.TryPurchase(EntityManager, e);
				if (result.Success)
				{
					EventManager.Publish(new PlaySfxEvent { Track = SfxTrack.CoinBag, Volume = 0.5f });
					fs.IsPurchased = true;
					ui.IsInteractable = false;
					// Remove hotkey if present
					var hk = e.GetComponent<HotKey>();
					if (hk != null) EntityManager.RemoveComponent<HotKey>(e);
					_needsRebuild = true;
				}
			}
			catch { }
		}

		private void EnsureInventoryEntities()
		{
			if (!_needsRebuild) return;
			_needsRebuild = false;

			// Clear previous
			var existing = EntityManager.GetEntitiesWithComponent<ForSaleItem>().ToList();
			foreach (var e in existing) EntityManager.DestroyEntity(e.Id);

			string shopId = !string.IsNullOrWhiteSpace(_currentShopId)
				? _currentShopId
				: StateSingleton.ActiveRunShopId;
			if (string.IsNullOrWhiteSpace(shopId)) return;

			if (!SaveCache.TryGetRunShop(shopId, out var shop, out _) || shop?.items == null || shop.items.Count == 0)
				return;

			EntityFactory.CreateForSaleFromRunShop(EntityManager, shop);

			// ensure gold icon
			_goldIcon ??= SafeLoadTexture("gold");
		}

		public void Draw()
		{
			var scene = EntityManager.GetEntitiesWithComponent<SceneState>().FirstOrDefault()?.GetComponent<SceneState>();
			if (scene == null || scene.Current != SceneId.Shop) return;

			EnsureInventoryEntities();

			var items = GetRelevantEntities()
				.Select(e => new { E = e, T = e.GetComponent<Transform>(), UI = e.GetComponent<UIElement>(), FS = e.GetComponent<ForSaleItem>() })
				.Where(x => x.FS != null)
				.ToList();

			if (items.Count == 0) return;

			int cols = Math.Max(1, MaxColumns);
			int w = Game1.VirtualWidth;

			for (int i = 0; i < items.Count; i++)
			{
				var x = items[i];
				int row = i / cols;
				int col = i % cols;
				int px = PanelMarginX + col * (TileWidth + HorizontalGap);
				int py = PanelMarginTop + row * (TileHeight + VerticalGap);
				var tileRect = new Rectangle(px, py, TileWidth, TileHeight);

				var baseCenter = new Vector2(tileRect.X + tileRect.Width / 2f, tileRect.Y + tileRect.Height / 2f);
				// Read current Position (parallax-adjusted from Update)
				var pos = x.T != null ? x.T.Position : baseCenter;
				var drawRect = new Rectangle(
					(int)System.Math.Round(pos.X - tileRect.Width / 2f),
					(int)System.Math.Round(pos.Y - tileRect.Height / 2f),
					tileRect.Width,
					tileRect.Height);

				// background trapezoid
				var innerRect = new Rectangle(
					drawRect.X + PaddingX,
					drawRect.Y + PaddingY,
					System.Math.Max(1, drawRect.Width - 2 * PaddingX),
					System.Math.Max(1, drawRect.Height - 2 * PaddingY));

				var angles = GetAnglesForIndex(i);
				var trap = PrimitiveTextureFactory.GetAntialiasedTrapezoid(
					_graphicsDevice,
					innerRect.Width,
					innerRect.Height,
					LeftSideOffset,
					angles.top,
					angles.right,
					angles.bottom,
					angles.left);
				_spriteBatch.Draw(trap, innerRect, Color.White);

				// update UI bounds and transform center
				if (x.UI != null)
				{
					// Bounds centered on the (possibly parallax-shifted) drawRect
					x.UI.Bounds = drawRect;
					x.UI.IsInteractable = !x.FS.IsPurchased;
				}

				// Name text (top-left inside padding)
				if (_font != null && !string.IsNullOrEmpty(x.FS.DisplayName))
				{
					Vector2 namePos = new Vector2(innerRect.X + PaddingX, innerRect.Y + NameOffsetY);
					_spriteBatch.DrawString(_font, x.FS.DisplayName, namePos, Color.White, 0f, Vector2.Zero, NameTextScale, SpriteEffects.None, 0f);
				}

				// Content
				Vector2 contentCenter = new Vector2(innerRect.X + innerRect.Width / 2f + ContentOffsetX, innerRect.Y + ContentOffsetY);
				switch (x.FS.ItemType)
				{
					case ForSaleItemType.Card:
					{
						if (x.FS.CardColor.HasValue)
						{
							var previewColor = x.FS.CardColor.Value;
							var card = EnsureCardPreview(x.FS.Id, previewColor);
							if (card != null)
							{
								var t = card.GetComponent<Transform>();
								if (t != null) t.Rotation = MathHelper.ToRadians(x.FS.DisplayRotationDeg);
								EventManager.Publish(new CardRenderScaledRotatedEvent
								{
									Card = card,
									Position = contentCenter,
									Scale = ContentScale
								});
							}
						}
						else if (ColorFanEnabled)
						{
							float angle = MathHelper.ToRadians(ColorFanAngleStepDeg);
							var drawSpecs = new (CardData.CardColor color, int dx, float rot)[]
							{
								// Back to front: black, red, white (white on top)
								(CardData.CardColor.Red, -ColorFanSpacingX, -angle),
								(CardData.CardColor.Black, 0, 0f),
								(CardData.CardColor.White, ColorFanSpacingX, angle)
							};
							foreach (var spec in drawSpecs)
							{
								var card = EnsureCardPreview(x.FS.Id, spec.color);
								if (card == null) continue;
								var t = card.GetComponent<Transform>();
								if (t != null) t.Rotation = spec.rot;
								EventManager.Publish(new CardRenderScaledRotatedEvent
								{
									Card = card,
									Position = contentCenter + new Vector2(spec.dx, ColorFanSpacingY),
									Scale = ContentScale
								});
							}
						}
						else
						{
							var card = EnsureCardPreview(x.FS.Id, CardData.CardColor.White);
							if (card != null)
							{
								var t = card.GetComponent<Transform>();
								if (t != null) t.Rotation = 0f;
								EventManager.Publish(new CardRenderScaledRotatedEvent
								{
									Card = card,
									Position = contentCenter,
									Scale = ContentScale
								});
							}
						}
						break;
					}
					case ForSaleItemType.Weapon:
					{
						var card = EnsureCardPreview(x.FS.Id, CardData.CardColor.White);
							if (card != null)
							{
								var t = card.GetComponent<Transform>();
								if (t != null) t.Rotation = 0f;
								EventManager.Publish(new CardRenderScaledRotatedEvent
								{
									Card = card,
									Position = contentCenter,
									Scale = ContentScale
								});
							}
						break;
					}
					case ForSaleItemType.Medal:
					{
						MedalIconRenderService.DrawMedalIcon(
							_spriteBatch,
							_graphicsDevice,
							_font,
							contentCenter,
							IconSize,
							x.FS.Id,
							_imageAssets);
						break;
					}
					case ForSaleItemType.Equipment:
					{
						DrawIconScaled(ResolveEquipmentSlotIcon(x.FS.Id), contentCenter, IconSize);
						break;
					}
				}

				// Price line
				if (x.FS.IsPurchased)
				{
					int priceY = innerRect.Y + PriceOffsetY;
					int priceX = innerRect.X + PaddingX;
					_spriteBatch.DrawString(_font, "Sold!", new Vector2(priceX, priceY), Color.White, 0f, Vector2.Zero, PriceTextScale, SpriteEffects.None, 0f);
				}
				else
				{
					string priceText = $"{x.FS.Price}";
					Vector2 priceSize = _font?.MeasureString(priceText) * PriceTextScale ?? Vector2.Zero;
					int priceY = innerRect.Y + PriceOffsetY;
					int priceX = innerRect.X + PaddingX;
					_spriteBatch.DrawString(_font, priceText, new Vector2(priceX, priceY), Color.White, 0f, Vector2.Zero, PriceTextScale, SpriteEffects.None, 0f);
					if (_goldIcon != null)
					{
						int iconW = (int)Math.Round(_goldIcon.Width * PriceIconScale);
						int iconH = (int)Math.Round(_goldIcon.Height * PriceIconScale);
						var goldRect = new Rectangle((int)Math.Round(priceX + priceSize.X + 6), priceY, iconW, iconH);
						_spriteBatch.Draw(_goldIcon, goldRect, Color.White);
					}
				}

				// Hover HotKey (FaceButton.X) when affordable and not purchased
				if (x.UI != null && x.UI.IsHovered && !x.FS.IsPurchased && PurchaseItemService.CanAfford(x.FS.Price))
				{
					var hk = x.E.GetComponent<HotKey>();
					if (hk == null)
					{
						EntityManager.AddComponent(x.E, new HotKey { Button = FaceButton.X, RequiresHold = true, Position = HotKeyPosition.Below });
					}
					else
					{
						hk.Button = FaceButton.X;
						hk.RequiresHold = true;
						hk.Position = HotKeyPosition.Below;
					}
				}
				else
				{
					// Remove HotKey when not eligible (not hovered / not affordable / purchased)
					var hk = x.E.GetComponent<HotKey>();
					if (hk != null) EntityManager.RemoveComponent<HotKey>(x.E);
				}
			}
		}

		private (float top, float right, float bottom, float left) GetAnglesForIndex(int index)
		{
			if (AlternateAngles)
			{
				bool useB = (index % 2) == 1;
				return useB
					? (TopAngleB, RightAngleB, BottomAngleB, LeftAngleB)
					: (TopAngleA, RightAngleA, BottomAngleA, LeftAngleA);
			}
			float Lerp(float a, float b, float t) => a + (b - a) * MathHelper.Clamp(t, 0f, 1f);
			return (
				Lerp(TopAngleA, TopAngleB, AngleMix),
				Lerp(RightAngleA, RightAngleB, AngleMix),
				Lerp(BottomAngleA, BottomAngleB, AngleMix),
				Lerp(LeftAngleA, LeftAngleB, AngleMix)
			);
		}

		private Entity EnsureCardPreview(string cardId, CardData.CardColor color)
		{
			if (string.IsNullOrWhiteSpace(cardId)) return null;
			string key = $"{cardId}|{color}";
			if (_cardPreviewCache.TryGetValue(key, out var existing) && existing != null)
			{
				var uiExisting = existing.GetComponent<UIElement>();
				if (uiExisting != null)
				{
					uiExisting.IsInteractable = false;
				}
				return existing;
			}
			var created = EntityFactory.CreateCardFromDefinition(EntityManager, cardId, color, allowWeapons: true, suppressStatDeltaDisplay: true);
			if (created != null)
			{
				_cardPreviewCache[key] = created;
				var ui = created.GetComponent<UIElement>();
				if (ui != null)
				{
					ui.IsInteractable = false;
				}
			}
			return created;
		}

		private void DrawIconScaled(string assetName, Vector2 center, int targetSize)
		{
			if (string.IsNullOrEmpty(assetName)) return;
			var tex = SafeLoadTexture(assetName);
			if (tex == null) return;
			float scale = Math.Min(targetSize / (float)tex.Width, targetSize / (float)tex.Height);
			int drawW = (int)Math.Round(tex.Width * scale);
			int drawH = (int)Math.Round(tex.Height * scale);
			var r = new Rectangle((int)Math.Round(center.X - drawW / 2f), (int)Math.Round(center.Y - drawH / 2f), drawW, drawH);
			_spriteBatch.Draw(tex, r, Color.White);
		}

		private string ResolveEquipmentSlotIcon(string equipmentId)
		{
			return EquipmentFactory.Create(equipmentId).Slot.ToString();
		}

		private Texture2D SafeLoadTexture(string assetName)
		{
			return _imageAssets.TryGetTexture(assetName);
		}
	}
}

