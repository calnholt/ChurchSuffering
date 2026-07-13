#nullable enable

using System;
using System.Collections.Generic;
using Crusaders30XX.ECS.DataOriented.Resources;

namespace Crusaders30XX.ECS.DataOriented.Storage;

/// <summary>
/// World/catalog-owned string interning table. Index zero is permanently reserved.
/// </summary>
public sealed class StringTable
{
    private readonly Dictionary<string, StringId> idsByValue;
    private readonly List<string?> values;

    public StringTable(int initialCapacity = 0)
    {
        if (initialCapacity < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(initialCapacity));
        }

        idsByValue = new Dictionary<string, StringId>(initialCapacity, StringComparer.Ordinal);
        values = new List<string?>(initialCapacity + 1) { null };
    }

    public int Count => values.Count - 1;

    public StringId Intern(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        if (idsByValue.TryGetValue(value, out StringId existing))
        {
            return existing;
        }

        var id = new StringId(values.Count);
        values.Add(value);
        idsByValue.Add(value, id);
        return id;
    }

    public bool TryFind(string value, out StringId id)
    {
        ArgumentNullException.ThrowIfNull(value);
        return idsByValue.TryGetValue(value, out id);
    }

    public string GetRequired(StringId id)
    {
        if (id.Value <= 0 || id.Value >= values.Count)
        {
            throw new KeyNotFoundException($"String ID {id.Value} is null or is not registered.");
        }

        return values[id.Value]!;
    }

    public bool TryGet(StringId id, out string? value)
    {
        if (id.Value <= 0 || id.Value >= values.Count)
        {
            value = null;
            return false;
        }

        value = values[id.Value];
        return true;
    }
}
