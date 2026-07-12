using System;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.Input
{
    public enum PlayerInputDevice
    {
        KeyboardMouse,
        Gamepad,
    }

    public enum GamepadGlyphStyle
    {
        Xbox,
        PlayStation,
    }

    public enum PlayerButton
    {
        Primary,
        Secondary,
        Cancel,
        Escape,
        Back,
        FaceB,
        FaceX,
        FaceY,
        Start,
        LeftShoulder,
        RightShoulder,
        LeftStick,
        Space,
        Enter,
        Shift,
        MoveUp,
        MoveDown,
        MoveLeft,
        MoveRight,
        F11,
        DebugMenu,
        EntityList,
        DebugDamage,
        Profiler,
        Quit,
    }

    [Flags]
    public enum PlayerButtonMask : ulong
    {
        None = 0,
    }

    public readonly record struct PlayerInputFrame(
        long Sequence,
        bool IsWindowActive,
        bool IsGamepadConnected,
        PlayerInputDevice Device,
        PlayerInputDevice PreviousDevice,
        GamepadGlyphStyle GamepadGlyphStyle,
        Vector2 PointerPosition,
        Vector2 PointerDelta,
        float ScrollDelta,
        Vector2 LeftStick,
        Vector2 RightStick,
        float LeftTrigger,
        float RightTrigger,
        PlayerButtonMask DownButtons,
        PlayerButtonMask PressedButtons,
        PlayerButtonMask ReleasedButtons)
    {
        public bool DeviceChanged => Device != PreviousDevice;

        public bool IsDown(PlayerButton button) => Contains(DownButtons, button);

        public bool WasPressed(PlayerButton button) => Contains(PressedButtons, button);

        public bool WasReleased(PlayerButton button) => Contains(ReleasedButtons, button);

        public static PlayerButtonMask Mask(PlayerButton button)
        {
            return (PlayerButtonMask)(1UL << (int)button);
        }

        private static bool Contains(PlayerButtonMask mask, PlayerButton button)
        {
            return (mask & Mask(button)) != 0;
        }
    }
}
