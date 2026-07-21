using ChurchSuffering.ECS.Input;

namespace ChurchSuffering.ECS.Events
{
    public enum PlayerCommand
    {
        QuitApplication,
        ToggleFullScreen,
        ToggleDebugMenu,
        ToggleEntityList,
        DealDebugDamage,
        ToggleProfiler,
        Cancel,
        ShowHint,
    }

    public sealed class PlayerInputEvent
    {
        public PlayerInputFrame Frame { get; init; }
    }

    public sealed class PlayerCommandEvent
    {
        public PlayerCommand Command { get; init; }
        public PlayerInputDevice Source { get; init; }
    }

    public sealed class SetPlayerInputEnabledEvent
    {
        public bool Enabled { get; init; }
    }
}
