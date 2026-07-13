#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Components;

public enum AnimationType : byte
{
    Fade = 0,
    Scale = 1,
    Move = 2,
    Rotate = 3,
}

[Flags]
public enum AnimationFlags : byte
{
    None = 0,
    Playing = 1 << 0,
    Looping = 1 << 1,
}

public struct Animation : IComponent
{
    public float Duration;
    public float CurrentTime;
    public AnimationType Type;
    public AnimationFlags Flags;

    public readonly bool IsPlaying => (Flags & AnimationFlags.Playing) != 0;

    public readonly bool IsLooping => (Flags & AnimationFlags.Looping) != 0;
}

public struct PositionTween : IComponent
{
    public Vector2 Target;
    public Vector2 Current;
    public float Speed;
    public bool Initialized;
}
