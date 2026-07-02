using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Services
{
	public static class RunMapShopGeneratorService
	{
		private const int ShopRngSalt = 0x5A4F5A4F;

		private static readonly string[] ColorNames = { "Red", "White", "Black" };

		public static List<RunMapShop> Generate(int runMapSeed, IReadOnlyList<RunMapNode> nodes)
		{
			if (nodes == null || nodes.Count == 0) return new List<RunMapShop>();

			var rng = new Random(runMapSeed ^ ShopRngSalt);
			var cardPool = BuildCardPool();
			var shops = new List<RunMapShop>(LocationMapConstants.RunMapShopCount);
			var placedPositions = new List<(float x, float y)>();
			var reachableIndices = RunMapReachabilityService.GetReachableNodeIndices(nodes);
			var anchorNodes = nodes
				.Select((n, i) => (Node: n, Index: i))
				.Where(x => x.Node != null && reachableIndices.Contains(x.Index))
				.Select(x => x.Node)
				.ToList();
			if (anchorNodes.Count == 0)
			{
				throw new InvalidOperationException(
					"[RunMapShopGeneratorService] No reachable quest nodes for shop placement.");
			}

			var displayNames = RunMapShopCatalog.PickDisplayNames(rng, LocationMapConstants.RunMapShopCount);
			var backgroundAssets = RunMapShopCatalog.PickBackgroundAssets(rng, LocationMapConstants.RunMapShopCount);
			int medalShopIndex = rng.Next(LocationMapConstants.RunMapShopCount);
			var nonMedalIndices = Enumerable.Range(0, LocationMapConstants.RunMapShopCount)
				.Where(i => i != medalShopIndex)
				.ToList();
			int equipmentShopIndex = nonMedalIndices[rng.Next(nonMedalIndices.Count)];

			for (int shopIndex = 0; shopIndex < LocationMapConstants.RunMapShopCount; shopIndex++)
			{
				if (!RunMapLandmarkPlacementService.TryPlace(rng, anchorNodes, nodes, placedPositions, out float x, out float y))
				{
					throw new InvalidOperationException(
						$"[RunMapShopGeneratorService] Failed to place shop {shopIndex}.");
				}

				placedPositions.Add((x, y));
				var items = RollShopItems(rng, cardPool);
				if (shopIndex == medalShopIndex)
				{
					InjectRandomMedalOffer(rng, items);
				}
				else if (shopIndex == equipmentShopIndex)
				{
					InjectRandomEquipmentOffer(rng, items);
				}
				shops.Add(new RunMapShop
				{
					id = ShopId(shopIndex),
					displayName = displayNames[shopIndex],
					backgroundAsset = backgroundAssets[shopIndex],
					worldX = x,
					worldY = y,
					items = items,
				});
			}

			return shops;
		}

		private static List<string> BuildCardPool()
		{
			var pool = new List<string>();
			foreach (var card in CardFactory.GetAllCards().Values)
			{
				if (card == null || !card.CanAddToLoadout || card.IsWeapon || card.IsToken) continue;
				string cardId = card.CardId;
				if (string.IsNullOrWhiteSpace(cardId) || StartingDeckGeneratorService.IsInDefaultStarterPool(cardId)) continue;
				pool.Add(cardId);
			}

			return pool.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
		}

		private static List<RunMapShopItem> RollShopItems(Random rng, List<string> cardPool)
		{
			var items = new List<RunMapShopItem>(LocationMapConstants.RunMapShopItemsPerShop);
			if (cardPool.Count == 0) return items;

			var pickedIds = cardPool.OrderBy(_ => rng.Next())
				.Take(LocationMapConstants.RunMapShopItemsPerShop)
				.ToList();
			var pickedColors = ColorNames.OrderBy(_ => rng.Next())
				.Take(LocationMapConstants.RunMapShopItemsPerShop)
				.ToList();

			for (int i = 0; i < LocationMapConstants.RunMapShopItemsPerShop; i++)
			{
				items.Add(new RunMapShopItem
				{
					cardId = pickedIds[i % pickedIds.Count],
					color = pickedColors[i],
					price = LocationMapConstants.RunMapShopCardPrice,
					isPurchased = false,
					displayRotationDeg = rng.Next(-5, 6),
				});
			}

			return items;
		}

		private static void InjectRandomMedalOffer(Random rng, List<RunMapShopItem> items)
		{
			if (items == null || items.Count == 0) return;

			var medalIds = MedalFactory.GetAllMedals().Keys.Select(id => id.ToKey()).ToList();
			if (medalIds.Count == 0) return;

			string medalId = medalIds[rng.Next(medalIds.Count)];
			int slotIndex = rng.Next(items.Count);
			items[slotIndex] = new RunMapShopItem
			{
				itemType = RunMapShopItem.ItemTypeMedal,
				cardId = medalId,
				color = string.Empty,
				price = LocationMapConstants.RunMapShopMedalPrice,
				isPurchased = false,
				displayRotationDeg = rng.Next(-5, 6),
			};
		}

		private static void InjectRandomEquipmentOffer(Random rng, List<RunMapShopItem> items)
		{
			if (items == null || items.Count == 0) return;

			var pool = RunMapEquipmentPoolService.BuildShopOfferPool(loadout: null);
			if (pool.Count == 0) return;

			string equipmentId = pool[rng.Next(pool.Count)];
			int slotIndex = rng.Next(items.Count);
			items[slotIndex] = new RunMapShopItem
			{
				itemType = RunMapShopItem.ItemTypeEquipment,
				cardId = equipmentId,
				color = string.Empty,
				price = LocationMapConstants.RunMapShopEquipmentPrice,
				isPurchased = false,
				displayRotationDeg = rng.Next(-5, 6),
			};
		}

		private static string ShopId(int index) => $"shop_{index}";
	}
}
