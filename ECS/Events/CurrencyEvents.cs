using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Events
{
	public class GoldChanged
	{
		public int OldGold { get; set; }
		public int NewGold { get; set; }
		public int Delta { get; set; }
		public string Reason { get; set; } = "";
	}

	public class ModifyGoldRequestEvent
	{
		public int Delta { get; set; }
		public string Reason { get; set; } = "";
	}
}


