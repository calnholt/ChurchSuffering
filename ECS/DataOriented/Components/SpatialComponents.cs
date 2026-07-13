#nullable enable

using Crusaders30XX.ECS.DataOriented.Core;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Components;

public struct Transform : IComponent
{
    public Vector2 Position;
    public Vector2 Scale;
    public float Rotation;
    public int ZOrder;

    public static Transform Identity => new()
    {
        Scale = Vector2.One,
    };
}

/// <summary>
/// Makes the transform local to another entity. Parent rotation and scale are not
/// propagated by the shared hierarchy contract.
/// </summary>
public struct ParentTransform : IComponent
{
    public EntityId Parent;
}

public struct ParallaxLayer : IComponent
{
    public float MultiplierX;
    public float MultiplierY;
    public float MaxOffset;
    public float SmoothTime;

    public static ParallaxLayer Ui => new()
    {
        MultiplierX = 0.025f,
        MultiplierY = 0.025f,
        MaxOffset = 48f,
        SmoothTime = 0.08f,
    };

    public static ParallaxLayer Location => new()
    {
        MultiplierX = 0.01f,
        MultiplierY = 0.01f,
        MaxOffset = 12f,
        SmoothTime = 0.01f,
    };

    public static ParallaxLayer Character => new()
    {
        MultiplierX = 0.01f,
        MultiplierY = 0.01f,
        MaxOffset = 48f,
        SmoothTime = 0.08f,
    };
}
