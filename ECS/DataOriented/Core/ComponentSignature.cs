#nullable enable

using System;

namespace Crusaders30XX.ECS.DataOriented.Core;

public readonly struct ComponentSignature : IEquatable<ComponentSignature>
{
    public const int MaximumTypeCount = 512;

    public readonly ulong Word0;
    public readonly ulong Word1;
    public readonly ulong Word2;
    public readonly ulong Word3;
    public readonly ulong Word4;
    public readonly ulong Word5;
    public readonly ulong Word6;
    public readonly ulong Word7;

    public ComponentSignature(
        ulong word0 = 0,
        ulong word1 = 0,
        ulong word2 = 0,
        ulong word3 = 0,
        ulong word4 = 0,
        ulong word5 = 0,
        ulong word6 = 0,
        ulong word7 = 0)
    {
        Word0 = word0;
        Word1 = word1;
        Word2 = word2;
        Word3 = word3;
        Word4 = word4;
        Word5 = word5;
        Word6 = word6;
        Word7 = word7;
    }

    public static ComponentSignature Empty => default;

    public bool IsEmpty =>
        (Word0 | Word1 | Word2 | Word3 | Word4 | Word5 | Word6 | Word7) == 0;

    public ComponentSignature With(int typeId)
    {
        ValidateTypeId(typeId);
        var mask = 1UL << (typeId & 63);

        return (typeId >> 6) switch
        {
            0 => new(Word0 | mask, Word1, Word2, Word3, Word4, Word5, Word6, Word7),
            1 => new(Word0, Word1 | mask, Word2, Word3, Word4, Word5, Word6, Word7),
            2 => new(Word0, Word1, Word2 | mask, Word3, Word4, Word5, Word6, Word7),
            3 => new(Word0, Word1, Word2, Word3 | mask, Word4, Word5, Word6, Word7),
            4 => new(Word0, Word1, Word2, Word3, Word4 | mask, Word5, Word6, Word7),
            5 => new(Word0, Word1, Word2, Word3, Word4, Word5 | mask, Word6, Word7),
            6 => new(Word0, Word1, Word2, Word3, Word4, Word5, Word6 | mask, Word7),
            _ => new(Word0, Word1, Word2, Word3, Word4, Word5, Word6, Word7 | mask),
        };
    }

    public ComponentSignature Without(int typeId)
    {
        ValidateTypeId(typeId);
        var mask = ~(1UL << (typeId & 63));

        return (typeId >> 6) switch
        {
            0 => new(Word0 & mask, Word1, Word2, Word3, Word4, Word5, Word6, Word7),
            1 => new(Word0, Word1 & mask, Word2, Word3, Word4, Word5, Word6, Word7),
            2 => new(Word0, Word1, Word2 & mask, Word3, Word4, Word5, Word6, Word7),
            3 => new(Word0, Word1, Word2, Word3 & mask, Word4, Word5, Word6, Word7),
            4 => new(Word0, Word1, Word2, Word3, Word4 & mask, Word5, Word6, Word7),
            5 => new(Word0, Word1, Word2, Word3, Word4, Word5 & mask, Word6, Word7),
            6 => new(Word0, Word1, Word2, Word3, Word4, Word5, Word6 & mask, Word7),
            _ => new(Word0, Word1, Word2, Word3, Word4, Word5, Word6, Word7 & mask),
        };
    }

    public bool Contains(int typeId)
    {
        ValidateTypeId(typeId);
        var mask = 1UL << (typeId & 63);

        return (typeId >> 6) switch
        {
            0 => (Word0 & mask) != 0,
            1 => (Word1 & mask) != 0,
            2 => (Word2 & mask) != 0,
            3 => (Word3 & mask) != 0,
            4 => (Word4 & mask) != 0,
            5 => (Word5 & mask) != 0,
            6 => (Word6 & mask) != 0,
            _ => (Word7 & mask) != 0,
        };
    }

    public bool ContainsAll(in ComponentSignature other) =>
        (Word0 & other.Word0) == other.Word0 &&
        (Word1 & other.Word1) == other.Word1 &&
        (Word2 & other.Word2) == other.Word2 &&
        (Word3 & other.Word3) == other.Word3 &&
        (Word4 & other.Word4) == other.Word4 &&
        (Word5 & other.Word5) == other.Word5 &&
        (Word6 & other.Word6) == other.Word6 &&
        (Word7 & other.Word7) == other.Word7;

    public bool Intersects(in ComponentSignature other) =>
        ((Word0 & other.Word0) |
         (Word1 & other.Word1) |
         (Word2 & other.Word2) |
         (Word3 & other.Word3) |
         (Word4 & other.Word4) |
         (Word5 & other.Word5) |
         (Word6 & other.Word6) |
         (Word7 & other.Word7)) != 0;

    public ComponentSignature Except(in ComponentSignature other) => new(
        Word0 & ~other.Word0,
        Word1 & ~other.Word1,
        Word2 & ~other.Word2,
        Word3 & ~other.Word3,
        Word4 & ~other.Word4,
        Word5 & ~other.Word5,
        Word6 & ~other.Word6,
        Word7 & ~other.Word7);

    public static ComponentSignature operator |(ComponentSignature left, ComponentSignature right) => new(
        left.Word0 | right.Word0,
        left.Word1 | right.Word1,
        left.Word2 | right.Word2,
        left.Word3 | right.Word3,
        left.Word4 | right.Word4,
        left.Word5 | right.Word5,
        left.Word6 | right.Word6,
        left.Word7 | right.Word7);

    public static ComponentSignature operator &(ComponentSignature left, ComponentSignature right) => new(
        left.Word0 & right.Word0,
        left.Word1 & right.Word1,
        left.Word2 & right.Word2,
        left.Word3 & right.Word3,
        left.Word4 & right.Word4,
        left.Word5 & right.Word5,
        left.Word6 & right.Word6,
        left.Word7 & right.Word7);

    public static bool operator ==(ComponentSignature left, ComponentSignature right) => left.Equals(right);

    public static bool operator !=(ComponentSignature left, ComponentSignature right) => !left.Equals(right);

    public bool Equals(ComponentSignature other) =>
        Word0 == other.Word0 &&
        Word1 == other.Word1 &&
        Word2 == other.Word2 &&
        Word3 == other.Word3 &&
        Word4 == other.Word4 &&
        Word5 == other.Word5 &&
        Word6 == other.Word6 &&
        Word7 == other.Word7;

    public override bool Equals(object? obj) => obj is ComponentSignature other && Equals(other);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Word0);
        hash.Add(Word1);
        hash.Add(Word2);
        hash.Add(Word3);
        hash.Add(Word4);
        hash.Add(Word5);
        hash.Add(Word6);
        hash.Add(Word7);
        return hash.ToHashCode();
    }

    private static void ValidateTypeId(int typeId)
    {
        if ((uint)typeId >= MaximumTypeCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(typeId),
                typeId,
                $"Component and tag type IDs must be between 0 and {MaximumTypeCount - 1}.");
        }
    }
}
