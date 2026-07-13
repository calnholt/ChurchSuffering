#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Components;

[Flags]
public enum SpriteFlags : byte
{
    None = 0,
    HasSourceRectangle = 1 << 0,
    Visible = 1 << 1,
    PixelAlignedDestination = 1 << 2,
}

public struct Sprite : IComponent
{
    public Rectangle SourceRectangle;
    public TextureAssetId Texture;
    public Color Tint;
    public SpriteFlags Flags;

    public readonly bool HasSourceRectangle => (Flags & SpriteFlags.HasSourceRectangle) != 0;

    public readonly bool IsVisible => (Flags & SpriteFlags.Visible) != 0;

    public readonly bool UsesPixelAlignedDestination =>
        (Flags & SpriteFlags.PixelAlignedDestination) != 0;
}

public struct ActorPresentationState : IComponent
{
    public Vector2 DrawOffset;
    public Vector2 ScaleMultiplier;
    public Color TintColor;
    public float DamageFlashTimer;
}

public struct BattlePresentationTransform : IComponent
{
    public Vector2 Offset;
    public Vector2 Scale;
}
