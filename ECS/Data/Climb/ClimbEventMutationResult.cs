using Crusaders30XX.ECS.Data.Save;

namespace Crusaders30XX.ECS.Data.Climb
{
	public sealed class ClimbEventMutationResult
	{
		public bool Succeeded { get; init; }
		public bool AlreadyResolved { get; init; }
		public string EventSlotId { get; init; } = string.Empty;
		public string RestrictedEntryId { get; init; } = string.Empty;
		public string RestrictionName { get; init; } = string.Empty;
		public bool RunLongPassivesChanged { get; init; }
		public string RunLongPassiveType { get; init; } = string.Empty;
		public int RunLongPassiveAmount { get; init; }
		public int RunLongPassiveTotal { get; init; }
		public ClimbResourceSave ResourcesGained { get; init; } = new();
		public string UpgradedEntryId { get; init; } = string.Empty;
		public string UpgradedCardKey { get; init; } = string.Empty;
		public bool ReachedFinalTime { get; init; }
	}
}
