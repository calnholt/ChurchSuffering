using ChurchSuffering.ECS.Input;

namespace ChurchSuffering.ECS.Events
{
	public sealed class RumbleRequested
	{
		public RumbleProfile Profile { get; init; }
		public float Scale { get; init; } = 1f;
		public RumbleGroup Group { get; init; } = RumbleGroup.Default;
	}

	public sealed class RumbleSettingsChangedEvent
	{
		public int Level { get; init; }
	}

	public sealed class RumbleGroupCleared
	{
		public RumbleGroup Group { get; init; }
	}
}
