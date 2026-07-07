using System;
using System.IO;
using System.Linq;
using System.Text.Json;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Events;

namespace Crusaders30XX.ECS.Services
{
	public static class PurchaseItemService
	{
		public struct PurchaseResult
		{
			public bool Success;
			public string ItemId;
			public int Spent;
			public int NewGold;
			public string LocationId;
			public string ShopName;
			public string Error;
		}

		public static bool CanAfford(int price)
		{
			try { return SaveCache.GetGold() >= price; }
			catch { return false; }
		}

		public static PurchaseResult TryPurchase(EntityManager entityManager, Entity itemEntity)
		{
			var result = new PurchaseResult { Success = false, ItemId = string.Empty, Spent = 0, NewGold = 0, LocationId = string.Empty, ShopName = string.Empty, Error = string.Empty };
			if (entityManager == null || itemEntity == null) { result.Error = "InvalidArgs"; return result; }

			var fs = itemEntity.GetComponent<ForSaleItem>();
			var ui = itemEntity.GetComponent<UIElement>();

			if (fs == null) { result.Error = "NotForSaleEntity"; return result; }
			if (fs.IsPurchased) { result.Error = "AlreadyPurchased"; return result; }

			int price = System.Math.Max(0, fs.Price);
			if (!CanAfford(price)) { result.Error = "InsufficientGold"; return result; }

			if (!string.IsNullOrWhiteSpace(fs.ShopId) && fs.ShopSlotIndex >= 0)
			{
				return TryPurchaseRunMapShop(entityManager, itemEntity, fs, price, ref result);
			}

			result.ItemId = fs.Id ?? string.Empty;
			result.Spent = price;
			result.ShopName = fs.SourceShopName ?? string.Empty;

			// Snapshot old gold for event payload
			int oldGold = 0;
			try { oldGold = SaveCache.GetGold(); } catch { oldGold = 0; }

			// Identify location id containing this shop/item for targeted JSON update
			string locationId = TryResolveLocationIdForShop(fs.Id, result.ShopName);
			result.LocationId = locationId ?? string.Empty;

			// Update save: subtract gold and add to collection
			if (!TrySpendGoldAndAddToCollection(fs.Id, price, fs.ItemType, out int newGold))
			{
				result.Error = "SaveUpdateFailed";
				return result;
			}
			result.NewGold = newGold;

			// Publish GoldChanged event so UI can update immediately
			try
			{
				EventManager.Publish(new GoldChanged
				{
					OldGold = oldGold,
					NewGold = newGold,
					Delta = newGold - oldGold,
					Reason = "Purchase"
				});
			}
			catch { }

			// Refresh runtime caches so UI reflects new state
			try { SaveCache.Reload(); } catch { }

			result.Success = true;
			return result;
		}

		private static PurchaseResult TryPurchaseRunMapShop(
			EntityManager entityManager,
			Entity itemEntity,
			ForSaleItem fs,
			int price,
			ref PurchaseResult result)
		{
			result.ItemId = fs.Id ?? string.Empty;
			result.Spent = price;
			result.ShopName = fs.SourceShopName ?? string.Empty;

			int oldGold = 0;
			try { oldGold = SaveCache.GetGold(); } catch { oldGold = 0; }

			if (!SaveCache.TryPurchaseRunMapShopItem(fs.ShopId, fs.ShopSlotIndex, out int newGold))
			{
				result.Error = "SaveUpdateFailed";
				return result;
			}

			if (fs.ItemType == ForSaleItemType.Medal)
			{
				SaveCache.MarkWayStationMedalPurchased(fs.Id);
				RunMedalService.AcquireAndEquip(entityManager, fs.Id);
			}
			else if (fs.ItemType == ForSaleItemType.Equipment)
			{
				RunEquipmentService.EquipOnPlayer(entityManager, fs.Id);
			}

			result.NewGold = newGold;
			result.Success = true;

			try
			{
				EventManager.Publish(new GoldChanged
				{
					OldGold = oldGold,
					NewGold = newGold,
					Delta = newGold - oldGold,
					Reason = "Purchase"
				});
			}
			catch { }

			try { SaveCache.Reload(); } catch { }

			return result;
		}

		private static bool TrySpendGoldAndAddToCollection(string itemId, int price, ForSaleItemType itemType, out int newGold)
		{
			newGold = 0;
			try
			{
				return SaveCache.TrySpendGoldAndAddToCollection(itemId, price, itemType, out newGold);
			}
			catch
			{
				return false;
			}
		}

		private static string TryResolveLocationIdForShop(string itemId, string shopName)
		{
			try
			{
				var all = LocationDefinitionCache.GetAll();
				foreach (var kv in all)
				{
					var def = kv.Value;
					if (def?.pointsOfInterest == null) continue;
					foreach (var poi in def.pointsOfInterest)
					{
						if (poi?.type != PointOfInterestType.Shop) continue;
						if (!string.IsNullOrWhiteSpace(shopName) && !string.Equals(poi.name ?? string.Empty, shopName, StringComparison.OrdinalIgnoreCase)) continue;
						if (poi.forSale != null && poi.forSale.Any(f => string.Equals(f.id, itemId, StringComparison.OrdinalIgnoreCase)))
						{
							return kv.Key;
						}
					}
				}
			}
			catch { }
			return null;
		}

	}
}
