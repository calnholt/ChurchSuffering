#nullable enable

using System;
using System.Runtime.InteropServices;

namespace Crusaders30XX.ECS.DataOriented.Rules;

[StructLayout(LayoutKind.Sequential, Pack = 8)]
public record struct RuleRandomState(ulong Value)
{
    private const ulong DefaultSeed = 0x9E3779B97F4A7C15UL;

    public static RuleRandomState FromSeed(ulong seed) => new(seed == 0 ? DefaultSeed : seed);
}

/// <summary>
/// Deterministic xorshift64* random stream backed by caller-owned state. Copying this
/// ref struct still advances the same state; no heap object, delegate, or reflection is used.
/// </summary>
public ref struct DeterministicRuleRandom
{
    private const ulong ZeroStateReplacement = 0x9E3779B97F4A7C15UL;
    private Span<RuleRandomState> state;

    public DeterministicRuleRandom(ref RuleRandomState state)
    {
        this.state = MemoryMarshal.CreateSpan(ref state, 1);
    }

    public ulong NextUInt64()
    {
        ulong value = state[0].Value;
        if (value == 0)
        {
            value = ZeroStateReplacement;
        }

        value ^= value >> 12;
        value ^= value << 25;
        value ^= value >> 27;
        state[0] = new RuleRandomState(value);
        return value * 2685821657736338717UL;
    }

    public int NextInt(int exclusiveMaximum)
    {
        if (exclusiveMaximum <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(exclusiveMaximum));
        }

        uint bound = (uint)exclusiveMaximum;
        uint threshold = unchecked(0u - bound) % bound;
        uint value;
        do
        {
            value = (uint)NextUInt64();
        }
        while (value < threshold);

        return (int)(value % bound);
    }

    public int NextPercent() => NextInt(100);

    public bool NextBool() => (NextUInt64() & 1UL) != 0;

    public void Shuffle<T>(Span<T> values)
    {
        for (int index = values.Length - 1; index > 0; index--)
        {
            int swapIndex = NextInt(index + 1);
            (values[index], values[swapIndex]) = (values[swapIndex], values[index]);
        }
    }
}
