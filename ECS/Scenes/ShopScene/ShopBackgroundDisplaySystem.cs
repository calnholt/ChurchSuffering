using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Crusaders30XX.ECS.Singletons;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Crusaders30XX.Diagnostics;
using System;
using System.Collections.Generic;

namespace Crusaders30XX.ECS.Systems
{
	/// <summary>
	/// Renders the shop background image with cover scaling, anchored to bottom, centered horizontally.
	/// </summary>
	[DebugTab("Shop Background")]
	public class ShopBackgroundDisplaySystem : Core.System
	{
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private Texture2D _background;

		private string _backgroundAsset = string.Empty;

		[DebugEditable(DisplayName = "Offset Y", Step = 2, Min = -2000, Max = 2000)]
		public int OffsetY { get; set; } = 0;

		public ShopBackgroundDisplaySystem(EntityManager entityManager, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets)
			: base(entityManager)
		{
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_imageAssets = imageAssets;

			EventManager.Subscribe<SetShopTitle>(OnSetShopTitle);
			EventManager.Subscribe<LoadSceneEvent>(OnLoadScene);
		}

		private void OnSetShopTitle(SetShopTitle evt)
		{
			_backgroundAsset = ResolveBackgroundAsset(evt);
			TryLoad();
		}

		private void OnLoadScene(LoadSceneEvent evt)
		{
			if (evt == null || evt.Scene != SceneId.Shop) return;
			if (string.IsNullOrWhiteSpace(_backgroundAsset) &&
				!string.IsNullOrWhiteSpace(StateSingleton.ActiveRunShopId) &&
				SaveCache.TryGetRunShop(StateSingleton.ActiveRunShopId, out var shop, out _))
			{
				_backgroundAsset = shop.backgroundAsset ?? string.Empty;
			}
			TryLoad();
		}

		private static string ResolveBackgroundAsset(SetShopTitle evt)
		{
			if (!string.IsNullOrWhiteSpace(evt?.BackgroundAsset))
			{
				return evt.BackgroundAsset;
			}

			if (!string.IsNullOrWhiteSpace(evt?.ShopId) &&
				SaveCache.TryGetRunShop(evt.ShopId, out var shop, out _) &&
				!string.IsNullOrWhiteSpace(shop.backgroundAsset))
			{
				return shop.backgroundAsset;
			}

			return string.Empty;
		}

		private void TryLoad()
		{
			try
			{
				_background = null;
				if (!string.IsNullOrWhiteSpace(_backgroundAsset))
				{
					_background = SafeLoadTexture(_backgroundAsset);
				}

				if (_background == null)
				{
					_background = SafeLoadTexture("desert-background");
				}
			}
			catch (Exception ex)
			{
				Console.WriteLine($"[ShopBackgroundDisplaySystem] Failed to load background: {ex.Message}");
				_background = null;
			}
		}

		private Texture2D SafeLoadTexture(string assetName)
		{
			if (string.IsNullOrWhiteSpace(assetName)) return null;
			return _imageAssets.TryGetTexture(assetName);
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }

		public void Draw()
		{
			if (_background == null) return;

			int viewportW = Game1.VirtualWidth;
			int viewportH = Game1.VirtualHeight;

			int texW = _background.Width;
			int texH = _background.Height;

			float scaleX = viewportW / (float)texW;
			float scaleY = viewportH / (float)texH;
			float scale = Math.Max(scaleX, scaleY);

			int drawW = (int)Math.Round(texW * scale);
			int drawH = (int)Math.Round(texH * scale);

			int x = (viewportW - drawW) / 2;
			int y = viewportH - drawH + OffsetY;

			var dest = new Rectangle(x, y, drawW, drawH);
			_spriteBatch.Draw(_background, dest, Color.White);
		}
	}
}
