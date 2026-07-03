using System.Collections.Generic;
using System.Numerics;
using Crusaders30XX.ECS.Components;
using Crusaders30XX.ECS.Events;
using Crusaders30XX.ECS.Objects.Enemies;

namespace Crusaders30XX.ECS.Data.Locations
{
	public class LocationDefinition
	{
		public string id { get; set; }
		public string name { get; set; }
		public List<PointOfInterestDefinition> pointsOfInterest { get; set; } = new List<PointOfInterestDefinition>();
	}

	public class PointOfInterestDefinition
	{
		public string id { get; set; }
		public Vector2 worldPosition { get; set; } = new Vector2(0f, 0f);
		public int revealRadius { get; set; } = 300;
		public int difficulty { get; set; } = 0;
		public bool isRevealed { get; set; } = false;
		public string background { get; set; } = string.Empty;
		public int unrevealedRadius { get; set; } = 50;
		public string name { get; set; } = string.Empty;
		public PointOfInterestType type { get; set; } = PointOfInterestType.Quest;
		public int rewardGold { get; set; } = 0;
		public List<LocationEventDefinition> events { get; set; } = new List<LocationEventDefinition>();
		public List<TribulationDefinition> tribulations { get; set; } = new List<TribulationDefinition>();
		public List<ForSaleItemDefinition> forSale { get; set; } = new List<ForSaleItemDefinition>();
		public MusicTrack musicTrack { get; set; } = MusicTrack.None;
	}

	public class TribulationDefinition
	{
		public string text { get; set; }
		public string trigger { get; set; }
	}

	public class LocationEventDefinition
	{
		public string id { get; set; }
		public string type { get; set; }
		public List<EnemyModification> modifications { get; set; } = new List<EnemyModification>();
	}

	public class EnemyModification
	{
		public string Type { get; set; }
		public int Delta { get; set; }
	}

	public class ForSaleItemDefinition
	{
		public string id { get; set; }
		public string type { get; set; } // Card | Medal | Equipment
		public int price { get; set; }
		public bool isPurchased { get; set; }
	}
}

