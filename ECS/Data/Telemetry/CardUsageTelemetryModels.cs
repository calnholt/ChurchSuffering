using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ChurchSuffering.ECS.Data.Telemetry
{
	public enum CardUsageKind
	{
		Played,
		Blocked,
		DiscardedForCost,
	}

	public sealed class CardUsageCounts
	{
		public string cardId { get; set; } = string.Empty;
		public string name { get; set; } = string.Empty;
		public string type { get; set; } = string.Empty;
		public int played { get; set; }
		public int blocked { get; set; }
		public int discardedForCost { get; set; }

		[JsonIgnore]
		public int Total => played + blocked + discardedForCost;
	}

	public sealed class CardUsageRun
	{
		public string runId { get; set; } = string.Empty;
		public int runMapSeed { get; set; }
		public DateTimeOffset startedAtUtc { get; set; }
		public DateTimeOffset? endedAtUtc { get; set; }
		public Dictionary<string, CardUsageCounts> cards { get; set; } =
			new(StringComparer.OrdinalIgnoreCase);
	}

	public sealed class CardUsageTelemetryDocument
	{
		public const int CURRENT_VERSION = 1;

		public int version { get; set; } = CURRENT_VERSION;
		public CardUsageRun activeRun { get; set; }
		public List<CardUsageRun> runs { get; set; } = new();
		public Dictionary<string, CardUsageCounts> lifetime { get; set; } =
			new(StringComparer.OrdinalIgnoreCase);
	}
}
