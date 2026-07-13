#nullable enable

using System;
using Crusaders30XX.ECS.DataOriented.Core;
using Crusaders30XX.ECS.DataOriented.Rendering;
using Crusaders30XX.ECS.DataOriented.Resources;
using Microsoft.Xna.Framework;

namespace Crusaders30XX.ECS.DataOriented.Gameplay.Presentation;

public readonly record struct FontAssetId(int Value)
{
    public static FontAssetId Null => default;
    public bool IsNull => Value == 0;
}

public readonly record struct TextStyleId(int Value)
{
    public static TextStyleId Null => default;
    public bool IsNull => Value == 0;
}

public enum TextAlignment : byte
{
    TopLeft,
    TopCenter,
    Center,
    BottomCenter,
}

[Flags]
public enum TextPresentationFlags : byte
{
    None = 0,
    Visible = 1 << 0,
    DropShadow = 1 << 1,
}

/// <summary>Unmanaged text intent; strings and fonts remain in external compact-ID catalogs.</summary>
public struct TextPresentation : IComponent
{
    public StringId Content;
    public TextStyleId Style;
    public Vector2 Offset;
    public Vector2 Scale;
    public Color Tint;
    public int ZOffset;
    public RenderLayer Layer;
    public TextAlignment Alignment;
    public float LetterSpacing;
    public TextPresentationFlags Flags;

    public readonly bool IsVisible => (Flags & TextPresentationFlags.Visible) != 0;
}

public static class TextStyleIds
{
    public static readonly TextStyleId Title = new(1);
    public static readonly TextStyleId Heading = new(2);
    public static readonly TextStyleId Hud = new(3);
    public static readonly TextStyleId Snapshot = new(4);
}

public static class TextContentIds
{
    public static readonly StringId Title = new(51001);
    public static readonly StringId Climb = new(51002);
    public static readonly StringId WayStation = new(51003);
    public static readonly StringId Achievements = new(51004);
    public static readonly StringId Health = new(51011);
    public static readonly StringId ActionPoints = new(51012);
    public static readonly StringId Courage = new(51013);
    public static readonly StringId Temperance = new(51014);
    public static readonly StringId TestFight = new(51015);

    public const int SnapshotBase = 52000;
    public static StringId SnapshotFixture(int fixtureIndex) => new(SnapshotBase + fixtureIndex);
}
