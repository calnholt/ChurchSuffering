using Crusaders30XX.ECS.Core;
using Crusaders30XX.ECS.Data.Locations;
using Crusaders30XX.ECS.Events;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Components
{
	public class PointOfInterest : IComponent
	{
		public Entity Owner { get; set; }
		public string Id { get; set; } = string.Empty;
		public Vector2 WorldPosition { get; set; } = Vector2.Zero;
		public int Difficulty { get; set; } = 0;
		public int RevealRadius { get; set; } = LocationMapConstants.DefaultRevealRadius;
		public bool IsCompleted { get; set; } = false;
		public bool IsRevealed { get; set; } = false;
		public int UnrevealedRadius { get; set; } = LocationMapConstants.DefaultUnrevealedRadius;
		public float DisplayRadius { get; set; } = 0f;
		public PointOfInterestType Type { get; set; } = PointOfInterestType.Quest;
		public bool IsRevealedByProximity { get; set; } = false;
		public MusicTrack MusicTrack { get; set; } = MusicTrack.None;
		public System.Collections.Generic.List<string> ChildPoiIds { get; set; } = new System.Collections.Generic.List<string>();
		public int RunMapIndex { get; set; } = -1;
		public string ShopId { get; set; } = string.Empty;
		public string TreasureId { get; set; } = string.Empty;
		public string EventId { get; set; } = string.Empty;
		public bool IsMapVisibleFromStart { get; set; }
	}

	public class POITitleTooltipSource : IComponent
	{
		public Entity Owner { get; set; }
		public string Title { get; set; } = string.Empty;
	}


	public enum PointOfInterestType
	{
		Quest,
		Shop,
		Treasure,
		Event,
		Hellrift
	}
}

