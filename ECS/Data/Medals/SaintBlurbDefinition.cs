using System.Collections.Generic;

namespace ChurchSuffering.ECS.Data.Medals
{
	public class SaintBlurbDefinition
	{
		public string medalId { get; set; } = string.Empty;
		public string lifespan { get; set; } = string.Empty;
		public List<string> bioParagraphs { get; set; } = new List<string>();
		public string patronages { get; set; } = string.Empty;
		public string prayerTitle { get; set; } = string.Empty;
		public string prayerText { get; set; } = string.Empty;
	}

	public class SaintBlurbFile
	{
		public List<SaintBlurbDefinition> medals { get; set; } = new List<SaintBlurbDefinition>();
	}
}
