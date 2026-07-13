#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Events;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Input;

/// <summary>
/// One primitive snapshot supplied by the external host after it has mapped keyboard, mouse, and
/// gamepad hardware to the stable data-oriented button/axis vocabulary. Gameplay code never sees
/// MonoGame hardware-state types.
/// </summary>
public readonly record struct HostInputSnapshot(
    Vector2 ScreenPointer,
    Vector2 PreviousScreenPointer,
    Vector2 LeftStick,
    Vector2 RightStick,
    float LeftTrigger,
    float RightTrigger,
    int ScrollValue,
    int PreviousScrollValue,
    ulong DownButtons,
    ulong PreviousDownButtons,
    PlayerInputDevice Device,
    bool IsWindowActive,
    Rectangle RenderDestination,
    int VirtualWidth,
    int VirtualHeight,
    PlayerInputDevice PreviousDevice = PlayerInputDevice.KeyboardMouse,
    bool IsGamepadConnected = false,
    GamepadGlyphStyle GamepadGlyphStyle = GamepadGlyphStyle.Xbox);

/// <summary>Events submitted to the root barrier before the Input phase.</summary>
public readonly record struct DataOrientedInputSubmission(
    PlayerInputEvent PlayerInput,
    CursorInputEvent CursorInput);

public interface IDataOrientedHostInputAdapter
{
    DataOrientedInputSubmission Convert(in HostInputSnapshot snapshot);
}

/// <summary>
/// Pure hardware-independent conversion from host values to ECS-040 events. The root publishes
/// both events and drains its one event runtime before scheduling the Input phase.
/// </summary>
public sealed class HostInputAdapter : IDataOrientedHostInputAdapter
{
    private long sequence;

    public DataOrientedInputSubmission Convert(in HostInputSnapshot snapshot)
    {
        if (snapshot.VirtualWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Virtual width must be positive.");
        }

        if (snapshot.VirtualHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(snapshot), "Virtual height must be positive.");
        }

        ulong down = snapshot.IsWindowActive ? snapshot.DownButtons : 0;
        ulong pressed = snapshot.IsWindowActive
            ? snapshot.DownButtons & ~snapshot.PreviousDownButtons
            : 0;
        ulong released = snapshot.IsWindowActive
            ? snapshot.PreviousDownButtons & ~snapshot.DownButtons
            : 0;
        Vector2 pointer = ToVirtualPosition(
            snapshot.ScreenPointer,
            snapshot.RenderDestination,
            snapshot.VirtualWidth,
            snapshot.VirtualHeight);
        Vector2 previousPointer = ToVirtualPosition(
            snapshot.PreviousScreenPointer,
            snapshot.RenderDestination,
            snapshot.VirtualWidth,
            snapshot.VirtualHeight);
        int rawScroll = snapshot.ScrollValue - snapshot.PreviousScrollValue;
        float scroll = rawScroll == 0 ? 0f : Math.Sign(rawScroll);

        var frame = new PlayerInputFrame(
            ++sequence,
            pointer,
            pointer - previousPointer,
            snapshot.LeftStick,
            snapshot.RightStick,
            scroll,
            Math.Clamp(snapshot.LeftTrigger, 0f, 1f),
            Math.Clamp(snapshot.RightTrigger, 0f, 1f),
            down,
            pressed,
            released,
            snapshot.Device,
            snapshot.IsWindowActive,
            snapshot.PreviousDevice,
            snapshot.IsGamepadConnected,
            snapshot.GamepadGlyphStyle);
        var cursor = new CursorInputEvent(
            frame.PointerPosition,
            frame.PointerDelta,
            frame.ScrollDelta,
            MathF.Abs(frame.RightStick.Y) > 0.15f ? frame.RightStick.Y : 0f,
            frame.IsDown(PlayerInputButton.Primary),
            frame.WasPressed(PlayerInputButton.Primary),
            frame.IsDown(PlayerInputButton.Secondary),
            frame.WasPressed(PlayerInputButton.Secondary),
            frame.Device);
        return new DataOrientedInputSubmission(new PlayerInputEvent(frame), cursor);
    }

    private static Vector2 ToVirtualPosition(
        Vector2 screenPosition,
        Rectangle destination,
        int virtualWidth,
        int virtualHeight)
    {
        float scaleX = destination.Width > 0 ? (float)destination.Width / virtualWidth : 1f;
        float scaleY = destination.Height > 0 ? (float)destination.Height / virtualHeight : 1f;
        float x = (screenPosition.X - destination.X) / Math.Max(0.001f, scaleX);
        float y = (screenPosition.Y - destination.Y) / Math.Max(0.001f, scaleY);
        return new Vector2(
            MathHelper.Clamp(x, 0f, virtualWidth),
            MathHelper.Clamp(y, 0f, virtualHeight));
    }
}
