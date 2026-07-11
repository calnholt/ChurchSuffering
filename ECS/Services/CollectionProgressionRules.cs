using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Data.Save;
using Crusaders30XX.ECS.Factories;

namespace Crusaders30XX.ECS.Services;

public static class CollectionProgressionRules
{
	public const int FirstLevelPoints = 20;
	public const int SubsequentLevelPoints = 30;
	public const int FirstShopRefreshTime = 8;
	public const int SecondShopRefreshTime = 16;
	public const int ThirdShopRefreshTime = 24;

	private const int CardWeight = 50;
	private const int MedalWeight = 30;
	private const int EquipmentWeight = 20;

	public static int GetCompletedLevelCount(int totalPoints)
	{
		totalPoints = Math.Max(0, totalPoints);
		if (totalPoints < FirstLevelPoints) return 0;
		return 1 + (totalPoints - FirstLevelPoints) / SubsequentLevelPoints;
	}

	public static (int Level, int PointsInLevel, int PointsRequired) GetLevelState(int totalPoints)
	{
		int level = GetCompletedLevelCount(totalPoints);
		int completedPointCost = level == 0
			? 0
			: FirstLevelPoints + (level - 1) * SubsequentLevelPoints;
		return (level, Math.Max(0, totalPoints - completedPointCost), level == 0 ? FirstLevelPoints : SubsequentLevelPoints);
	}

	public static int CalculateClimbPoints(int climbTime, bool completedFinalBoss, bool abandoned)
	{
		if (abandoned) return 0;
		int points = 0;
		if (climbTime >= FirstShopRefreshTime) points += 1;
		if (climbTime >= SecondShopRefreshTime) points += 3;
		if (climbTime >= ThirdShopRefreshTime) points += 5;
		if (completedFinalBoss) points += 3;
		return points;
	}

	public static void ReconcileEarnedPacks(PlayerCollectionSave collection, Random random = null)
	{
		if (collection == null) return;
		EnsureCollections(collection);
		random ??= Random.Shared;
		int completedLevels = GetCompletedLevelCount(collection.totalPoints);
		while (collection.processedRewardLevels < completedLevels)
		{
			collection.processedRewardLevels++;
			var pack = CreatePack(collection, random);
			if (pack == null) continue;
			collection.pendingBoosterPacks.Add(pack);
		}
	}

	public static BoosterPackSave CreatePack(PlayerCollectionSave collection, Random random = null)
	{
		if (collection == null) return null;
		EnsureCollections(collection);
		random ??= Random.Shared;
		var pack = new BoosterPackSave();
		for (int slot = 0; slot < 3; slot++)
		{
			var cards = GetEligibleCardIds(collection);
			var medals = GetEligibleMedalIds(collection);
			var equipment = GetEligibleEquipmentIds(collection);
			if (cards.Count == 0 && medals.Count == 0 && equipment.Count == 0) break;

			string kind = RollKind(cards.Count > 0, medals.Count > 0, equipment.Count > 0, random);
			string id = kind switch
			{
				"card" => Pick(cards, random),
				"medal" => Pick(medals, random),
				"equipment" => Pick(equipment, random),
				_ => string.Empty,
			};
			if (string.IsNullOrWhiteSpace(id)) break;

			var reward = new BoosterPackRewardSave
			{
				kind = kind,
				id = id,
				cardColor = PickCardColor(random),
			};
			pack.rewards.Add(reward);
			AddOwned(collection, reward);
		}
		return pack.rewards.Count == 3 ? pack : null;
	}

	public static List<string> GetEligibleCardIds(PlayerCollectionSave collection)
	{
		var owned = new HashSet<string>(collection?.cardIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
		return CardFactory.GetAllCards().Values
			.Where(card => card != null && card.CanAddToLoadout && !card.IsWeapon && !card.IsToken)
			.Select(card => card.CardId)
			.Where(id => !string.IsNullOrWhiteSpace(id) && !owned.Contains(id))
			.Distinct(StringComparer.OrdinalIgnoreCase)
			.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static List<string> GetEligibleMedalIds(PlayerCollectionSave collection)
	{
		var owned = new HashSet<string>(collection?.medalIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
		return MedalFactory.GetAllMedals().Keys
			.Select(id => id.ToKey())
			.Where(id => !string.IsNullOrWhiteSpace(id) && !owned.Contains(id))
			.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static List<string> GetEligibleEquipmentIds(PlayerCollectionSave collection)
	{
		var owned = new HashSet<string>(collection?.equipmentIds ?? new List<string>(), StringComparer.OrdinalIgnoreCase);
		return EquipmentFactory.GetAllEquipment().Keys
			.Select(id => id.ToKey())
			.Where(id => !string.IsNullOrWhiteSpace(id) && !owned.Contains(id))
			.OrderBy(id => id, StringComparer.OrdinalIgnoreCase)
			.ToList();
	}

	public static void AddOwned(PlayerCollectionSave collection, BoosterPackRewardSave reward)
	{
		if (collection == null || reward == null || string.IsNullOrWhiteSpace(reward.id)) return;
		EnsureCollections(collection);
		switch (reward.kind)
		{
			case "card": AddDistinct(collection.cardIds, reward.id); break;
			case "medal": AddDistinct(collection.medalIds, reward.id); break;
			case "equipment": AddDistinct(collection.equipmentIds, reward.id); break;
		}
	}

	private static string RollKind(bool cardsAvailable, bool medalsAvailable, bool equipmentAvailable, Random random)
	{
		var weights = new Dictionary<string, int>();
		if (cardsAvailable) weights["card"] = CardWeight;
		if (medalsAvailable) weights["medal"] = MedalWeight;
		if (equipmentAvailable) weights["equipment"] = EquipmentWeight;

		foreach (var missing in new[]
		{
			(cardsAvailable, CardWeight),
			(medalsAvailable, MedalWeight),
			(equipmentAvailable, EquipmentWeight),
		})
		{
			if (missing.Item1) continue;
			int share = missing.Item2 / Math.Max(1, weights.Count);
			foreach (var key in weights.Keys.ToList()) weights[key] += share;
		}

		int roll = random.Next(weights.Values.Sum());
		foreach (var (kind, weight) in weights)
		{
			if (roll < weight) return kind;
			roll -= weight;
		}
		return weights.Keys.First();
	}

	private static string Pick(IReadOnlyList<string> values, Random random) => values[random.Next(values.Count)];

	private static string PickCardColor(Random random) => new[] { "White", "Red", "Black" }[random.Next(3)];

	private static void AddDistinct(List<string> values, string value)
	{
		if (!values.Contains(value, StringComparer.OrdinalIgnoreCase)) values.Add(value);
	}

	private static void EnsureCollections(PlayerCollectionSave collection)
	{
		collection.cardIds ??= new List<string>();
		collection.medalIds ??= new List<string>();
		collection.equipmentIds ??= new List<string>();
		collection.pendingBoosterPacks ??= new List<BoosterPackSave>();
	}
}
