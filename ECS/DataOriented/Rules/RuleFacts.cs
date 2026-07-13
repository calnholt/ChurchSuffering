#nullable enable

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Crusaders30XX.ECS.DataOriented.Definitions;

namespace Crusaders30XX.ECS.DataOriented.Rules;

[StructLayout(LayoutKind.Sequential, Pack = 4)]
public readonly record struct RuleFact(RuleFactId Id, int Value);

/// <summary>
/// Allocation-free lookup over facts sorted by ascending, unique <see cref="RuleFactId.Value"/>.
/// Construction validates the ordering once; lookup uses binary search.
/// </summary>
public readonly ref struct RuleFactReader
{
    private readonly ReadOnlySpan<RuleFact> facts;

    public RuleFactReader(ReadOnlySpan<RuleFact> facts)
    {
        ushort previous = 0;
        for (var index = 0; index < facts.Length; index++)
        {
            ushort current = facts[index].Id.Value;
            if (current == 0 || (index > 0 && current <= previous))
            {
                throw new ArgumentException(
                    "Rule facts must have non-null, strictly ascending, unique IDs.",
                    nameof(facts));
            }

            previous = current;
        }

        this.facts = facts;
    }

    public int Count => facts.Length;

    public ReadOnlySpan<RuleFact> Facts => facts;

    public bool Contains(RuleFactId id) => TryGet(id, out _);

    public int GetOrDefault(RuleFactId id, int defaultValue = 0) =>
        TryGet(id, out int value) ? value : defaultValue;

    public int GetRequired(RuleFactId id)
    {
        if (TryGet(id, out int value))
        {
            return value;
        }

        throw new KeyNotFoundException($"Required rule fact {id.Value} was not supplied.");
    }

    public bool TryGet(RuleFactId id, out int value)
    {
        int low = 0;
        int high = facts.Length - 1;
        while (low <= high)
        {
            int middle = low + ((high - low) >> 1);
            RuleFact fact = facts[middle];
            if (fact.Id.Value == id.Value)
            {
                value = fact.Value;
                return true;
            }

            if (fact.Id.Value < id.Value)
            {
                low = middle + 1;
            }
            else
            {
                high = middle - 1;
            }
        }

        value = default;
        return false;
    }
}
