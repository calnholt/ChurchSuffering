using Crusaders30XX.ECS.Core;

namespace Crusaders30XX.ECS.Events
{
	public class BlockAssignmentAdded
	{
		public Entity Card;
		public int DeltaBlock;
		public string Color; // "Red" | "White" | "Black"
	}

	public class BlockAssignmentRemoved
	{
		public Entity Card;
		public int DeltaBlock;
		public string Color; // optional, e.g., "Red", "White", "Black"
	}
}

