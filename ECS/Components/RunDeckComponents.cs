using ChurchSuffering.ECS.Core;

namespace ChurchSuffering.ECS.Components
{
	/// <summary>
	/// Marks an entity as one stable run-persistent loadout entry.
	/// </summary>
	public class RunDeckCard : IComponent
	{
		public Entity Owner { get; set; }
		public string EntryId { get; set; } = string.Empty;
		public string CardKey { get; set; } = string.Empty;
	}
}
