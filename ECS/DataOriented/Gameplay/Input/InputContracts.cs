#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Components;

public enum PlayerInputDevice : byte
{
    KeyboardMouse = 0,
    Gamepad = 1,
}

/// <summary>The gamepad glyph family selected by the centralized hardware adapter.</summary>
public enum GamepadGlyphStyle : byte
{
    Xbox = 0,
    PlayStation = 1,
}

/// <summary>
/// Stable, presentation-independent gamepad glyph vocabulary. Presentation chooses the Xbox or
/// PlayStation artwork from <see cref="GamepadGlyphStyle"/> without seeing MonoGame button types.
/// </summary>
public enum GamepadGlyph : byte
{
    A = 0,
    B = 1,
    X = 2,
    Y = 3,
    View = 4,
    Start = 5,
    LeftShoulder = 6,
    RightShoulder = 7,
    LeftStick = 8,
}

public enum PlayerInputButton : byte
{
    Primary = 0,
    Secondary = 1,
    Cancel = 2,
    ShowHint = 3,
    ToggleFullScreen = 4,
    ToggleDebugMenu = 5,
    ToggleEntityList = 6,
    DealDebugDamage = 7,
    ToggleProfiler = 8,
    Quit = 9,
    Modifier = 10,
    Escape = 11,
    Back = 12,
    FaceB = 13,
    FaceX = 14,
    FaceY = 15,
    Start = 16,
    LeftShoulder = 17,
    RightShoulder = 18,
    LeftStick = 19,
    Space = 20,
    Enter = 21,
    Shift = 22,
    MoveUp = 23,
    MoveDown = 24,
    MoveLeft = 25,
    MoveRight = 26,
}

/// <summary>Maps stable input buttons to the glyphs used by gamepad hot-key presentation.</summary>
public static class PlayerInputGlyphs
{
    public static bool TryResolve(PlayerInputButton button, out GamepadGlyph glyph)
    {
        switch (button)
        {
            case PlayerInputButton.Primary:
                glyph = GamepadGlyph.A;
                return true;
            case PlayerInputButton.FaceB:
                glyph = GamepadGlyph.B;
                return true;
            case PlayerInputButton.Secondary:
            case PlayerInputButton.FaceX:
                glyph = GamepadGlyph.X;
                return true;
            case PlayerInputButton.FaceY:
                glyph = GamepadGlyph.Y;
                return true;
            case PlayerInputButton.Back:
                glyph = GamepadGlyph.View;
                return true;
            case PlayerInputButton.Start:
                glyph = GamepadGlyph.Start;
                return true;
            case PlayerInputButton.LeftShoulder:
                glyph = GamepadGlyph.LeftShoulder;
                return true;
            case PlayerInputButton.RightShoulder:
                glyph = GamepadGlyph.RightShoulder;
                return true;
            case PlayerInputButton.ShowHint:
            case PlayerInputButton.LeftStick:
                glyph = GamepadGlyph.LeftStick;
                return true;
            default:
                glyph = default;
                return false;
        }
    }
}

[Flags]
public enum PlayerInputFlags : byte
{
    None = 0,
    WindowActive = 1 << 0,
    InputEnabled = 1 << 1,
    CursorInteractionEnabled = 1 << 2,
}

public readonly record struct PlayerInputFrame(
    long Sequence,
    Vector2 PointerPosition,
    Vector2 PointerDelta,
    Vector2 LeftStick,
    Vector2 RightStick,
    float ScrollDelta,
    float LeftTrigger,
    float RightTrigger,
    ulong DownButtons,
    ulong PressedButtons,
    ulong ReleasedButtons,
    PlayerInputDevice Device,
    bool IsWindowActive,
    PlayerInputDevice PreviousDevice = PlayerInputDevice.KeyboardMouse,
    bool IsGamepadConnected = false,
    GamepadGlyphStyle GamepadGlyphStyle = GamepadGlyphStyle.Xbox)
{
    public bool DeviceChanged => Device != PreviousDevice;

    public bool IsDown(PlayerInputButton button) => (DownButtons & Mask(button)) != 0;

    public bool WasPressed(PlayerInputButton button) => (PressedButtons & Mask(button)) != 0;

    public bool WasReleased(PlayerInputButton button) => (ReleasedButtons & Mask(button)) != 0;

    public static ulong Mask(PlayerInputButton button) => 1UL << (int)button;
}

public enum CursorTargetKind : byte
{
    None = 0,
    UI = 1,
    Diagnostic = 2,
}

/// <summary>Hot, unique input state. Hardware adapters submit frames through PlayerInputEvent.</summary>
public struct PlayerInputState : IComponent
{
    public PlayerInputFrame Frame;
    public EntityId CursorTarget;
    public EntityId PreviousHoverTarget;
    public StringId CursorContext;
    public StringId CommandContext;
    public float CursorCoverage;
    public CursorTargetKind TargetKind;
    public PlayerInputFlags Flags;

    public readonly bool IsInputEnabled => (Flags & PlayerInputFlags.InputEnabled) != 0;

    public readonly bool IsCursorInteractionEnabled =>
        (Flags & PlayerInputFlags.CursorInteractionEnabled) != 0;
}

/// <summary>Explicit input-context membership; absent membership falls back to the UI layer.</summary>
public struct InputContextMember : IComponent
{
    public StringId ContextId;
}

public struct PlayerInputSingleton : ITag
{
}

/// <summary>Opt-out used by cursor targeting for entities that remain visually present.</summary>
public struct FilteredFromCursor : ITag
{
}
