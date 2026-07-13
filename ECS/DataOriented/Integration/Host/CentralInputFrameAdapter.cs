#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Components;
using Crusaders30XX.ECS.DataOriented.Gameplay.Input;
using Microsoft.Xna.Framework;
using LegacyInputDevice = Crusaders30XX.ECS.Input.PlayerInputDevice;
using LegacyInputFrame = Crusaders30XX.ECS.Input.PlayerInputFrame;
using LegacyInputButton = Crusaders30XX.ECS.Input.PlayerButton;
using LegacyInputMask = Crusaders30XX.ECS.Input.PlayerButtonMask;
using LegacyGlyphStyle = Crusaders30XX.ECS.Input.GamepadGlyphStyle;

namespace Crusaders30XX.ECS.DataOriented.Integration.Host;

/// <summary>
/// Converts the frame captured by the repository's sole MonoGame hardware adapter into the
/// data-oriented host boundary. This type never polls keyboard, mouse, or gamepad state.
/// </summary>
public sealed class CentralInputFrameAdapter
{
    private readonly HostInputAdapter input = new();

    public HostInputSnapshot CreateSnapshot(
        in LegacyInputFrame frame,
        Rectangle renderDestination,
        int virtualWidth,
        int virtualHeight)
    {
        if (virtualWidth <= 0) throw new ArgumentOutOfRangeException(nameof(virtualWidth));
        if (virtualHeight <= 0) throw new ArgumentOutOfRangeException(nameof(virtualHeight));

        ulong down = MapButtons(frame.DownButtons);
        ulong pressed = MapButtons(frame.PressedButtons);
        ulong released = MapButtons(frame.ReleasedButtons);
        ulong previousDown = (down & ~pressed) | released;
        Vector2 previousPointer = frame.PointerPosition - frame.PointerDelta;
        Vector2 screenPointer = ToScreen(frame.PointerPosition, renderDestination, virtualWidth, virtualHeight);
        Vector2 previousScreenPointer = ToScreen(previousPointer, renderDestination, virtualWidth, virtualHeight);
        int scroll = frame.ScrollDelta == 0f ? 0 : Math.Sign(frame.ScrollDelta);

        return new HostInputSnapshot(
            screenPointer,
            previousScreenPointer,
            frame.LeftStick,
            frame.RightStick,
            frame.LeftTrigger,
            frame.RightTrigger,
            scroll,
            0,
            down,
            previousDown,
            frame.Device == LegacyInputDevice.Gamepad
                ? PlayerInputDevice.Gamepad
                : PlayerInputDevice.KeyboardMouse,
            frame.IsWindowActive,
            renderDestination,
            virtualWidth,
            virtualHeight,
            frame.PreviousDevice == LegacyInputDevice.Gamepad
                ? PlayerInputDevice.Gamepad
                : PlayerInputDevice.KeyboardMouse,
            frame.IsGamepadConnected,
            frame.GamepadGlyphStyle == LegacyGlyphStyle.PlayStation
                ? GamepadGlyphStyle.PlayStation
                : GamepadGlyphStyle.Xbox);
    }

    public DataOrientedInputSubmission Convert(
        in LegacyInputFrame frame,
        Rectangle renderDestination,
        int virtualWidth,
        int virtualHeight)
    {
        HostInputSnapshot snapshot = CreateSnapshot(in frame, renderDestination, virtualWidth, virtualHeight);
        return input.Convert(in snapshot);
    }

    private static ulong MapButtons(LegacyInputMask source)
    {
        ulong destination = 0;
        Add(ref destination, PlayerInputButton.Primary, source, LegacyInputButton.Primary);
        Add(ref destination, PlayerInputButton.Secondary, source, LegacyInputButton.Secondary);
        Add(ref destination, PlayerInputButton.Cancel, source, LegacyInputButton.Cancel);
        Add(ref destination, PlayerInputButton.Escape, source, LegacyInputButton.Escape);
        Add(ref destination, PlayerInputButton.Back, source, LegacyInputButton.Back);
        Add(ref destination, PlayerInputButton.FaceB, source, LegacyInputButton.FaceB);
        Add(ref destination, PlayerInputButton.FaceX, source, LegacyInputButton.FaceX);
        Add(ref destination, PlayerInputButton.FaceY, source, LegacyInputButton.FaceY);
        Add(ref destination, PlayerInputButton.Start, source, LegacyInputButton.Start);
        Add(ref destination, PlayerInputButton.LeftShoulder, source, LegacyInputButton.LeftShoulder);
        Add(ref destination, PlayerInputButton.RightShoulder, source, LegacyInputButton.RightShoulder);
        Add(ref destination, PlayerInputButton.LeftStick, source, LegacyInputButton.LeftStick);
        Add(ref destination, PlayerInputButton.ShowHint, source, LegacyInputButton.LeftStick);
        Add(ref destination, PlayerInputButton.Space, source, LegacyInputButton.Space);
        Add(ref destination, PlayerInputButton.Enter, source, LegacyInputButton.Enter);
        Add(ref destination, PlayerInputButton.Shift, source, LegacyInputButton.Shift);
        Add(ref destination, PlayerInputButton.MoveUp, source, LegacyInputButton.MoveUp);
        Add(ref destination, PlayerInputButton.MoveDown, source, LegacyInputButton.MoveDown);
        Add(ref destination, PlayerInputButton.MoveLeft, source, LegacyInputButton.MoveLeft);
        Add(ref destination, PlayerInputButton.MoveRight, source, LegacyInputButton.MoveRight);
        Add(ref destination, PlayerInputButton.ToggleFullScreen, source, LegacyInputButton.F11);
        Add(ref destination, PlayerInputButton.ToggleDebugMenu, source, LegacyInputButton.DebugMenu);
        Add(ref destination, PlayerInputButton.ToggleEntityList, source, LegacyInputButton.EntityList);
        Add(ref destination, PlayerInputButton.DealDebugDamage, source, LegacyInputButton.DebugDamage);
        Add(ref destination, PlayerInputButton.ToggleProfiler, source, LegacyInputButton.Profiler);
        Add(ref destination, PlayerInputButton.Quit, source, LegacyInputButton.Quit);
        Add(ref destination, PlayerInputButton.Modifier, source, LegacyInputButton.Shift);
        return destination;
    }

    private static void Add(
        ref ulong destination,
        PlayerInputButton target,
        LegacyInputMask source,
        LegacyInputButton legacy)
    {
        if ((source & LegacyInputFrame.Mask(legacy)) != 0)
            destination |= Crusaders30XX.ECS.DataOriented.Components.PlayerInputFrame.Mask(target);
    }

    private static Vector2 ToScreen(
        Vector2 virtualPosition,
        Rectangle destination,
        int virtualWidth,
        int virtualHeight)
    {
        float scaleX = destination.Width > 0 ? (float)destination.Width / virtualWidth : 1f;
        float scaleY = destination.Height > 0 ? (float)destination.Height / virtualHeight : 1f;
        return new Vector2(
            destination.X + virtualPosition.X * scaleX,
            destination.Y + virtualPosition.Y * scaleY);
    }
}
