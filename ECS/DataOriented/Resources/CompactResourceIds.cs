#nullable enable

namespace Crusaders30XX.ECS.DataOriented.Resources;

/// <summary>
/// Common debug/catalog contract for integer-backed resource identifiers.
/// Runtime code should use the concrete identifier type rather than this interface.
/// </summary>
public interface ICompactResourceId
{
    int Value { get; }

    bool IsNull { get; }
}

public readonly record struct StringId(int Value) : ICompactResourceId
{
    public static StringId Null => default;

    public bool IsNull => Value == 0;
}

public readonly record struct TextureAssetId(int Value) : ICompactResourceId
{
    public static TextureAssetId Null => default;

    public bool IsNull => Value == 0;
}

public readonly record struct SoundId(int Value) : ICompactResourceId
{
    public static SoundId Null => default;

    public bool IsNull => Value == 0;
}

public readonly record struct VisualEffectRecipeId(int Value) : ICompactResourceId
{
    public static VisualEffectRecipeId Null => default;

    public bool IsNull => Value == 0;
}
