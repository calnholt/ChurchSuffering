using System.Collections.Generic;
using Crusaders30XX.Diagnostics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Singletons;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Services;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace Crusaders30XX.ECS.Systems
{
	[DebugTab("Shop Scene System")]
	public class ShopSceneSystem : Core.System
	{
		private readonly SystemManager _systemManager;
		private readonly World _world;
		private readonly GraphicsDevice _graphicsDevice;
		private readonly SpriteBatch _spriteBatch;
		private readonly ImageAssetService _imageAssets;
		private bool _firstLoad = true;
		private string _shopTitle = "Shop";
		private string _shopId = string.Empty;
		private string _shopBackgroundAsset = string.Empty;

		private ShopBackgroundDisplaySystem _shopBackgroundDisplaySystem;
		private LoadoutButtonDisplaySystem _loadoutButtonDisplaySystem;
		private ForSaleDisplaySystem _forSaleDisplaySystem;

		public ShopSceneSystem(EntityManager em, SystemManager sm, World world, GraphicsDevice gd, SpriteBatch sb, ImageAssetService imageAssets) : base(em)
		{
			_systemManager = sm;
			_world = world;
			_graphicsDevice = gd;
			_spriteBatch = sb;
			_imageAssets = imageAssets;
			EventManager.Subscribe<SetShopTitle>(_ =>
			{
				_shopTitle = string.IsNullOrWhiteSpace(_.Title) ? "Shop" : _.Title;
				_shopId = _.ShopId ?? string.Empty;
				_shopBackgroundAsset = _.BackgroundAsset ?? string.Empty;
			});
			EventManager.Subscribe<LoadSceneEvent>(_ =>
			{
				if (_.Scene != SceneId.Shop) return;
				AddShopSystems();
				ApplyShopFromSaveIfNeeded();
				EventManager.Publish(new UpdateLocationNameEvent { Title = _shopTitle });
				EventManager.Publish(new SetShopTitle
				{
					Title = _shopTitle,
					ShopId = _shopId,
					BackgroundAsset = _shopBackgroundAsset,
				});
			});
			EventManager.Subscribe<DeleteCachesEvent>(_ =>
			{
				_world.RemoveSystem(_shopBackgroundDisplaySystem);
				_world.RemoveSystem(_forSaleDisplaySystem);
				_world.RemoveSystem(_loadoutButtonDisplaySystem);
				_firstLoad = true;
			});
		}

		protected override IEnumerable<Entity> GetRelevantEntities()
		{
			yield break;
		}

		public void Draw()
		{
			FrameProfiler.Measure("ShopBackgroundDisplaySystem.Draw", _shopBackgroundDisplaySystem.Draw);
			FrameProfiler.Measure("ForSaleDisplaySystem.Draw", _forSaleDisplaySystem.Draw);
			FrameProfiler.Measure("LoadoutButtonDisplaySystem.Draw", _loadoutButtonDisplaySystem.Draw);
		}

		private void ApplyShopFromSaveIfNeeded()
		{
			string shopId = !string.IsNullOrWhiteSpace(_shopId)
				? _shopId
				: StateSingleton.ActiveRunShopId;
			if (string.IsNullOrWhiteSpace(shopId)) return;
			if (!SaveCache.TryGetRunShop(shopId, out var shop, out _)) return;

			if (string.IsNullOrWhiteSpace(_shopTitle) || _shopTitle == "Shop")
			{
				_shopTitle = string.IsNullOrWhiteSpace(shop.displayName) ? "Shop" : shop.displayName;
			}
			if (string.IsNullOrWhiteSpace(_shopBackgroundAsset))
			{
				_shopBackgroundAsset = shop.backgroundAsset ?? string.Empty;
			}
		}

		private void AddShopSystems()
		{
			if (!_firstLoad) return;
			_firstLoad = false;
			_shopBackgroundDisplaySystem = new ShopBackgroundDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_shopBackgroundDisplaySystem);
			_forSaleDisplaySystem = new ForSaleDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch, _imageAssets);
			_world.AddSystem(_forSaleDisplaySystem);
			_loadoutButtonDisplaySystem = new LoadoutButtonDisplaySystem(_world.EntityManager, _graphicsDevice, _spriteBatch);
			_world.AddSystem(_loadoutButtonDisplaySystem);
		}

		protected override void UpdateEntity(Entity entity, GameTime gameTime) { }
	}
}

