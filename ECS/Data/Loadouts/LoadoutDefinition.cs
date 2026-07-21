using System.Collections.Generic;

namespace Crusaders30XX.ECS.Data.Loadouts
{
	public class LoadoutCardEntry
	{
		public string entryId { get; set; } = string.Empty;
		public string cardKey { get; set; } = string.Empty;
		public string secondaryColor { get; set; } = string.Empty;
		public bool isStarter { get; set; }
		public bool countsAsTraded { get; set; }
		public List<string> restrictions { get; set; } = new();
		/// <summary>Exact stack counts for persistent stacked restrictions, keyed by restriction name.</summary>
		public Dictionary<string, int> restrictionStacks { get; set; } = new();
		/// <summary>Permanent run-long boons applied to this exact deck entry.</summary>
		public List<CardBoonSave> boons { get; set; } = new();
	}

	public class CardBoonSave
	{
		public string type { get; set; } = string.Empty;
		public int amount { get; set; }
	}

	public static class CardBoonKinds
	{
		public const string Wild = "wild";
		public const string Overcharged = "overcharged";
		public const string Quickened = "quickened";
		public const string Versatile = "versatile";
		public const string Honed = "honed";
		public const string Guarded = "guarded";

		public static readonly string[] All =
		{
			Wild,
			Overcharged,
			Quickened,
			Versatile,
			Honed,
			Guarded,
		};
	}

	public class LoadoutDefinition
	{
		public string id { get; set; }
		public string name { get; set; }
		public List<LoadoutCardEntry> cards { get; set; } = new();
		public string weaponId { get; set; }
		public string temperanceId { get; set; }
		public string chestId { get; set; }
		public string legsId { get; set; }
		public string armsId { get; set; }
		public string headId { get; set; }
		/// <summary>Remaining uses for the equipped chest piece. Null means full (MaxUses) when loading.</summary>
		public int? chestRemainingUses { get; set; }
		/// <summary>Remaining uses for the equipped legs piece. Null means full (MaxUses) when loading.</summary>
		public int? legsRemainingUses { get; set; }
		/// <summary>Remaining uses for the equipped arms piece. Null means full (MaxUses) when loading.</summary>
		public int? armsRemainingUses { get; set; }
		/// <summary>Remaining uses for the equipped head piece. Null means full (MaxUses) when loading.</summary>
		public int? headRemainingUses { get; set; }
		public List<string> medalIds { get; set; } = new();
	}
}
