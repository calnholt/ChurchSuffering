using ChurchSuffering.ECS.Core;
using ChurchSuffering.ECS.Input;

namespace ChurchSuffering.ECS.Components
{
    public class PlayerInputState : IComponent
    {
        public Entity Owner { get; set; }
        public PlayerInputFrame Frame { get; set; }
        public CursorTarget CursorTarget { get; set; } = CursorTarget.None;
        public bool IsCursorInteractionEnabled { get; set; } = true;
    }

    public class InputContext : IComponent
    {
        public Entity Owner { get; set; }
        public string Id { get; set; } = InputContextIds.Gameplay;
        public int Priority { get; set; }
        public bool IsActive { get; set; } = true;
        public bool AcceptsCursor { get; set; } = true;
        public bool AcceptsCommands { get; set; } = true;
        public bool IsDiagnostic { get; set; }
    }

    public class InputContextMember : IComponent
    {
        public Entity Owner { get; set; }
        public string ContextId { get; set; } = InputContextIds.Gameplay;
    }

    public static class InputContextIds
    {
        public const string Gameplay = "gameplay";
        public const string Overlay = "overlay";
        public const string Diagnostic = "diagnostic";
    }
}
